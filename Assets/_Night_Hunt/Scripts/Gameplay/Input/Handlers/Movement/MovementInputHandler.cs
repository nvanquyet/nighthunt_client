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
        private InputAction jumpAction;
        private InputAction rollAction;

        // Current input state
        [SerializeField] private Vector2 moveInput;
        private bool isSprinting;
        private bool isCrouching;
        private bool isCameraLocked; // ✅ NEW
        // One-shot flags consumed by IsJumping() / IsRolling()
        private bool isJumping;
        private bool isRolling;

        // ── Fire-time STRAFE override ────────────────────────────────────────
        // When _lockOverrideActive = true, IsCameraLocked() returns _lockOverrideValue
        // regardless of the manual toggle.  Used by CombatInputHandler during fire.
        private bool _lockOverrideActive;
        private bool _lockOverrideValue;

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
                jumpAction = playerActionMap.FindAction("Jump");
                rollAction = playerActionMap.FindAction("Roll");
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

            if (jumpAction != null) jumpAction.started += OnJumpStarted;
            if (rollAction != null) rollAction.started += OnRollStarted;

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

            if (jumpAction != null) jumpAction.started -= OnJumpStarted;
            if (rollAction != null) rollAction.started -= OnRollStarted;

            // Reset state
            moveInput = Vector2.zero;
            isSprinting = false;
            isCrouching = false;
            isJumping = false;
            isRolling = false;
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

        private void OnJumpStarted(InputAction.CallbackContext context) { isJumping = true; }
        private void OnRollStarted(InputAction.CallbackContext context) { isRolling = true; }

        #endregion

        #region Public API

        public Vector2 GetMoveInput() => moveInput;
        public bool IsSprinting() => isSprinting;
        public bool IsCrouching() => isCrouching;

        /// <summary>
        /// Returns true once if Jump was pressed since last call (consumes the flag).
        /// Called by CharacterPredictedMovement.GatherInput() every prediction tick.
        /// </summary>
        public bool IsJumping()
        {
            if (!isJumping) return false;
            isJumping = false;
            return true;
        }

        /// <summary>
        /// Returns true once if Roll was pressed since last call (consumes the flag).
        /// Called by CharacterPredictedMovement.GatherInput() every prediction tick.
        /// </summary>
        public bool IsRolling()
        {
            if (!isRolling) return false;
            isRolling = false;
            return true;
        }

        /// <summary>
        /// Returns the effective camera-lock state.
        /// If a fire override is active (see SetCameraLockOverride), returns that value
        /// instead of the manual X-key toggle state.
        /// </summary>
        public bool IsCameraLocked() => _lockOverrideActive ? _lockOverrideValue : isCameraLocked;

        /// <summary>
        /// Called by CombatInputHandler to temporarily force STRAFE mode during fire.
        /// <para><c>active = true</c>  → override to <paramref name="forcedValue"/> regardless of X-key.</para>
        /// <para><c>active = false</c> → remove override; X-key toggle resumes.</para>
        /// </summary>
        public void SetCameraLockOverride(bool active, bool forcedValue = false)
        {
            bool prevEffective = IsCameraLocked();
            _lockOverrideActive = active;
            _lockOverrideValue  = forcedValue;
            bool newEffective = IsCameraLocked();
            // Only fire event when the effective lock state actually changes —
            // prevents CameraStateManager from toggling Free ↔ Locked every fire press/release
            // when the player already had the same lock state before firing.
            if (newEffective != prevEffective)
                OnCameraLockToggled?.Invoke(newEffective);
        }

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