using UnityEngine;
using UnityEngine.InputSystem;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Networking;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Domain.QuickSlot;

namespace NightHunt.Inventory.Input
{
    /// <summary>
    /// Handles quick slot input (Ctrl+1/2/3/4)
    /// Separated from InventoryInputHandler for clear responsibility
    /// </summary>
    public class QuickSlotInputHandler : MonoBehaviour, IInputHandler
    {
        [Header("Configuration")]
        [SerializeField] private QuickSlotConfig config;
        [SerializeField] private float useCooldown = 0.5f;

        [Header("References")]
        [SerializeField] private QuickSlotManager quickSlotManager;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        private InputActionMap inventoryActionMap;
        private InputAction quickSlot1Action;
        private InputAction quickSlot2Action;
        private InputAction quickSlot3Action;
        private InputAction quickSlot4Action;

        private NetworkPlayer networkPlayer;
        private bool inputEnabled = false;
        private bool isInventoryOpen = false;
        private float lastUseTime = 0f;

        // Events
        public event System.Action<int> OnQuickSlotUsed; // 0-3

        #region Lifecycle

        private void Awake()
        {
            InitializeActions();
        }

        private void OnEnable()
        {
            RegisterWithManager();
            SubscribeToInventoryEvents();
        }

        private void OnDisable()
        {
            DisableInput();
            UnregisterFromManager();
            UnsubscribeFromInventoryEvents();
        }

        #endregion

        #region Initialization

        private void InitializeActions()
        {
            if (InputLayerManager.Instance == null)
            {
                Debug.LogError("[QuickSlotInputHandler] InputLayerManager.Instance is null!");
                return;
            }

            inventoryActionMap = InputLayerManager.Instance.InventoryMap;

            if (inventoryActionMap != null)
            {
                quickSlot1Action = inventoryActionMap.FindAction("QuickSlot1");
                quickSlot2Action = inventoryActionMap.FindAction("QuickSlot2");
                quickSlot3Action = inventoryActionMap.FindAction("QuickSlot3");
                quickSlot4Action = inventoryActionMap.FindAction("QuickSlot4");
            }
            else
            {
                Debug.LogError("[QuickSlotInputHandler] 'Inventory' action map not found!");
            }
        }

        public void Initialize(NetworkPlayer player)
        {
            networkPlayer = player;

            // Auto-find QuickSlotManager if not assigned
            if (quickSlotManager == null)
            {
                quickSlotManager = FindObjectOfType<QuickSlotManager>();
                if (quickSlotManager == null)
                {
                    Debug.LogWarning("[QuickSlotInputHandler] QuickSlotManager not found in scene!");
                }
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

            if (networkPlayer != null && !networkPlayer.IsOwner)
            {
                Debug.LogWarning("[QuickSlotInputHandler] Cannot enable: Not owner!");
                return;
            }

            inputEnabled = true;

            // Subscribe to quick slot actions
            if (quickSlot1Action != null)
                quickSlot1Action.performed += ctx => UseQuickSlot(0);

            if (quickSlot2Action != null)
                quickSlot2Action.performed += ctx => UseQuickSlot(1);

            if (quickSlot3Action != null)
                quickSlot3Action.performed += ctx => UseQuickSlot(2);

            if (quickSlot4Action != null)
                quickSlot4Action.performed += ctx => UseQuickSlot(3);

            Debug.Log("[QuickSlotInputHandler] Input enabled");
        }

        public void DisableInput()
        {
            if (!inputEnabled) return;

            inputEnabled = false;

            // Unsubscribe
            if (quickSlot1Action != null)
                quickSlot1Action.performed -= ctx => UseQuickSlot(0);

            if (quickSlot2Action != null)
                quickSlot2Action.performed -= ctx => UseQuickSlot(1);

            if (quickSlot3Action != null)
                quickSlot3Action.performed -= ctx => UseQuickSlot(2);

            if (quickSlot4Action != null)
                quickSlot4Action.performed -= ctx => UseQuickSlot(3);

            Debug.Log("[QuickSlotInputHandler] Input disabled");
        }

        #endregion

        #region Quick Slot Usage

        private void UseQuickSlot(int slotIndex)
        {
            // Block if inventory is open
            if (isInventoryOpen)
            {
                if (enableDebugLogs)
                    Debug.Log($"[QuickSlotInputHandler] Blocked - inventory is open");
                return;
            }

            // Cooldown check
            if (Time.time - lastUseTime < useCooldown)
            {
                if (enableDebugLogs)
                    Debug.Log($"[QuickSlotInputHandler] Cooldown active");
                return;
            }

            if (quickSlotManager == null)
            {
                Debug.LogError("[QuickSlotInputHandler] QuickSlotManager not assigned!");
                return;
            }

            // Get item from slot
            var item = quickSlotManager.GetItem(slotIndex);
            if (item == null)
            {
                if (enableDebugLogs)
                    Debug.Log($"[QuickSlotInputHandler] Slot {slotIndex} is empty");
                return;
            }

            // Handle based on item type
            switch (item.Definition.ItemType)
            {
                case ItemType.Consumable:
                    // Start consume with progress bar (can be cancelled)
                    QuickSlotEvents.InvokeRequestConsume(item);
                    
                    if (enableDebugLogs)
                        Debug.Log($"[QuickSlotInputHandler] Started consuming {item.Definition.ItemId}");
                    break;

                case ItemType.Throwable:
                    // Instant equip to hand (like weapon switch)
                    QuickSlotEvents.InvokeRequestEquipThrowable(item);
                    
                    if (enableDebugLogs)
                        Debug.Log($"[QuickSlotInputHandler] Equipped throwable {item.Definition.ItemId}");
                    break;

                default:
                    Debug.LogWarning($"[QuickSlotInputHandler] Item type {item.Definition.ItemType} not supported for quick slots");
                    return;
            }

            // Fire event
            OnQuickSlotUsed?.Invoke(slotIndex);

            // Update cooldown
            lastUseTime = Time.time;
        }

        #endregion

        #region Inventory Events

        private void SubscribeToInventoryEvents()
        {
            InventoryEvents.OnInventoryOpened += OnInventoryOpened;
            InventoryEvents.OnInventoryClosed += OnInventoryClosed;
        }

        private void UnsubscribeFromInventoryEvents()
        {
            InventoryEvents.OnInventoryOpened -= OnInventoryOpened;
            InventoryEvents.OnInventoryClosed -= OnInventoryClosed;
        }

        private void OnInventoryOpened()
        {
            isInventoryOpen = true;
        }

        private void OnInventoryClosed()
        {
            isInventoryOpen = false;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Assign QuickSlotManager reference
        /// </summary>
        public void SetQuickSlotManager(QuickSlotManager manager)
        {
            quickSlotManager = manager;
        }

        #endregion
    }
}