using UnityEngine;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using NightHunt.Gameplay.Character.Movement;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Networking.Prediction.FishNet;
using Unity.Cinemachine;

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
        [Header("Movement Settings")]
        [SerializeField] protected MovementSettings movementSettings;

        [Header("Rotation")]
        [SerializeField] protected float tankTurnSpeed = 10f;
        [SerializeField] protected float lockTurnSpeed = 18f;

        [Header("Camera Lock")]
        [SerializeField] protected bool allowCameraLockToggle = true;
        [SerializeField] protected bool startWithCameraLock = false;

        [Header("Network Interpolation")]
        [SerializeField] protected float interpolationSpeed = 15f;

        [Header("Debug")]
        [SerializeField] protected bool enableDebugLogs = false;

        // Components
        protected CinemachineCamera _camera;

        // ===== INPUT (OWNER ONLY) =====
        protected Vector2 _moveInput;
        protected bool _sprint;
        protected bool _crouch;
        protected bool _cameraLocked;
        protected float _yaw;

        // ===== STATE =====
        protected Vector3 _velocity;
        protected float _verticalVelocity;
        protected float _stamina;

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

        /// <summary>
        /// Initialize physics components in Awake
        /// </summary>
        protected abstract void InitializePhysicsComponents();

        /// <summary>
        /// Get physics component name for debug display
        /// </summary>
        protected abstract string GetPhysicsComponentName();

        #endregion

        #region UNITY LIFECYCLE

        private void Awake()
        {
            _camera = GetComponentInChildren<CinemachineCamera>();
            InitializePhysicsComponents();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            _stamina = movementSettings.maxStamina;
            _targetPosition = transform.position;
            _targetRotation = transform.rotation;
            _verticalVelocity = -2f;
            _cameraLocked = startWithCameraLock;

            if (enableDebugLogs)
                Debug.Log($"[{GetType().Name}] OnStartNetwork - IsOwner={base.Owner.IsLocalClient}, IsServer={IsServerStarted}");
        }

        #endregion

        #region INPUT GATHERING

        /// <summary>
        /// Get input from InputManager - consistent with CharacterNormalMovement
        /// </summary>
        protected virtual void GatherInput()
        {
            if (!IsOwner) return;

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

            // Capture camera yaw
            if (_camera != null)
            {
                _yaw = _camera.transform.eulerAngles.y;
            }
        }

        #endregion

        #region TICK & REPLICATION

        protected override void TimeManager_OnTick()
        {
            if (!IsOwner && !IsServerStarted)
                return;

            // Owner: gather input first
            if (IsOwner)
            {
                GatherInput();
            }

            // Build replicate data
            MovementReplicateData replicateData = new(
                _moveInput,
                _yaw,
                _sprint,
                _crouch,
                _cameraLocked
            );

            // Send to server
            Replicate(replicateData, ReplicateState.Ticked, Channel.Unreliable);

            // Create reconcile
            CreateReconcile();
        }

        [Replicate]
        private void Replicate(
            MovementReplicateData data,
            ReplicateState state = ReplicateState.Invalid,
            Channel channel = Channel.Unreliable)
        {
            SimulateMovement(data, TickDelta);
        }

        #endregion

        #region MOVEMENT SIMULATION

        /// <summary>
        /// Core movement simulation - same for all physics implementations
        /// </summary>
        protected virtual void SimulateMovement(MovementReplicateData data, float dt)
        {
            bool grounded = IsGrounded();

            // ===== STAMINA =====
            _stamina = Mathf.Min(
                _stamina + movementSettings.staminaRegenRate * dt,
                movementSettings.maxStamina
            );

            float speed = movementSettings.baseSpeed;

            if (data.Sprint && _stamina > movementSettings.minStaminaToSprint)
            {
                speed *= movementSettings.sprintMultiplier;
                _stamina -= movementSettings.staminaDrainRate * dt;
            }
            else if (data.Crouch)
            {
                speed *= movementSettings.crouchMultiplier;
            }

            // ===== INPUT DIRECTION =====
            Vector3 inputDir = new Vector3(data.Move.x, 0f, data.Move.y);
            if (inputDir.sqrMagnitude > 1f)
                inputDir.Normalize();

            Quaternion camRot = Quaternion.Euler(0f, data.Yaw, 0f);
            Vector3 moveDir = Vector3.zero;

            // ===== ROTATION & MOVEMENT =====
            if (data.CameraLocked)
            {
                // STRAFE MODE
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    camRot,
                    lockTurnSpeed * dt * 100f
                );

                if (inputDir.sqrMagnitude > 0.001f)
                {
                    moveDir = camRot * inputDir;
                }
            }
            else
            {
                // TANK MODE
                if (inputDir.sqrMagnitude > 0.001f)
                {
                    moveDir = camRot * inputDir;
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
            Vector3 horizontalMovement = moveDir * speed;
            Vector3 finalMovement = new Vector3(
                horizontalMovement.x,
                _verticalVelocity,
                horizontalMovement.z
            );

            // Delegate to physics implementation
            ApplyMovement(finalMovement, dt);

            // Update velocity from physics component
            _velocity = GetCurrentVelocity();
        }

        #endregion

        #region RECONCILIATION

        public override void CreateReconcile()
        {
            if (!IsSpawned) return;

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

                // Reset physics state
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
        public bool IsSprinting() => _sprint;
        public bool IsCrouching() => _crouch;
        public bool IsCameraLocked() => _cameraLocked;

        public virtual void SetWeightPenalty(float penalty) { }
        public virtual void SetStaminaDrainMultiplier(float multiplier) { }

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

        #endregion

        #region DEBUG

        protected virtual void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

            if (IsOwner)
            {
                // Owner position (green)
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