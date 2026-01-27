using UnityEngine;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Managing;
using NightHunt.Data;
using NightHunt.Networking;
using NightHunt.Networking.Prediction.FishNet;
using NightHunt.Gameplay.Character.Movement;
using Unity.Cinemachine;
using MovementState = NightHunt.Gameplay.Character.Movement.MovementState;

namespace NightHunt.Gameplay.Character
{
    /// <summary>
    /// 🎯 ADAPTIVE MOVEMENT: Tự động adjust settings dựa trên player count
    /// 
    /// SCALABILITY:
    /// - 1-2 players: Tight thresholds, high quality
    /// - 3-5 players: Balanced thresholds
    /// - 6-10 players: Relaxed thresholds, performance priority
    /// - 10+ players: Ultra relaxed, maximum performance
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class CharacterPredictedMovement : FishNetPredictedBehaviour<MovementReplicateData, MovementReconcileData>, IMovementController
    {
        // Settings - Hardcoded để test, sau đó sẽ thêm [SerializeField] nếu OK
        [SerializeField] private MovementSettings movementSettings;

        // Adaptive Scaling - Hardcoded values
        private bool enableAdaptiveScaling = true;
        private float scalingUpdateInterval = 3f;

        // Base Settings (1-2 Players) - Test values
        private float baseSoftThreshold = 0.25f;
        private float baseHardThreshold = 2.0f;
        private float baseSoftTime = 0.12f;
        private float baseDeadZone = 0.05f;
        private float baseInterpolationTime = 0.12f; // Giảm để responsive hơn
        private int baseReconcileInterval = 2; // Tăng frequency để sync tốt hơn

        // Scaled Settings (6-10 Players) - Test values
        private float scaledSoftThreshold = 0.5f;
        private float scaledHardThreshold = 3.5f;
        private float scaledSoftTime = 0.2f;
        private float scaledDeadZone = 0.08f;
        private float scaledInterpolationTime = 0.2f;
        private int scaledReconcileInterval = 4;

        // Advanced - Test values cho smooth movement
        private bool useVelocityExtrapolation = true;
        private float maxExtrapolationDistance = 0.2f; // Giảm để tránh overshoot
        private float extrapolationDamping = 0.7f; // Tăng damping
        private bool separateVerticalReconcile = true;
        private float verticalDeadZoneMultiplier = 2.5f;
        private bool useTickBasedInterpolation = true;
        private float interpolationDamping = 0.08f; // Giảm để smooth hơn

        // Debug
        private bool enableDebugLogs = false;
        private bool showAdaptiveInfo = false;
        
        // ============= ADAPTIVE STATE =============
        private float _currentSoftThreshold;
        private float _currentHardThreshold;
        private float _currentSoftTime;
        private float _currentDeadZone;
        private float _currentInterpolationTime;
        private int _currentReconcileInterval;
        private float _currentVerticalDeadZone;
        
        private int _cachedPlayerCount = 0;
        private float _lastScalingUpdateTime = 0f;
        private float _previousScaleFactor = 0f;
        private float _playerCountCheckInterval = 2f; // Check player count mỗi 2 giây
        private float _lastPlayerCountCheck = 0f;
        
        // Components
        private CharacterController _characterController;
        private CharacterStats _characterStats;
        private NetworkPlayer _networkPlayer;
        private CinemachineCamera _playerCamera;
        private UnityEngine.Camera _mainCamera;
        
        private bool _isInitialized = false;
        private bool _hasValidTimeManager = false;

        // Input
        private struct InputSnapshot
        {
            public Vector2 MoveInput;
            public bool SprintHeld;
            public bool CrouchHeld;
            public uint FrameNumber;
            public void Reset()
            {
                MoveInput = Vector2.zero;
                SprintHeld = false;
                CrouchHeld = false;
                FrameNumber = 0;
            }
        }

        private InputSnapshot _currentInput;
        private InputSnapshot _nextInput;

        // State
        private float _currentStamina;
        private float _currentMoveSpeed;
        private Vector3 _velocity;
        private float _verticalVelocity;
        private float _weightPenalty;
        private float _staminaDrainMultiplier = 1f;
        private bool _isGrounded;

        // Prediction
        private MovementReplicateData _lastTickedReplicateData;
        
        // Interpolation (PUBG-style smooth interpolation)
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private Vector3 _targetVelocity;
        private Vector3 _serverVelocity; // Server-authoritative velocity
        private Vector3 _positionVelocity;
        private Vector3 _lastServerPosition;
        private float _timeSinceLastUpdate;
        private Vector3 _interpolationVelocity;
        private float _interpolationSmoothTime;
        private uint _lastInterpolationTick;
        private Vector3 _previousPosition; // For velocity calculation
        private float _previousPositionTime;
        
        // Reconcile
        private enum ReconcileMode { None, Soft, Hard }
        private ReconcileMode _currentReconcileMode;
        private Vector3 _reconcileStartPos;
        private Vector3 _reconcileTargetPos;
        private Quaternion _reconcileStartRot;
        private Quaternion _reconcileTargetRot;
        private float _reconcileProgress;
        private Vector3 _reconcileVelocity;
        
        private uint _lastReconcileTick;
        private float _lastReconcileTime;
        private int _consecutiveHardReconciles;

        // ============= INITIALIZATION =============
        
        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _characterStats = GetComponent<CharacterStats>();
            _networkPlayer = GetComponent<NetworkPlayer>();
            _playerCamera = GetComponentInChildren<CinemachineCamera>();
            _mainCamera = UnityEngine.Camera.main;
            
            if (_mainCamera == null)
                _mainCamera = FindFirstObjectByType<UnityEngine.Camera>();
            
            if (_characterController == null)
            {
                Debug.LogError("[Movement] CharacterController is REQUIRED!", this);
                enabled = false;
                return;
            }
            
            _currentInput.Reset();
            _nextInput.Reset();
            
            // Initialize adaptive settings to base values
            ApplyBaseSettings();
        }

