// using UnityEngine;
// using FishNet.Object;
// using FishNet.Component.Transforming;
// using NightHunt.Data;
// using NightHunt.Gameplay.Character.Movement;
// using NightHunt.Gameplay.Character.Stats;
// using NightHunt.Gameplay.Input.Core;
// using NightHunt.Inventory.Stats;
// using Unity.Cinemachine;
//
// namespace NightHunt.Gameplay.Character
// {
//     [RequireComponent(typeof(CharacterController))]
//     [RequireComponent(typeof(NetworkTransform))]
//     public class CharacterNormalMovement : NetworkBehaviour, IMovementController
//     {
//         [Header("Settings")] 
//         [SerializeField] private MovementSettings movementSettings;
//
//         [Header("Control Mode")]
//         [SerializeField] private bool allowCameraLockToggle = true;
//         [SerializeField] private bool startWithCameraLock = false;
//         [Range(5f, 20f)] [SerializeField] private float freeModeRotationSpeed = 10f;
//         [Range(10f, 30f)] [SerializeField] private float lockModeRotationSpeed = 20f;
//
//         [Header("Network")]
//         [Range(10, 60)] [SerializeField] private int sendRate = 30;
//         [Range(10, 30)] [SerializeField] private int rotationSyncRate = 20;
//
//         [Header("Reconciliation")]
//         [SerializeField] private float reconcileThreshold = 0.5f;
//         [SerializeField] private float reconcileSpeed = 10f;
//
//         [Header("Debug")] 
//         [SerializeField] private bool enableDebugLogs = false;
//
//         // Components
//         private CharacterController _characterController;
//         private PlayerStats _playerStats;
//         private NetworkTransform _networkTransform;
//         private CinemachineCamera _playerCamera;
//
//         // Movement State
//         private float _currentStamina;
//         private float _currentMoveSpeed;
//         private Vector3 _velocity;
//         private float _verticalVelocity;
//         private float _weightPenalty;
//         private float _staminaDrainMultiplier = 1f;
//         private bool _isGrounded;
//
//         // Input State
//         private Vector2 _moveInput;
//         private bool _sprintHeld;
//         private bool _crouchHeld;
//         private float _cameraYaw;
//         private bool _isCameraLocked;
//
//         // Movement Direction (dùng cho Tank mode - tránh giật)
//         private Vector3 _targetMoveDirection;
//
//         // Network Sync
//         private float _lastInputSendTime;
//         private float _lastRotationSyncTime;
//         private float _inputSendInterval;
//         private float _rotationSyncInterval;
//
//         // Reconciliation
//         private Vector3 _serverPosition;
//
//         // Remote Client Interpolation (cho rotation)
//         private Quaternion _remoteTargetRotation;
//         private float _remoteRotationSpeed = 15f;
//
//         // ============= INITIALIZATION =============
//
//         private void Awake()
//         {
//             _characterController = GetComponent<CharacterController>();
//             _playerStats = GetComponent<PlayerStats>();
//             _networkTransform = GetComponent<NetworkTransform>();
//             _playerCamera = GetComponentInChildren<CinemachineCamera>();
//
//             if (_characterController == null || _networkTransform == null)
//             {
//                 Debug.LogError("[Movement] Missing required components!");
//                 enabled = false;
//             }
//         }
//
//         private void Start()
//         {
//             if (movementSettings == null)
//             {
//                 movementSettings = CreateFallbackSettings();
//             }
//
//             LoadCharacterConfig();
//             _inputSendInterval = 1f / sendRate;
//             _rotationSyncInterval = 1f / rotationSyncRate;
//             _isCameraLocked = startWithCameraLock;
//         }
//
//         public override void OnStartNetwork()
//         {
//             base.OnStartNetwork();
//
//             _currentStamina = movementSettings?.maxStamina ?? 100f;
//             _verticalVelocity = 0f;
//             _velocity = Vector3.zero;
//             _isGrounded = false;
//             _targetMoveDirection = Vector3.zero;
//
//             if (transform != null)
//             {
//                 _serverPosition = transform.position;
//                 _remoteTargetRotation = transform.rotation;
//             }
//
//             _lastInputSendTime = 0f;
//             _lastRotationSyncTime = 0f;
//         }
//
//         // ============= INPUT =============
//
//         private void GetMovementState()
//         {
//             if (!IsOwner) return;
//             
//             var handler = InputManager.Instance?.MovementHandler;
//             if (handler == null) return;
//             
//             _moveInput = handler.GetMoveInput();
//             _sprintHeld = handler.IsSprinting();
//             _crouchHeld = handler.IsCrouching();
//             
//             if (allowCameraLockToggle)
//             {
//                 _isCameraLocked = handler.IsCameraLocked();
//             }
//             
//             if (_playerCamera != null)
//             {
//                 _cameraYaw = _playerCamera.transform.eulerAngles.y;
//             }
//         }
//
//         // ============= UPDATE =============
//
//         private void Update()
//         {
//             if (!IsSpawned) return;
//
//             float deltaTime = Time.deltaTime;
//
//             if (IsServerStarted)
//             {
//                 // Server: simulate movement
//                 SimulateMovement(deltaTime);
//             }
//             else if (IsOwner)
//             {
//                 // Owner: get input + predict + send to server
//                 GetMovementState();
//                 SimulateMovement(deltaTime);
//
//                 // Send input to server
//                 if (Time.time - _lastInputSendTime >= _inputSendInterval)
//                 {
//                     SendInputToServer();
//                     _lastInputSendTime = Time.time;
//                 }
//
//                 // Send rotation to other clients
//                 if (Time.time - _lastRotationSyncTime >= _rotationSyncInterval)
//                 {
//                     SyncRotationToClients(transform.rotation);
//                     _lastRotationSyncTime = Time.time;
//                 }
//
//                 // Reconcile position với server
//                 CheckReconciliation();
//             }
//             else
//             {
//                 // Remote client: interpolate rotation từ owner
//                 InterpolateRemoteRotation(deltaTime);
//             }
//         }
//
//         private void LateUpdate()
//         {
//             if (!IsSpawned || !IsOwner) return;
//
//             if (_playerStats != null)
//             {
//                 _playerStats.SetStamina(_currentStamina);
//             }
//         }
//
//         // ============= MOVEMENT SIMULATION =============
//
//         private void SimulateMovement(float deltaTime)
//         {
//             if (_characterController == null) return;
//
//             _isGrounded = _characterController.isGrounded;
//
//             // Stamina
//             _currentStamina = Mathf.Min(
//                 _currentStamina + movementSettings.staminaRegenRate * deltaTime,
//                 movementSettings.maxStamina
//             );
//
//             // Speed calculation
//             float finalSpeed = movementSettings.baseSpeed;
//
//             if (_sprintHeld && CanSprint())
//             {
//                 finalSpeed *= movementSettings.sprintMultiplier;
//                 _currentStamina = Mathf.Max(0f, 
//                     _currentStamina - movementSettings.staminaDrainRate * _staminaDrainMultiplier * deltaTime);
//             }
//             else if (_crouchHeld)
//             {
//                 finalSpeed *= movementSettings.crouchMultiplier;
//             }
//
//             finalSpeed *= (1f - _weightPenalty);
//
//             // Input direction
//             Vector3 inputDir = new Vector3(_moveInput.x, 0f, _moveInput.y);
//             if (inputDir.sqrMagnitude > 1f) inputDir.Normalize();
//
//             Quaternion cameraRotation = Quaternion.Euler(0f, _cameraYaw, 0f);
//             Vector3 horizontalVelocity = Vector3.zero;
//
//             // ============= ROTATION & MOVEMENT =============
//
//             if (_isCameraLocked)
//             {
//                 // STRAFE MODE: luôn nhìn theo camera
//                 transform.rotation = Quaternion.Slerp(
//                     transform.rotation,
//                     cameraRotation,
//                     lockModeRotationSpeed * deltaTime
//                 );
//
//                 // Di chuyển theo input relative to camera
//                 if (inputDir.sqrMagnitude > 0.0001f)
//                 {
//                     Vector3 worldDir = cameraRotation * inputDir;
//                     horizontalVelocity = worldDir * finalSpeed;
//                 }
//             }
//             else
//             {
//                 // TANK MODE: xoay theo hướng di chuyển
//                 if (inputDir.sqrMagnitude > 0.0001f)
//                 {
//                     // Tính target direction (hướng muốn đi) - INSTANT
//                     Vector3 worldMoveDir = cameraRotation * inputDir;
//                     _targetMoveDirection = worldMoveDir.normalized;
//                     
//                     // Target rotation
//                     Quaternion targetRotation = Quaternion.LookRotation(_targetMoveDirection);
//                     
//                     // Smooth rotation
//                     transform.rotation = Quaternion.Slerp(
//                         transform.rotation,
//                         targetRotation,
//                         freeModeRotationSpeed * deltaTime
//                     );
//
//                     // Di chuyển theo target direction (không dùng transform.forward để tránh giật)
//                     horizontalVelocity = _targetMoveDirection * finalSpeed;
//                 }
//                 else
//                 {
//                     // Đứng yên: xoay chậm theo camera
//                     float angleDiff = Quaternion.Angle(transform.rotation, cameraRotation);
//                     
//                     if (angleDiff > 5f)
//                     {
//                         transform.rotation = Quaternion.Slerp(
//                             transform.rotation,
//                             cameraRotation,
//                             freeModeRotationSpeed * 0.5f * deltaTime
//                         );
//                     }
//
//                     _targetMoveDirection = Vector3.zero;
//                 }
//             }
//
//             // ============= GRAVITY =============
//
//             if (_isGrounded)
//             {
//                 if (_verticalVelocity < 0f)
//                 {
//                     _verticalVelocity = -2f;
//                 }
//             }
//             else
//             {
//                 _verticalVelocity += Physics.gravity.y * deltaTime;
//                 _verticalVelocity = Mathf.Max(_verticalVelocity, -40f);
//             }
//
//             // ============= APPLY MOVEMENT =============
//
//             Vector3 totalMovement = new Vector3(horizontalVelocity.x, _verticalVelocity, horizontalVelocity.z);
//             _characterController.Move(totalMovement * deltaTime);
//
//             _velocity = horizontalVelocity;
//             _currentMoveSpeed = horizontalVelocity.magnitude;
//         }
//
//         // ============= NETWORK SYNC =============
//
//         /// <summary>
//         /// Owner → Server: Send input only
//         /// </summary>
//         private void SendInputToServer()
//         {
//             if (!IsOwner) return;
//
//             ServerReceiveInput(
//                 _moveInput,
//                 _cameraYaw,
//                 _sprintHeld,
//                 _crouchHeld,
//                 _currentStamina,
//                 _isCameraLocked
//             );
//         }
//
//         /// <summary>
//         /// Server validates input
//         /// </summary>
//         [ServerRpc(RequireOwnership = true, RunLocally = false)]
//         private void ServerReceiveInput(
//             Vector2 moveInput,
//             float cameraYaw,
//             bool sprinting,
//             bool crouching,
//             float stamina,
//             bool cameraLocked)
//         {
//             // Validation
//             _moveInput.x = Mathf.Clamp(moveInput.x, -1f, 1f);
//             _moveInput.y = Mathf.Clamp(moveInput.y, -1f, 1f);
//             _currentStamina = Mathf.Clamp(stamina, 0f, movementSettings.maxStamina);
//             
//             while (cameraYaw < 0f) cameraYaw += 360f;
//             while (cameraYaw >= 360f) cameraYaw -= 360f;
//             _cameraYaw = cameraYaw;
//
//             _sprintHeld = sprinting && CanSprint();
//             _crouchHeld = crouching;
//             _isCameraLocked = cameraLocked;
//         }
//
//         /// <summary>
//         /// Owner → Other Clients: Sync rotation (KHÔNG gửi về owner)
//         /// </summary>
//         private void SyncRotationToClients(Quaternion rotation)
//         {
//             if (!IsOwner) return;
//
//             ObserversReceiveRotation(rotation);
//         }
//
//         /// <summary>
//         /// Remote clients nhận rotation từ owner
//         /// </summary>
//         [ObserversRpc(ExcludeOwner = true, ExcludeServer = false)]
//         private void ObserversReceiveRotation(Quaternion rotation)
//         {
//             // Remote client: set target rotation để interpolate
//             _remoteTargetRotation = rotation;
//         }
//
//         /// <summary>
//         /// Remote client: smooth interpolate rotation
//         /// </summary>
//         private void InterpolateRemoteRotation(float deltaTime)
//         {
//             transform.rotation = Quaternion.Slerp(
//                 transform.rotation,
//                 _remoteTargetRotation,
//                 _remoteRotationSpeed * deltaTime
//             );
//         }
//
//         // ============= RECONCILIATION =============
//
//         /// <summary>
//         /// Owner: reconcile position với server (KHÔNG reconcile rotation)
//         /// </summary>
//         private void CheckReconciliation()
//         {
//             if (!IsOwner) return;
//
//             Vector3 networkPos = transform.position;
//             float posDistance = Vector3.Distance(networkPos, _serverPosition);
//
//             if (posDistance > reconcileThreshold)
//             {
//                 if (enableDebugLogs)
//                 {
//                     Debug.LogWarning($"[Reconcile] Position diff: {posDistance:F3}m");
//                 }
//
//                 transform.position = Vector3.Lerp(
//                     transform.position,
//                     networkPos,
//                     reconcileSpeed * Time.deltaTime
//                 );
//
//                 if (_characterController.isGrounded)
//                 {
//                     _verticalVelocity = -2f;
//                 }
//             }
//
//             _serverPosition = networkPos;
//         }
//
//         // ============= IMovementController =============
//
//         public void SetMoveInput(Vector2 input) => _moveInput = input;
//         public void SetSprinting(bool sprinting) => _sprintHeld = sprinting;
//         public void SetCrouching(bool crouching) => _crouchHeld = crouching;
//         public float GetCurrentMoveSpeed() => _currentMoveSpeed;
//         public float GetStamina() => _currentStamina;
//         public bool IsSprinting() => _sprintHeld && CanSprint();
//         public bool IsCrouching() => _crouchHeld;
//
//         public void SetWeightPenalty(float penalty)
//         {
//             _weightPenalty = Mathf.Clamp01(penalty);
//         }
//
//         public void SetStaminaDrainMultiplier(float multiplier)
//         {
//             _staminaDrainMultiplier = Mathf.Max(0f, multiplier);
//         }
//
//         public MovementState GetCurrentState()
//         {
//             return new MovementState
//             {
//                 Position = transform.position,
//                 Rotation = transform.rotation,
//                 Velocity = _velocity,
//                 IsSprinting = _sprintHeld,
//                 IsCrouching = _crouchHeld,
//                 Stamina = _currentStamina
//             };
//         }
//
//         public void SetState(MovementState state)
//         {
//             transform.position = state.Position;
//             transform.rotation = state.Rotation;
//             _velocity = state.Velocity;
//             _sprintHeld = state.IsSprinting;
//             _crouchHeld = state.IsCrouching;
//             _currentStamina = state.Stamina;
//         }
//
//         public void SetCameraLock(bool locked) => _isCameraLocked = locked;
//         public bool IsCameraLocked() => _isCameraLocked;
//
//         // ============= UTILITIES =============
//
//         private bool CanSprint()
//         {
//             return _currentStamina >= movementSettings.minStaminaToSprint;
//         }
//
//         private void LoadCharacterConfig()
//         {
//             if (_playerStats == null) return;
//             var config = GameConfigLoader.Instance?.GetCharacterConfig("CHAR_DEFAULT");
//             if (config != null)
//             {
//                 movementSettings.baseSpeed = config.BaseMoveSpeed;
//                 movementSettings.maxStamina = config.BaseStamina;
//             }
//         }
//
//         private MovementSettings CreateFallbackSettings()
//         {
//             MovementSettings settings = ScriptableObject.CreateInstance<MovementSettings>();
//             settings.baseSpeed = 5f;
//             settings.sprintMultiplier = 1.5f;
//             settings.crouchMultiplier = 0.6f;
//             settings.maxStamina = 100f;
//             settings.staminaDrainRate = 20f;
//             settings.staminaRegenRate = 15f;
//             settings.minStaminaToSprint = 10f;
//             return settings;
//         }
//
//         // ============= DEBUG =============
//
//         private void OnDrawGizmos()
//         {
//             if (!enableDebugLogs || !Application.isPlaying) return;
//
//             if (IsOwner)
//             {
//                 // Owner: predicted position (green)
//                 Gizmos.color = Color.green;
//                 Gizmos.DrawWireSphere(transform.position, 0.2f);
//
//                 // Server position (red)
//                 Gizmos.color = Color.red;
//                 Gizmos.DrawWireSphere(_serverPosition, 0.15f);
//
//                 // Connection line
//                 Gizmos.color = Color.yellow;
//                 Gizmos.DrawLine(transform.position, _serverPosition);
//
//                 // Forward direction (blue)
//                 Gizmos.color = Color.blue;
//                 Gizmos.DrawRay(transform.position + Vector3.up, transform.forward * 1.5f);
//
//                 // Target move direction (magenta) - Tank mode
//                 if (!_isCameraLocked && _targetMoveDirection.sqrMagnitude > 0.01f)
//                 {
//                     Gizmos.color = Color.magenta;
//                     Gizmos.DrawRay(transform.position + Vector3.up, _targetMoveDirection * 2f);
//                 }
//
//                 // Mode indicator
//                 if (_isCameraLocked)
//                 {
//                     Gizmos.color = Color.cyan;
//                     Gizmos.DrawWireCube(transform.position + Vector3.up * 2.5f, Vector3.one * 0.3f);
//                 }
//             }
//             else
//             {
//                 // Remote client (white)
//                 Gizmos.color = Color.white;
//                 Gizmos.DrawWireSphere(transform.position, 0.2f);
//                 
//                 // Forward
//                 Gizmos.color = Color.gray;
//                 Gizmos.DrawRay(transform.position + Vector3.up, transform.forward * 1.5f);
//             }
//         }
//
//         private void OnGUI()
//         {
//             if (!enableDebugLogs || !IsOwner) return;
//
//             GUI.color = Color.white;
//             GUILayout.BeginArea(new Rect(10, 10, 350, 100));
//             GUILayout.Label($"Mode: {(_isCameraLocked ? "STRAFE" : "TANK")} | Press [Tab]");
//             GUILayout.Label($"Speed: {_currentMoveSpeed:F2} m/s | Stamina: {_currentStamina:F0}");
//             GUILayout.EndArea();
//         }
//     }
// }