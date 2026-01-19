using UnityEngine;
using NightHunt.Data;
using FishNet.Object;
using NightHunt.Networking;
using NightHunt.Gameplay.Character.Movement;
using NightHunt.Gameplay.Core.Prediction;
using NightHunt.Gameplay.Input;
using NightHunt.Gameplay.Core.Utils;

namespace NightHunt.Gameplay.Character
{
    /// <summary>
    /// Handles character movement for top-down 3D game
    /// Supports sprint, crouch, weight penalties, and stamina
    /// Implements IPredictable for client-side prediction
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class CharacterMovement : MonoBehaviour, IPredictable<MovementState>
    {
        [Header("Movement Settings")]
        [SerializeField] private float baseMoveSpeed = 5f;
        [SerializeField] private float sprintMultiplier = 1.5f;
        [SerializeField] private float crouchMultiplier = 0.6f;
        [SerializeField] private float rotationSpeed = 10f;

        [Header("Stamina Settings")]
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float staminaDrainRate = 20f; // per second when sprinting
        [SerializeField] private float staminaRegenRate = 15f; // per second when not sprinting
        [SerializeField] private float minStaminaToSprint = 10f;

        private CharacterController characterController;
        private CharacterStats characterStats;
        private NetworkPlayer networkPlayer; // Reference to NetworkPlayer to check server/client
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
        private MovementSync movementSync;
        private PredictionManager<MovementState> predictionManager;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            characterStats = GetComponent<CharacterStats>();
            networkPlayer = GetComponent<NetworkPlayer>();
            currentStamina = maxStamina;

            // Initialize prediction
            predictionManager = new PredictionManager<MovementState>(this);
            
            // Get movement sync
            movementSync = GetComponent<MovementSync>();
            if (movementSync == null)
            {
                movementSync = gameObject.AddComponent<MovementSync>();
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
            var inputHandler = GetComponent<PlayerInputHandler>();
            if (inputHandler != null)
            {
                var inputPrediction = inputHandler.GetInputPrediction();
                movementPrediction = new MovementPrediction(this, inputPrediction);
                if (movementSync != null)
                {
                    movementSync.SetMovementPrediction(movementPrediction);
                }
            }
        }

        private void Update()
        {
            // SERVER AUTHORITY với CLIENT-SIDE PREDICTION
            // 
            // Logic:
            // - Server instance: Chạy movement (server authority, check lại input)
            // - Client owner instance: Chạy prediction (smooth, responsive)
            // - Client non-owner instance: KHÔNG chạy (chỉ nhận từ NetworkTransform)
            // - NetworkTransform sync từ server → client reconcile nếu sai lệch
            
            bool isServerInstance = networkPlayer != null && networkPlayer.IsServerInitialized;
            bool isClientOwner = networkPlayer != null && networkPlayer.IsOwner && !isServerInstance;
            
            // Chỉ chạy movement trên server HOẶC client owner (prediction)
            if (!isServerInstance && !isClientOwner)
            {
                // Client non-owner: Không chạy movement, chỉ nhận từ NetworkTransform
                return;
            }
            
            // Server instance (authority) HOẶC Client owner (prediction): Chạy movement
            UpdateStamina();
            UpdateMovement();
            ApplyMovement();

            // Client prediction
            if (isClientOwner && predictionManager != null)
            {
                predictionManager.Predict();
            }
        }

        /// <summary>
        /// Set movement input from input handler
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
            // Can only sprint if have enough stamina and not crouching
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
                isSprinting = false; // Can't sprint while crouching
            }
        }

        private void UpdateStamina()
        {
            if (isSprinting && moveInput.magnitude > 0.1f)
            {
                // Drain stamina while sprinting
                currentStamina -= staminaDrainRate * staminaDrainMultiplier * Time.deltaTime;
                currentStamina = Mathf.Max(0f, currentStamina);

                // Stop sprinting if out of stamina
                if (currentStamina <= 0f)
                {
                    isSprinting = false;
                }
            }
            else
            {
                // Regenerate stamina when not sprinting
                currentStamina += staminaRegenRate * Time.deltaTime;
                currentStamina = Mathf.Min(maxStamina, currentStamina);
            }

            // Update character stats
            if (characterStats != null)
            {
                characterStats.SetStamina(currentStamina);
            }
        }

        private void UpdateMovement()
        {
            // Calculate move speed based on state
            currentMoveSpeed = baseMoveSpeed;

            // Apply multipliers
            if (isSprinting)
            {
                currentMoveSpeed *= sprintMultiplier;
            }
            else if (isCrouching)
            {
                currentMoveSpeed *= crouchMultiplier;
            }

            // Apply weight penalty
            currentMoveSpeed *= (1f - weightPenalty);

            // Apply zone modifiers (if any)
            if (characterStats != null)
            {
                currentMoveSpeed *= characterStats.GetSpeedMultiplier();
            }

            // Calculate movement direction
            Vector3 moveDirection = new Vector3(moveInput.x, 0f, moveInput.y).normalized;

            // Rotate character to face movement direction
            if (moveDirection.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            // Calculate velocity
            velocity = moveDirection * currentMoveSpeed;
            velocity.y = -9.81f; // Gravity for CharacterController
        }

        private void ApplyMovement()
        {
            if (characterController != null)
            {
                characterController.Move(velocity * Time.deltaTime);
            }
        }

        /// <summary>
        /// Set weight penalty (0-1, where 1 = 100% penalty)
        /// </summary>
        public void SetWeightPenalty(float penalty)
        {
            weightPenalty = Mathf.Clamp01(penalty);
        }

        /// <summary>
        /// Set stamina drain multiplier
        /// </summary>
        public void SetStaminaDrainMultiplier(float multiplier)
        {
            staminaDrainMultiplier = multiplier;
        }

        /// <summary>
        /// Get current stamina
        /// </summary>
        public float GetStamina() => currentStamina;
        
        /// <summary>
        /// Get current stamina (alias for MovementSync)
        /// </summary>
        public float GetCurrentStamina() => currentStamina;
        
        /// <summary>
        /// Set stamina (for network sync)
        /// </summary>
        public void SetStamina(float stamina)
        {
            currentStamina = Mathf.Clamp(stamina, 0f, maxStamina);
            if (characterStats != null)
            {
                characterStats.SetStamina(currentStamina);
            }
        }

        /// <summary>
        /// Get current move speed
        /// </summary>
        public float GetCurrentMoveSpeed() => currentMoveSpeed;

        /// <summary>
        /// Check if can sprint
        /// </summary>
        public bool CanSprint() => currentStamina >= minStaminaToSprint && !isCrouching;

        #region IPredictable Implementation

        /// <summary>
        /// Get current state for prediction
        /// </summary>
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

        /// <summary>
        /// Set state (for reconciliation)
        /// </summary>
        public void SetState(MovementState state)
        {
            transform.position = state.Position;
            transform.rotation = state.Rotation;
            velocity = state.Velocity;
            isSprinting = state.IsSprinting;
            isCrouching = state.IsCrouching;
            currentStamina = state.Stamina;

            // Update character stats
            if (characterStats != null)
            {
                characterStats.SetStamina(currentStamina);
            }
        }

        #endregion
    }
}