        private void Start()
        {
            if (movementSettings == null)
            {
                Debug.LogError("[Movement] MovementSettings is NULL! Creating fallback...", this);
                movementSettings = CreateFallbackSettings();
            }
            LoadCharacterConfig();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            
            if (base.TimeManager == null)
            {
                Debug.LogError("[Movement] ❌ TimeManager is NULL!", this);
                _hasValidTimeManager = false;
                enabled = false;
                return;
            }
            
            _hasValidTimeManager = true;
            
            if (_characterController == null)
            {
                _characterController = GetComponent<CharacterController>();
                if (_characterController == null)
                {
                    Debug.LogError("[Movement] ❌ CharacterController NULL!", this);
                    enabled = false;
                    return;
                }
            }

            // Initialize state
            _currentStamina = movementSettings?.maxStamina ?? 100f;
            _verticalVelocity = 0f;
            _velocity = Vector3.zero;
            _isGrounded = false;
            
            _currentInput.Reset();
            _nextInput.Reset();

            if (transform != null)
            {
                _targetPosition = transform.position;
                _targetRotation = transform.rotation;
                _targetVelocity = Vector3.zero;
                _lastServerPosition = transform.position;
            }
            
            _positionVelocity = Vector3.zero;
            _interpolationVelocity = Vector3.zero;
            _reconcileVelocity = Vector3.zero;
            _serverVelocity = Vector3.zero;
            _timeSinceLastUpdate = 0f;
            _interpolationSmoothTime = baseInterpolationTime;
            _currentReconcileMode = ReconcileMode.None;
            _reconcileProgress = 1f;
            _lastReconcileTick = 0;
            _lastInterpolationTick = 0;
            _lastReconcileTime = 0f;
            _consecutiveHardReconciles = 0;
            _previousPosition = transform.position;
            _previousPositionTime = Time.time;
            _isInitialized = true;

            // ✅ Initial adaptive scaling
            if (enableAdaptiveScaling)
            {
                UpdateAdaptiveScaling();
            }

            float tickRate = 1f / (float)base.TimeManager.TickDelta;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[Movement] ✅ ADAPTIVE INIT: Tick={tickRate:F1}Hz, " +
                         $"SoftThresh={_currentSoftThreshold:F3}m, DeadZone={_currentDeadZone:F3}m, " +
                         $"ReconcileInt={_currentReconcileInterval}");
            }
            
            SetTickCallbacks(TickCallback.Tick);
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            SetTickCallbacks(TickCallback.None);
            _isInitialized = false;
            _hasValidTimeManager = false;
        }

        // ============= ADAPTIVE SCALING =============

        /// <summary>
        /// 🎯 Core adaptive logic: Scale settings based on player count
        /// </summary>
        private void UpdateAdaptiveScaling()
        {
            if (!enableAdaptiveScaling || base.NetworkManager == null) return;

            int playerCount = GetCurrentPlayerCount();
            
            // Only update if player count changed significantly (prevent micro-adjustments)
            if (Mathf.Abs(playerCount - _cachedPlayerCount) < 1) return;
            
            // Calculate scaling factor (0 = base settings, 1 = scaled settings)
            // 1-2 players = 0.0, 4 players = 0.3, 6 players = 0.5, 10+ players = 1.0
            float newScaleFactor = Mathf.Clamp01((playerCount - 2f) / 8f);
            
            // Apply smoothing to prevent sudden threshold changes
            float scaleFactor = Mathf.Lerp(_previousScaleFactor, newScaleFactor, 0.3f);
            
            _cachedPlayerCount = playerCount;
            _previousScaleFactor = scaleFactor;
            
            // Lerp between base and scaled settings
            _currentSoftThreshold = Mathf.Lerp(baseSoftThreshold, scaledSoftThreshold, scaleFactor);
            _currentHardThreshold = Mathf.Lerp(baseHardThreshold, scaledHardThreshold, scaleFactor);
            _currentSoftTime = Mathf.Lerp(baseSoftTime, scaledSoftTime, scaleFactor);
            _currentDeadZone = Mathf.Lerp(baseDeadZone, scaledDeadZone, scaleFactor);
            _currentInterpolationTime = Mathf.Lerp(baseInterpolationTime, scaledInterpolationTime, scaleFactor);
            _currentVerticalDeadZone = _currentDeadZone * verticalDeadZoneMultiplier;
            
            // Reconcile interval is discrete, so use steps
            // More aggressive scaling for multiple clients to reduce overhead
            // QUAN TRỌNG: Tăng interval ngay từ 2 players để tránh lag
            if (playerCount <= 1)
                _currentReconcileInterval = baseReconcileInterval;
            else if (playerCount == 2)
                _currentReconcileInterval = baseReconcileInterval + 1; // Tăng ngay từ 2 players
            else if (playerCount <= 4)
                _currentReconcileInterval = baseReconcileInterval + 2;
            else if (playerCount <= 6)
                _currentReconcileInterval = scaledReconcileInterval;
            else if (playerCount <= 8)
                _currentReconcileInterval = scaledReconcileInterval + 1;
            else
                _currentReconcileInterval = scaledReconcileInterval + 2;
            
            if (showAdaptiveInfo || enableDebugLogs)
            {
                Debug.Log($"[ADAPTIVE] 👥 Players: {playerCount} | Scale: {scaleFactor:F2}\n" +
                         $"  SoftThresh: {_currentSoftThreshold:F3}m\n" +
                         $"  HardThresh: {_currentHardThreshold:F3}m\n" +
                         $"  DeadZone: {_currentDeadZone:F3}m\n" +
                         $"  ReconcileInt: {_currentReconcileInterval}\n" +
                         $"  InterpTime: {_currentInterpolationTime:F3}s");
            }
        }

        private int GetCurrentPlayerCount()
        {
            // Cache player count check để tránh performance hit
            float currentTime = Time.time;
            if (currentTime - _lastPlayerCountCheck < _playerCountCheckInterval && _cachedPlayerCount > 0)
            {
                return _cachedPlayerCount;
            }
            
            _lastPlayerCountCheck = currentTime;
            
            try
            {
                if (base.NetworkManager == null) return 1;
                
                if (IsServerStarted && base.NetworkManager.ServerManager != null)
                {
                    int count = base.NetworkManager.ServerManager.Clients.Count;
                    _cachedPlayerCount = count;
                    return count;
                }
                else if (base.NetworkManager.ClientManager != null && base.NetworkManager.ClientManager.Connection != null)
                {
                    // Client: Use NetworkManager's spawned objects cache thay vì FindObjectsByType
                    // FindObjectsByType rất tốn performance!
                    if (base.NetworkManager.ClientManager.Objects != null && base.NetworkManager.ClientManager.Objects.Spawned != null)
                    {
                        int count = 0;
                        foreach (var obj in base.NetworkManager.ClientManager.Objects.Spawned.Values)
                        {
                            if (obj != null && obj.GetComponent<CharacterPredictedMovement>() != null)
                            {
                                count++;
                            }
                        }
                        _cachedPlayerCount = Mathf.Max(1, count);
                        return _cachedPlayerCount;
                    }
                }
            }
            catch (System.Exception e)
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning($"[Movement] GetCurrentPlayerCount error: {e.Message}");
                }
            }
            
            return _cachedPlayerCount > 0 ? _cachedPlayerCount : 1;
        }

        private void ApplyBaseSettings()
        {
            _currentSoftThreshold = baseSoftThreshold;
            _currentHardThreshold = baseHardThreshold;
            _currentSoftTime = baseSoftTime;
            _currentDeadZone = baseDeadZone;
            _currentInterpolationTime = baseInterpolationTime;
            _currentReconcileInterval = baseReconcileInterval;
            _currentVerticalDeadZone = baseDeadZone * verticalDeadZoneMultiplier;
        }

        // ============= UPDATE LOOPS =============

        private void Update()
        {
            if (!_isInitialized || !_hasValidTimeManager) return;
            
            _timeSinceLastUpdate += Time.deltaTime;
            
            // ✅ Periodic adaptive scaling update (giảm frequency để tránh lag)
            if (enableAdaptiveScaling && Time.time - _lastScalingUpdateTime > scalingUpdateInterval)
            {
                // Chỉ update nếu player count thay đổi
                int currentPlayerCount = GetCurrentPlayerCount();
                if (currentPlayerCount != _cachedPlayerCount)
                {
                    UpdateAdaptiveScaling();
                    _lastScalingUpdateTime = Time.time;
                }
            }
            
            if (!IsOwner && !IsServerStarted)
            {
                UpdateNonOwnerInterpolation(Time.deltaTime);
            }
            
            if (_currentReconcileMode == ReconcileMode.Soft)
            {
                UpdateSoftReconcile(Time.deltaTime);
            }
        }

        private void LateUpdate()
        {
            if (!_isInitialized) return;
            
            if (_characterStats != null && IsOwner)
            {
                _characterStats.SetStamina(_currentStamina);
            }
        }

        // ============= INTERPOLATION =============

        /// <summary>
        /// QUAN TRỌNG: Client B phải thấy Client A di chuyển từ A→B với vận tốc X, cùng thời gian
        /// Non-owner CHỈ interpolate, KHÔNG simulate movement
        /// </summary>
        private void UpdateNonOwnerInterpolation(float deltaTime)
        {
            if (transform == null || base.TimeManager == null) return;
            
            float tickDelta = (float)base.TimeManager.TickDelta;
            
            // Interpolation time - phải đủ nhanh để responsive nhưng đủ chậm để smooth
            // Giảm smooth time để responsive hơn
            float smoothTime = useTickBasedInterpolation 
                ? Mathf.Max(_currentInterpolationTime, tickDelta * 1.0f) // Giảm từ 1.2f
                : _currentInterpolationTime;
            
            // Adaptive smooth time nếu network lag
            if (_timeSinceLastUpdate > tickDelta * 2f)
            {
                smoothTime *= 1.1f; // Giảm từ 1.15f
            }
            
            Vector3 targetPos = _targetPosition;
            
            // QUAN TRỌNG: Extrapolation dùng server velocity để đảm bảo đúng vận tốc
            // Client B phải thấy Client A di chuyển với đúng velocity từ server
            if (useVelocityExtrapolation && _serverVelocity.sqrMagnitude > 0.01f)
            {
                // Chỉ extrapolate khi cần (đã lâu không nhận update)
                if (_timeSinceLastUpdate > tickDelta * 1.0f) // Giảm threshold
                {
                    // Tính thời gian cần extrapolate
                    float extrapolationTime = Mathf.Min(_timeSinceLastUpdate - tickDelta, smoothTime * 0.3f); // Giảm từ 0.4f
                    
                    // Dùng server velocity với damping để tránh overshoot
                    float dampingFactor = extrapolationDamping;
                    Vector3 extrapolationVelocity = _serverVelocity * dampingFactor;
                    
                    // Extrapolate: position = target + velocity * time
                    Vector3 extrapolation = extrapolationVelocity * extrapolationTime;
                    
                    // Limit để tránh overshoot
                    if (extrapolation.magnitude > maxExtrapolationDistance)
                    {
                        extrapolation = extrapolation.normalized * maxExtrapolationDistance;
                    }
                    
                    targetPos += extrapolation;
                    
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[Interp] Extrapolating: Vel={extrapolationVelocity:F2} | Time={extrapolationTime:F3} | Dist={extrapolation.magnitude:F2}");
                    }
                }
            }
            
            // Smooth interpolation đến target position
            // Dùng SmoothDamp nhưng với smooth time ngắn hơn để responsive
            float currentSmoothTime = smoothTime;
            
            // Điều chỉnh smooth time dựa trên distance
            float distanceToTarget = Vector3.Distance(transform.position, targetPos);
            if (distanceToTarget > 0.3f) // Giảm từ 0.5f
            {
                currentSmoothTime *= 1.2f; // Giảm từ 1.3f
            }
            else if (distanceToTarget < 0.03f) // Giảm từ 0.05f
            {
                currentSmoothTime *= 0.6f; // Giảm từ 0.7f để responsive hơn
            }
            
            // Apply damping (giảm để responsive hơn)
            currentSmoothTime *= (1f + interpolationDamping);
            
            // Interpolate position với smooth time ngắn hơn
            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPos,
                ref _interpolationVelocity,
                currentSmoothTime,
                Mathf.Infinity,
                deltaTime
            );
            
            // Smooth rotation với speed cao hơn
            float rotationSpeed = Mathf.Clamp(1f / currentSmoothTime, 10f, 30f); // Tăng từ 8f
            float rotationLerpFactor = Mathf.Clamp01(deltaTime * rotationSpeed);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                _targetRotation,
                rotationLerpFactor
            );
        }

        private void UpdateSoftReconcile(float deltaTime)
        {
            if (transform == null) return;
            
            // Adaptive reconcile speed based on error magnitude
            float errorMagnitude = Vector3.Distance(_reconcileStartPos, _reconcileTargetPos);
            float adaptiveSoftTime = _currentSoftTime;
            
            // Larger errors reconcile faster to prevent visual lag
            if (errorMagnitude > _currentSoftThreshold * 2f)
            {
                adaptiveSoftTime *= 0.7f;
            }
            
            _reconcileProgress += deltaTime / adaptiveSoftTime;
            _reconcileProgress = Mathf.Clamp01(_reconcileProgress);
            
            // Use smoother easing for better visual quality
            float t = EaseOutCubic(_reconcileProgress);
            
            // Use SmoothDamp for position instead of Lerp for smoother movement
            transform.position = Vector3.SmoothDamp(
                transform.position,
                _reconcileTargetPos,
                ref _reconcileVelocity,
                adaptiveSoftTime * 0.5f,
                Mathf.Infinity,
                deltaTime
            );
            
            // Smoother rotation interpolation
            float rotationLerpFactor = Mathf.Clamp01(deltaTime / (adaptiveSoftTime * 0.5f));
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                _reconcileTargetRot,
                rotationLerpFactor
            );
            
            if (_reconcileProgress >= 1f)
            {
                _currentReconcileMode = ReconcileMode.None;
                _reconcileVelocity = Vector3.zero;
            }
        }

        private float EaseOutCubic(float t)
        {
            return 1f - Mathf.Pow(1f - t, 3f);
        }
        
        private float EaseInOutCubic(float t)
        {
            return t < 0.5f 
                ? 4f * t * t * t 
                : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        }

        // ============= FISHNET TICK =============

        protected override void TimeManager_OnTick()
        {
            if (!_isInitialized || !_hasValidTimeManager || base.TimeManager == null || _characterController == null || transform == null)
                return;

            if (_nextInput.FrameNumber > _currentInput.FrameNumber)
            {
                _currentInput = _nextInput;
            }

            MovementReplicateData replicateData = BuildMoveData();
            
            // QUAN TRỌNG: Chỉ replicate nếu có data hợp lệ
            if (IsOwner && replicateData.MoveInput.sqrMagnitude > 0.001f || !IsOwner)
            {
                try
                {
                    // Sử dụng ReplicateState.Ticked thay vì Invalid
                    PerformReplicate(replicateData, ReplicateState.Ticked, Channel.Unreliable);
                }
                catch (System.Exception e)
                {
                    if (enableDebugLogs)
                    {
                        Debug.LogError($"[Movement] PerformReplicate error: {e.Message}");
                    }
                    return;
                }
            }
            
            // ✅ Adaptive reconcile interval based on network conditions
            // QUAN TRỌNG: Chỉ reconcile khi cần thiết để tránh lag với nhiều clients
            uint currentTick = base.TimeManager.LocalTick;
            float timeSinceLastReconcile = Time.time - _lastReconcileTime;
            
            // Dynamic reconcile interval adjustment
            int dynamicInterval = _currentReconcileInterval;
            
            // PUBG-style: Với 2+ clients, reconcile thường xuyên hơn để đảm bảo sync
            // Nhưng không quá thường xuyên để tránh lag
            if (_cachedPlayerCount >= 2)
            {
                // Giữ interval hợp lý, không tăng quá nhiều
                dynamicInterval = Mathf.Max(dynamicInterval, baseReconcileInterval);
            }
            
            // Increase interval if we've had many hard reconciles (network is stable)
            if (_consecutiveHardReconciles == 0 && timeSinceLastReconcile > 0.5f)
            {
                dynamicInterval = Mathf.Min(dynamicInterval + 1, scaledReconcileInterval + 2);
            }
            // Decrease interval if we're having issues (nhưng không quá thấp)
            else if (_consecutiveHardReconciles > 2)
            {
                dynamicInterval = Mathf.Max(dynamicInterval - 1, baseReconcileInterval);
            }
            
            // Check if we should reconcile
            bool shouldReconcile = (currentTick - _lastReconcileTick) >= dynamicInterval;
            
            // Also reconcile if too much time has passed (safety check) - nhưng tăng threshold
            if (!shouldReconcile && timeSinceLastReconcile > 0.5f) // Tăng từ 0.3f lên 0.5f
            {
                shouldReconcile = true;
            }
            
            // QUAN TRỌNG: Không reconcile nếu đang trong soft reconcile để tránh conflict
            if (shouldReconcile && _currentReconcileMode == ReconcileMode.None)
            {
                try
                {
                    CreateReconcile();
                    _lastReconcileTick = currentTick;
                    _lastReconcileTime = Time.time;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[Movement] CreateReconcile error: {e.Message}");
                }
            }
        }

        private MovementReplicateData BuildMoveData()
        {
            if (!IsOwner)
                return default;

            float cameraYaw = transform.eulerAngles.y;
            if (_playerCamera != null)
            {
                cameraYaw = _playerCamera.transform.eulerAngles.y;
            }

            return new MovementReplicateData(
                _currentInput.MoveInput,
                Quaternion.Euler(0f, cameraYaw, 0f),
                _currentInput.SprintHeld,
                _currentInput.CrouchHeld
            );
        }

        // ============= REPLICATE =============

        [Replicate]
        private void PerformReplicate(MovementReplicateData data, ReplicateState state, Channel channel = Channel.Unreliable)
        {
            if (!_isInitialized || _characterController == null || transform == null)
                return;

            float delta = TickDelta;
            bool useDefaultForces = false;

            // QUAN TRỌNG: Non-owner KHÔNG được simulate movement!
            // Chỉ lấy position từ server và interpolate
            if (!IsServerStarted && !IsOwner)
            {
                // Non-owner: Chỉ update target position từ server, KHÔNG simulate
                if (IsTicked(state) && IsCreated(state))
                {
                    _lastTickedReplicateData.Dispose();
                    _lastTickedReplicateData = data;
                    
                    // QUAN TRỌNG: Lấy position TRƯỚC KHI simulate
                    // Position này là từ server sau khi server đã simulate
                    // Server simulate TRƯỚC, sau đó mới gửi cho client
                    Vector3 serverPosBeforeSimulate = transform.position;
                    
                    // Calculate velocity từ server position
                    Vector3 displacement = serverPosBeforeSimulate - _lastServerPosition;
                    
                    if (delta > 0f && displacement.sqrMagnitude > 0.0001f)
                    {
                        // Velocity = displacement / time (từ server)
                        Vector3 calculatedVelocity = displacement / delta;
                        
                        // Smooth velocity nhưng giữ đúng magnitude
                        float velocitySmoothing = 0.85f; // Giảm một chút để responsive hơn
                        _serverVelocity = Vector3.Lerp(_serverVelocity, calculatedVelocity, velocitySmoothing);
                        _targetVelocity = _serverVelocity;
                        
                        if (enableDebugLogs)
                        {
                            Debug.Log($"[ClientB] ServerPos={serverPosBeforeSimulate:F2} | Displacement={displacement:F2} | Vel={_serverVelocity:F2} | Speed={_serverVelocity.magnitude:F2}");
                        }
                    }
                    else
                    {
                        // Nếu không di chuyển, velocity = 0
                        _serverVelocity = Vector3.zero;
                        _targetVelocity = Vector3.zero;
                    }
                    
                    // Update target position từ server
                    // Client B sẽ interpolate đến đây trong Update()
                    _targetPosition = serverPosBeforeSimulate;
                    _targetRotation = transform.rotation;
                    _lastServerPosition = serverPosBeforeSimulate;
                    _timeSinceLastUpdate = 0f;
                    _lastInterpolationTick = base.TimeManager?.LocalTick ?? 0;
                    
                    _previousPosition = serverPosBeforeSimulate;
                    _previousPositionTime = Time.time;
                }
                else if (!IsCreated(state))
                {
                    // Missing data - use last known
                    uint currentTick = data.GetTick();
                    uint lastKnownTick = _lastTickedReplicateData.GetTick();
                    uint tickDifference = currentTick > lastKnownTick ? currentTick - lastKnownTick : 0;

                    if (tickDifference > 0 && tickDifference <= 2)
                    {
                        data.Dispose();
                        data = _lastTickedReplicateData;
                    }
                    else
                    {
                        // No valid data - don't simulate
                        return; // QUAN TRỌNG: Non-owner không simulate nếu không có data
                    }
                }
                
                // QUAN TRỌNG: Non-owner KHÔNG simulate movement!
                // Chỉ owner và server mới simulate
                // Non-owner chỉ interpolate trong Update()
                return;
            }

            // Owner hoặc Server: Simulate movement
            SimulateMovement(data, useDefaultForces, delta);
        }

        // ============= RECONCILE =============

        [Reconcile]
        private void PerformReconcile(MovementReconcileData data, Channel channel = Channel.Unreliable)
        {
            if (!IsOwner || !_isInitialized || transform == null)
                return;
            
            Vector3 currentPos = transform.position;
            Vector3 serverPos = data.Position;
            
            Vector3 horizontalCurrent = new Vector3(currentPos.x, 0f, currentPos.z);
            Vector3 horizontalServer = new Vector3(serverPos.x, 0f, serverPos.z);
            float horizontalError = Vector3.Distance(horizontalCurrent, horizontalServer);
            float verticalError = Mathf.Abs(currentPos.y - serverPos.y);
            
            // ✅ Use adaptive dead zones
            bool needsHorizontalReconcile = horizontalError > _currentDeadZone;
            bool needsVerticalReconcile = separateVerticalReconcile && verticalError > _currentVerticalDeadZone;
            
            if (!needsHorizontalReconcile && !needsVerticalReconcile)
            {
                _currentReconcileMode = ReconcileMode.None;
                return;
            }
            
            float totalError = Mathf.Sqrt(horizontalError * horizontalError + verticalError * verticalError);
            
            // PUBG-style: Strict server authority - nếu lệch > threshold thì phải fix ngay
            if (totalError >= _currentHardThreshold)
            {
                _consecutiveHardReconciles++;
                
                // PUBG: Hard snap khi lệch > 1.5m hoặc nhiều consecutive errors
                // Đảm bảo server authority, không để client drift quá xa
                bool shouldHardSnap = totalError >= 1.5f || _consecutiveHardReconciles > 2;
                
                if (shouldHardSnap)
                {
                    _currentReconcileMode = ReconcileMode.Hard;
                    transform.position = serverPos;
                    transform.rotation = data.Rotation;
                    _reconcileVelocity = Vector3.zero;
                    _interpolationVelocity = Vector3.zero;
                    
                    // Update server velocity từ reconcile data
                    _serverVelocity = new Vector3(data.Velocity.x, 0f, data.Velocity.z);
                    _targetVelocity = _serverVelocity;
                    
                    // Reset interpolation state
                    _targetPosition = serverPos;
                    _targetRotation = data.Rotation;
                    _lastServerPosition = serverPos;
                    _timeSinceLastUpdate = 0f;
                    
                    if (enableDebugLogs)
                    {
                        Debug.LogWarning($"[HARD] Error={totalError:F3}m (thresh={_currentHardThreshold:F3}m) | Consecutive={_consecutiveHardReconciles}");
                    }
                }
                else
                {
                    // Use soft reconcile để smooth hơn
                    _currentReconcileMode = ReconcileMode.Soft;
                    _reconcileStartPos = currentPos;
                    _reconcileStartRot = transform.rotation;
                    
                    Vector3 correctedPos = currentPos;
                    if (needsHorizontalReconcile)
                    {
                        correctedPos.x = serverPos.x;
                        correctedPos.z = serverPos.z;
                    }
                    if (needsVerticalReconcile)
                    {
                        correctedPos.y = serverPos.y;
                    }
                    
                    _reconcileTargetPos = correctedPos;
                    _reconcileTargetRot = data.Rotation;
                    _reconcileProgress = 0f;
                    
                    // Update server velocity
                    _serverVelocity = new Vector3(data.Velocity.x, 0f, data.Velocity.z);
                    _targetVelocity = _serverVelocity;
                }
            }
            else if (totalError >= _currentSoftThreshold)
            {
                _consecutiveHardReconciles = 0;
                _currentReconcileMode = ReconcileMode.Soft;
                _reconcileStartPos = currentPos;
                _reconcileStartRot = transform.rotation;
                
                Vector3 correctedPos = currentPos;
                if (needsHorizontalReconcile)
                {
                    correctedPos.x = serverPos.x;
                    correctedPos.z = serverPos.z;
                }
                if (needsVerticalReconcile)
                {
                    correctedPos.y = serverPos.y;
                }
                
                _reconcileTargetPos = correctedPos;
                _reconcileTargetRot = data.Rotation;
                _reconcileProgress = 0f;
                
                // Update server velocity
                _serverVelocity = new Vector3(data.Velocity.x, 0f, data.Velocity.z);
                _targetVelocity = _serverVelocity;
            }
            else
            {
                _consecutiveHardReconciles = 0;
                _currentReconcileMode = ReconcileMode.None;
                
                // PUBG-style: Smooth micro-correction cho small errors
                Vector3 correctedPos = currentPos;
                bool needsCorrection = false;
                
                if (needsHorizontalReconcile)
                {
                    correctedPos.x = serverPos.x;
                    correctedPos.z = serverPos.z;
                    needsCorrection = true;
                }
                if (needsVerticalReconcile)
                {
                    correctedPos.y = serverPos.y;
                    needsCorrection = true;
                }
                
                if (needsCorrection)
                {
                    // Very smooth correction để không notice
                    float correctionSpeed = 0.2f; // Giảm từ 0.3 để smooth hơn
                    transform.position = Vector3.Lerp(currentPos, correctedPos, correctionSpeed);
                }
                
                // Update target position để interpolation smooth
                _targetPosition = correctedPos;
                _targetRotation = data.Rotation;
                
                // Update server velocity
                _serverVelocity = new Vector3(data.Velocity.x, 0f, data.Velocity.z);
                _targetVelocity = _serverVelocity;
                
                // Smooth rotation
                transform.rotation = Quaternion.Slerp(transform.rotation, data.Rotation, 0.2f);
            }

            _velocity = new Vector3(data.Velocity.x, 0f, data.Velocity.z);
            _verticalVelocity = data.Velocity.y;
            _currentStamina = data.Stamina;
        }

        public override void CreateReconcile()
        {
            if (!_isInitialized || transform == null)
                return;
            
            // QUAN TRỌNG: FishNet REQUIRES gọi PerformReconcile() trong CreateReconcile()
            // Đây là requirement của FishNet's code generation
            // CreateReconcile() được gọi bởi cả server và client:
            // - Server: Tạo data để gửi cho clients
            // - Client: Tạo local data làm fallback nếu packet bị mất
            
            // Build reconcile data
            MovementReconcileData data = CreateReconcileData();
            
            // QUAN TRỌNG: Phải gọi PerformReconcile() - FishNet requirement
            // FishNet sẽ tự động handle việc gửi/nhận reconcile data
            // PerformReconcile() sẽ chỉ chạy trên client owner khi nhận được data từ server
            PerformReconcile(data, Channel.Unreliable);
        }

        protected override MovementReconcileData CreateReconcileData()
        {
            // QUAN TRỌNG: Phải gửi đúng velocity từ server để client B thấy đúng vận tốc
            // Velocity = horizontal velocity + vertical velocity
            Vector3 fullVelocity = new Vector3(_velocity.x, _verticalVelocity, _velocity.z);
            
            // Debug log để verify velocity
            if (enableDebugLogs && IsServerStarted)
            {
                Debug.Log($"[Reconcile] Pos={transform.position:F2} | Vel={fullVelocity:F2} | Speed={fullVelocity.magnitude:F2}");
            }
            
            return new MovementReconcileData(transform.position, transform.rotation, fullVelocity, _currentStamina);
        }

        // ============= MOVEMENT SIMULATION =============

        private void SimulateMovement(MovementReplicateData data, bool useDefaultForces, float delta)
        {
            if (_characterController == null) return;
            
            _isGrounded = _characterController.isGrounded;
            
            if (useDefaultForces)
            {
                if (!_isGrounded)
                {
                    _characterController.Move(new Vector3(0f, -1f, 0f) * delta);
                }
                _velocity = Vector3.zero;
                _currentMoveSpeed = 0f;
                return;
            }

            _currentStamina = Mathf.Min(_currentStamina + movementSettings.staminaRegenRate * delta, movementSettings.maxStamina);

            float finalSpeed = movementSettings.baseSpeed;

            if (data.IsSprinting && CanSprint())
            {
                finalSpeed *= movementSettings.sprintMultiplier;
                _currentStamina = Mathf.Max(0f, _currentStamina - movementSettings.staminaDrainRate * _staminaDrainMultiplier * delta);
            }
            else if (data.IsCrouching)
            {
                finalSpeed *= movementSettings.crouchMultiplier;
            }

            finalSpeed *= (1f - _weightPenalty);

            Vector3 moveDir = new Vector3(data.MoveInput.x, 0f, data.MoveInput.y);
            if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

            moveDir = data.Rotation * moveDir;
            Vector3 horizontalVelocity = moveDir * finalSpeed;

            if (_isGrounded)
            {
                if (_verticalVelocity < 0f)
                {
                    _verticalVelocity = -2f;
                }
            }
            else
            {
                _verticalVelocity += Physics.gravity.y * delta * 3f;
                _verticalVelocity = Mathf.Max(_verticalVelocity, -40f);
            }

            Vector3 totalMovement = new Vector3(horizontalVelocity.x, _verticalVelocity, horizontalVelocity.z);
            _characterController.Move(totalMovement * delta);

            if (moveDir.sqrMagnitude > 0.0001f)
            {
                transform.rotation = data.Rotation;
            }

            _velocity = horizontalVelocity;
            _currentMoveSpeed = horizontalVelocity.magnitude;
        }

        private bool CanSprint()
        {
            return _currentStamina >= movementSettings.minStaminaToSprint;
        }

        // ============= INPUT API =============

        public void SetMoveInput(Vector2 input)
        {
            _nextInput.MoveInput = input;
            _nextInput.FrameNumber = (uint)Time.frameCount;
        }

        public void SetSprinting(bool sprinting)
        {
            _nextInput.SprintHeld = sprinting;
            _nextInput.FrameNumber = (uint)Time.frameCount;
        }

        public void SetCrouching(bool crouching)
        {
            _nextInput.CrouchHeld = crouching;
            _nextInput.FrameNumber = (uint)Time.frameCount;
        }

        // ============= GETTERS =============

        public float GetCurrentMoveSpeed() => _currentMoveSpeed;
        public float GetStamina() => _currentStamina;
        public bool IsSprinting() => _currentInput.SprintHeld && CanSprint();
        public bool IsCrouching() => _currentInput.CrouchHeld;

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
                Velocity = _velocity
            };
        }

        public void SetState(MovementState state)
        {
            transform.position = state.Position;
            transform.rotation = state.Rotation;
            _velocity = state.Velocity;
        }

        // ============= UTILITIES =============

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

        // ============= DEBUG =============

        private void OnGUI()
        {
            if (!showAdaptiveInfo || !_isInitialized) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Box($"ADAPTIVE MOVEMENT\n" +
                         $"Players: {_cachedPlayerCount}\n" +
                         $"Soft Thresh: {_currentSoftThreshold:F3}m\n" +
                         $"Dead Zone: {_currentDeadZone:F3}m\n" +
                         $"Reconcile Int: {_currentReconcileInterval}\n" +
                         $"Interp Time: {_currentInterpolationTime:F3}s");
            GUILayout.EndArea();
        }
    }
}