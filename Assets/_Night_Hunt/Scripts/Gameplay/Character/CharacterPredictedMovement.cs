using UnityEngine;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using NightHunt.Gameplay.Character.Movement;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Networking.Prediction.FishNet;
using NightHunt.StatSystem.Core.Interfaces;
using NightHunt.StatSystem.Core.Types;
using Unity.Cinemachine;

namespace NightHunt.Gameplay.Character
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class CharacterPredictedMovement
        : FishNetPredictedBehaviour<MovementReplicateData, MovementReconcileData>,
          IMovementController
    {
        [Header("Movement Settings")]
        [SerializeField] private MovementSettings movementSettings;

        [Header("Rotation")]
        [SerializeField] private float tankTurnSpeed = 10f;
        [SerializeField] private float lockTurnSpeed = 18f;

        [Header("Camera Lock")]
        [SerializeField] private bool allowCameraLockToggle = true;
        [SerializeField] private bool startWithCameraLock = false;

        [Header("Network Interpolation")]
        [SerializeField] private float interpolationSpeed = 15f;

        [Header("Stamina Recovery")]
        [Tooltip("Thời gian delay (giây) trước khi bắt đầu hồi stamina khi đứng yên/di chuyển chậm")]
        [SerializeField] private float staminaRecoveryDelay = 1.5f;
        [Tooltip("Tốc độ di chuyển tối đa (m/s) để được coi là 'di chuyển chậm'")]
        [SerializeField] private float slowMovementThreshold = 1.0f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        // Components
        private CharacterController _cc;
        private CinemachineCamera _camera;
        private IPlayerStatSystem _playerStatSystem;

        // ===== INPUT (OWNER ONLY) =====
        private Vector2 _moveInput;
        private bool _sprint;
        private bool _crouch;
        private bool _cameraLocked;
        private float _yaw;

        // ===== STATE =====
        private Vector3 _velocity;
        private float _verticalVelocity;
        private float _stamina;
        
        // ===== STAMINA RECOVERY =====
        private float _staminaRecoveryTimer = 0f; // Timer đếm thời gian đứng yên/chậm

        // ===== NON OWNER INTERPOLATION =====
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;

        #region INIT

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _camera = GetComponentInChildren<CinemachineCamera>();
            _playerStatSystem = GetComponent<IPlayerStatSystem>();

            if (_cc == null)
            {
                Debug.LogError("[CharacterPredictedMovement] CharacterController NOT FOUND!");
            }
            
            if (_playerStatSystem == null)
            {
                Debug.LogWarning("[CharacterPredictedMovement] IPlayerStatSystem NOT FOUND! Stamina will use MovementSettings fallback.");
            }
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            // Initialize stamina: ưu tiên current value từ PlayerStatSystem (ví dụ sau respawn có thể không full)
            // Fallback về maxStamina nếu stat system null hoặc chưa init (trả về 0)
            float maxStamina = GetMaxStamina();
            float currentStamina = _playerStatSystem != null
                ? _playerStatSystem.GetStat(PlayerStatType.Stamina)
                : 0f;
            _stamina = currentStamina > 0f ? currentStamina : maxStamina;
            _targetPosition = transform.position;
            _targetRotation = transform.rotation;
            _verticalVelocity = -2f;
            _staminaRecoveryTimer = 0f;
            
            // Initialize camera lock state
            _cameraLocked = startWithCameraLock;

            if (enableDebugLogs)
                Debug.Log($"[CharacterPredictedMovement] OnStartNetwork - IsOwner={base.Owner.IsLocalClient}, IsServer={IsServerStarted}, CameraLock={_cameraLocked}, MaxStamina={maxStamina}");
        }

        #endregion

        // ===================== INPUT GATHERING =====================

        /// <summary>
        /// Get input from InputManager - EXACTLY LIKE CharacterNormalMovement
        /// </summary>
        private void GatherInput()
        {
            if (!IsOwner) return;

            var inputManager = InputManager.Instance;
            if (inputManager == null)
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[CharacterPredictedMovement] InputManager.Instance is NULL!");
                return;
            }

            var handler = inputManager.MovementHandler;
            if (handler == null)
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[CharacterPredictedMovement] MovementHandler is NULL!");
                return;
            }

            // Get movement input
            _moveInput = handler.GetMoveInput();
            _sprint = handler.IsSprinting();
            _crouch = handler.IsCrouching();
            
            // Camera lock toggle - SAME AS CharacterNormalMovement
            if (allowCameraLockToggle)
            {
                bool newCameraLockState = handler.IsCameraLocked();
                
                // Log when state changes
                if (newCameraLockState != _cameraLocked)
                {
                    _cameraLocked = newCameraLockState;
                    Debug.Log($"[CharacterPredictedMovement] Camera Lock changed to: {(_cameraLocked ? "STRAFE" : "TANK")}");
                }
            }

            // Capture camera yaw
            if (_camera != null)
            {
                _yaw = _camera.transform.eulerAngles.y;
            }

            if (enableDebugLogs && _moveInput.sqrMagnitude > 0.01f)
            {
                Debug.Log($"[GatherInput] move={_moveInput}, sprint={_sprint}, lock={_cameraLocked}, yaw={_yaw:F1}");
            }
        }

        // ===================== INPUT API (for manual override if needed) =====================

        public void SetMoveInput(Vector2 input) => _moveInput = input;
        public void SetSprinting(bool sprint) => _sprint = sprint;
        public void SetCrouching(bool crouch) => _crouch = crouch;
        public void SetCameraLock(bool locked)
        {
            if (_cameraLocked != locked)
            {
                _cameraLocked = locked;
                Debug.Log($"[CharacterPredictedMovement] Manual camera lock set to: {(_cameraLocked ? "STRAFE" : "TANK")}");
            }
        }

        // ===================== TICK =====================

        protected override void TimeManager_OnTick()
        {
            // ONLY Owner + Server simulate during OnTick
            if (!IsOwner && !IsServerStarted)
                return;

            // CRITICAL: Owner must gather input FIRST
            if (IsOwner)
            {
                GatherInput();
            }

            // Build replicate data from current input state
            MovementReplicateData replicateData = new(
                _moveInput,
                _yaw,
                _sprint,
                _crouch,
                _cameraLocked
            );

            if (enableDebugLogs && _moveInput.sqrMagnitude > 0.01f)
            {
                Debug.Log($"[Tick] Sending replicate: move={replicateData.Move}, yaw={replicateData.Yaw:F1}, cameraLocked={replicateData.CameraLocked}");
            }

            // Send to server (owner) or process locally (server)
            Replicate(replicateData, ReplicateState.Ticked, Channel.Unreliable);

            // Server-only: tạo và gửi reconcile data về client
            // Client KHÔNG gọi — chỉ nhận từ server
            if (IsServerStarted)
            {
                CreateReconcile();
            }
        }

        // ===================== REPLICATE =====================

        [Replicate]
        private void Replicate(
            MovementReplicateData data,
            ReplicateState state = ReplicateState.Invalid,
            Channel channel = Channel.Unreliable)
        {
            if (enableDebugLogs && data.Move.sqrMagnitude > 0.01f)
            {
                Debug.Log($"[Replicate] Simulating: move={data.Move}, state={state}, cameraLocked={data.CameraLocked}");
            }

            // Simulate movement with fixed TickDelta
            SimulateMovement(data, TickDelta);
        }

        // ===================== MOVEMENT SIM =====================

        private void SimulateMovement(MovementReplicateData data, float dt)
        {
            if (_cc == null) return;

            bool grounded = _cc.isGrounded;

            // ===== CALCULATE SPEED FIRST (for stamina logic) =====
            // Lấy baseSpeed từ PlayerStatSystem.MovementSpeed (có modifier từ item/buff)
            // Fallback về movementSettings.baseSpeed nếu stat system null
            float speed = GetBaseSpeed();
            bool canSprint = data.Sprint && _stamina > GetMinStaminaToSprint();

            if (canSprint)
            {
                // Sử dụng SprintSpeedMultiplier từ GameConfigData, fallback về movementSettings
                float sprintMultiplier = GetSprintSpeedMultiplier();
                speed *= sprintMultiplier;
            }
            else if (data.Crouch)
            {
                speed *= movementSettings.crouchMultiplier;
            }

            // ===== STAMINA LOGIC =====
            // Movement LUÔN tự tính drain + regen dựa trên MovementSettings config.
            // PlayerStatSystem chỉ là data-store/sync layer — không có logic regen riêng.
            // Sau khi tính xong: nếu có stat system → write-back lên server để SyncList → UI/client.
            // Nếu không có stat system → _stamina local tự đủ (fallback/standalone mode).
            float maxStamina = GetMaxStamina();
            Vector3 inputDir = new Vector3(data.Move.x, 0f, data.Move.y);
            float inputMagnitude = inputDir.magnitude;

            // ── DRAIN khi sprint ─────────────────────────────────────────────
            if (data.Sprint && canSprint)
            {
                float drainRate = GetStaminaDrainRate();
                _stamina = Mathf.Max(0f, _stamina - drainRate * dt);
            }
            else
            {
                // ── REGEN khi đứng yên / di chuyển chậm / không sprint ───────
                // Phải đợi staminaRecoveryDelay giây mới bắt đầu hồi
                float expectedSpeed   = inputMagnitude * speed;
                bool isIdle           = inputMagnitude < 0.01f;
                bool isMovingSlowly   = !isIdle && expectedSpeed <= slowMovementThreshold;
                bool isIdleOrSlow     = isIdle || isMovingSlowly;
                bool isBelowThreshold = _stamina < GetMinStaminaToSprint(); // hồi kể cả khi dưới ngưỡng sprint

                if (isIdleOrSlow || isBelowThreshold)
                {
                    _staminaRecoveryTimer += dt;
                    if (_staminaRecoveryTimer >= staminaRecoveryDelay)
                    {
                        float regenRate = GetStaminaRegenRate();
                        _stamina = Mathf.Min(_stamina + regenRate * dt, maxStamina);
                    }
                }
                else
                {
                    // Đang di chuyển nhanh (không sprint) → reset timer
                    _staminaRecoveryTimer = 0f;
                }
            }

            // ── WRITE-BACK lên PlayerStatSystem (server only) ─────────────────
            // PlayerStatSystem.SyncList tự broadcast về tất cả client → UI đọc đúng.
            // Client KHÔNG ghi — giá trị đúng đến từ Reconcile mỗi tick.
            if (IsServerStarted && _playerStatSystem != null)
            {
                _playerStatSystem.SetCurrentStat(PlayerStatType.Stamina, _stamina);
            }

            // ===== INPUT DIRECTION =====
            if (inputDir.sqrMagnitude > 1f)
                inputDir.Normalize();

            Quaternion camRot = Quaternion.Euler(0f, data.Yaw, 0f);
            Vector3 moveDir = Vector3.zero;

            // ===== ROTATION & MOVEMENT =====
            if (data.CameraLocked)
            {
                // ============= STRAFE MODE =============
                // Always face camera direction
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    camRot,
                    lockTurnSpeed * dt * 100f
                );

                // Move relative to camera
                if (inputDir.sqrMagnitude > 0.001f)
                {
                    moveDir = camRot * inputDir;
                }
            }
            else
            {
                // ============= TANK MODE =============
                if (inputDir.sqrMagnitude > 0.001f)
                {
                    // Calculate world move direction
                    moveDir = camRot * inputDir;
                    
                    // Rotate towards move direction
                    Quaternion targetRot = Quaternion.LookRotation(moveDir);
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation,
                        targetRot,
                        tankTurnSpeed * dt * 100f
                    );
                }
                else
                {
                    // Idle: slowly turn towards camera
                    float angle = Quaternion.Angle(transform.rotation, camRot);
                    if (angle > 5f)
                    {
                        transform.rotation = Quaternion.RotateTowards(
                            transform.rotation,
                            camRot,
                            tankTurnSpeed * 0.5f * dt * 100f
                        );
                    }
                }
            }

            // ===== GRAVITY =====
            if (grounded)
            {
                if (_verticalVelocity < 0f)
                    _verticalVelocity = -2f;
            }
            else
            {
                _verticalVelocity += Physics.gravity.y * dt;
            }

            // ===== APPLY MOVEMENT =====
            Vector3 finalMove = moveDir * speed;
            finalMove.y = _verticalVelocity;

            if (enableDebugLogs && data.Move.sqrMagnitude > 0.01f)
            {
                Debug.Log($"[Move] moveDir={moveDir}, speed={speed}, finalMove*dt={(finalMove * dt).magnitude:F4}");
            }

            _cc.Move(finalMove * dt);
            _velocity = finalMove;
        }
        
        // ===================== STAMINA HELPERS =====================
        
        /// <summary>
        /// Lấy base movement speed từ PlayerStatSystem.MovementSpeed
        /// Stat này có thể bị modify bởi items/buffs (ví dụ: boots tăng speed)
        /// Fallback về movementSettings.baseSpeed nếu stat system null
        /// </summary>
        private float GetBaseSpeed()
        {
            if (_playerStatSystem != null)
            {
                float statSpeed = _playerStatSystem.GetStat(PlayerStatType.MovementSpeed);
                // Chỉ dùng stat nếu hợp lệ (> 0), tránh trường hợp chưa init
                if (statSpeed > 0f) return statSpeed;
            }
            return movementSettings != null ? movementSettings.baseSpeed : 5f;
        }

        /// <summary>
        /// Lấy max stamina từ PlayerStatSystem hoặc fallback về MovementSettings
        /// </summary>
        private float GetMaxStamina()
        {
            if (_playerStatSystem != null)
            {
                return _playerStatSystem.GetStat(PlayerStatType.MaxStamina);
            }
            return movementSettings != null ? movementSettings.maxStamina : 100f;
        }
        
        /// <summary>
        /// Lấy tốc độ hồi stamina từ MovementSettings
        /// </summary>
        private float GetStaminaRegenRate()
        {
            return movementSettings != null ? movementSettings.staminaRegenRate : 15f;
        }
        
        /// <summary>
        /// Lấy tốc độ tiêu hao stamina từ MovementSettings
        /// </summary>
        private float GetStaminaDrainRate()
        {
            return movementSettings != null ? movementSettings.staminaDrainRate : 20f;
        }
        
        /// <summary>
        /// Lấy min stamina cần để sprint từ MovementSettings
        /// </summary>
        private float GetMinStaminaToSprint()
        {
            return movementSettings != null ? movementSettings.minStaminaToSprint : 10f;
        }
        
        /// <summary>
        /// Lấy sprint speed multiplier từ MovementSettings
        /// </summary>
        private float GetSprintSpeedMultiplier()
        {
            return movementSettings != null ? movementSettings.sprintSpeedMultiplier : 1.6f;
        }

        // ===================== RECONCILE =====================

        public override void CreateReconcile()
        {
            // Chỉ server mới tạo và gửi reconcile data
            // FishNet tự route về đúng owner client
            if (!IsServerStarted || !IsSpawned) return;

            MovementReconcileData reconcileData = CreateReconcileData();
            Reconcile(reconcileData, Channel.Unreliable);
        }

        protected override MovementReconcileData CreateReconcileData()
        {
            return new MovementReconcileData(
                transform.position,
                transform.rotation,
                _velocity,
                _stamina
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
                transform.rotation = data.Rotation;
                _velocity = data.Velocity;
                _stamina = data.Stamina;
            }
            else if (!IsServerStarted)
            {
                _targetPosition = data.Position;
                _targetRotation = data.Rotation;
            }
        }

        // ===================== NON OWNER INTERPOLATION =====================

        private void Update()
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

        // ===================== IMovementController =====================

        public float GetCurrentMoveSpeed() => _velocity.magnitude;
        public float GetStamina() => _stamina;
        public bool IsSprinting() => _sprint;
        public bool IsCrouching() => _crouch;
        public bool IsCameraLocked() => _cameraLocked;

        public void SetWeightPenalty(float penalty) { }
        public void SetStaminaDrainMultiplier(float multiplier) { }

        public MovementState GetCurrentState()
        {
            return new MovementState
            {
                Position = transform.position,
                Rotation = transform.rotation,
                Velocity = _velocity
            };
        }

        public void SetState(MovementState state)
        {
            transform.position = state.Position;
            transform.rotation = state.Rotation;
            _velocity = state.Velocity;
        }

        // ===================== DEBUG ======================

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

            if (IsOwner)
            {
                // Owner predicted position (green)
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(transform.position, 0.3f);
                
                // Forward direction (blue)
                Gizmos.color = Color.blue;
                Gizmos.DrawRay(transform.position + Vector3.up, transform.forward * 2f);

                // Camera direction (yellow)
                if (_camera != null)
                {
                    Gizmos.color = Color.yellow;
                    Vector3 camForward = Quaternion.Euler(0, _yaw, 0) * Vector3.forward;
                    Gizmos.DrawRay(transform.position + Vector3.up * 1.5f, camForward * 1.5f);
                }

                // Camera lock indicator
                if (_cameraLocked)
                {
                    // STRAFE MODE - Cyan cube
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawWireCube(transform.position + Vector3.up * 2.5f, Vector3.one * 0.3f);
                }
                else
                {
                    // TANK MODE - Magenta cube
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawWireCube(transform.position + Vector3.up * 2.5f, Vector3.one * 0.2f);
                }

                // Movement input direction (red)
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

        private void OnGUI()
        {
            if (!IsOwner || !Application.isPlaying || !enableDebugLogs) return;

            GUI.color = Color.white;
            GUILayout.BeginArea(new Rect(10, 10, 500, 220));
            
            GUILayout.Label($"=== PREDICTION DEBUG ===");
            GUILayout.Label($"InputManager: {(InputManager.Instance != null ? "OK" : "NULL!")}");
            GUILayout.Label($"Camera Lock Toggle Allowed: {allowCameraLockToggle}");
            GUILayout.Label($"Mode: {(_cameraLocked ? "STRAFE (Camera Lock ON)" : "TANK (Camera Lock OFF)")}");
            GUILayout.Label($"Press [Tab] to toggle camera lock");
            GUILayout.Label($"Input: ({_moveInput.x:F2}, {_moveInput.y:F2})");
            GUILayout.Label($"Velocity: {_velocity.magnitude:F2} m/s");
            GUILayout.Label($"Stamina: {_stamina:F0} / {GetMaxStamina():F0}");
            GUILayout.Label($"Recovery Timer: {_staminaRecoveryTimer:F2}s / {staminaRecoveryDelay:F2}s");
            GUILayout.Label($"Sprint: {_sprint} | Crouch: {_crouch}");
            GUILayout.Label($"Camera Yaw: {_yaw:F1}°");
            GUILayout.Label($"TickDelta: {TickDelta:F4}s");
            
            GUILayout.EndArea();
        }
    }
}