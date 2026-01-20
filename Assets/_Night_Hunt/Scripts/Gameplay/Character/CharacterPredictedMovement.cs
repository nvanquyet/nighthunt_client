using UnityEngine;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using NightHunt.Data;
using NightHunt.Networking;
using NightHunt.Networking.Prediction.FishNet;
using NightHunt.Gameplay.Character.Movement;
using Unity.Cinemachine;
using MovementState = NightHunt.Gameplay.Character.Movement.MovementState;

namespace NightHunt.Gameplay.Character
{
    /// <summary>
    /// Game-specific character movement với FishNet CSP (Client-Side Prediction).
    /// 
    /// ARCHITECTURE:
    /// - Kế thừa từ FishNetPredictedBehaviour<MovementReplicateData, MovementReconcileData>
    /// - Implement đầy đủ CSP pattern: Replicate → Reconcile → Replay
    /// - CharacterController-based movement
    /// - INPUT BUFFERING: Giải quyết timing issue giữa Unity Update và FishNet Tick
    /// 
    /// RESPONSIBILITIES:
    /// 1. Input handling: SetMoveInput/SetSprinting/SetCrouching (buffered)
    /// 2. Movement simulation: Speed calculation, stamina, weight penalty
    /// 3. Network prediction: Client-side prediction + server reconciliation
    /// 4. Future prediction: Smooth movement cho spectators
    /// 5. Integration: CharacterStats, MovementSettings, NetworkPlayer
    /// 
    /// CSP FLOW:
    /// 1. Owner: Input (Update) → Buffer → BuildMoveData (Tick) → PerformReplicate → SimulateMovement
    /// 2. Server: Receive replicate → SimulateMovement → CreateReconcile → Send to client
    /// 3. Client: Receive reconcile → Check mismatch → Replay if needed
    /// 4. Spectator: Predict future ticks using last known input
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class CharacterPredictedMovement : FishNetPredictedBehaviour<MovementReplicateData, MovementReconcileData>
    {
        [Header("Settings")] [SerializeField] private MovementSettings movementSettings;

        [Header("Debug")] [SerializeField] private bool enableDebugLogs = false;
        
        private uint _lastMovedTick;
        
        // References
        private CharacterController _characterController;
        private CharacterStats _characterStats;
        private NetworkPlayer _networkPlayer;
        private CinemachineCamera _playerCamera;

        // ========== INPUT BUFFERING SYSTEM ==========
        /// <summary>
        /// Input buffer để giải quyết timing issue giữa Unity Update và FishNet Tick.
        /// 
        /// PROBLEM:
        /// - PlayerInputHandler.Update() chạy mỗi frame (~60 FPS)
        /// - TimeManager_OnTick() chạy mỗi tick (~30 Hz)
        /// - Nếu Update chạy SAU Tick trong cùng 1 frame → Input bị delay!
        /// 
        /// SOLUTION:
        /// - Update() ghi input vào _nextInputBuffer
        /// - Tick() swap buffer: _currentInputBuffer = _nextInputBuffer
        /// - BuildMoveData() đọc từ _currentInputBuffer
        /// 
        /// BENEFITS:
        /// - Đảm bảo input luôn sync với tick
        /// - Tránh input bị skip hoặc delay
        /// - Thread-safe (Unity single-threaded nên không cần lock)
        /// </summary>
        private struct InputBuffer
        {
            public Vector2 MoveInput;
            public bool SprintHeld;
            public bool CrouchHeld;
            public uint LastUpdateFrame; // Track frame number để detect updates

            public void Reset()
            {
                MoveInput = Vector2.zero;
                SprintHeld = false;
                CrouchHeld = false;
                LastUpdateFrame = 0;
            }
        }

        private InputBuffer _currentInputBuffer; // Input được dùng trong tick hiện tại
        private InputBuffer _nextInputBuffer; // Input mới nhất từ Update()

        // State tracking
        private float _currentStamina;
        private float _currentMoveSpeed;
        private Vector3 _velocity;
        private float _verticalVelocity;
        private float _weightPenalty;
        private float _staminaDrainMultiplier = 1f;

        // Client-side prediction cache
        private MovementReplicateData _lastTickedReplicateData;

        // ========== UNITY LIFECYCLE ==========

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _characterStats = GetComponent<CharacterStats>();
            _networkPlayer = GetComponent<NetworkPlayer>();
            _playerCamera = GetComponentInChildren<CinemachineCamera>();
        }

