using UnityEngine;
using UnityEngine.InputSystem;

namespace _Night_Hunt.Scripts.NightHuntInput
{
     /// <summary>
    /// Per-player input handler
    /// Isolated input processing for each player instance
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerInputHandler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInput playerInput;
        
        [Header("Debug")]
        [SerializeField] private bool showDebug = false;
        
        // Input values - accessed by PlayerController
        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool CrouchHeld { get; private set; }
        public bool FireHeld { get; private set; }
        public bool AimHeld { get; private set; }
        public bool ReloadPressed { get; private set; }
        public bool InteractPressed { get; private set; }
        public bool UseItemPressed { get; private set; }
        public float ScrollInput { get; private set; }
        
        // Touch-specific
        public Vector2 TouchPosition { get; private set; }
        public bool IsTouchActive { get; private set; }
        
        private int playerId;
        private InputActionMap gameplayActions;
        
        private void Awake()
        {
            if (playerInput == null)
                playerInput = GetComponent<PlayerInput>();
            
            // Get player ID from PlayerInput
            playerId = playerInput.playerIndex;
            
            // Get gameplay action map
            gameplayActions = playerInput.actions.FindActionMap("Gameplay");
            
            if (gameplayActions == null)
            {
                Debug.LogError("[Input] 'Gameplay' action map not found!");
                return;
            }
            
            // Register with InputManager
            InputManager.Instance?.RegisterPlayer(playerId, this);
        }

        private void OnEnable()
        {
            if (gameplayActions != null)
            {
                // Subscribe to all input actions
                var move = gameplayActions.FindAction("Move");
                var look = gameplayActions.FindAction("Look");
                var sprint = gameplayActions.FindAction("Sprint");
                var crouch = gameplayActions.FindAction("Crouch");
                var fire = gameplayActions.FindAction("Fire");
                var aim = gameplayActions.FindAction("Aim");
                var reload = gameplayActions.FindAction("Reload");
                var interact = gameplayActions.FindAction("Interact");
                var useItem = gameplayActions.FindAction("UseItem");
                var scroll = gameplayActions.FindAction("Scroll");
                var touch = gameplayActions.FindAction("TouchPosition");
                var touchPress = gameplayActions.FindAction("TouchPress");
                
                if (move != null) move.performed += OnMove;
                if (move != null) move.canceled += OnMove;
                
                if (look != null) look.performed += OnLook;
                if (look != null) look.canceled += OnLook;
                
                if (sprint != null) sprint.performed += OnSprint;
                if (sprint != null) sprint.canceled += OnSprint;
                
                if (crouch != null) crouch.performed += OnCrouch;
                if (crouch != null) crouch.canceled += OnCrouch;
                
                if (fire != null) fire.performed += OnFire;
                if (fire != null) fire.canceled += OnFire;
                
                if (aim != null) aim.performed += OnAim;
                if (aim != null) aim.canceled += OnAim;
                
                if (reload != null) reload.performed += OnReload;
                if (interact != null) interact.performed += OnInteract;
                if (useItem != null) useItem.performed += OnUseItem;
                
                if (scroll != null) scroll.performed += OnScroll;
                if (scroll != null) scroll.canceled += OnScroll;
                
                if (touch != null) touch.performed += OnTouchPosition;
                if (touchPress != null) touchPress.performed += OnTouchPress;
                if (touchPress != null) touchPress.canceled += OnTouchPress;
                
                gameplayActions.Enable();
            }
        }

        private void OnDisable()
        {
            if (gameplayActions != null)
            {
                var move = gameplayActions.FindAction("Move");
                var look = gameplayActions.FindAction("Look");
                var sprint = gameplayActions.FindAction("Sprint");
                var crouch = gameplayActions.FindAction("Crouch");
                var fire = gameplayActions.FindAction("Fire");
                var aim = gameplayActions.FindAction("Aim");
                var reload = gameplayActions.FindAction("Reload");
                var interact = gameplayActions.FindAction("Interact");
                var useItem = gameplayActions.FindAction("UseItem");
                var scroll = gameplayActions.FindAction("Scroll");
                var touch = gameplayActions.FindAction("TouchPosition");
                var touchPress = gameplayActions.FindAction("TouchPress");
                
                if (move != null) move.performed -= OnMove;
                if (move != null) move.canceled -= OnMove;
                
                if (look != null) look.performed -= OnLook;
                if (look != null) look.canceled -= OnLook;
                
                if (sprint != null) sprint.performed -= OnSprint;
                if (sprint != null) sprint.canceled -= OnSprint;
                
                if (crouch != null) crouch.performed -= OnCrouch;
                if (crouch != null) crouch.canceled -= OnCrouch;
                
                if (fire != null) fire.performed -= OnFire;
                if (fire != null) fire.canceled -= OnFire;
                
                if (aim != null) aim.performed -= OnAim;
                if (aim != null) aim.canceled -= OnAim;
                
                if (reload != null) reload.performed -= OnReload;
                if (interact != null) interact.performed -= OnInteract;
                if (useItem != null) useItem.performed -= OnUseItem;
                
                if (scroll != null) scroll.performed -= OnScroll;
                if (scroll != null) scroll.canceled -= OnScroll;
                
                if (touch != null) touch.performed -= OnTouchPosition;
                if (touchPress != null) touchPress.performed -= OnTouchPress;
                if (touchPress != null) touchPress.canceled -= OnTouchPress;
                
                gameplayActions.Disable();
            }
        }

