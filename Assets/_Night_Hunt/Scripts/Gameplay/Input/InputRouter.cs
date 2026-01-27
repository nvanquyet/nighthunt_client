using System;
using UnityEngine;
using UnityEngine.InputSystem;
using NightHunt.Networking;
using NightHunt.Gameplay.Core.State;

namespace NightHunt.Gameplay.Input
{
    /// <summary>
    /// Central router for player input.
    /// - Reads actions from the shared InputActionAsset (InputSystem_Actions)
    /// - Enables / disables action maps via InputLayerManager based on high-level input state
    /// - Exposes C# events / callbacks for gameplay systems (movement, interaction, inventory, UI...)
    /// - Only processes input for the local owner (NetworkPlayer.IsOwner)
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    public class InputRouter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private NetworkPlayer networkPlayer;
        [SerializeField] private InputLayerManager inputLayerManager;

        // Cached components
        private PlayerInput _playerInput;
        private InputActionAsset _inputActions;

        // Action maps
        private InputActionMap _playerMap;
        private InputActionMap _combatMap;
        private InputActionMap _cameraMap;
        private InputActionMap _inventoryMap;
        private InputActionMap _uiMap;

        // Player actions
        private InputAction _move;
        private InputAction _sprint;
        private InputAction _crouch;
        private InputAction _reload;
        
        // Combat actions
        private InputAction _attack;

        // UI actions
        private InputAction _openMenu;

        #region Events

        public event Action<Vector2> OnMove;
        public event Action<bool> OnSprintChanged;
        public event Action<bool> OnCrouchChanged;
        public event Action OnReload;
        public event Action<bool> OnAttackChanged; // true when pressed, false when released

        // Note: OnInteract, OnPickup, OnInventoryToggle are now handled by package InteractionInputHandler

        /// <summary>
        /// Fired when user opens menu (escape)
        /// </summary>
        public event Action OnMenuToggle;

        #endregion

        private bool IsOwner
        {
            get
            {
                if (networkPlayer == null)
                    return false;
                return networkPlayer.IsOwner;
            }
        }

        private void Awake()
        {
            try
            {
                Debug.Log($"[InputRouter] Awake - Go={gameObject.name}");
                
                _playerInput = GetComponent<PlayerInput>();

                if (networkPlayer == null)
                {
                    networkPlayer = GetComponent<NetworkPlayer>();
                }

                if (inputLayerManager == null)
                {
                    Debug.Log($"[InputRouter] Getting InputLayerManager.Instance");
                    inputLayerManager = InputLayerManager.Instance;
                    Debug.Log($"[InputRouter] InputLayerManager.Instance = {inputLayerManager != null}");
                }

                if (_playerInput == null)
                {
                    Debug.LogError("[InputRouter] PlayerInput is missing.");
                    enabled = false;
                    return;
                }

                _inputActions = _playerInput.actions;
                if (_inputActions == null)
                {
                    Debug.LogError("[InputRouter] PlayerInput.actions is null. Please assign InputSystem_Actions asset.");
                    enabled = false;
                    return;
                }

                CacheActionMapsAndActions();
                Debug.Log($"[InputRouter] Awake completed successfully");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[InputRouter] EXCEPTION in Awake for {gameObject.name}: {ex.Message}\n{ex.StackTrace}");
                enabled = false;
            }
        }

        private void OnEnable()
        {
            try
            {
                Debug.Log($"[InputRouter] OnEnable - Go={gameObject.name}, IsOwner={IsOwner}");
                
                // Always subscribe actions (they check IsOwner internally)
                SubscribeActions();
                
                // Enable/disable maps based on ownership
                UpdateInputMapsForOwnership();
                
                Debug.Log($"[InputRouter] OnEnable completed successfully");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[InputRouter] EXCEPTION in OnEnable for {gameObject.name}: {ex.Message}\n{ex.StackTrace}");
                // Don't re-throw - just disable to prevent further issues
                DisableAllMaps();
            }
        }

