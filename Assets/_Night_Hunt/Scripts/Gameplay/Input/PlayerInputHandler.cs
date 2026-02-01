using UnityEngine;
using UnityEngine.InputSystem;
using NightHunt.Networking;
using NightHunt.Gameplay.Character;
using NightHunt.Gameplay.Core;
using NightHunt.InteractionSystem.Utilities;

namespace NightHunt.Gameplay.Input
{
    /// <summary>
    /// Handles player movement input at gameplay level.
    /// This class no longer reads InputActions directly – that responsibility
    /// is moved to InputRouter. It simply subscribes to router events and
    /// forwards movement state into CharacterPredictedMovement / prediction.
    /// </summary>
    [RequireComponent(typeof(InputRouter))]
    public class PlayerInputHandler : MonoBehaviour
    {
        [Header("Debug Settings")]
        [SerializeField] private bool enableLog = false;

        private InputRouter _inputRouter;
        private InputPrediction inputPrediction;

        // Current input state
        private Vector2 moveInput;
        private bool isCrouching;
        private bool isSprinting;
        private Vector3 aimDirection;

        // Camera reference for aim direction
        private UnityEngine.Camera playerCamera;

        private IMovementController _predictedMovement;

        private void Awake()
        {
            try
            {
                if (enableLog)
                    Debug.Log($"[PlayerInputHandler] Awake - Go={gameObject.name}");
                
                NetworkPlayer networkPlayer = gameObject.FindInHierarchy<NetworkPlayer>();
                if (networkPlayer != null)
                {
                    _inputRouter = ComponentRegistry.GetInputRouter(networkPlayer);
                }
                else
                {
                    _inputRouter = NightHunt.InteractionSystem.Utilities.ComponentFinder.FindComponentInHierarchy<InputRouter>(gameObject, includeInactive: false);
                }
                
                if (_inputRouter == null)
                {
                    Debug.LogError($"[PlayerInputHandler] InputRouter component not found on {gameObject.name}!");
                    enabled = false;
                    return;
                }
                
                inputPrediction = new InputPrediction();

                // Find camera (for top-down, this might be different)
                playerCamera = UnityEngine.Camera.main;
                if (playerCamera == null)
                {
                    playerCamera = FindFirstObjectByType<UnityEngine.Camera>();
                }
                
                if (enableLog)
                    Debug.Log($"[PlayerInputHandler] Awake completed successfully");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PlayerInputHandler] EXCEPTION in Awake for {gameObject.name}: {ex.Message}\n{ex.StackTrace}");
                enabled = false;
            }
        }

        private void Update()
        {
            // Update aim direction based on mouse position (for top-down)
            UpdateAimDirection();

            // Store input for prediction (legacy system)
            if (inputPrediction != null)
            {
                inputPrediction.AddCommand(moveInput, isSprinting, isCrouching, false, aimDirection);
            }

            // Submit input to CharacterMovementPredicted (which uses PredictedMovement package)
            if (_predictedMovement == null)
            {
                _predictedMovement = GetComponent<IMovementController>();
                if (_predictedMovement == null)
                {
                    Debug.LogWarning($"[PlayerInputHandler] CharacterMovementPredicted component not found on NetworkPlayer!");
                }
            }

            if (_predictedMovement != null)
            {
                // Debug log để verify movement component và state
                if (enableLog && moveInput.sqrMagnitude > 0.0001f)
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
                    if (enableLog && moveInput.sqrMagnitude > 0.0001f)
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

        // Public getters for input state
        public Vector2 GetMoveInput() => moveInput;
        public bool IsCrouching() => isCrouching;
        public bool IsSprinting() => isSprinting;
        public Vector3 GetAimDirection() => aimDirection;

        /// <summary>
        /// Get input prediction instance
        /// </summary>
        public InputPrediction GetInputPrediction() => inputPrediction;

        // Combat input state (read from InputRouter)
        private bool isAttacking = false;
        private bool isReloading = false;
        private bool scoutModeEnabled = false;

        /// <summary>
        /// Initialize with network player (called by NetworkPlayer)
        /// </summary>
        public void Initialize(NetworkPlayer player)
        {
            // Component is already initialized in Awake, this is just for compatibility
        }

        /// <summary>
        /// Enable input processing
        /// </summary>
        public void EnableInput()
        {
            enabled = true;
        }

        /// <summary>
        /// Disable input processing
        /// </summary>
        public void DisableInput()
        {
            enabled = false;
        }

        /// <summary>
        /// Check if attacking (read from InputRouter combat actions)
        /// </summary>
        public bool IsAttacking() => isAttacking;

        /// <summary>
        /// Check if reloading
        /// </summary>
        public bool IsReloading() => isReloading;

        /// <summary>
        /// Set scout mode (disables attack input)
        /// </summary>
        public void SetScoutMode(bool active)
        {
            scoutModeEnabled = active;
            // TODO: Disable combat actions when scout mode is active
        }

        private void OnEnable()
        {
            if (_inputRouter != null)
            {
                _inputRouter.OnMove += HandleMove;
                _inputRouter.OnSprintChanged += HandleSprintChanged;
                _inputRouter.OnCrouchChanged += HandleCrouchChanged;
                _inputRouter.OnAttackChanged += HandleAttackChanged;
                _inputRouter.OnReload += HandleReload;
            }
        }

        private void OnDisable()
        {
            if (_inputRouter != null)
            {
                _inputRouter.OnMove -= HandleMove;
                _inputRouter.OnSprintChanged -= HandleSprintChanged;
                _inputRouter.OnCrouchChanged -= HandleCrouchChanged;
                _inputRouter.OnAttackChanged -= HandleAttackChanged;
                _inputRouter.OnReload -= HandleReload;
            }
        }

        private void HandleMove(Vector2 move)
        {
            moveInput = move;
        }

        private void HandleSprintChanged(bool isSprintingValue)
        {
            isSprinting = isSprintingValue;
        }

        private void HandleCrouchChanged(bool isCrouchingValue)
        {
            isCrouching = isCrouchingValue;
        }

        private void HandleAttackChanged(bool attacking)
        {
            isAttacking = attacking && !scoutModeEnabled; // Disable attack in scout mode
        }

        private void HandleReload()
        {
            isReloading = true;
            // Reset after a frame (or use a timer)
            StartCoroutine(ResetReloadFlag());
        }

        private System.Collections.IEnumerator ResetReloadFlag()
        {
            yield return null;
            isReloading = false;
        }
    }
}

