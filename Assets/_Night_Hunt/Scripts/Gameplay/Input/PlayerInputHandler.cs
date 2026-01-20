using UnityEngine;
using UnityEngine.InputSystem;
using NightHunt.Networking;
using NightHunt.Gameplay.Character;

namespace NightHunt.Gameplay.Input
{
    /// <summary>
    /// Handles player input using Unity's new Input System
    /// Supports multiple players without input conflicts
    /// Uses PlayerInput component for multi-player support
    /// Integrated with InputLayerManager for proper enable/disable management
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerInputHandler : MonoBehaviour
    {
        [Header("Input Settings")]
        [SerializeField] private float mouseSensitivity = 1f;
        [SerializeField] private bool invertY = false;

        private PlayerInput playerInput;
        private InputActionAsset inputActions; // Lấy từ PlayerInput.actions
        private InputActionMap playerActionMap;
        private InputActionMap uiActionMap;
        private InputLayerManager inputLayerManager;
        private InputPrediction inputPrediction;

        // Input actions
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction attackAction;
        private InputAction interactAction; 
        private InputAction crouchAction;
        private InputAction sprintAction;
        private InputAction reloadAction; 
        private InputAction inventoryAction;

        // Current input state
        private Vector2 moveInput;
        private Vector2 lookInput;
        private bool isAttacking;
        private bool isInteracting;
        private bool isCrouching;
        private bool isSprinting;
        private bool isReloading;
        private Vector3 aimDirection;

        // Camera reference for aim direction
        private UnityEngine.Camera playerCamera;

        private NetworkPlayer networkPlayer;
        private CharacterPredictedMovement _predictedMovement;
        private bool inputEnabled = false;

        private void Awake()
        {
            playerInput = GetComponent<PlayerInput>();
            inputLayerManager = InputLayerManager.Instance;
            inputPrediction = new InputPrediction();

            // Lấy InputActionAsset từ PlayerInput component
            // PlayerInput component đã có field "Actions" để assign InputActionAsset
            if (playerInput != null)
            {
                inputActions = playerInput.actions;
            }

            if (inputActions == null)
            {
                Debug.LogError("[PlayerInputHandler] PlayerInput.actions is null! Please assign InputActionAsset to PlayerInput component.");
                return;
            }

            // Setup action maps
            playerActionMap = inputActions.FindActionMap("Player");
            uiActionMap = inputActions.FindActionMap("UI");

            if (playerActionMap != null)
            {
                moveAction = playerActionMap.FindAction("Move");
                lookAction = playerActionMap.FindAction("Look");
                attackAction = playerActionMap.FindAction("Attack");
                interactAction = playerActionMap.FindAction("Interact");
                crouchAction = playerActionMap.FindAction("Crouch");
                sprintAction = playerActionMap.FindAction("Sprint");
            }
            else
            {
                Debug.LogWarning("[PlayerInputHandler] Player action map not found in InputActionAsset!");
            }

            // Find camera (for top-down, this might be different)
            playerCamera = UnityEngine.Camera.main;
            if (playerCamera == null)
            {
                playerCamera = FindFirstObjectByType<UnityEngine.Camera>();
            }
        }

        private void OnEnable()
        {
            // DON'T auto-enable input here!
            // Input should only be enabled by NetworkPlayer when IsOwner
            // Auto-enabling causes both host and client to control the same player
        }

        private void OnDisable()
        {
            DisableInput();
        }

        /// <summary>
        /// Initialize with network player reference
        /// </summary>
        public void Initialize(NetworkPlayer player)
        {
            networkPlayer = player;
        }

        /// <summary>
        /// Enable input handling
        /// Only call this when NetworkPlayer.IsOwner = true
        /// </summary>
        public void EnableInput()
        {
            if (inputEnabled) return;
            
            // Safety check: Only enable if network player is owner
            if (networkPlayer != null && !networkPlayer.IsOwner)
            {
                Debug.LogWarning("[PlayerInputHandler] Cannot enable input: Not owner!");
                return;
            }

            inputEnabled = true;

            // Use InputLayerManager to enable Player action map
            if (inputLayerManager != null)
            {
                inputLayerManager.TransitionToState(InputState.PlayerAlive);
            }
            else if (playerActionMap != null)
            {
                // Fallback if InputLayerManager not available
                playerActionMap.Enable();
            }

            // Subscribe to input events
            if (moveAction != null)
                moveAction.performed += OnMovePerformed;
            if (moveAction != null)
                moveAction.canceled += OnMoveCanceled;

            if (lookAction != null)
                lookAction.performed += OnLookPerformed;

            if (attackAction != null)
                attackAction.performed += OnAttackPerformed;
            if (attackAction != null)
                attackAction.canceled += OnAttackCanceled;

            if (interactAction != null)
                interactAction.performed += OnInteractPerformed;
            if (interactAction != null)
                interactAction.canceled += OnInteractCanceled;

            if (crouchAction != null)
                crouchAction.performed += OnCrouchPerformed;
            if (crouchAction != null)
                crouchAction.canceled += OnCrouchCanceled;

            if (sprintAction != null)
                sprintAction.performed += OnSprintPerformed;
            if (sprintAction != null)
                sprintAction.canceled += OnSprintCanceled;
        }

        /// <summary>
        /// Disable input handling
        /// </summary>
        public void DisableInput()
        {
            if (!inputEnabled) return;

            inputEnabled = false;

            // Use InputLayerManager to disable Player action map
            if (inputLayerManager != null)
            {
                inputLayerManager.TransitionToState(InputState.PlayerDead);
            }
            else if (playerActionMap != null)
            {
                // Fallback if InputLayerManager not available
                playerActionMap.Disable();
            }

            // Unsubscribe from input events
            if (moveAction != null)
                moveAction.performed -= OnMovePerformed;
            if (moveAction != null)
                moveAction.canceled -= OnMoveCanceled;

            if (lookAction != null)
                lookAction.performed -= OnLookPerformed;

            if (attackAction != null)
                attackAction.performed -= OnAttackPerformed;
            if (attackAction != null)
                attackAction.canceled -= OnAttackCanceled;

            if (interactAction != null)
                interactAction.performed -= OnInteractPerformed;
            if (interactAction != null)
                interactAction.canceled -= OnInteractCanceled;

            if (crouchAction != null)
                crouchAction.performed -= OnCrouchPerformed;
            if (crouchAction != null)
                crouchAction.canceled -= OnCrouchCanceled;

            if (sprintAction != null)
                sprintAction.performed -= OnSprintPerformed;
            if (sprintAction != null)
                sprintAction.canceled -= OnSprintCanceled;

            // Reset input state
            moveInput = Vector2.zero;
            lookInput = Vector2.zero;
            isAttacking = false;
            isInteracting = false;
            isCrouching = false;
            isSprinting = false;
        }

        private void Update()
        {
            // Only process input if enabled AND network player is owner
            if (!inputEnabled) return;
            
            // Double-check: Only owner should have input enabled
            if (networkPlayer != null && !networkPlayer.IsOwner)
            {
                DisableInput();
                return;
            }

            // Update aim direction based on mouse position (for top-down)
            UpdateAimDirection();

            // Store input for prediction (legacy system)
            if (inputPrediction != null)
            {
                inputPrediction.AddCommand(moveInput, isSprinting, isCrouching, isAttacking, aimDirection);
            }

            // Submit input to CharacterMovementPredicted (which uses PredictedMovement package)
            if (_predictedMovement == null && networkPlayer != null)
            {
                _predictedMovement = networkPlayer.GetComponent<CharacterPredictedMovement>();
                if (_predictedMovement == null)
                {
                    Debug.LogWarning($"[PlayerInputHandler] CharacterMovementPredicted component not found on NetworkPlayer!");
                }
            }

            if (_predictedMovement != null)
            {
                // Debug log để verify movement component và state
                if (moveInput.sqrMagnitude > 0.0001f)
                {
                    Debug.Log($"[PlayerInputHandler] Input: move={moveInput} sprint={isSprinting} crouch={isCrouching} IsSpawned={_predictedMovement.IsSpawned} IsOwner={_predictedMovement.IsOwner}");
                }
                
                if (_predictedMovement.IsSpawned && _predictedMovement.IsOwner)
                {
                    // Use SetMoveInput/SetSprinting/SetCrouching API (CharacterMovement will handle SubmitInput internally)
                    _predictedMovement.SetMoveInput(moveInput);
                    _predictedMovement.SetSprinting(isSprinting);
                    _predictedMovement.SetCrouching(isCrouching);
                }
                else
                {
                    // Debug log nếu không thể submit
                    if (moveInput.sqrMagnitude > 0.0001f)
                    {
                        Debug.LogWarning($"[PlayerInputHandler] Cannot submit input: IsSpawned={_predictedMovement.IsSpawned} IsOwner={_predictedMovement.IsOwner}");
                    }
                }
            }
        }

        private void UpdateAimDirection()
        {
            if (playerCamera == null) return;

            // For top-down view, aim direction is from player to mouse world position
            UnityEngine.Ray ray = playerCamera.ScreenPointToRay(UnityEngine.Input.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, transform.position);
            
            if (groundPlane.Raycast(ray, out float distance))
            {
                Vector3 hitPoint = ray.GetPoint(distance);
                aimDirection = (hitPoint - transform.position).normalized;
                aimDirection.y = 0; // Keep on horizontal plane
            }
        }

        // Input event handlers
        private void OnMovePerformed(InputAction.CallbackContext context)
        {
            moveInput = context.ReadValue<Vector2>();
        }

        private void OnMoveCanceled(InputAction.CallbackContext context)
        {
            moveInput = Vector2.zero;
        }

        private void OnLookPerformed(InputAction.CallbackContext context)
        {
            lookInput = context.ReadValue<Vector2>();
        }

        private void OnAttackPerformed(InputAction.CallbackContext context)
        {
            isAttacking = true;
        }

        private void OnAttackCanceled(InputAction.CallbackContext context)
        {
            isAttacking = false;
        }

        private void OnInteractPerformed(InputAction.CallbackContext context)
        {
            isInteracting = true;
        }

        private void OnInteractCanceled(InputAction.CallbackContext context)
        {
            isInteracting = false;
        }

        private void OnCrouchPerformed(InputAction.CallbackContext context)
        {
            isCrouching = !isCrouching; // Toggle
        }

        private void OnCrouchCanceled(InputAction.CallbackContext context)
        {
            // Crouch is toggle, so nothing on cancel
        }

        private void OnSprintPerformed(InputAction.CallbackContext context)
        {
            isSprinting = true;
        }

        private void OnSprintCanceled(InputAction.CallbackContext context)
        {
            isSprinting = false;
        }

        // Public getters for input state
        public Vector2 GetMoveInput() => moveInput;
        public Vector2 GetLookInput() => lookInput;
        public bool IsAttacking() => isAttacking;
        public bool IsInteracting() => isInteracting;
        public bool IsCrouching() => isCrouching;
        public bool IsSprinting() => isSprinting;
        public bool IsReloading() => isReloading;
        public Vector3 GetAimDirection() => aimDirection;

        public void SetReloading(bool reloading) => isReloading = reloading;

        /// <summary>
        /// Get input prediction instance
        /// </summary>
        public InputPrediction GetInputPrediction() => inputPrediction;

        /// <summary>
        /// Set scout mode (disables attack input)
        /// </summary>
        public void SetScoutMode(bool active)
        {
            if (inputLayerManager != null)
            {
                if (active)
                {
                    inputLayerManager.TransitionToState(InputState.ScoutMode);
                }
                else
                {
                    inputLayerManager.TransitionToState(InputState.PlayerAlive);
                }
            }
        }
    }
}

