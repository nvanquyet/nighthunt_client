using UnityEngine;
using UnityEngine.InputSystem;
using NightHunt.Gameplay.Input.Core;
using System;

namespace NightHunt.Gameplay.Input.Handlers.UI
{
    /// <summary>
    /// Handles ONLY UI-related input (Inventory Toggle, QuickSlot Hotkeys).
    /// Separated from gameplay input for clean architecture.
    /// Pattern matches MovementInputHandler structure.
    /// </summary>
    public class UIInputHandler : MonoBehaviour, IInputHandler
    {
        private InputActionMap uiActionMap;
        private InputAction toggleInventoryAction;
        private InputAction quickSlot1Action;
        private InputAction quickSlot2Action;
        private InputAction quickSlot3Action;
        private InputAction quickSlot4Action;
        private InputAction cancelAction; // For cancelling item usage
        
        private bool inputEnabled = false;
        
        // Events for UI Manager to subscribe
        public event Action OnInventoryToggled;
        public event Action<int> OnQuickSlotPressed; // int = slot index (0-3)
        public event Action OnCancelPressed;
        
        #region Lifecycle
        
        private void Awake()
        {
            //InitializeActions();
        }
        
        private void OnEnable()
        {
            //RegisterWithManager();
            //EnableInput(); // UI input is always enabled
        }
        
        private void OnDisable()
        {
            //DisableInput();
            //UnregisterFromManager();
        }
        
        #endregion  
        
        #region Initialization
        
        /// <summary>
        /// Initialize input actions from InputLayerManager.
        /// Assumes there's a "UI" action map with these actions:
        /// - ToggleInventory (default: Tab or I)
        /// - QuickSlot1 (default: 1)
        /// - QuickSlot2 (default: 2)
        /// - QuickSlot3 (default: 3)
        /// - QuickSlot4 (default: 4)
        /// - Cancel (default: ESC)
        /// </summary>
        private void InitializeActions()
        {
            if (InputLayerManager.Instance == null)
            {
                Debug.LogError("[UIInputHandler] InputLayerManager.Instance is null!");
                return;
            }
            
            // Try to get UI action map
            // If your InputLayerManager doesn't have a UIMap property, you'll need to add it
            // For now, we'll try to find it by name
            var inputAsset = InputLayerManager.Instance.GetComponent<PlayerInput>()?.actions;
            if (inputAsset != null)
            {
                uiActionMap = inputAsset.FindActionMap("UI");
                
                if (uiActionMap == null)
                {
                    Debug.LogError("[UIInputHandler] 'UI' action map not found! Please create it in your Input Actions asset.");
                    return;
                }
                
                toggleInventoryAction = uiActionMap.FindAction("ToggleInventory");
                quickSlot1Action = uiActionMap.FindAction("QuickSlot1");
                quickSlot2Action = uiActionMap.FindAction("QuickSlot2");
                quickSlot3Action = uiActionMap.FindAction("QuickSlot3");
                quickSlot4Action = uiActionMap.FindAction("QuickSlot4");
                cancelAction = uiActionMap.FindAction("Cancel");
                
                // Log warnings for missing actions
                if (toggleInventoryAction == null)
                    Debug.LogWarning("[UIInputHandler] 'ToggleInventory' action not found");
                if (quickSlot1Action == null)
                    Debug.LogWarning("[UIInputHandler] 'QuickSlot1' action not found");
                if (cancelAction == null)
                    Debug.LogWarning("[UIInputHandler] 'Cancel' action not found");
            }
            else
            {
                Debug.LogError("[UIInputHandler] Could not find PlayerInput component with Input Actions!");
            }
        }
        
        /// <summary>
        /// Register with InputLayerManager.
        /// </summary>
        private void RegisterWithManager()
        {
            InputLayerManager.Instance?.RegisterHandler(this);
        }
        
