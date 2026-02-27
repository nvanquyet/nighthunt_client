using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem;
using NightHunt.Gameplay.Input.Core;

namespace NightHunt.Gameplay.Input.Handlers.Movement
{
    /// <summary>
    /// Handles ONLY player movement input (Move, Sprint, Crouch, Camera Lock)
    /// Separated from combat and interaction for clean architecture
    /// Components read input values from this handler via InputManager
    /// </summary>
    public class MovementInputHandler : MonoBehaviour, IInputHandler
    {
        private InputActionMap playerActionMap;
        private InputAction moveAction;
        private InputAction sprintAction;
        private InputAction crouchAction;
        private InputAction toggleCameraLockAction; // ✅ NEW

        // Current input state
        [SerializeField] private Vector2 moveInput;
        private bool isSprinting;
        private bool isCrouching;
        private bool isCameraLocked; // ✅ NEW

        private bool inputEnabled = false;
        
        #region Lifecycle

        private void Awake()
        {
            InitializeActions();
        }

        private void OnEnable()
        {
            RegisterWithManager();
        }

        private void OnDisable()
        {
            DisableInput();
            UnregisterFromManager();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize input actions from InputLayerManager
        /// </summary>
        private void InitializeActions()
        {
            if (InputLayerManager.Instance == null)
            {
                Debug.LogError("[MovementInputHandler] InputLayerManager.Instance is null!");
                return;
            }

            playerActionMap = InputLayerManager.Instance.PlayerMap;

            if (playerActionMap != null)
            {
                moveAction = playerActionMap.FindAction("Move");
                sprintAction = playerActionMap.FindAction("Sprint");
                crouchAction = playerActionMap.FindAction("Crouch");
                toggleCameraLockAction = playerActionMap.FindAction("ToggleCameraLock"); // ✅ NEW
            }
            else
            {
                Debug.LogError("[MovementInputHandler] 'Player' action map not found!");
            }
        }

        /// <summary>
        /// Register with InputLayerManager
        /// </summary>
        private void RegisterWithManager()
        {
            InputLayerManager.Instance?.RegisterHandler(this);
        }

        /// <summary>
        /// Unregister from InputLayerManager
        /// </summary>
        private void UnregisterFromManager()
        {
            InputLayerManager.Instance?.UnregisterHandler(this);
        }

        #endregion

        #region IInputHandler Implementation

        public bool IsInputEnabled => inputEnabled;

        public InputActionMap GetActionMap()
        {
            if (playerActionMap == null && InputLayerManager.Instance != null)
                playerActionMap = InputLayerManager.Instance.PlayerMap;
            return playerActionMap;
        }

        /// <summary>
        /// Enable movement input
        /// </summary>
        public void EnableInput()
        {
            if (inputEnabled) return;

            // Retry nếu Awake chạy trước khi InputLayerManager sẵn sàng
            if (playerActionMap == null) InitializeActions();
            if (playerActionMap == null)
            {
                Debug.LogError("[MovementInputHandler] playerActionMap null – không thể EnableInput!");
                return;
            }

            inputEnabled = true;

            // Subscribe to input events
            if (moveAction != null)
            {
                moveAction.performed += OnMovePerformed;
                moveAction.canceled += OnMoveCanceled;
            }

            if (sprintAction != null)
            {
                sprintAction.performed += OnSprintPerformed;
                sprintAction.canceled += OnSprintCanceled;
            }

            if (crouchAction != null)
            {
                crouchAction.performed += OnCrouchPerformed;
            }

            // ✅ NEW: Subscribe to toggle camera lock
            if (toggleCameraLockAction != null)
            {
                toggleCameraLockAction.performed += OnToggleCameraLockPerformed;
            }

            Debug.Log("[MovementInputHandler] Input enabled");
        }

        /// <summary>
        /// Disable movement input
        /// </summary>
        public void DisableInput()
        {
            if (!inputEnabled) return;

            inputEnabled = false;

            // Unsubscribe
            if (moveAction != null)
            {
                moveAction.performed -= OnMovePerformed;
                moveAction.canceled -= OnMoveCanceled;
            }

            if (sprintAction != null)
            {
                sprintAction.performed -= OnSprintPerformed;
                sprintAction.canceled -= OnSprintCanceled;
            }

            if (crouchAction != null)
            {
                crouchAction.performed -= OnCrouchPerformed;
            }

            // ✅ NEW: Unsubscribe toggle camera lock
            if (toggleCameraLockAction != null)
            {
                toggleCameraLockAction.performed -= OnToggleCameraLockPerformed;
            }

            // Reset state
            moveInput = Vector2.zero;
            isSprinting = false;
            isCrouching = false;
            // Don't reset camera lock on disable - preserve state

            Debug.Log("[MovementInputHandler] Input disabled");
        }

        #endregion

        #region Input Event Handlers

        private void OnMovePerformed(InputAction.CallbackContext context)
        {
            moveInput = context.ReadValue<Vector2>();
        }

        private void OnMoveCanceled(InputAction.CallbackContext context)
        {
            moveInput = Vector2.zero;
        }

        private void OnSprintPerformed(InputAction.CallbackContext context)
        {
            isSprinting = true;
        }

        private void OnSprintCanceled(InputAction.CallbackContext context)
        {
            isSprinting = false;
        }

        private void OnCrouchPerformed(InputAction.CallbackContext context)
        {
            isCrouching = !isCrouching; // Toggle
        }

        // ✅ NEW: Toggle camera lock handler
        private void OnToggleCameraLockPerformed(InputAction.CallbackContext context)
        {
            isCameraLocked = !isCameraLocked; // Toggle
            Debug.Log($"[MovementInputHandler] Camera Lock: {(isCameraLocked ? "ON (Strafe)" : "OFF (Tank)")}");
            OnCameraLockToggled?.Invoke(isCameraLocked);
        }

        #endregion

        #region Public API

        public Vector2 GetMoveInput() => moveInput;
        public bool IsSprinting() => isSprinting;
        public bool IsCrouching() => isCrouching;
        public bool IsCameraLocked() => isCameraLocked; // ✅ NEW

        #endregion

        #region Events

        /// <summary>
        /// Fired when the X key toggles the camera lock state.
        /// bool = new locked state (true = Locked, false = Free).
        /// </summary>
        public event Action<bool> OnCameraLockToggled;

        #endregion
    }
}