        private void Update()
        {
            // Check if ownership changed and update input maps accordingly
            // This handles the case where OnEnable was called before ownership was set
            if (networkPlayer != null)
            {
                bool currentIsOwner = networkPlayer.IsOwner;
                // We'll track this in a field to detect changes
                if (_lastIsOwner != currentIsOwner)
                {
                    _lastIsOwner = currentIsOwner;
                    UpdateInputMapsForOwnership();
                }
            }
        }

        private bool _lastIsOwner = false;

        // Current input state for this player (per-player state, not global)
        private InputState _currentInputState = InputState.PlayerAlive;

        /// <summary>
        /// Update input maps based on current ownership
        /// Called from NetworkPlayer when ownership changes
        /// Each player manages their own action maps independently
        /// </summary>
        public void UpdateInputMapsForOwnership()
        {
            if (!IsOwner)
            {
                // Never process input for non-owner
                Debug.Log($"[InputRouter] Not owner, disabling all maps for {gameObject.name}");
                DisableAllMaps();
                return;
            }

            // Owner: enable input maps based on current state
            Debug.Log($"[InputRouter] Is owner, enabling input maps for {gameObject.name}, state={_currentInputState}");
            UpdateActionMapsForState(_currentInputState);
        }

        /// <summary>
        /// Transition to new input state for this player only
        /// This is per-player state, not global like InputLayerManager
        /// </summary>
        public void TransitionToState(InputState newState)
        {
            if (!IsOwner)
            {
                Debug.Log($"[InputRouter] Not owner, ignoring state transition to {newState}");
                return;
            }

            if (_currentInputState == newState)
            {
                return; // Already in this state
            }

            Debug.Log($"[InputRouter] State transition: {_currentInputState} → {newState} for {gameObject.name}");
            _currentInputState = newState;
            UpdateActionMapsForState(newState);
        }

        /// <summary>
        /// Update action maps based on current state (per-player)
        /// </summary>
        private void UpdateActionMapsForState(InputState state)
        {
            if (!IsOwner)
            {
                DisableAllMaps();
                return;
            }

            // Disable all maps first
            DisableAllMaps();

            // Enable maps based on state
            switch (state)
            {
                case InputState.PlayerAlive:
                    _playerMap?.Enable();
                    _combatMap?.Enable();
                    _cameraMap?.Enable();
                    break;

                case InputState.PlayerDead:
                    // Spectator controls only
                    _uiMap?.Enable();
                    break;

                case InputState.Spectating:
                    _uiMap?.Enable();
                    break;

                case InputState.MenuOpen:
                    _uiMap?.Enable();
                    break;

                case InputState.Paused:
                    _uiMap?.Enable();
                    break;

                case InputState.ScoutMode:
                    _cameraMap?.Enable();
                    break;

                case InputState.InventoryOpen:
                    _uiMap?.Enable();
                    break;
            }

            Debug.Log($"[InputRouter] Maps updated for state {state} on {gameObject.name}");
        }

        private void OnDisable()
        {
            UnsubscribeActions();
            DisableAllMaps();
        }

        private void CacheActionMapsAndActions()
        {
            _playerMap = _inputActions.FindActionMap("Player", throwIfNotFound: false);
            _combatMap = _inputActions.FindActionMap("Combat", throwIfNotFound: false);
            _cameraMap = _inputActions.FindActionMap("Camera", throwIfNotFound: false);
            _inventoryMap = _inputActions.FindActionMap("Inventory", throwIfNotFound: false);
            _uiMap = _inputActions.FindActionMap("UI", throwIfNotFound: false);

            if (_playerMap != null)
            {
                _move = _playerMap.FindAction("Move", false);
                _sprint = _playerMap.FindAction("Sprint", false);
                _crouch = _playerMap.FindAction("Crouch", false);
                _reload = _playerMap.FindAction("Reload", false);
            }

            // Note: Interact, Pickup, and Inventory actions are now handled by package InteractionInputHandler

            if (_uiMap != null)
            {
                _openMenu = _uiMap.FindAction("OpenMenu", false);
            }
        }