        /// <summary>
        /// Unregister from InputLayerManager.
        /// </summary>
        private void UnregisterFromManager()
        {
            InputLayerManager.Instance?.UnregisterHandler(this);
        }
        
        #endregion
        
        #region IInputHandler Implementation
        
        public bool IsInputEnabled => inputEnabled;
        
        public InputActionMap GetActionMap() => uiActionMap;
        
        /// <summary>
        /// Enable UI input.
        /// </summary>
        public void EnableInput()
        {
            if (inputEnabled) return;
            
            inputEnabled = true;
            
            // Subscribe to input events
            if (toggleInventoryAction != null)
            {
                toggleInventoryAction.performed += OnToggleInventoryPerformed;
            }
            
            if (quickSlot1Action != null)
            {
                quickSlot1Action.performed += OnQuickSlot1Performed;
            }
            
            if (quickSlot2Action != null)
            {
                quickSlot2Action.performed += OnQuickSlot2Performed;
            }
            
            if (quickSlot3Action != null)
            {
                quickSlot3Action.performed += OnQuickSlot3Performed;
            }
            
            if (quickSlot4Action != null)
            {
                quickSlot4Action.performed += OnQuickSlot4Performed;
            }
            
            if (cancelAction != null)
            {
                cancelAction.performed += OnCancelPerformed;
            }
            
            // Enable action map
            uiActionMap?.Enable();
            
            Debug.Log("[UIInputHandler] Input enabled");
        }
        
        /// <summary>
        /// Disable UI input.
        /// </summary>
        public void DisableInput()
        {
            if (!inputEnabled) return;
            
            inputEnabled = false;
            
            // Unsubscribe
            if (toggleInventoryAction != null)
            {
                toggleInventoryAction.performed -= OnToggleInventoryPerformed;
            }
            
            if (quickSlot1Action != null)
            {
                quickSlot1Action.performed -= OnQuickSlot1Performed;
            }
            
            if (quickSlot2Action != null)
            {
                quickSlot2Action.performed -= OnQuickSlot2Performed;
            }
            
            if (quickSlot3Action != null)
            {
                quickSlot3Action.performed -= OnQuickSlot3Performed;
            }
            
            if (quickSlot4Action != null)
            {
                quickSlot4Action.performed -= OnQuickSlot4Performed;
            }
            
            if (cancelAction != null)
            {
                cancelAction.performed -= OnCancelPerformed;
            }
            
            // Disable action map
            uiActionMap?.Disable();
            
            Debug.Log("[UIInputHandler] Input disabled");
        }
        
        #endregion
        
        #region Input Event Handlers
        
        private void OnToggleInventoryPerformed(InputAction.CallbackContext context)
        {
            Debug.Log("[UIInputHandler] Inventory toggled");
            OnInventoryToggled?.Invoke();
        }
        
        private void OnQuickSlot1Performed(InputAction.CallbackContext context)
        {
            Debug.Log("[UIInputHandler] QuickSlot 1 pressed");
            OnQuickSlotPressed?.Invoke(0);
        }
        
        private void OnQuickSlot2Performed(InputAction.CallbackContext context)
        {
            Debug.Log("[UIInputHandler] QuickSlot 2 pressed");
            OnQuickSlotPressed?.Invoke(1);
        }
        
        private void OnQuickSlot3Performed(InputAction.CallbackContext context)
        {
            Debug.Log("[UIInputHandler] QuickSlot 3 pressed");
            OnQuickSlotPressed?.Invoke(2);
        }
        
        private void OnQuickSlot4Performed(InputAction.CallbackContext context)
        {
            Debug.Log("[UIInputHandler] QuickSlot 4 pressed");
            OnQuickSlotPressed?.Invoke(3);
        }
        
        private void OnCancelPerformed(InputAction.CallbackContext context)
        {
            Debug.Log("[UIInputHandler] Cancel pressed");
            OnCancelPressed?.Invoke();
        }
        
        #endregion
    }
}