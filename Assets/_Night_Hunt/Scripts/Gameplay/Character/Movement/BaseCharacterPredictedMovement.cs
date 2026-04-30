using System;
using UnityEngine;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using NightHunt.Gameplay.Character.Movement;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Networking.Prediction.FishNet;
using Unity.Cinemachine;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Utilities;

namespace NightHunt.Gameplay.Character
{
    /// <summary>
    /// Abstract base class for character predicted movement.
    /// 
    /// ARCHITECTURE:
    /// - Handles input gathering, tick callbacks, and replication
    /// - Delegates physics implementation to derived classes
    /// - Supports both CharacterController and Rigidbody
    /// 
    /// DERIVED CLASSES MUST IMPLEMENT:
    /// - IsGrounded() - Check if character is on ground
    /// - ApplyMovement() - Apply final movement vector
    /// - GetCurrentVelocity() - Get current movement velocity
    /// - ResetPhysicsState() - Reset physics when reconciling
    /// </summary>
    public abstract class BaseCharacterPredictedMovement
        : FishNetPredictedBehaviour<MovementReplicateData, MovementReconcileData>,
            IMovementController
    {
        [Header("Movement Settings")] [SerializeField]
        protected MovementSettings movementSettings;

        [Header("Rotation")] [SerializeField] protected float tankTurnSpeed = 10f;
        [SerializeField] protected float lockTurnSpeed = 18f;

        [Header("Camera Lock")] [SerializeField]
        protected bool allowCameraLockToggle = true;

        [SerializeField] protected bool startWithCameraLock = false;

        [Header("Network Interpolation")] [SerializeField]
        protected float interpolationSpeed = 15f;

        [Header("Stamina Recovery (Advanced)")] [SerializeField]
        protected float staminaRecoveryDelay = 1.5f;

        [SerializeField] protected float slowMovementThreshold = 1.0f;

        [Header("Debug")] [SerializeField] protected bool enableDebugLogs = false;
        [Tooltip("Bật để log chuyên sâu: tại sao character move khi KHÔNG có input. Throttle 2/giây để không spam.")]
        [SerializeField] protected bool diagnoseMysteryMove = false;

        [Header("Grounding")]
        [SerializeField, Tooltip("Grace window (seconds) to smooth transient ground-check flips (prevents flapping)")]
        protected float groundedHysteresisTime = 0.12f;

        // Throttle diagnostic log to avoid console spam (2 logs/sec)
        private float _diagTimer = 0f;
        // Grounded hysteresis timer (counts down when raw IsGrounded() is false)
        private float _groundedGraceTimer = 0f;

        // Ground info populated by derived implementations via SetGroundInfo
        protected Vector3 GroundNormal { get; private set; } = Vector3.up;
        protected float GroundSlopeAngle { get; private set; } = 0f;

        // Components

        // ===== INPUT (OWNER ONLY) =====
        protected CinemachineCamera _cinemachineCamera;

        // The real Unity Camera driven by CinemachineBrain — used to read the live yaw.
        // CinemachineCamera's own transform never rotates; the Brain writes the result here.
        protected UnityEngine.Camera _mainCamera;
        protected Vector2 _moveInput;
        protected bool _sprint;
        protected bool _crouch;
        protected bool _cameraLocked;
        protected float _yaw;

        /// <summary>Aim-derived yaw for character model facing (cursor-to-ground in STRAFE mode).</summary>
        protected float _aimYaw;

        // ===== STATE =====
        protected Vector3 _velocity;
        protected float _verticalVelocity;
        protected float _stamina;

        // ===== STAMINA RECOVERY =====
        protected float _staminaRecoveryTimer = 0f;
        private float _aimFallbackLogTimer = 0f;

        // ===== JUMP / ROLL (OWNER) =====
        private bool _jumpRequest;
        private bool _rollRequest;

        // ===== ROLL STATE =====
        protected bool _isRolling;
        protected float _rollTimer;
        /// <summary>Horizontal direction locked at roll-start. Not reconciled — deterministic from tick data.</summary>
        protected Vector3 _rollDir;

        // ===== STATS =====
        protected IPlayerStatSystem _playerStatSystem;

        // ===== RUNTIME MODIFIERS =====
        // Applied on top of the stat-system value every tick.
        // Call SetSpeedMultiplier() from zones, buffs, item effects, weight events, etc.
        // Multiple sources: chain-multiply before calling, or keep per-source and rebuild.
        private float _runtimeSpeedMultiplier       = 1f;
        private float _runtimeStaminaDrainMultiplier = 1f;
        
        // ===== ANIMATION EVENTS =====
        public event System.Action OnJumpTriggered;
        public event System.Action OnRollTriggered;

        // ===== DEATH STATE =====
        // Resolved lazily in GatherInput() to avoid Awake ordering issues.
        private NightHunt.Gameplay.Core.State.CharacterLifecycleController _lifecycle;
        private bool _lifecycleResolved;

        // ===== SPAWN GRACE =====
        // On a dedicated server, the client receives the spawn packet and starts prediction
        // while the very first reconcile tick (which would correct any prediction error) is
        // still in transit (~1-2 RTT ticks away).  If IsGrounded() returns false during
        // that window (e.g. groundLayer mask mismatch, spawn point Y slightly above terrain),
        // _verticalVelocity accumulates downward and the player visually sinks through the
        // floor before the first reconcile can push them back up.
        //
        // Solution: for the first N ticks after spawn, clamp _verticalVelocity to ≥ 0
        // so no downward displacement is applied.  This is transparent on host (0 RTT)
        // and invisible on DS (< 0.5 s at 50 Hz tickRate before real grounded detection
        // takes over).  Jump inputs in the grace window still work (positive vertVel).
        private int _spawnGraceTicksRemaining;
        private bool _spawnGraceIgnoreDownwardClampUntilGrounded;

        // ===== NON OWNER INTERPOLATION =====
        protected Vector3 _targetPosition;
        protected Quaternion _targetRotation;


        #region ABSTRACT METHODS - MUST IMPLEMENT

        /// <summary>
        /// Check if character is grounded
        /// CharacterController: use controller.isGrounded
        /// Rigidbody: use raycast or collider checks
        /// </summary>
        protected abstract bool IsGrounded();

        /// <summary>
        /// Called by derived physics implementations to publish ground hit normal.
        /// Base will compute slope angle for consumers and debug.
        /// </summary>
        /// <param name="grounded">Whether a ground contact was detected.</param>
        /// <param name="normal">Contact normal (valid only if grounded==true).</param>
        protected void SetGroundInfo(bool grounded, Vector3 normal)
        {
            GroundNormal     = normal;
            GroundSlopeAngle = Vector3.Angle(normal, Vector3.up);
        }

        /// <summary>
        /// Apply movement vector to character
        /// CharacterController: controller.Move(movement * dt)
        /// Rigidbody: rb.velocity = movement or rb.MovePosition()
        /// </summary>
        /// <param name="movement">Final movement vector (includes gravity)</param>
        /// <param name="dt">Delta time</param>
        protected abstract void ApplyMovement(Vector3 movement, float dt);

        /// <summary>
        /// Get current velocity from physics component
        /// CharacterController: return stored velocity
        /// Rigidbody: return rb.velocity
        /// </summary>
        protected abstract Vector3 GetCurrentVelocity();

        /// <summary>
        /// Reset physics state during reconciliation
        /// CharacterController: reset vertical velocity
        /// Rigidbody: reset velocity and angular velocity
        /// </summary>
        protected abstract void ResetPhysicsState();

        /// <summary>Initialize physics components in Awake</summary>
        protected abstract void InitializePhysicsComponents();

        /// <summary>Get physics component name for debug display</summary>
        protected abstract string GetPhysicsComponentName();

        /// <summary>
        /// Returns true if a wall blocks movement in <paramref name="direction"/> over <paramref name="stepDistance"/> metres.
        /// Called each tick while rolling to cancel on impact. Default returns false (no wall detection).
        /// </summary>
        protected virtual bool IsRollBlocked(Vector3 direction, float stepDistance) => false;

        /// <summary>
        /// Called every simulation tick when the crouch state changes.
        /// Override in derived physics implementations to resize the collider.
        /// </summary>
        protected virtual void UpdateCrouchPhysics(bool isCrouching) { }

        #endregion

        #region UNITY LIFECYCLE

        private void Awake()
        {
            _cinemachineCamera ??= ComponentResolver.Find<CinemachineCamera>(this)
        .OnSelf()
        .InChildren()
        .InParent()
        .OrLogWarning("[Auto] CinemachineCamera not found")
        .Resolve();
            _playerStatSystem = ComponentResolver.Find<IPlayerStatSystem>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] IPlayerStatSystem not found")
        .Resolve();
            InitializePhysicsComponents();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            if (movementSettings == null)
            {
                Debug.LogWarning(
                    $"[{GetType().Name}] movementSettings is NULL in OnStartNetwork! Character may not work correctly.");
                return;
            }

            // Initialize stamina using PlayerStatSystem if available, otherwise fallback to MovementSettings
            float maxStamina = GetMaxStamina();
            float currentStamina = _playerStatSystem != null
                ? _playerStatSystem.GetStat(PlayerStatType.Stamina)
                : 0f;
            _stamina = currentStamina > 0f ? currentStamina : maxStamina;

            // Sync physics component to spawn position (FishNet sets transform before this callback).
            ResetPhysicsState();

            _targetPosition      = transform.position;
            _targetRotation      = transform.rotation;
            _verticalVelocity    = 0f;
            _cameraLocked        = startWithCameraLock;
            _staminaRecoveryTimer = 0f;
            _groundedGraceTimer  = 0f;

            // Give the prediction system time to settle before allowing downward fall.
            // 30 ticks ≈ 0.6 s at 50 Hz tickRate.  On host (0 RTT) this window is never
            // needed but is harmless.  On DS it bridges the gap between spawn and the
            // first authoritative reconcile from the server.
            _spawnGraceTicksRemaining = 30;
            _spawnGraceIgnoreDownwardClampUntilGrounded = false;

            _cinemachineCamera ??= ComponentResolver.Find<CinemachineCamera>(this)
        .OnSelf()
        .InChildren()
        .InParent()
        .OrLogWarning("[Auto] CinemachineCamera not found")
        .Resolve();
            _mainCamera ??= UnityEngine.Camera.main;

            if (enableDebugLogs)
                Debug.Log(
                    $"[{GetType().Name}] OnStartNetwork - IsOwner={base.Owner.IsLocalClient}, IsServer={IsServerStarted}, MaxStamina={maxStamina}, StartPos={transform.position}");
        }