        private void SubscribeActions()
        {
            if (_move != null)
            {
                _move.performed += HandleMovePerformed;
                _move.canceled += HandleMoveCanceled;
            }

            if (_sprint != null)
            {
                _sprint.performed += HandleSprintPerformed;
                _sprint.canceled += HandleSprintCanceled;
            }

            if (_crouch != null)
            {
                _crouch.performed += HandleCrouchPerformed;
            }

            // Note: Interact and Pickup are handled by package InteractionInputHandler

            if (_reload != null)
            {
                _reload.performed += HandleReloadPerformed;
            }

            if (_attack != null)
            {
                _attack.performed += HandleAttackPerformed;
                _attack.canceled += HandleAttackCanceled;
            }

            // Note: Inventory toggle is handled by package InteractionInputHandler

            if (_openMenu != null)
            {
                _openMenu.performed += HandleMenuPerformed;
            }
        }

        private void UnsubscribeActions()
        {
            if (_move != null)
            {
                _move.performed -= HandleMovePerformed;
                _move.canceled -= HandleMoveCanceled;
            }

            if (_sprint != null)
            {
                _sprint.performed -= HandleSprintPerformed;
                _sprint.canceled -= HandleSprintCanceled;
            }

            if (_crouch != null)
            {
                _crouch.performed -= HandleCrouchPerformed;
            }

            // Note: Interact and Pickup are handled by package InteractionInputHandler

            if (_reload != null)
            {
                _reload.performed -= HandleReloadPerformed;
            }

            if (_attack != null)
            {
                _attack.performed -= HandleAttackPerformed;
                _attack.canceled -= HandleAttackCanceled;
            }

            // Note: Inventory toggle is handled by package InteractionInputHandler

            if (_openMenu != null)
            {
                _openMenu.performed -= HandleMenuPerformed;
            }
        }

        #region Action callbacks

        private void HandleMovePerformed(InputAction.CallbackContext ctx)
        {
            if (!IsOwner) return;
            Vector2 value = ctx.ReadValue<Vector2>();
            OnMove?.Invoke(value);
        }

        private void HandleMoveCanceled(InputAction.CallbackContext ctx)
        {
            if (!IsOwner) return;
            OnMove?.Invoke(Vector2.zero);
        }

        private void HandleSprintPerformed(InputAction.CallbackContext ctx)
        {
            if (!IsOwner) return;
            OnSprintChanged?.Invoke(true);
        }

        private void HandleSprintCanceled(InputAction.CallbackContext ctx)
        {
            if (!IsOwner) return;
            OnSprintChanged?.Invoke(false);
        }

        private void HandleCrouchPerformed(InputAction.CallbackContext ctx)
        {
            if (!IsOwner) return;
            OnCrouchChanged?.Invoke(true);
        }

        // Note: Interact and Pickup handlers removed - handled by package InteractionInputHandler

        private void HandleReloadPerformed(InputAction.CallbackContext ctx)
        {
            if (!IsOwner) return;
            OnReload?.Invoke();
        }

        private void HandleAttackPerformed(InputAction.CallbackContext ctx)
        {
            if (!IsOwner) return;
            OnAttackChanged?.Invoke(true);
        }

        private void HandleAttackCanceled(InputAction.CallbackContext ctx)
        {
            if (!IsOwner) return;
            OnAttackChanged?.Invoke(false);
        }

        // Note: Inventory toggle handler removed - handled by package InteractionInputHandler
        // InputLayerManager state transitions should be handled by gameplay UI that listens to InventoryEvents

        private void HandleMenuPerformed(InputAction.CallbackContext ctx)
        {
            if (!IsOwner) return;

            OnMenuToggle?.Invoke();

            // Toggle menu state for this player only
            if (_currentInputState == InputState.MenuOpen)
            {
                TransitionToState(InputState.PlayerAlive);
            }
            else
            {
                TransitionToState(InputState.MenuOpen);
            }
        }

        #endregion

        #region Map helpers

        private void EnableDefaultMaps()
        {
            _playerMap?.Enable();
            _combatMap?.Enable();
            _cameraMap?.Enable();
            _inventoryMap?.Disable();
            _uiMap?.Disable();
        }

        private void DisableAllMaps()
        {
            _playerMap?.Disable();
            _combatMap?.Disable();
            _cameraMap?.Disable();
            _inventoryMap?.Disable();
            _uiMap?.Disable();
        }

        #endregion
    }
}

