using UnityEngine;
using NightHunt.Data;
using FishNet.Object;
using FishNet.Component.Transforming;
using NightHunt.Networking;
using NightHunt.Gameplay.Character.Movement;
using NightHunt.Gameplay.Core.Prediction;
using NightHunt.Gameplay.Input;
using NightHunt.Gameplay.Core.Utils;

namespace NightHunt.Gameplay.Character
{
    /// <summary>
    /// Character movement with Client-Side Prediction + NetworkTransform
    /// 
    /// ARCHITECTURE:
    /// 1. SERVER ONLY: Execute authoritative movement
    /// 2. CLIENT OWNER: Predict movement locally + Reconcile with server
    /// 3. REMOTE CLIENTS: Apply NetworkTransform state (automatic)
    /// 4. NetworkTransform: Sync position/rotation from server to clients
    /// 
    /// NetworkTransform Setup Required:
    /// - Synchronize: To Observers
    /// - Server Authoritative: ON
    /// - Send To Owner: OFF (critical for prediction!)
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NetworkTransform))]
    public class CharacterMovement : NetworkBehaviour, IPredictable<MovementState>
    {
        [Header("Movement Settings")]
        [SerializeField] private float baseMoveSpeed = 5f;
        [SerializeField] private float sprintMultiplier = 1.5f;
        [SerializeField] private float crouchMultiplier = 0.6f;
        [SerializeField] private float rotationSpeed = 10f;

        [Header("Stamina Settings")]
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float staminaDrainRate = 20f;
        [SerializeField] private float staminaRegenRate = 15f;
        [SerializeField] private float minStaminaToSprint = 10f;

        [Header("Prediction Settings")]
        [SerializeField] private float reconciliationThreshold = 0.5f; // Distance to trigger reconciliation
        [SerializeField] private bool enablePrediction = true;

        private CharacterController characterController;
        private CharacterStats characterStats;
        private NetworkTransform networkTransform;
        private NetworkPlayer networkPlayer;
        
        private float currentStamina;
        private Vector2 moveInput;
        private bool isSprinting;
        private bool isCrouching;
        private Vector3 velocity;
        private float currentMoveSpeed;

        // Weight penalty
        private float weightPenalty = 0f;
        private float staminaDrainMultiplier = 1f;

        // Prediction
        private MovementPrediction movementPrediction;
        private PredictionManager<MovementState> predictionManager;
        
        // Server state tracking for reconciliation
        private Vector3 lastServerPosition;
        private Quaternion lastServerRotation;
        private int lastReconcileTick;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            characterStats = GetComponent<CharacterStats>();
            networkTransform = GetComponent<NetworkTransform>();
            networkPlayer = GetComponent<NetworkPlayer>();
            
            currentStamina = maxStamina;

            // Initialize prediction
            if (enablePrediction)
            {
                predictionManager = new PredictionManager<MovementState>(this);
            }
        }

        private void Start()
        {
            // Load base stats from config
            if (characterStats != null)
            {
                var config = GameConfigLoader.Instance?.GetCharacterConfig("CHAR_DEFAULT");
                if (config != null)
                {
                    baseMoveSpeed = config.BaseMoveSpeed;
                    maxStamina = config.BaseStamina;
                }
            }

            currentStamina = maxStamina;

            // Initialize movement prediction
            if (enablePrediction)
            {
                var inputHandler = GetComponent<PlayerInputHandler>();
                if (inputHandler != null)
                {
                    var inputPrediction = inputHandler.GetInputPrediction();
                    movementPrediction = new MovementPrediction(this, inputPrediction);
                }
            }

            // Track initial server state
            lastServerPosition = transform.position;
            lastServerRotation = transform.rotation;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            
            // Verify NetworkTransform setup
            if (networkTransform != null)
            {
                // NetworkTransform should be:
                // - Server Authoritative: ON
                // - Send To Owner: OFF (for prediction)
                Debug.Log($"[CharacterMovement] NetworkTransform initialized for {networkPlayer?.PlayerName}");
            }
            else
            {
                Debug.LogError("[CharacterMovement] NetworkTransform component missing!");
            }
        }

        private void Update()
        {
            if (!IsSpawned) return;

            // ✅ CRITICAL LOGIC:
            // 
            // SERVER: Execute authoritative movement
            // - Receives input from ServerRpc
            // - Runs movement logic
            // - NetworkTransform syncs to all clients
            //
            // CLIENT OWNER: Predict movement
            // - Runs same movement logic locally (responsive)
            // - Reconciles with server state from NetworkTransform
            //
            // REMOTE CLIENTS: Do nothing
            // - NetworkTransform handles position/rotation
            // - No movement logic runs
            
            bool isServer = IsServerInitialized;
            bool isClientOwner = IsOwner && !isServer;
            
            if (isServer)
            {
                // ✅ SERVER: Authoritative movement
                ExecuteMovement();
            }
            else if (isClientOwner)
            {
                // ✅ CLIENT OWNER: Predictive movement
                ExecuteMovement();
                
                // Store prediction state
                if (enablePrediction && predictionManager != null)
                {
                    predictionManager.Predict();
                }
                
                // Reconcile with server state
                ReconcileWithServer();
            }
            // ✅ REMOTE CLIENTS: Do nothing (NetworkTransform handles it)
        }

        /// <summary>
        /// Execute movement logic (runs on Server and Client Owner)
        /// </summary>
        private void ExecuteMovement()
        {
            UpdateStamina();
            UpdateMovement();
            ApplyMovement();
        }

        /// <summary>
        /// CLIENT OWNER: Reconcile predicted state with server authority
        /// </summary>
        private void ReconcileWithServer()
        {
            if (!enablePrediction || networkTransform == null) return;

            // Get current server position from NetworkTransform
            // NetworkTransform updates transform.position/rotation on clients
            Vector3 serverPosition = transform.position;
            Quaternion serverRotation = transform.rotation;

            // Check if server state changed significantly
            bool positionChanged = Vector3.Distance(serverPosition, lastServerPosition) > 0.01f;
            bool rotationChanged = Quaternion.Angle(serverRotation, lastServerRotation) > 1f;

            if (positionChanged || rotationChanged)
            {
                // Calculate prediction error
                float positionError = Vector3.Distance(transform.position, serverPosition);

                if (positionError > reconciliationThreshold)
                {
                    Debug.LogWarning($"[CharacterMovement] Reconciliation triggered! Error: {positionError:F3}m");

                    if (movementPrediction != null)
                    {
                        // Create server state
                        var serverState = new MovementState
                        {
                            Position = serverPosition,
                            Rotation = serverRotation,
                            Velocity = velocity,
                            IsSprinting = isSprinting,
                            IsCrouching = isCrouching,
                            Stamina = currentStamina
                        };

                        // Get current tick from FishNet
                        int serverTick = (int) (TimeManager != null ? TimeManager.Tick : 0);
                        
                        // Reconcile prediction
                        movementPrediction.Reconcile(serverState, serverTick);
                        lastReconcileTick = serverTick;
                    }
                    else
                    {
                        // Fallback: Snap to server state
                        transform.position = serverPosition;
                        transform.rotation = serverRotation;
                    }
                }

                // Update last server state
                lastServerPosition = serverPosition;
                lastServerRotation = serverRotation;
            }
        }

        /// <summary>
        /// Set movement input (called by NetworkPlayer ServerRpc or local input)
        /// </summary>
        public void SetMoveInput(Vector2 input)
        {
            moveInput = input;
        }

        /// <summary>
        /// Set sprinting state
        /// </summary>
        public void SetSprinting(bool sprinting)
        {
            if (sprinting && currentStamina >= minStaminaToSprint && !isCrouching)
            {
                isSprinting = true;
            }
            else
            {
                isSprinting = false;
            }
        }

        /// <summary>
        /// Set crouching state
        /// </summary>
        public void SetCrouching(bool crouching)
        {
            isCrouching = crouching;
            if (isCrouching)
            {
                isSprinting = false;
            }
        }

        private void UpdateStamina()
        {
            if (isSprinting && moveInput.magnitude > 0.1f)
            {
                // Drain stamina
                currentStamina -= staminaDrainRate * staminaDrainMultiplier * Time.deltaTime;
                currentStamina = Mathf.Max(0f, currentStamina);

                if (currentStamina <= 0f)
                {
                    isSprinting = false;
                }
            }
            else
            {
                // Regenerate stamina
                currentStamina += staminaRegenRate * Time.deltaTime;
                currentStamina = Mathf.Min(maxStamina, currentStamina);
            }

            if (characterStats != null)
            {
                characterStats.SetStamina(currentStamina);
            }
        }

        private void UpdateMovement()
        {
            // Calculate speed
            currentMoveSpeed = baseMoveSpeed;

            if (isSprinting)
            {
                currentMoveSpeed *= sprintMultiplier;
            }
            else if (isCrouching)
            {
                currentMoveSpeed *= crouchMultiplier;
            }

            currentMoveSpeed *= (1f - weightPenalty);

            if (characterStats != null)
            {
                currentMoveSpeed *= characterStats.GetSpeedMultiplier();
            }

            // Calculate direction
            Vector3 moveDirection = new Vector3(moveInput.x, 0f, moveInput.y).normalized;

            // Rotate character
            if (moveDirection.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            // Calculate velocity
            velocity = moveDirection * currentMoveSpeed;
            velocity.y = -9.81f; // Gravity
        }

        private void ApplyMovement()
        {
            if (characterController != null)
            {
                characterController.Move(velocity * Time.deltaTime);
            }
        }

        #region Public API

        public void SetWeightPenalty(float penalty)
        {
            weightPenalty = Mathf.Clamp01(penalty);
        }

        public void SetStaminaDrainMultiplier(float multiplier)
        {
            staminaDrainMultiplier = multiplier;
        }

        public float GetStamina() => currentStamina;
        public float GetCurrentStamina() => currentStamina;
        
        public void SetStamina(float stamina)
        {
            currentStamina = Mathf.Clamp(stamina, 0f, maxStamina);
            if (characterStats != null)
            {
                characterStats.SetStamina(currentStamina);
            }
        }

        public float GetCurrentMoveSpeed() => currentMoveSpeed;
        public bool CanSprint() => currentStamina >= minStaminaToSprint && !isCrouching;

        #endregion

        #region IPredictable Implementation

        public MovementState GetCurrentState()
        {
            return new MovementState
            {
                Position = transform.position,
                Rotation = transform.rotation,
                Velocity = velocity,
                IsSprinting = isSprinting,
                IsCrouching = isCrouching,
                Stamina = currentStamina
            };
        }

        public void SetState(MovementState state)
        {
            transform.position = state.Position;
            transform.rotation = state.Rotation;
            velocity = state.Velocity;
            isSprinting = state.IsSprinting;
            isCrouching = state.IsCrouching;
            currentStamina = state.Stamina;

            if (characterStats != null)
            {
                characterStats.SetStamina(currentStamina);
            }
        }

        #endregion

        #if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || !IsSpawned) return;

            // Draw reconciliation threshold
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, reconciliationThreshold);

            // Show prediction error for client owner
            if (IsOwner && !IsServerInitialized && enablePrediction)
            {
                float error = Vector3.Distance(transform.position, lastServerPosition);
                if (error > 0.01f)
                {
                    Gizmos.color = error > reconciliationThreshold ? Color.red : Color.green;
                    Gizmos.DrawLine(transform.position, lastServerPosition);
                    Gizmos.DrawWireSphere(lastServerPosition, 0.2f);
                }
            }
        }
        #endif
    }
}