        #endregion

        #region INPUT GATHERING

        protected virtual void GatherInput()
        {
            if (!IsOwner || !IsSpawned) return;

            // ── Death guard ──────────────────────────────────────────────────────
            // Lazily resolve CharacterLifecycleController so Awake ordering is not
            // a problem (the controller may not yet be initialized during Awake).
            if (!_lifecycleResolved)
            {
                _lifecycle = NightHunt.Utilities.ComponentResolver
                    .Find<NightHunt.Gameplay.Core.State.CharacterLifecycleController>(this)
                    .OnSelf().InChildren().InParent()
                    .Resolve();
                _lifecycleResolved = true;
            }

            if (_lifecycle != null && _lifecycle.IsDead)
            {
                // Zero all inputs so the simulation produces zero movement / no rotation.
                _moveInput = Vector2.zero;
                _sprint    = false;
                _crouch    = false;
                return;
            }

            var inputManager = InputManager.Instance;
            if (inputManager == null)
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"[{GetType().Name}] InputManager.Instance is NULL!");
                return;
            }

            var handler = inputManager.MovementHandler;
            if (handler == null)
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"[{GetType().Name}] MovementHandler is NULL!");
                return;
            }

            // Get movement input
            _moveInput = handler.GetMoveInput();
            _sprint = handler.IsSprinting();
            _crouch = handler.IsCrouching();

            // Camera lock toggle
            if (allowCameraLockToggle)
            {
                bool newCameraLockState = handler.IsCameraLocked();
                if (newCameraLockState != _cameraLocked)
                {
                    _cameraLocked = newCameraLockState;
                    if (enableDebugLogs)
                        Debug.Log($"[{GetType().Name}] Camera Lock: {(_cameraLocked ? "STRAFE" : "TANK")}");
                }
            }

            // ── Camera Yaw ────────────────────────────────────────────────────────
            // CinemachineCamera (child of player prefab) là VIRTUAL camera — transform
            // của nó KHÔNG bao giờ xoay. Cinemachine lưu orbital angles nội bộ và
            // ghi kết quả lên Camera.main (scene-level, có CinemachineBrain) mỗi LateUpdate.
            // → _yaw PHẢI đọc từ Camera.main, KHÔNG đọc từ _cinemachineCamera.transform.
            var freshCam = UnityEngine.Camera.main;
            if (freshCam != null)
            {
                _mainCamera = freshCam;
                _yaw = _mainCamera.transform.eulerAngles.y;
            }
            else
            {
                // Giữ _yaw frame trước — KHÔNG fallback về _cinemachineCamera (luôn sai)
                if (enableDebugLogs)
                    Debug.LogWarning($"[{GetType().Name}] Camera.main NULL — keeping _yaw={_yaw:F1}");
            }

            // ── Jump / Roll one-shot ──────────────────────────────────────────────
            if (handler.IsJumping()) _jumpRequest = true;
            if (handler.IsRolling()) _rollRequest = true;

            // ── Aim Yaw ───────────────────────────────────────────────────────────
            // Priority 1: CombatHandler.GetAimDirection()
            //   → Chỉ trả direction khi _isFiring = true (xem CombatInputHandler.GetAimDirection)
            //   → Trả Vector3.zero khi KHÔNG fire → fallback về camera yaw ✅
            //
            // Priority 2 (raycast trực tiếp tại đây): ĐÃ BỎ
            //   CombatInputHandler đã handle raycast nội bộ và expose qua GetAimDirection().
            //   Nếu để Priority 2 ở đây sẽ bypass _isFiring gate
            //   → character LUÔN nhìn theo chuột dù không bấm bắn ← đây là bug cũ.
            //
            // Priority 3: _yaw (camera yaw) — safe default, set sẵn ở trên
            _aimYaw = _yaw;

            bool aimResolved = false;

            // Priority 1: CombatHandler (chỉ non-zero khi đang fire)
            var combatH = InputManager.Instance?.CombatHandler;
            if (combatH != null)
            {
                Vector3 aimDir = combatH.GetAimDirection();
                if (aimDir.sqrMagnitude > 0.001f)
                {
                    _aimYaw = Quaternion.LookRotation(aimDir, Vector3.up).eulerAngles.y;
                    aimResolved = true;
                }
            }

            // Throttled debug log — không spam 50 msgs/giây
            if (!aimResolved && enableDebugLogs)
            {
                _aimFallbackLogTimer += TickDelta;
                if (_aimFallbackLogTimer >= 5f)
                {
                    _aimFallbackLogTimer = 0f;
                    string reason = combatH == null ? "CombatHandler=NULL" : "not firing";
                    Debug.LogWarning($"[{GetType().Name}] AimYaw fallback to camera yaw — {reason}");
                }
            }
            else
            {
                _aimFallbackLogTimer = 0f;
            }
        }

        #endregion

        #region TICK & REPLICATION

        protected override void TimeManager_OnTick()
        {
            if (!IsSpawned) return;
            if (!IsOwner && !IsServerStarted) return;

            MovementReplicateData replicateData = default;
            if (IsOwner)
            {
                GatherInput();
                replicateData = new MovementReplicateData(
                    _moveInput,
                    _yaw,
                    _aimYaw,
                    _sprint,
                    _crouch,
                    _cameraLocked,
                    _jumpRequest,
                    _rollRequest
                );
                // One-shot: consumed by this tick's Replicate call.
                _jumpRequest = false;
                _rollRequest = false;
            }

            // All (Owner, Server, Non-owner): Call Replicate
            // Owner uses real data, Server uses data received from Owner via network,
            // Non-owner will be blocked in Replicate() method
            Replicate(replicateData, ReplicateState.Ticked, Channel.Unreliable);

            if (IsServerStarted)
            {
                CreateReconcile();
            }
        }

        [Replicate]
        private void Replicate(
            MovementReplicateData data,
            ReplicateState state = ReplicateState.Invalid,
            Channel channel = Channel.Unreliable)
        {
            if (this == null || !IsSpawned) return;
            if (!IsOwner && !IsServerStarted) return;

            SimulateMovement(data, TickDelta, state);
        }

        #endregion

        #region MOVEMENT SIMULATION

        /// <summary>Core movement simulation — runs on owner and server each tick.</summary>
        protected virtual void SimulateMovement(MovementReplicateData data, float dt, ReplicateState state = ReplicateState.Invalid)
        {
            if (!IsSpawned || movementSettings == null) return;

            // ── Ground state (with small hysteresis to avoid flapping) ──────────────
            // Raw ground from physics implementation
            bool rawGrounded = IsGrounded();

            if (rawGrounded)
                _groundedGraceTimer = groundedHysteresisTime;
            else
                _groundedGraceTimer = Mathf.Max(0f, _groundedGraceTimer - dt);

            bool grounded = _groundedGraceTimer > 0f;

            // Server-side grounding diagnostic: log every 50 ticks to trace grounding state on DS.
            // Enable with enableDebugLogs on the component in the Inspector.
            if (enableDebugLogs && IsServerStarted && !IsReplaying(state)
                && (TimeManager.Tick % 50 == 0))
            {
                Debug.Log($"[SERVER][Ground] raw={rawGrounded} grounded={grounded} " +
                          $"vertVel={_verticalVelocity:F3} pos={transform.position} " +
                          $"grace={_spawnGraceTicksRemaining}");
            }

            // ── Speed modifier ────────────────────────────────────────────────────
            float speed = GetBaseSpeed();

            // Apply weight-based movement penalty from the stat system.
            // IPlayerStatSystem.GetMovementSpeedMultiplier() returns a 0-1 multiplier
            // that factors in carry weight vs. capacity (0.1 minimum at extreme overweight).
            if (_playerStatSystem != null)
                speed *= _playerStatSystem.GetMovementSpeedMultiplier();

            // Apply runtime multipliers from zones, buffs, consumables, penalties, etc.
            speed *= _runtimeSpeedMultiplier;

            bool canSprint = data.Sprint && _stamina > GetMinStaminaToSprint();
            if      (canSprint)    speed *= GetSprintSpeedMultiplier();
            else if (data.Crouch)  speed *= movementSettings.crouchMultiplier;

            // Notify physics implementation when crouch state changes (e.g. capsule resize).
            UpdateCrouchPhysics(data.Crouch);

            // ── Stamina ───────────────────────────────────────────────────────────
            if (canSprint)
            {
                _stamina = Mathf.Max(0f, _stamina - GetStaminaDrainRate() * _runtimeStaminaDrainMultiplier * dt);
                _staminaRecoveryTimer = 0f;
            }
            else if (!_isRolling)
            {
                _staminaRecoveryTimer += dt;
                if (_staminaRecoveryTimer >= staminaRecoveryDelay)
                    _stamina = Mathf.Min(_stamina + GetStaminaRegenRate() * dt, GetMaxStamina());
            }

            if (IsServerStarted && _playerStatSystem != null)
                _playerStatSystem.SetCurrentStat(PlayerStatType.Stamina, _stamina);

            // ── Input & rotation ─────────────────────────────────────────────────
            Vector3 inputDir = new Vector3(data.Move.x, 0f, data.Move.y);
            if (inputDir.sqrMagnitude > 1f) inputDir.Normalize();

            Quaternion camRot = Quaternion.Euler(0f, data.Yaw, 0f);
            Quaternion aimRot = Quaternion.Euler(0f, data.AimYaw, 0f);
            Vector3 moveDir   = Vector3.zero;

            // ── [DIAG] Throttle counter ───────────────────────────────────────────
            bool diagLog = false;
            if (diagnoseMysteryMove && IsOwner)
            {
                _diagTimer -= dt;
                if (_diagTimer <= 0f) { _diagTimer = 0.5f; diagLog = true; }
            }

            // ── [DIAG] INPUT SNAPSHOT ─────────────────────────────────────────────
            if (diagLog)
            {
                Debug.Log(
                    $"[DIAG][INPUT] move=({data.Move.x:F3},{data.Move.y:F3}) " +
                    $"yaw={data.Yaw:F1} aimYaw={data.AimYaw:F1} " +
                    $"sprint={data.Sprint} crouch={data.Crouch} " +
                    $"jump={data.Jump} roll={data.Roll} camLock={data.CameraLocked} " +
                    $"grounded={grounded} pos={transform.position:F2}");
            }

            Quaternion rotBefore = transform.rotation;

            if (data.CameraLocked)
            {
                // STRAFE: face aim direction, move relative to camera.
                transform.rotation = Quaternion.RotateTowards(transform.rotation, aimRot, lockTurnSpeed * dt * 100f);
                if (inputDir.sqrMagnitude > 0.001f) moveDir = camRot * inputDir;
            }
            else
            {
                if (inputDir.sqrMagnitude > 0.001f)
                {
                    // TANK: rotate toward movement direction on input.
                    moveDir = camRot * inputDir;
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation, Quaternion.LookRotation(moveDir), tankTurnSpeed * dt * 100f);
                }
                // TANK: no idle auto-rotate — prevents slope drift from rotation-induced XZ forces.
                // (Combat system drives camera lock via SetCameraLock() when aiming/firing.)
            }


            // ── [DIAG] ROTATION CHANGE ────────────────────────────────────────────
            if (diagLog)
            {
                float rotDelta = Quaternion.Angle(rotBefore, transform.rotation);
                if (rotDelta > 0.01f)
                    Debug.Log($"[DIAG][ROT] Rotation changed {rotDelta:F2}° this tick | " +
                              $"curYaw={transform.eulerAngles.y:F1} mode={(data.CameraLocked ? "STRAFE" : "TANK")}");
            }

            // ── Roll ─────────────────────────────────────────────────────────────
            float rollHSpeed = movementSettings.rollDistance / movementSettings.rollDuration;

            // Tick active roll.
            if (_isRolling)
            {
                bool done = (movementSettings.rollMode == RollMode.Leap
                    ? rawGrounded && _verticalVelocity <= 0f  // Leap ends on actual landing
                    : ((_rollTimer -= dt) <= 0f));             // Dash ends on timer

                if (done) { _isRolling = false; _rollTimer = 0f; _rollDir = Vector3.zero; }
            }

            // Start new roll.
            if (data.Roll && !_isRolling && grounded && movementSettings.enableRoll
                && _stamina >= movementSettings.rollStaminaCost)
            {
                _isRolling = true;
                _rollTimer = movementSettings.rollDuration;
                _stamina   = Mathf.Max(0f, _stamina - movementSettings.rollStaminaCost);
                _rollDir   = inputDir.sqrMagnitude > 0.01f ? (camRot * inputDir).normalized : transform.forward;

                if (movementSettings.rollMode == RollMode.Leap && movementSettings.rollLeapHeight > 0f)
                {
                    _verticalVelocity = Mathf.Sqrt(2f * movementSettings.gravity * movementSettings.rollLeapHeight);
                    _spawnGraceIgnoreDownwardClampUntilGrounded = true;
                    Debug.Log($"[ROLL_FIX] Leap roll start -> allow falling during spawn grace. vertVel={_verticalVelocity:F3}");
                }
                
                OnRollTriggered?.Invoke(); // Fire animation event
            }

            // Apply roll override.
            if (_isRolling)
            {
                moveDir = _rollDir;
                if (movementSettings.rollMode == RollMode.Dash)
                {
                    // Ease-out trong rollEaseOutFraction cuối để kết thúc mượt.
                    float t = _rollTimer / movementSettings.rollDuration;
                    float w = movementSettings.rollEaseOutFraction;
                    speed = rollHSpeed * (w > 0f && t < w ? Mathf.SmoothStep(0f, 1f, t / w) : 1f);
                }
                else
                {
                    speed = rollHSpeed; // Leap: horizontal cố định, gravity tạo arc tự nhiên.
                }
            }

            // Gravity first; jump overrides after — ensures jump launch velocity is unmodified this tick.
            float vertVelBefore = _verticalVelocity;
            if (rawGrounded && _verticalVelocity <= 0f)
            {
                _verticalVelocity = -movementSettings.groundedStickDownVelocity;
            }
            else
            {
                float mult = _verticalVelocity < 0f ? Mathf.Max(1f, movementSettings.fallGravityMultiplier) : 1f;
                _verticalVelocity -= movementSettings.gravity * mult * dt;
                _verticalVelocity  = Mathf.Max(_verticalVelocity, -movementSettings.maxFallSpeed);
            }

            // Spawn grace: clamp downward velocity for the first N ticks after spawn.
            // Prevents free-fall on DS clients where the physics grounded-check may not yet
            // have a valid result while the first reconcile is still in transit.
            // Positive velocity (jump) is NOT clamped — jump still works in grace window.
            // IsReplaying guard: reconcile replay calls SimulateMovement multiple times in one
            // frame; without the guard the counter drains N× faster than real time.
            if (_spawnGraceTicksRemaining > 0 && !IsReplaying(state) && !_spawnGraceIgnoreDownwardClampUntilGrounded)
            {
                _spawnGraceTicksRemaining--;
                if (_verticalVelocity < 0f)
                    _verticalVelocity = 0f;
            }

            if (data.Jump && grounded && movementSettings.enableJump)
            {
                _verticalVelocity = Mathf.Sqrt(2f * movementSettings.gravity * movementSettings.jumpHeight);
                _spawnGraceIgnoreDownwardClampUntilGrounded = true;
                Debug.Log($"[ROLL_FIX] Jump start -> allow falling during spawn grace. vertVel={_verticalVelocity:F3}");
                OnJumpTriggered?.Invoke(); // Fire animation event
            }

            if (rawGrounded && _verticalVelocity <= 0f && _spawnGraceIgnoreDownwardClampUntilGrounded)
            {
                _spawnGraceIgnoreDownwardClampUntilGrounded = false;
                Debug.Log("[ROLL_FIX] Landed on raw ground -> restore spawn grace downward clamp.");
            }

            // ── [DIAG] FINAL MOVEMENT ─────────────────────────────────────────────
            Vector3 finalMovement = new Vector3(moveDir.x * speed, _verticalVelocity, moveDir.z * speed);
            if (diagLog)
            {
                Debug.Log(
                    $"[DIAG][MOVE] moveDir=({moveDir.x:F3},{moveDir.z:F3}) speed={speed:F2} " +
                    $"vertVel={_verticalVelocity:F3} (was {vertVelBefore:F3}) " +
                    $"rolling={_isRolling} grounded={grounded}\n" +
                    $"           → finalMovement=({finalMovement.x:F3},{finalMovement.y:F3},{finalMovement.z:F3})");
            }

            // ── [DIAG] SUSPICIOUS: no input nhưng finalMovement có horizontal ────
            if (diagnoseMysteryMove && IsOwner
                && inputDir.sqrMagnitude < 0.001f
                && !_isRolling
                && (Mathf.Abs(finalMovement.x) > 0.01f || Mathf.Abs(finalMovement.z) > 0.01f))
            {
                Debug.LogError(
                    $"[DIAG][⚠️ MYSTERY MOVE] Không có input nhưng horizontal != 0! " +
                    $"finalMovement=({finalMovement.x:F3},{finalMovement.y:F3},{finalMovement.z:F3}) " +
                    $"moveDir=({moveDir.x:F3},{moveDir.z:F3}) speed={speed:F2} rolling={_isRolling}");
            }

            // ── Apply ─────────────────────────────────────────────────────────────
            ApplyMovement(finalMovement, dt);
            _velocity = GetCurrentVelocity();

            // ── [DIAG] POSITION CHANGE ────────────────────────────────────────────
            if (diagnoseMysteryMove && IsOwner && inputDir.sqrMagnitude < 0.001f && !_isRolling)
            {
                // So sánh vị trí trước/sau ApplyMovement (chỉ hữu ích nếu log position trước)
                // → Để đơn giản, log velocity trả về.
                Vector3 appliedVel = GetCurrentVelocity();
                if (Mathf.Abs(appliedVel.x) > 0.01f || Mathf.Abs(appliedVel.z) > 0.01f)
                {
                    Debug.LogError(
                        $"[DIAG][⚠️ VEL POST-APPLY] Sau ApplyMovement: velocity=({appliedVel.x:F3},{appliedVel.y:F3},{appliedVel.z:F3}) " +
                        $"dù not available input! Check ApplyMovement log bên dưới.");
                }
            }
        }

        #endregion

        #region STAT HELPERS

        /// <summary>
        /// Get base movement speed from PlayerStatSystem (MovementSpeed) if available,
        /// otherwise fallback to MovementSettings.baseSpeed.
        /// </summary>
        protected virtual float GetBaseSpeed()
        {
            if (_playerStatSystem != null)
            {
                float statSpeed = _playerStatSystem.GetStat(PlayerStatType.MovementSpeed);
                if (statSpeed > 0f)
                    return statSpeed;
            }

            return movementSettings != null ? movementSettings.baseSpeed : 5f;
        }

        /// <summary>
        /// Get max stamina from PlayerStatSystem or fallback to MovementSettings.
        /// </summary>
        protected virtual float GetMaxStamina()
        {
            if (_playerStatSystem != null)
            {
                return _playerStatSystem.GetStat(PlayerStatType.MaxStamina);
            }

            return movementSettings != null ? movementSettings.maxStamina : 100f;
        }

        /// <summary>
        /// Get stamina regeneration rate from MovementSettings.
        /// </summary>
        protected virtual float GetStaminaRegenRate()
        {
            return movementSettings != null ? movementSettings.staminaRegenRate : 15f;
        }

        /// <summary>
        /// Get stamina drain rate from MovementSettings.
        /// </summary>
        protected virtual float GetStaminaDrainRate()
        {
            return movementSettings != null ? movementSettings.staminaDrainRate : 20f;
        }

        /// <summary>
        /// Get minimum stamina required to sprint from MovementSettings.
        /// </summary>
        protected virtual float GetMinStaminaToSprint()
        {
            return movementSettings != null ? movementSettings.minStaminaToSprint : 10f;
        }

        /// <summary>
        /// Get sprint speed multiplier from MovementSettings.
        /// </summary>
        protected virtual float GetSprintSpeedMultiplier()
        {
            return movementSettings != null ? movementSettings.sprintSpeedMultiplier : 1.6f;
        }

        #endregion

        #region RECONCILIATION

        public override void CreateReconcile()
        {
            if (!IsServerStarted || !IsSpawned)
                return;

            var data = new MovementReconcileData(
                transform.position,
                transform.rotation,
                _velocity,
                _stamina,
                _isRolling,
                _rollTimer,
                _rollDir
            );

            Reconcile(data, Channel.Unreliable);
        }

        protected override MovementReconcileData CreateReconcileData()
        {
            return new MovementReconcileData(
                transform.position,
                transform.rotation,
                _velocity,
                _stamina,
                _isRolling,
                _rollTimer,
                _rollDir
            );
        }

        [Reconcile]
        private void Reconcile(
            MovementReconcileData data,
            Channel channel = Channel.Unreliable)
        {
            if (IsOwner)
            {
                transform.position = data.Position;
                // Rotation is not reconciled — client-authoritative yaw converges deterministically.
                _velocity    = data.Velocity;
                _stamina     = data.Stamina;
                _isRolling   = data.IsRolling;
                _rollTimer   = data.RollTimer;
                _rollDir     = data.RollDir;
                // Restore authoritative vertical speed; avoids fall-speed loss mid-air.
                _verticalVelocity = data.Velocity.y;

                _groundedGraceTimer = 0f; // Reset coyote timer after reconcile teleport.
                ResetPhysicsState();
            }
            else if (!IsServerStarted)
            {
                _targetPosition = data.Position;
                _targetRotation = data.Rotation;
            }
        }

        #endregion

        #region NON OWNER INTERPOLATION

        protected virtual void Update()
        {
            if (!IsOwner && !IsServerStarted)
            {
                transform.position = Vector3.Lerp(
                    transform.position,
                    _targetPosition,
                    Time.deltaTime * interpolationSpeed
                );

                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    _targetRotation,
                    Time.deltaTime * interpolationSpeed
                );
            }
        }

        #endregion

        #region IMovementController IMPLEMENTATION

        public void SetMoveInput(Vector2 input) => _moveInput = input;
        public void SetSprinting(bool sprint) => _sprint = sprint;
        public void SetCrouching(bool crouch) => _crouch = crouch;
        public void SetCameraLock(bool locked) => _cameraLocked = locked;

        public float GetCurrentMoveSpeed() => _velocity.magnitude;
        public float GetStamina() => _stamina;
        /// <summary>Hysteresis-smoothed grounded state (same value the animator's OnGround param uses).</summary>
        public bool IsGroundedPublic => _groundedGraceTimer > 0f;
        public bool IsSprinting() => _sprint;
        public bool IsCrouching() => _crouch;
        public bool IsCameraLocked() => _cameraLocked;
        public bool IsRolling() => _isRolling;
        public Vector2 GetMoveInput() => _moveInput;
        public float GetCameraYaw() => _yaw;

        // SetWeightPenalty / SetStaminaDrainMultiplier are interface stubs.
        // Weight penalty logic now read directly from IPlayerStatSystem.GetMovementSpeedMultiplier().
        public virtual void SetWeightPenalty(float penalty) { /* handled via stat system */ }
        public virtual void SetStaminaDrainMultiplier(float multiplier)
        {
            _runtimeStaminaDrainMultiplier = Mathf.Max(0f, multiplier);
        }

        /// <summary>
        /// Set a flat speed multiplier applied every tick on top of the stat-system value.
        /// Use for zones (slow zone = 0.5), buffs (speed boost = 1.3), carry-weight penalties, etc.
        /// Caller is responsible for resetting to 1f when the effect ends.
        /// </summary>
        public void SetSpeedMultiplier(float multiplier)
        {
            _runtimeSpeedMultiplier = Mathf.Max(0f, multiplier);
        }

        public MovementState GetCurrentState()
        {
            return new MovementState
            {
                Position   = transform.position,
                Rotation   = transform.rotation,
                Velocity   = _velocity,
                IsSprinting = _sprint,
                IsCrouching = _crouch,
                Stamina    = _stamina
            };
        }

        public void SetState(MovementState state)
        {
            transform.position    = state.Position;
            transform.rotation    = state.Rotation;
            _velocity             = state.Velocity;
            _verticalVelocity     = state.Velocity.y;
            _sprint               = state.IsSprinting;
            _crouch               = state.IsCrouching;
            _stamina              = state.Stamina;
            ResetPhysicsState();
        }

        /// <summary>
        /// Server: Teleport the character to a new position/rotation.
        /// Resets velocity and delegates physics-component sync to derived classes
        /// via ResetPhysicsState (e.g. CharacterController disable/re-enable).
        /// The server's next reconcile tick will broadcast the corrected state.
        /// </summary>
        public virtual void Teleport(Vector3 position, Quaternion rotation)
        {
            transform.position = position;
            transform.rotation = rotation;
            _velocity = Vector3.zero;
            _verticalVelocity = 0f; // SimulateMovement sets correct value on first tick post-teleport.
            ResetPhysicsState();

            if (enableDebugLogs)
                Debug.Log($"[{GetType().Name}] Teleport → pos={position}, rot={rotation.eulerAngles}");
        }

        // IMovementController.Teleport bridge
        void IMovementController.Teleport(Vector3 position, Quaternion rotation)
            => Teleport(position, rotation);

        #endregion

        #region DEBUG

        protected virtual void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

            // Guard: NetworkObject may not be spawned yet (before OnStartNetwork) —
            // IsSpawned dereferences NB._networkObject which is null at that point → NullRef.
            // Use try-catch because FishNet doesn't expose a safe "is initialized" check.
            try
            {
                if (!IsSpawned) return;
            }
            catch
            {
                return;
            }

            if (IsOwner)
            {
                // Owner position (green)
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(transform.position, 0.3f);

                // Forward direction (blue)
                Gizmos.color = Color.blue;
                Gizmos.DrawRay(transform.position + Vector3.up, transform.forward * 2f);

                // Camera direction (yellow)
                if (_cinemachineCamera != null)
                {
                    Gizmos.color = Color.yellow;
                    Vector3 camForward = Quaternion.Euler(0, _yaw, 0) * Vector3.forward;
                    Gizmos.DrawRay(transform.position + Vector3.up * 1.5f, camForward * 1.5f);
                }

                // Mode indicator
                Gizmos.color = _cameraLocked ? Color.cyan : Color.magenta;
                Gizmos.DrawWireCube(transform.position + Vector3.up * 2.5f, Vector3.one * 0.3f);

                // Input direction (red)
                if (_moveInput.sqrMagnitude > 0.01f)
                {
                    Gizmos.color = Color.red;
                    Vector3 inputDir = new Vector3(_moveInput.x, 0, _moveInput.y);
                    Vector3 worldInput = Quaternion.Euler(0, _yaw, 0) * inputDir;
                    Gizmos.DrawRay(transform.position + Vector3.up, worldInput.normalized * 1.2f);
                }
            }
            else if (!IsServerStarted)
            {
                // Remote client (white)
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(transform.position, 0.3f);

                // Target position (cyan)
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(_targetPosition, 0.2f);
                Gizmos.DrawLine(transform.position, _targetPosition);
            }
        }

        protected virtual void OnGUI()
        {
            if (!IsOwner || !Application.isPlaying || !enableDebugLogs) return;

            GUI.color = Color.white;
            GUILayout.BeginArea(new Rect(10, 10, 500, 240));

            GUILayout.Label($"=== {GetType().Name.ToUpper()} DEBUG ===");
            GUILayout.Label($"Physics: {GetPhysicsComponentName()}");
            GUILayout.Label($"InputManager: {(InputManager.Instance != null ? "OK" : "NULL")}");
            GUILayout.Label($"Mode: {(_cameraLocked ? "STRAFE" : "TANK")} (Press Tab)");
            GUILayout.Label($"Input: ({_moveInput.x:F2}, {_moveInput.y:F2})");
            GUILayout.Label($"Velocity: {_velocity.magnitude:F2} m/s");
            GUILayout.Label($"Stamina: {_stamina:F0}/{movementSettings.maxStamina:F0}");
            GUILayout.Label($"Sprint: {_sprint} | Crouch: {_crouch}");
            GUILayout.Label($"IsGrounded: {IsGrounded()}");
            GUILayout.Label($"TickDelta: {TickDelta:F4}s");

            GUILayout.EndArea();
        }

        #endregion
    }
}
