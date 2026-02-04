using NightHunt.Gameplay.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using NightHunt.Gameplay.Input.Core;

namespace NightHunt.Inventory.Input
{
    /// <summary>
    /// Handles ONLY inventory input (Open Inventory, Quick Slots, Drop Item)
    /// Components read input values from this handler via InputManager
    /// </summary>
    public class InventoryInputHandler : MonoBehaviour, IInputHandler
    {
        private InputActionMap inventoryActionMap;
        private InputActionMap uiActionMap;

        // Inventory actions
        private InputAction openInventoryAction;
        private InputAction dropItemAction;
        private InputAction quickSlot1Action;
        private InputAction quickSlot2Action;
        private InputAction quickSlot3Action;
        private InputAction quickSlot4Action;

        private InputAction cancelAction; 

        private bool inputEnabled = false;
        private bool inventoryOpen = false;

        // Events
        public event System.Action OnToggleInventory;
        public event System.Action OnDropItem;
        public event System.Action<int> OnQuickSlotPressed; // 0-3
        public event System.Action OnCancel;


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


        #region Initialization

        private void InitializeActions()
        {
            if (InputLayerManager.Instance == null)
            {
                Debug.LogError("[InventoryInputHandler] InputLayerManager.Instance is null!");
                return;
            }

            inventoryActionMap = InputLayerManager.Instance.InventoryMap;
            uiActionMap = InputLayerManager.Instance.UIMap;

            if (inventoryActionMap != null)
            {
                openInventoryAction = inventoryActionMap.FindAction("OpenInventory");
                dropItemAction = inventoryActionMap.FindAction("DropItem");
                quickSlot1Action = inventoryActionMap.FindAction("QuickSlot1");
                quickSlot2Action = inventoryActionMap.FindAction("QuickSlot2");
                quickSlot3Action = inventoryActionMap.FindAction("QuickSlot3");
                quickSlot4Action = inventoryActionMap.FindAction("QuickSlot4");
            }
            else
            {
                Debug.LogError("[InventoryInputHandler] 'Inventory' action map not found!");
            }

            if (uiActionMap != null)
            {
                cancelAction = uiActionMap.FindAction("Cancel");
            }
        }

        private void RegisterWithManager()
        {
            InputLayerManager.Instance?.RegisterHandler(this);
        }

        private void UnregisterFromManager()
        {
            InputLayerManager.Instance?.UnregisterHandler(this);
        }

        #endregion

        #region IInputHandler Implementation

        public bool IsInputEnabled => inputEnabled;

        public InputActionMap GetActionMap() => inventoryActionMap;

        public void EnableInput()
        {
            if (inputEnabled) return;

            inputEnabled = true;

            // Subscribe to quick slots (always active during gameplay)
            if (quickSlot1Action != null)
                quickSlot1Action.performed += ctx => OnQuickSlotPressed?.Invoke(0);

            if (quickSlot2Action != null)
                quickSlot2Action.performed += ctx => OnQuickSlotPressed?.Invoke(1);

            if (quickSlot3Action != null)
                quickSlot3Action.performed += ctx => OnQuickSlotPressed?.Invoke(2);

            if (quickSlot4Action != null)
                quickSlot4Action.performed += ctx => OnQuickSlotPressed?.Invoke(3);

            if (openInventoryAction != null)
                openInventoryAction.performed += OnOpenInventoryPerformed;

            if (dropItemAction != null)
                dropItemAction.performed += OnDropItemPerformed;

            Debug.Log("[InventoryInputHandler] Input enabled");
        }

        public void DisableInput()
        {
            if (!inputEnabled) return;

            inputEnabled = false;

            // Unsubscribe
            if (quickSlot1Action != null)
                quickSlot1Action.performed -= ctx => OnQuickSlotPressed?.Invoke(0);

            if (quickSlot2Action != null)
                quickSlot2Action.performed -= ctx => OnQuickSlotPressed?.Invoke(1);

            if (quickSlot3Action != null)
                quickSlot3Action.performed -= ctx => OnQuickSlotPressed?.Invoke(2);

            if (quickSlot4Action != null)
                quickSlot4Action.performed -= ctx => OnQuickSlotPressed?.Invoke(3);

            if (openInventoryAction != null)
                openInventoryAction.performed -= OnOpenInventoryPerformed;

            if (dropItemAction != null)
                dropItemAction.performed -= OnDropItemPerformed;

            if (cancelAction != null)
                cancelAction.performed -= OnCancelPerformed;

            // Disable action maps
            if (inventoryOpen)
            {
                inventoryOpen = false;
            }

            Debug.Log("[InventoryInputHandler] Input disabled");
        }

        #endregion

        #region Input Event Handlers

        private void OnOpenInventoryPerformed(InputAction.CallbackContext ctx)
        {
            inventoryOpen = !inventoryOpen;

            // When inventory opens, transition input state
            if (InputLayerManager.Instance != null)
            {
                if (inventoryOpen)
                {
                    InputLayerManager.Instance.TransitionToState(InputState.InventoryOpen);

                    // Subscribe to cancel when inventory is open
                    if (cancelAction != null)
                        cancelAction.performed += OnCancelPerformed;
                }
                else
                {
                    InputLayerManager.Instance.TransitionToState(InputState.PlayerAlive);

                    // Unsubscribe cancel
                    if (cancelAction != null)
                        cancelAction.performed -= OnCancelPerformed;
                }
            }

            OnToggleInventory?.Invoke();
        }

        private void OnDropItemPerformed(InputAction.CallbackContext ctx)
        {
            OnDropItem?.Invoke();
        }

        private void OnCancelPerformed(InputAction.CallbackContext ctx)
        {
            // Close inventory when ESC is pressed
            if (inventoryOpen)
            {
                inventoryOpen = false;

                if (InputLayerManager.Instance != null)
                {
                    InputLayerManager.Instance.TransitionToState(InputState.PlayerAlive);
                }

                OnCancel?.Invoke();
                OnToggleInventory?.Invoke(); // Also fire toggle event
            }
        }

        #endregion

        #region Public API

        public bool IsInventoryOpen() => inventoryOpen;

        public void ForceCloseInventory()
        {
            if (inventoryOpen)
            {
                inventoryOpen = false;

                if (InputLayerManager.Instance != null)
                {
                    InputLayerManager.Instance.TransitionToState(InputState.PlayerAlive);
                }
            }
        }

        #endregion
    }
}