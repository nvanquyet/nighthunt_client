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
    /// ✅ FIXED: Smooth movement giống FishNet example
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class CharacterPredictedMovement : FishNetPredictedBehaviour<MovementReplicateData, MovementReconcileData>
    {
        [Header("Settings")] 
        [SerializeField] private MovementSettings movementSettings;

        [Header("Debug")] 
        [SerializeField] private bool enableDebugLogs = false;
        
        private CharacterController _characterController;
        private CharacterStats _characterStats;
        private NetworkPlayer _networkPlayer;
        private CinemachineCamera _playerCamera;

        // INPUT BUFFERING
        private struct InputBuffer
        {
            public Vector2 MoveInput;
            public bool SprintHeld;
            public bool CrouchHeld;
            public uint LastUpdateFrame;

            public void Reset()
            {
                MoveInput = Vector2.zero;
                SprintHeld = false;
                CrouchHeld = false;
                LastUpdateFrame = 0;
            }
        }

        private InputBuffer _currentInputBuffer;
        private InputBuffer _nextInputBuffer;

        // State
        private float _currentStamina;
        private float _currentMoveSpeed;
        private Vector3 _velocity;
        private float _verticalVelocity;
        private float _weightPenalty;
        private float _staminaDrainMultiplier = 1f;

        // ✅ CLIENT PREDICTION cache
        private MovementReplicateData _lastTickedReplicateData;

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
                Debug.LogError($"[CharacterPredictedMovement] MovementSettings is NULL!", this);
                movementSettings = CreateFallbackSettings();
            }

            LoadCharacterConfig();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            _currentStamina = movementSettings.maxStamina;
            _verticalVelocity = 0f;
            _currentInputBuffer.Reset();
            _nextInputBuffer.Reset();

            float tickRate = 1f / (float)TimeManager.TickDelta;
            if (enableDebugLogs)
            {
                Debug.Log($"[Movement] OnStartNetwork: IsOwner={base.Owner.IsLocalClient}, " +
                         $"IsServer={IsServerStarted}, TickRate={tickRate:F1} Hz");
            }
        }

        private void LateUpdate()
        {
            if (_characterStats != null && IsOwner)
            {
                _characterStats.SetStamina(_currentStamina);
            }
        }

        // ========== FISHNET CSP ==========

        protected override void TimeManager_OnTick()
        {
            // Update input buffer
            if (_nextInputBuffer.LastUpdateFrame > _currentInputBuffer.LastUpdateFrame)
            {
                _currentInputBuffer = _nextInputBuffer;
            }

            MovementReplicateData replicateData = BuildMoveData();
            PerformReplicate(replicateData, ReplicateState.Invalid, Channel.Unreliable);
            CreateReconcile();
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
                _currentInputBuffer.MoveInput,
                Quaternion.Euler(0f, cameraYaw, 0f),
                _currentInputBuffer.SprintHeld,
                _currentInputBuffer.CrouchHeld
            );
        }

        [Replicate]
        private void PerformReplicate(MovementReplicateData data, ReplicateState state, Channel channel = Channel.Unreliable)
        {
            float delta = TickDelta;
            bool useDefaultForces = false;

            // ✅ CLIENT-SIDE PREDICTION (spectators watching other players)
            if (!IsServerStarted && !IsOwner)
            {
                if (IsTicked(state) && IsCreated(state))
                {
                    // Real data from server, cache it
                    _lastTickedReplicateData.Dispose();
                    _lastTickedReplicateData = data;
                }
                else if (!IsCreated(state)) // Future prediction
                {
                    uint currentTick = data.GetTick();
                    uint lastKnownTick = _lastTickedReplicateData.GetTick();

                    // ✅ Predict up to 2 ticks ahead
                    if (currentTick > lastKnownTick && currentTick - lastKnownTick <= 2)
                    {
                        data.Dispose();
                        data = _lastTickedReplicateData;
                        
                        // Don't predict one-time actions (jump, etc.)
                        // data.IsSprinting = false; // Optional: don't predict sprint
                    }
                    else
                    {
                        // Too far in future, use minimal forces
                        useDefaultForces = true;
                    }
                }
            }

            // ✅ Simulate movement
            SimulateMovement(data, useDefaultForces, delta);
        }

        [Reconcile]
        private void PerformReconcile(MovementReconcileData data, Channel channel = Channel.Unreliable)
        {
            if (!IsOwner) return;

            // ✅ SIMPLE DIRECT SET - NO DISABLE NEEDED
            transform.position = data.Position;
            transform.rotation = data.Rotation;

            _velocity = new Vector3(data.Velocity.x, 0f, data.Velocity.z);
            _verticalVelocity = data.Velocity.y;
            _currentStamina = data.Stamina;

            if (enableDebugLogs)
            {
                float posError = Vector3.Distance(transform.position, data.Position);
                if (posError > 0.01f)
                {
                    Debug.LogWarning($"[RECONCILE] Tick={data.GetTick()}, PosError={posError:F3}m");
                }
            }
        }

        public override void CreateReconcile()
        {
            MovementReconcileData data = CreateReconcileData();
            PerformReconcile(data, Channel.Unreliable);
        }

        protected override MovementReconcileData CreateReconcileData()
        {
            Vector3 fullVelocity = new Vector3(_velocity.x, _verticalVelocity, _velocity.z);

            return new MovementReconcileData(
                transform.position,
                transform.rotation,
                fullVelocity,
                _currentStamina
            );
        }

        // ========== MOVEMENT SIMULATION ==========

        /// <summary>
        /// ✅ FIXED: Handle both normal and default forces
        /// </summary>
        private void SimulateMovement(MovementReplicateData data, bool useDefaultForces, float delta)
        {
            if (useDefaultForces)
            {
                // ✅ Apply minimal gravity to prevent CharacterController clipping
                // (Same as FishNet example)
                Vector3 minimalForces = new Vector3(0f, -1f, 0f);
                
                if (_characterController != null)
                {
                    _characterController.Move(minimalForces * delta);
                }

                _velocity = Vector3.zero;
                _currentMoveSpeed = 0f;
                return;
            }

            // ✅ NORMAL MOVEMENT (same as before but cleaner)

            // Stamina regen
            _currentStamina = Mathf.Min(
                _currentStamina + movementSettings.staminaRegenRate * delta,
                movementSettings.maxStamina
            );

            // Calculate speed
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

            // Horizontal movement
            Vector3 moveDir = new Vector3(data.MoveInput.x, 0f, data.MoveInput.y);
            if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

            moveDir = data.Rotation * moveDir;
            Vector3 horizontalVelocity = moveDir * finalSpeed;

            // Vertical movement (gravity)
            _verticalVelocity += Physics.gravity.y * delta * 3f;
            _verticalVelocity = Mathf.Max(_verticalVelocity, -40f);

            // Combine
            Vector3 totalMovement = new Vector3(
                horizontalVelocity.x,
                _verticalVelocity,
                horizontalVelocity.z
            );

            // Apply movement
            if (_characterController != null)
            {
                _characterController.Move(totalMovement * delta);
            }
            else
            {
                transform.position += totalMovement * delta;
            }

            // Update rotation
            if (moveDir.sqrMagnitude > 0.0001f)
            {
                transform.rotation = data.Rotation;
            }

            // Track state
            _velocity = horizontalVelocity;
            _currentMoveSpeed = horizontalVelocity.magnitude;
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

        // ========== PUBLIC API ==========

        public void SetMoveInput(Vector2 input)
        {
            _nextInputBuffer.MoveInput = input;
            _nextInputBuffer.LastUpdateFrame = (uint)Time.frameCount;
        }

        public void SetSprinting(bool sprinting)
        {
            _nextInputBuffer.SprintHeld = sprinting;
            _nextInputBuffer.LastUpdateFrame = (uint)Time.frameCount;
        }

        public void SetCrouching(bool crouching)
        {
            _nextInputBuffer.CrouchHeld = crouching;
            _nextInputBuffer.LastUpdateFrame = (uint)Time.frameCount;
        }

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