        private void OnDestroy()
        {
            InputManager.Instance?.UnregisterPlayer(playerId);
        }

        #region Input Callbacks

        private void OnMove(InputAction.CallbackContext context)
        {
            Vector2 input = context.ReadValue<Vector2>();
            
            // Apply deadzone
            float deadzone = InputManager.Instance?.GetJoystickDeadzone() ?? 0.15f;
            if (input.magnitude < deadzone)
            {
                input = Vector2.zero;
            }
            else
            {
                // Normalize beyond deadzone
                input = input.normalized * ((input.magnitude - deadzone) / (1f - deadzone));
            }
            
            MoveInput = input;
            
            if (showDebug)
                Debug.Log($"[Input] Player {playerId} Move: {MoveInput}");
        }

        private void OnLook(InputAction.CallbackContext context)
        {
            Vector2 input = context.ReadValue<Vector2>();
            
            // Apply sensitivity
            float sensitivity = InputManager.Instance?.GetMouseSensitivity() ?? 1f;
            input *= sensitivity;
            
            // Invert Y if needed
            if (InputManager.Instance?.GetInvertYAxis() ?? false)
            {
                input.y = -input.y;
            }
            
            LookInput = input;
            
            if (showDebug)
                Debug.Log($"[Input] Player {playerId} Look: {LookInput}");
        }

        private void OnSprint(InputAction.CallbackContext context)
        {
            SprintHeld = context.ReadValueAsButton();
            
            if (showDebug)
                Debug.Log($"[Input] Player {playerId} Sprint: {SprintHeld}");
        }

        private void OnCrouch(InputAction.CallbackContext context)
        {
            CrouchHeld = context.ReadValueAsButton();
            
            if (showDebug)
                Debug.Log($"[Input] Player {playerId} Crouch: {CrouchHeld}");
        }

        private void OnFire(InputAction.CallbackContext context)
        {
            FireHeld = context.ReadValueAsButton();
            
            if (showDebug)
                Debug.Log($"[Input] Player {playerId} Fire: {FireHeld}");
        }

        private void OnAim(InputAction.CallbackContext context)
        {
            AimHeld = context.ReadValueAsButton();
            
            if (showDebug)
                Debug.Log($"[Input] Player {playerId} Aim: {AimHeld}");
        }

        private void OnReload(InputAction.CallbackContext context)
        {
            ReloadPressed = true;
            
            if (showDebug)
                Debug.Log($"[Input] Player {playerId} Reload");
        }

        private void OnInteract(InputAction.CallbackContext context)
        {
            InteractPressed = true;
            
            if (showDebug)
                Debug.Log($"[Input] Player {playerId} Interact");
        }

        private void OnUseItem(InputAction.CallbackContext context)
        {
            UseItemPressed = true;
            
            if (showDebug)
                Debug.Log($"[Input] Player {playerId} UseItem");
        }

        private void OnScroll(InputAction.CallbackContext context)
        {
            ScrollInput = context.ReadValue<float>();
            
            if (showDebug && Mathf.Abs(ScrollInput) > 0.01f)
                Debug.Log($"[Input] Player {playerId} Scroll: {ScrollInput}");
        }

        private void OnTouchPosition(InputAction.CallbackContext context)
        {
            TouchPosition = context.ReadValue<Vector2>();
        }

        private void OnTouchPress(InputAction.CallbackContext context)
        {
            IsTouchActive = context.ReadValueAsButton();
        }

        #endregion

        /// <summary>
        /// Reset one-frame inputs (must be called after consuming)
        /// </summary>
        public void ResetOneFrameInputs()
        {
            ReloadPressed = false;
            InteractPressed = false;
            UseItemPressed = false;
            ScrollInput = 0f;
        }

        /// <summary>
        /// Get normalized move direction in world space
        /// </summary>
        public Vector3 GetWorldMoveDirection(Transform cameraTransform)
        {
            if (MoveInput.magnitude < 0.01f)
                return Vector3.zero;
            
            // Convert 2D input to 3D world direction relative to camera
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;
            
            // Flatten to horizontal plane
            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();
            
            return (forward * MoveInput.y + right * MoveInput.x).normalized;
        }

        public int GetPlayerId() => playerId;
    }
}