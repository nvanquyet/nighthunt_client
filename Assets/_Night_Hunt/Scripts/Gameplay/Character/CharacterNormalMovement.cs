using UnityEngine;
using FishNet.Object;
using FishNet.Component.Transforming;
using NightHunt.Gameplay.Character.Movement;
using NightHunt.InteractionSystem.Utilities;
using Unity.Cinemachine;

namespace NightHunt.Gameplay.Character
{
    /// <summary>
    /// Simple movement system using RPC + NetworkTransform instead of client-side prediction.
    /// Simpler and easier to maintain, but may have higher latency than predicted movement.
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

        private void Awake()
        {
            // Use ComponentFinder to find components in hierarchy (supports child objects)
            _characterController = gameObject.FindInHierarchy<CharacterController>();
            _characterStats = gameObject.FindInHierarchy<CharacterStats>();
            _networkTransform = gameObject.FindInHierarchy<NetworkTransform>();
            _playerCamera = gameObject.FindInHierarchy<CinemachineCamera>();

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
            // QUAN TRỌNG: Movement speed CHỈ load từ CharacterStats system (stat config)
            // Không còn fallback về movementSettings.baseSpeed nữa
            // Điều này cho phép dynamic modifiers (booster, status effects, equipment, etc.)
            float baseSpeed = 5f; // Default fallback chỉ khi CharacterStats null (shouldn't happen)
            
            if (_characterStats != null)
            {
                float speedFromStats = _characterStats.GetSpeedMultiplier();
                if (speedFromStats > 0.01f)
                {
                    baseSpeed = speedFromStats;
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[NormalMovement] Using stat speed: {speedFromStats} (from CharacterStats)");
                    }
                }
                else
                {
                    Debug.LogError($"[NormalMovement] ❌ Stat speed is invalid ({speedFromStats})! Check CharacterStatsConfig MoveSpeed value. Using fallback: {baseSpeed}");
                }
            }
            else
            {
                Debug.LogError($"[NormalMovement] ❌ CharacterStats is NULL! Movement speed cannot be loaded. Using fallback: {baseSpeed}");
            }
            
            float finalSpeed = baseSpeed;

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

        private void UpdateInterpolation(float deltaTime)
        {
            // QUAN TRỌNG: NetworkTransform tự động sync position từ server
            // Không cần làm gì thêm, NetworkTransform đã handle interpolation
            // Chỉ cần đảm bảo NetworkTransform component được config đúng
        }

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

        private bool CanSprint()
        {
            return _currentStamina >= movementSettings.minStaminaToSprint;
        }

        private void LoadCharacterConfig()
        {
            if (_characterStats == null) return;
            // NOTE: Character stats are now loaded from CharacterStatsConfig ScriptableObject
            // or from CharacterStats component's serialized fields
            // No need to load from GameConfigLoader anymore
            // If needed, stamina can be read from CharacterStats.GetMaxStamina()
        }

        private MovementSettings CreateFallbackSettings()
        {
            MovementSettings settings = ScriptableObject.CreateInstance<MovementSettings>();
            // NOTE: baseSpeed đã được remove, giờ dùng CharacterStats system
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
