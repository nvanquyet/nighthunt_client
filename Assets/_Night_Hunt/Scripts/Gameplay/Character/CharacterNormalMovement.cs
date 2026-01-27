using UnityEngine;
using FishNet.Object;
using FishNet.Component.Transforming;
using NightHunt.Data;
using NightHunt.Gameplay.Character.Movement;
using Unity.Cinemachine;

namespace NightHunt.Gameplay.Character
{
    /// <summary>
    /// 🎮 SIMPLE MOVEMENT: Dùng RPC + NetworkTransform thay vì Client-Side Prediction
    /// 
    /// PROS:
    /// - Đơn giản hơn, ít bug hơn
    /// - Không cần reconcile logic phức tạp
    /// - Dễ debug và maintain
    /// 
    /// CONS:
    /// - Có thể lag hơn vì không có client-side prediction
    /// - Phụ thuộc vào network latency
    /// - Không responsive bằng prediction cho owner
    /// 
    /// USAGE:
    /// - Phù hợp cho casual game hoặc khi network tốt
    /// - Không phù hợp cho competitive FPS
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NetworkTransform))]
    public class CharacterNormalMovement : NetworkBehaviour, IMovementController
    {
        [Header("Settings")] 
        [SerializeField] private MovementSettings movementSettings;

        [Header("Network")]
        [Tooltip("Send rate for movement updates (times per second)")]
        [Range(10, 60)]
        [SerializeField] private int sendRate = 30;
        
        [Tooltip("Interpolation time for non-owner clients")]
        [Range(0.05f, 0.3f)]
        [SerializeField] private float interpolationTime = 0.15f;

        [Header("Debug")] 
        [SerializeField] private bool enableDebugLogs = false;

        // Components
        private CharacterController _characterController;
        private CharacterStats _characterStats;
        private NetworkTransform _networkTransform;
        private CinemachineCamera _playerCamera;

        // State
        private float _currentStamina;
        private float _currentMoveSpeed;
        private Vector3 _velocity;
        private float _verticalVelocity;
        private float _weightPenalty;
        private float _staminaDrainMultiplier = 1f;
        private bool _isGrounded;

        // Input
        private Vector2 _moveInput;
        private bool _sprintHeld;
        private bool _crouchHeld;

        // Network
        private float _lastSendTime;
        private float _sendInterval;

        // Interpolation (non-owner)
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private Vector3 _positionVelocity;

        // ============= INITIALIZATION =============

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _characterStats = GetComponent<CharacterStats>();
            _networkTransform = GetComponent<NetworkTransform>();
            _playerCamera = GetComponentInChildren<CinemachineCamera>();

            if (_characterController == null)
            {
                Debug.LogError("[NormalMovement] CharacterController is REQUIRED!", this);
                enabled = false;
                return;
            }

            if (_networkTransform == null)
            {
                Debug.LogError("[NormalMovement] NetworkTransform is REQUIRED!", this);
                enabled = false;
                return;
            }
        }

        private void Start()
        {
            if (movementSettings == null)
            {
                Debug.LogError("[NormalMovement] MovementSettings is NULL! Creating fallback...", this);
                movementSettings = CreateFallbackSettings();
            }
            
            LoadCharacterConfig();
            _sendInterval = 1f / sendRate;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            // Initialize state
            _currentStamina = movementSettings?.maxStamina ?? 100f;
            _verticalVelocity = 0f;
            _velocity = Vector3.zero;
            _isGrounded = false;

            if (transform != null)
            {
                _targetPosition = transform.position;
                _targetRotation = transform.rotation;
            }

            _positionVelocity = Vector3.zero;
            _lastSendTime = 0f;
        }

        // ============= UPDATE LOOPS =============

        private void Update()
        {
            if (!IsSpawned) return;

            float deltaTime = Time.deltaTime;

            // QUAN TRỌNG: Server phải simulate movement LIÊN TỤC (mỗi frame)
            // Không chỉ khi nhận RPC, để gravity và movement work đúng
            if (IsServerStarted)
            {
                // Server: Simulate movement liên tục trong Update
                SimulateMovement(deltaTime);
            }
            // Owner: Simulate movement locally (client-side prediction)
            else if (IsOwner)
            {
                // Owner: Simulate movement
                SimulateMovement(deltaTime);
                
                // QUAN TRỌNG: Owner phải sync với NetworkTransform position từ server
                // NetworkTransform sẽ override position nếu server position khác
                // Nhưng owner vẫn có thể predict trước
                
                // Send movement to server periodically
                if (Time.time - _lastSendTime >= _sendInterval)
                {
                    SendMovementToServer();
                    _lastSendTime = Time.time;
                }
            }
            // Non-owner: NetworkTransform tự động sync, không cần làm gì
            // NetworkTransform sẽ handle interpolation
        }
        
        // KHÔNG dùng FixedUpdate cho server vì đã simulate trong Update
        // FixedUpdate có thể gây double simulation

        private void LateUpdate()
        {
            if (!IsSpawned) return;

            if (_characterStats != null && IsOwner)
            {
                _characterStats.SetStamina(_currentStamina);
            }
        }

        // ============= MOVEMENT SIMULATION =============

        private void SimulateMovement(float deltaTime)
        {
            if (_characterController == null) return;

            _isGrounded = _characterController.isGrounded;

            // Stamina regen
            _currentStamina = Mathf.Min(
                _currentStamina + movementSettings.staminaRegenRate * deltaTime,
                movementSettings.maxStamina
            );

            // Calculate speed
            float finalSpeed = movementSettings.baseSpeed;

            if (_sprintHeld && CanSprint())
            {
                finalSpeed *= movementSettings.sprintMultiplier;
                _currentStamina = Mathf.Max(
                    0f,
                    _currentStamina - movementSettings.staminaDrainRate * _staminaDrainMultiplier * deltaTime
                );
            }
            else if (_crouchHeld)
            {
                finalSpeed *= movementSettings.crouchMultiplier;
            }

            finalSpeed *= (1f - _weightPenalty);

            // Movement direction
            Vector3 moveDir = new Vector3(_moveInput.x, 0f, _moveInput.y);
            if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

            // Apply rotation
            float cameraYaw = transform.eulerAngles.y;
            if (_playerCamera != null)
            {
                cameraYaw = _playerCamera.transform.eulerAngles.y;
            }
            Quaternion rotation = Quaternion.Euler(0f, cameraYaw, 0f);
            moveDir = rotation * moveDir;

            Vector3 horizontalVelocity = moveDir * finalSpeed;

            // QUAN TRỌNG: Gravity phải apply LIÊN TỤC trên server
            // Check grounded state TRƯỚC khi apply gravity
            if (_isGrounded)
            {
                // Reset vertical velocity khi grounded
                if (_verticalVelocity < 0f)
                {
                    _verticalVelocity = -2f; // Small downward force để stick to ground
                }
            }
            else
            {
                // Apply gravity khi không grounded
                // QUAN TRỌNG: Dùng Physics.gravity.y trực tiếp, không nhân 3
                _verticalVelocity += Physics.gravity.y * deltaTime;
                _verticalVelocity = Mathf.Max(_verticalVelocity, -40f); // Terminal velocity
            }

            // Apply movement
            Vector3 totalMovement = new Vector3(horizontalVelocity.x, _verticalVelocity, horizontalVelocity.z);
            _characterController.Move(totalMovement * deltaTime);

            // Rotate character
            if (moveDir.sqrMagnitude > 0.0001f)
            {
                transform.rotation = rotation;
            }

            _velocity = horizontalVelocity;
            _currentMoveSpeed = horizontalVelocity.magnitude;
        }

        // ============= NETWORK =============

        /// <summary>
        /// Send movement input to server via RPC
        /// </summary>
        private void SendMovementToServer()
        {
            if (!IsOwner) return;

            float cameraYaw = transform.eulerAngles.y;
            if (_playerCamera != null)
            {
                cameraYaw = _playerCamera.transform.eulerAngles.y;
            }

            RpcSendMovement(
                _moveInput,
                Quaternion.Euler(0f, cameraYaw, 0f),
                _sprintHeld,
                _crouchHeld,
                transform.position,
                _currentStamina
            );
        }

        /// <summary>
        /// RPC: Client sends movement to server
        /// </summary>
        [ServerRpc(RequireOwnership = true, RunLocally = false)]
        private void RpcSendMovement(
            Vector2 moveInput,
            Quaternion rotation,
            bool sprinting,
            bool crouching,
            Vector3 position,
            float stamina)
        {
            // Server validates and applies movement
            // NetworkTransform will sync position automatically
            ApplyMovementOnServer(moveInput, rotation, sprinting, crouching, stamina);
        }

        /// <summary>
        /// Server applies movement input (state update only)
        /// Server sẽ simulate movement liên tục trong Update
        /// </summary>
        private void ApplyMovementOnServer(
            Vector2 moveInput,
            Quaternion rotation,
            bool sprinting,
            bool crouching,
            float stamina)
        {
            // QUAN TRỌNG: Chỉ update input state, KHÔNG simulate ở đây
            // Server sẽ simulate liên tục trong Update/FixedUpdate
            _moveInput = moveInput;
            _sprintHeld = sprinting;
            _crouchHeld = crouching;
            _currentStamina = stamina;
            
            // Update rotation nếu cần
            if (rotation != Quaternion.identity)
            {
                transform.rotation = rotation;
            }

            // KHÔNG gọi SimulateMovement ở đây!
            // Server sẽ simulate liên tục trong Update/FixedUpdate
            // NetworkTransform sẽ tự động sync position
        }

        // ============= INTERPOLATION (NON-OWNER) =============

        private void UpdateInterpolation(float deltaTime)
        {
            // QUAN TRỌNG: NetworkTransform tự động sync position từ server
            // Không cần làm gì thêm, NetworkTransform đã handle interpolation
            // Chỉ cần đảm bảo NetworkTransform component được config đúng
        }

        // ============= IMovementController IMPLEMENTATION =============

        public void SetMoveInput(Vector2 input)
        {
            _moveInput = input;
        }

        public void SetSprinting(bool sprinting)
        {
            _sprintHeld = sprinting;
        }

        public void SetCrouching(bool crouching)
        {
            _crouchHeld = crouching;
        }

        public float GetCurrentMoveSpeed() => _currentMoveSpeed;
        public float GetStamina() => _currentStamina;
        public bool IsSprinting() => _sprintHeld && CanSprint();
        public bool IsCrouching() => _crouchHeld;

        public void SetWeightPenalty(float penalty)
        {
            _weightPenalty = Mathf.Clamp01(penalty);
        }

        public void SetStaminaDrainMultiplier(float multiplier)
        {
            _staminaDrainMultiplier = Mathf.Max(0f, multiplier);
        }

        public MovementState GetCurrentState()
        {
            return new MovementState
            {
                Position = transform.position,
                Rotation = transform.rotation,
                Velocity = _velocity,
                IsSprinting = _sprintHeld,
                IsCrouching = _crouchHeld,
                Stamina = _currentStamina
            };
        }

        public void SetState(MovementState state)
        {
            transform.position = state.Position;
            transform.rotation = state.Rotation;
            _velocity = state.Velocity;
            _sprintHeld = state.IsSprinting;
            _crouchHeld = state.IsCrouching;
            _currentStamina = state.Stamina;
        }

        // ============= UTILITIES =============

        private bool CanSprint()
        {
            return _currentStamina >= movementSettings.minStaminaToSprint;
        }

        private void LoadCharacterConfig()
        {
            if (_characterStats == null) return;
            var config = GameConfigLoader.Instance?.GetCharacterConfig("CHAR_DEFAULT");
            if (config != null)
            {
                movementSettings.baseSpeed = config.BaseMoveSpeed;
                movementSettings.maxStamina = config.BaseStamina;
            }
        }

        private MovementSettings CreateFallbackSettings()
        {
            MovementSettings settings = ScriptableObject.CreateInstance<MovementSettings>();
            settings.baseSpeed = 5f;
            settings.sprintMultiplier = 1.5f;
            settings.crouchMultiplier = 0.6f;
            settings.maxStamina = 100f;
            settings.staminaDrainRate = 20f;
            settings.staminaRegenRate = 15f;
            settings.minStaminaToSprint = 10f;
            return settings;
        }
    }
}