        private void Start()
        {
            if (movementSettings == null)
            {
                Debug.LogError($"[CharacterPredictedMovement] MovementSettings is NULL! Please assign in Inspector.",
                    this);
                movementSettings = CreateFallbackSettings();
            }

            LoadCharacterConfig();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            // Init state
            _currentStamina = movementSettings.maxStamina;
            _verticalVelocity = 0f;

            // Reset input buffers
            _currentInputBuffer.Reset();
            _nextInputBuffer.Reset();

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"[CharacterPredictedMovement] OnStartNetwork: IsOwner={base.Owner.IsLocalClient}, IsServer={IsServerStarted}, " +
                    $"OwnerId={OwnerId}, Position={transform.position}");
            }
        }

        private void LateUpdate()
        {
            // Sync stamina to CharacterStats (chỉ owner)
            if (_characterStats != null && IsOwner)
            {
                _characterStats.SetStamina(_currentStamina);
            }
        }

        // ========== FISHNET CSP IMPLEMENTATION ==========

        protected override void TimeManager_OnTick()
        {
            // ✅ SWAP INPUT BUFFER
            // Dùng input từ frame trước để tránh timing issue
            if (_nextInputBuffer.LastUpdateFrame > _currentInputBuffer.LastUpdateFrame)
            {
                _currentInputBuffer = _nextInputBuffer;

                if (enableDebugLogs && _currentInputBuffer.MoveInput.sqrMagnitude > 0.0001f)
                {
                    Debug.Log($"[Tick] Swapped input buffer: Frame={Time.frameCount}, Tick={TimeManager.LocalTick}, " +
                              $"Input={_currentInputBuffer.MoveInput}, Sprint={_currentInputBuffer.SprintHeld}");
                }
            }

            // ✅ BUILD & SEND REPLICATE DATA
            MovementReplicateData replicateData = BuildMoveData();
            PerformReplicate(replicateData, ReplicateState.Invalid, Channel.Unreliable);

            // ✅ CREATE & SEND RECONCILE DATA
            CreateReconcile();
        }

        /// <summary>
        /// Build replicate data từ buffered input.
        /// Chỉ owner build, non-owner return default (sẽ nhận qua network).
        /// </summary>
        private MovementReplicateData BuildMoveData()
        {
            // Non-owner return default (server sẽ gửi data qua network)
            if (!IsOwner)
                return default;

            // Get camera rotation
            float cameraYaw = transform.eulerAngles.y;
            if (_playerCamera != null)
            {
                cameraYaw = _playerCamera.transform.eulerAngles.y;
            }

            // ✅ USE BUFFERED INPUT
            return new MovementReplicateData(
                _currentInputBuffer.MoveInput,
                Quaternion.Euler(0f, cameraYaw, 0f),
                _currentInputBuffer.SprintHeld,
                _currentInputBuffer.CrouchHeld
            );
        }

        [Replicate]
        private void PerformReplicate(MovementReplicateData data, ReplicateState state,
            Channel channel = Channel.Unreliable)
        {
            bool asServer = IsServerStarted;
            bool replaying = IsReplaying(state);

            if (enableDebugLogs)
            {
                string role = asServer
                    ? (IsOwner ? "Server+Owner" : "Server")
                    : (IsOwner ? "Client(Owner)" : "Client(Spectator)");
                Debug.Log($"[Replicate] Role={role}, State={state}, Tick={data.GetTick()}, " +
                          $"Input={data.MoveInput}, Sprint={data.IsSprinting}, Pos={transform.position}");
            }

            // ✅ CLIENT-SIDE PREDICTION (cho spectators xem người khác di chuyển)
            if (!IsServerStarted && !IsOwner)
            {
                if (IsTicked(state) && IsCreated(state))
                {
                    // Cache replicate data nhận từ server
                    _lastTickedReplicateData = data;
                }
                else if (!IsCreated(state))
                {
                    // Predict future tick using last known input
                    uint currentTick = data.GetTick();
                    uint lastKnownTick = _lastTickedReplicateData.GetTick();

                    if (currentTick > lastKnownTick && currentTick - lastKnownTick <= 2)
                    {
                        // Use last known input (predict max 2 ticks)
                        data = _lastTickedReplicateData;
                    }
                    else
                    {
                        // Too far or no cache, use default (no movement)
                        data = default;
                    }
                }
            }

            // ✅ SIMULATE MOVEMENT
            SimulateMovement(data, asServer, replaying);
        }

        [Reconcile]
        private void PerformReconcile(MovementReconcileData data, Channel channel = Channel.Unreliable)
        {
            if (!IsOwner) return;

            float positionError = Vector3.Distance(transform.position, data.Position);
            float rotationError = Quaternion.Angle(transform.rotation, data.Rotation);

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"[Reconcile] Tick={data.GetTick()}, PosError={positionError:F3}m, RotError={rotationError:F1}°, " +
                    $"ServerPos={data.Position}, ClientPos={transform.position}");
            }

            // ✅ FIX: ALWAYS reconcile nếu có error (không dùng threshold cao)
            if (positionError > 0.001f)
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning($"[Reconcile] APPLYING CORRECTION: Error={positionError:F3}m");
                }

                // ✅ FIX: Disable CharacterController để teleport
                if (_characterController != null)
                {
                    _characterController.enabled = false;
                    transform.position = data.Position;
                    transform.rotation = data.Rotation;
                    _characterController.enabled = true;
                }
                else
                {
                    transform.position = data.Position;
                    transform.rotation = data.Rotation;
                }

                _velocity = new Vector3(data.Velocity.x, 0f, data.Velocity.z);
                _verticalVelocity = data.Velocity.y;

                // ✅ RESET last moved tick để force re-simulate
                _lastMovedTick = 0;
            }

            // ✅ Always sync stamina
            _currentStamina = data.Stamina;
        }

        public override void CreateReconcile()
        {
            MovementReconcileData data = CreateReconcileData();

            if (enableDebugLogs && IsServerStarted)
            {
                Debug.Log($"[CreateReconcile] Server sending: Tick={TimeManager.LocalTick}, Pos={data.Position}, " +
                          $"Vel={data.Velocity}, Stamina={data.Stamina:F1}");
            }

            PerformReconcile(data, Channel.Unreliable);
        }

        protected override MovementReconcileData CreateReconcileData()
        {
            // Combine horizontal velocity + vertical velocity
            Vector3 fullVelocity = new Vector3(_velocity.x, _verticalVelocity, _velocity.z);

            return new MovementReconcileData(
                transform.position,
                transform.rotation,
                fullVelocity,
                _currentStamina
            );
        }

        // ========== MOVEMENT SIMULATION ==========

        private void SimulateMovement(MovementReplicateData data, bool asServer, bool replaying)
        {
            float delta = TickDelta;
            uint currentTick = data.GetTick();

            // ✅ FIX 1: PREVENT DUPLICATE MOVES
            // Nếu tick này đã được simulate rồi (do replay), SKIP!
            // EXCEPT: Server luôn simulate (authoritative)
            if (!asServer && currentTick == _lastMovedTick)
            {
                if (enableDebugLogs)
                {
                    Debug.Log(
                        $"[Simulate] SKIP duplicate: Tick={currentTick} already processed, Pos={transform.position}");
                }

                return;
            }

            // ✅ Mark tick as processed
            if (!asServer)
            {
                _lastMovedTick = currentTick;
            }

            // ✅ HANDLE DEFAULT DATA
            if (data.Equals(default(MovementReplicateData)))
            {
                ApplyGravityOnly(delta);
                return;
            }

            // ✅ STAMINA REGEN
            _currentStamina = Mathf.Min(
                _currentStamina + movementSettings.staminaRegenRate * delta,
                movementSettings.maxStamina
            );

            // ✅ CALCULATE SPEED (FRESH mỗi tick - KHÔNG tích lũy)
            float finalSpeed = movementSettings.baseSpeed;

            if (data.IsSprinting && CanSprint())
            {
                finalSpeed *= movementSettings.sprintMultiplier;

                float drainRate = movementSettings.staminaDrainRate * _staminaDrainMultiplier;
                _currentStamina = Mathf.Max(0f, _currentStamina - drainRate * delta);
            }
            else if (data.IsCrouching)
            {
                finalSpeed *= movementSettings.crouchMultiplier;
            }

            finalSpeed *= (1f - _weightPenalty);

            // ✅ HORIZONTAL MOVEMENT
            Vector3 moveDir = new Vector3(data.MoveInput.x, 0f, data.MoveInput.y);
            if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

            moveDir = data.Rotation * moveDir;
            Vector3 horizontalVelocity = moveDir * finalSpeed;

            // ✅ VERTICAL MOVEMENT
            _verticalVelocity += Physics.gravity.y * delta * 3f;
            _verticalVelocity = Mathf.Max(_verticalVelocity, -40f);

            // ✅ COMBINE
            Vector3 totalMovement = new Vector3(
                horizontalVelocity.x,
                _verticalVelocity,
                horizontalVelocity.z
            );

            // ✅ APPLY MOVEMENT (CHỈ 1 LẦN PER TICK)
            Vector3 positionBefore = transform.position;

            if (_characterController != null)
            {
                _characterController.Move(totalMovement * delta);
            }
            else
            {
                transform.position += totalMovement * delta;
            }

            Vector3 positionAfter = transform.position;
            Vector3 actualMovement = positionAfter - positionBefore;

            // ✅ UPDATE ROTATION
            if (moveDir.sqrMagnitude > 0.0001f)
            {
                transform.rotation = data.Rotation;
            }

            // ✅ TRACK STATE
            _velocity = horizontalVelocity;
            _currentMoveSpeed = horizontalVelocity.magnitude;

            if (enableDebugLogs && data.MoveInput.sqrMagnitude > 0.0001f)
            {
                string role = asServer ? "Server" : (IsOwner ? "Client(Owner)" : "Client(Spectator)");
                Debug.Log($"[Simulate] Role={role}, Tick={currentTick}, Speed={finalSpeed:F2}, " +
                          $"FinalVel={horizontalVelocity.magnitude:F2}, Pos={transform.position}, " +
                          $"ActualMove={actualMovement.magnitude:F3}, Input={data.MoveInput}, Replaying={replaying}");
            }
        }


        private void ApplyGravityOnly(float delta)
        {
            _verticalVelocity += Physics.gravity.y * delta * 3f;
            _verticalVelocity = Mathf.Max(_verticalVelocity, -40f);

            Vector3 gravityMovement = new Vector3(0f, _verticalVelocity, 0f);

            if (_characterController != null)
            {
                _characterController.Move(gravityMovement * delta);
            }

            _velocity = Vector3.zero;
            _currentMoveSpeed = 0f;
        }


        private bool CanSprint()
        {
            return _currentStamina >= movementSettings.minStaminaToSprint;
        }

        // ========== CONFIG LOADING ==========

        private void LoadCharacterConfig()
        {
            if (_characterStats == null) return;

            var config = GameConfigLoader.Instance?.GetCharacterConfig("CHAR_DEFAULT");
            if (config != null)
            {
                movementSettings.baseSpeed = config.BaseMoveSpeed;
                movementSettings.maxStamina = config.BaseStamina;

                if (enableDebugLogs)
                {
                    Debug.Log(
                        $"[CharacterPredictedMovement] Loaded config: Speed={config.BaseMoveSpeed}, Stamina={config.BaseStamina}");
                }
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

            Debug.LogWarning("[CharacterPredictedMovement] Created fallback MovementSettings");
            return settings;
        }

        // ========== PUBLIC API (CALLED FROM PlayerInputHandler.Update) ==========

        /// <summary>
        /// Set move input (buffered).
        /// Gọi từ PlayerInputHandler.Update() mỗi frame.
        /// Input sẽ được buffer và dùng trong tick tiếp theo.
        /// </summary>
        public void SetMoveInput(Vector2 input)
        {
            _nextInputBuffer.MoveInput = input;
            _nextInputBuffer.LastUpdateFrame = (uint)Time.frameCount;

            if (enableDebugLogs && input.sqrMagnitude > 0.0001f)
            {
                Debug.Log($"[SetMoveInput] Frame={Time.frameCount}, Input={input}, BufferedForNextTick");
            }
        }

        /// <summary>
        /// Set sprinting state (buffered).
        /// </summary>
        public void SetSprinting(bool sprinting)
        {
            _nextInputBuffer.SprintHeld = sprinting;
            _nextInputBuffer.LastUpdateFrame = (uint)Time.frameCount;
        }

        /// <summary>
        /// Set crouching state (buffered).
        /// </summary>
        public void SetCrouching(bool crouching)
        {
            _nextInputBuffer.CrouchHeld = crouching;
            _nextInputBuffer.LastUpdateFrame = (uint)Time.frameCount;
        }

        // ========== PUBLIC GETTERS ==========

        public float GetCurrentMoveSpeed() => _currentMoveSpeed;
        public float GetStamina() => _currentStamina;
        public bool IsSprinting() => _currentInputBuffer.SprintHeld && CanSprint();
        public bool IsCrouching() => _currentInputBuffer.CrouchHeld;

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
    }
}