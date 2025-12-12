using _Night_Hunt.Scripts.Network.Prediction;
using _Night_Hunt.Scripts.NightHuntInput;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace _Night_Hunt.Scripts.Gameplay.Character
{
    /// <summary>
    /// Networked player controller with client-side prediction
    /// Handles movement, rotation, and basic interactions
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(CharacterStats))]
    public class PlayerController : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private CharacterController characterController;
        [SerializeField] private CharacterStats stats;
        [SerializeField] private Transform cameraRig;
        [SerializeField] private PlayerInputHandler inputHandler;
        
        [Header("Movement Settings")]
        [SerializeField] private float walkSpeed = 3f;
        [SerializeField] private float sprintSpeedMultiplier = 1.5f;
        [SerializeField] private float crouchSpeedMultiplier = 0.5f;
        [SerializeField] private float rotationSpeed = 720f;
        [SerializeField] private float gravity = -20f;
        
        [Header("Stamina Settings")]
        [SerializeField] private float sprintStaminaDrain = 10f;
        [SerializeField] private float staminaRegenRate = 15f;
        [SerializeField] private float staminaRegenDelay = 1f;
        
        [Header("Network")]
        [SerializeField] private float reconciliationThreshold = 0.5f;
        
        // Synced variables (Server -> Clients)
        [SyncVar] private Vector3 syncPosition;
        [SyncVar] private Quaternion syncRotation;
        
        // Local state
        private Vector3 velocity;
        private float verticalVelocity;
        private bool isGrounded;
        private bool isSprinting;
        private bool isCrouching;
        private float timeSinceLastStaminaUse;
        
        // Prediction
        private PredictionController prediction;
        private uint lastProcessedTick = 0;
        
        // Input cache for server
        private struct InputData
        {
            public uint tick;
            public Vector3 moveInput;
            public Vector3 lookDirection;
            public bool sprint;
            public bool crouch;
        }
        
        private const int INPUT_BUFFER_SIZE = 64;
        private InputData[] inputBuffer = new InputData[INPUT_BUFFER_SIZE];

        private void Awake()
        {
            if (characterController == null)
                characterController = GetComponent<CharacterController>();
            
            if (stats == null)
                stats = GetComponent<CharacterStats>();
            
            prediction = gameObject.AddComponent<PredictionController>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            
            if (IsOwner)
            {
                SetupLocalPlayer();
            }
            else
            {
                SetupRemotePlayer();
            }
        }

        private void SetupLocalPlayer()
        {
            // Setup camera
            if (cameraRig != null)
            {
                cameraRig.gameObject.SetActive(true);
            }
            
            // Get or create input handler
            if (inputHandler == null)
            {
                var playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
                if (playerInput == null)
                {
                    playerInput = gameObject.AddComponent<UnityEngine.InputSystem.PlayerInput>();
                }
                
                inputHandler = GetComponent<PlayerInputHandler>();
                if (inputHandler == null)
                {
                    inputHandler = gameObject.AddComponent<PlayerInputHandler>();
                }
            }
            
            Debug.Log($"[Player] Local player setup complete");
        }

        private void SetupRemotePlayer()
        {
            // Disable camera for remote players
            if (cameraRig != null)
            {
                cameraRig.gameObject.SetActive(false);
            }
            
            // Disable input handler for remote players
            if (inputHandler != null)
            {
                inputHandler.enabled = false;
            }
            
            Debug.Log($"[Player] Remote player setup complete");
        }

        private void Update()
        {
            if (IsOwner)
            {
                // Local player - process input with prediction
                ProcessLocalInput();
            }
            else
            {
                // Remote player - interpolate to synced position
                InterpolateRemotePlayer();
            }
            
            // Stamina regeneration
            UpdateStamina();
        }

        #region Local Player Movement (Client-Side Prediction)

        private void ProcessLocalInput()
        {
            if (inputHandler == null) return;
            
            // Get input
            Vector3 moveInput = inputHandler.GetWorldMoveDirection(cameraRig);
            bool sprintInput = inputHandler.SprintHeld;
            bool crouchInput = inputHandler.CrouchHeld;
            
            // Create input state
            uint currentTick = prediction.GetCurrentTick();
            InputData input = new InputData
            {
                tick = currentTick,
                moveInput = moveInput,
                lookDirection = GetLookDirection(),
                sprint = sprintInput && CanSprint(),
                crouch = crouchInput
            };
            
            // Store input in buffer
            inputBuffer[currentTick % INPUT_BUFFER_SIZE] = input;
            
            // Send to server
            CmdProcessInput(input);
            
            // Process locally (prediction)
            ProcessMovement(input);
            
            // Record prediction state
            prediction.RecordState(new PredictionController.TransformState
            {
                tick = currentTick,
                position = transform.position,
                rotation = transform.rotation,
                velocity = velocity
            });
            
            // Reset one-frame inputs
            inputHandler.ResetOneFrameInputs();
        }

        [ServerRpc]
        private void CmdProcessInput(InputData input)
        {
            // Ignore old inputs
            if (input.tick <= lastProcessedTick)
                return;
            
            lastProcessedTick = input.tick;
            
            // Process on server
            ProcessMovement(input);
            
            // Update synced variables
            syncPosition = transform.position;
            syncRotation = transform.rotation;
            
            // Send back authoritative state
            TargetReconcile(input.tick, syncPosition, syncRotation);
        }

        [TargetRpc]
        private void TargetReconcile(uint tick, Vector3 serverPos, Quaternion serverRot)
        {
            // Reconcile with server state
            PredictionController.TransformState serverState = new PredictionController.TransformState
            {
                tick = tick,
                position = serverPos,
                rotation = serverRot,
                velocity = velocity
            };
            
            bool needsReconciliation = prediction.ReconcileState(serverState);
            
            if (needsReconciliation)
            {
                // Position was corrected - update character controller
                characterController.enabled = false;
                transform.position = serverPos;
                transform.rotation = serverRot;
                characterController.enabled = true;
            }
        }

        #endregion

        #region Movement Logic (Shared)

        private void ProcessMovement(InputData input)
        {
            // Ground check
            isGrounded = characterController.isGrounded;
            
            // Calculate movement speed
            float currentSpeed = CalculateSpeed(input.sprint, input.crouch);
            
            // Move
            Vector3 moveDirection = input.moveInput * currentSpeed;
            
            // Apply gravity
            if (isGrounded && verticalVelocity < 0)
            {
                verticalVelocity = -2f;
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }
            
            moveDirection.y = verticalVelocity;
            
            // Apply movement
            characterController.Move(moveDirection * Time.deltaTime);
            
            // Rotate towards look direction
            if (input.lookDirection.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(input.lookDirection);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
            }
            
            // Update state
            isSprinting = input.sprint;
            isCrouching = input.crouch;
            velocity = characterController.velocity;
            
            // Use stamina if sprinting
            if (isSprinting && velocity.magnitude > 0.1f)
            {
                float staminaCost = sprintStaminaDrain * Time.deltaTime;
                stats.UseStamina(staminaCost);
                timeSinceLastStaminaUse = 0f;
            }
        }

        private float CalculateSpeed(bool sprint, bool crouch)
        {
            float speed = walkSpeed;
            
            // Apply stat modifier
            speed *= stats.GetMoveSpeed();
            
            // Apply sprint/crouch
            if (sprint && !crouch)
            {
                speed *= sprintSpeedMultiplier;
            }
            else if (crouch)
            {
                speed *= crouchSpeedMultiplier;
            }
            
            return speed;
        }

        private bool CanSprint()
        {
            // Check stamina
            if (stats.GetCurrentStamina() <= 0)
                return false;
            
            // Check weight
            var weightSystem = GetComponent<WeightSystem>();
            if (weightSystem != null && !weightSystem.CanSprint())
                return false;
            
            return true;
        }

        #endregion

        #region Remote Player Interpolation

        private void InterpolateRemotePlayer()
        {
            // Smoothly move towards synced position
            float distance = Vector3.Distance(transform.position, syncPosition);
            
            if (distance > 0.1f)
            {
                // Interpolate position
                float speed = distance > 2f ? 15f : 5f;
                transform.position = Vector3.Lerp(
                    transform.position,
                    syncPosition,
                    Time.deltaTime * speed
                );
                
                // Interpolate rotation
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    syncRotation,
                    Time.deltaTime * 10f
                );
            }
        }

        #endregion

        #region Stamina

        private void UpdateStamina()
        {
            timeSinceLastStaminaUse += Time.deltaTime;
            
            // Regenerate stamina after delay
            if (timeSinceLastStaminaUse >= staminaRegenDelay)
            {
                float regenAmount = staminaRegenRate * Time.deltaTime;
                stats.RegenerateStamina(regenAmount);
            }
        }

        #endregion

        #region Camera & Look

        private Vector3 GetLookDirection()
        {
            if (cameraRig == null) return transform.forward;
            
            // Get look direction from camera
            Vector3 forward = cameraRig.forward;
            forward.y = 0;
            forward.Normalize();
            
            return forward;
        }

        #endregion

        #region Getters

        public bool IsMoving() => velocity.magnitude > 0.1f;
        public bool IsSprinting() => isSprinting;
        public bool IsCrouching() => isCrouching;
        public Vector3 GetVelocity() => velocity;
        public CharacterStats GetStats() => stats;

        #endregion
    }

   
}