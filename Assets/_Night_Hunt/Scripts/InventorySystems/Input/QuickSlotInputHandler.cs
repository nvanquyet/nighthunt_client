using UnityEngine;
using UnityEngine.InputSystem;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Networking;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Utilities;
using NightHunt.Inventory.Core.Data;
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

        [Header("References")]
        [SerializeField] private QuickSlotManager quickSlotManager;
        [SerializeField] private QuickSlotCooldownManager cooldownManager;

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
        
        // Fast/slow press tracking
        private int selectedSlotIndex = -1;
        private float lastKeyPressTime = 0f;
        private const float FastPressThreshold = 0.3f;

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
                InventoryLogger.LogError("QuickSlotInputHandler", "InputLayerManager.Instance is null!");
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
                InventoryLogger.LogError("QuickSlotInputHandler", "'Inventory' action map not found!");
            }
        }

        public void Initialize(NetworkPlayer player)
        {
            networkPlayer = player;

            // Get QuickSlotManager from player component (not FindObjectOfType)
            if (quickSlotManager == null && player != null)
            {
                quickSlotManager = player.GetComponent<QuickSlotManager>();
                if (quickSlotManager == null)
                {
                    InventoryLogger.LogWarning("QuickSlotInputHandler", "QuickSlotManager not found on player!", enableDebugLogs);
                }
            }
            
            // Get QuickSlotCooldownManager from player component
            if (cooldownManager == null && player != null)
            {
                cooldownManager = player.GetComponent<QuickSlotCooldownManager>();
                if (cooldownManager == null)
                {
                    InventoryLogger.LogWarning("QuickSlotInputHandler", "QuickSlotCooldownManager not found on player!", enableDebugLogs);
                }
            }
        }
        
        /// <summary>
        /// Sets the QuickSlotManager reference directly.
        /// Alternative to Initialize if manager is already known.
        /// </summary>
        public void SetQuickSlotManager(QuickSlotManager manager)
        {
            quickSlotManager = manager;
            
            InventoryLogger.Log("QuickSlotInputHandler", "QuickSlotManager injected", enableDebugLogs);
        }
        
        /// <summary>
        /// Sets the QuickSlotCooldownManager reference directly.
        /// </summary>
        public void SetCooldownManager(QuickSlotCooldownManager manager)
        {
            cooldownManager = manager;
            
            InventoryLogger.Log("QuickSlotInputHandler", "QuickSlotCooldownManager injected", enableDebugLogs);
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
                InventoryLogger.LogWarning("QuickSlotInputHandler", "Cannot enable: Not owner!", enableDebugLogs);
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

            InventoryLogger.Log("QuickSlotInputHandler", "Input enabled", enableDebugLogs);
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

            InventoryLogger.Log("QuickSlotInputHandler", "Input disabled", enableDebugLogs);
        }

        #endregion

        #region Quick Slot Usage

        private void UseQuickSlot(int slotIndex)
        {
            // Block if inventory is open
            if (isInventoryOpen)
            {
                InventoryLogger.Log("QuickSlotInputHandler", "Blocked - inventory is open", enableDebugLogs);
                return;
            }

            if (quickSlotManager == null)
            {
                InventoryLogger.LogError("QuickSlotInputHandler", "QuickSlotManager not assigned!");
                return;
            }

            // Get item from slot
            var item = quickSlotManager.GetItem(slotIndex);
            if (item == null)
            {
                // Empty slot - log "Trống"
                InventoryLogger.Log("QuickSlotInputHandler", "Trống", enableDebugLogs);
                return;
            }

            // Check cooldown
            if (cooldownManager != null && cooldownManager.IsOnCooldown(slotIndex))
            {
                InventoryLogger.Log("QuickSlotInputHandler", "Cooldown active", enableDebugLogs);
                return;
            }

            // Fast/slow press logic
            float timeSinceLastPress = Time.time - lastKeyPressTime;
            bool isFastPress = timeSinceLastPress < FastPressThreshold && selectedSlotIndex == slotIndex;
            
            if (isFastPress)
            {
                // Fast press: Select + Use immediately
                QuickSlotEvents.InvokeQuickSlotSelected(slotIndex);
                ExecuteUse(slotIndex, item);
            }
            else
            {
                // Slow press or different slot: Select only
                if (selectedSlotIndex != slotIndex)
                {
                    // Unselect previous slot
                    if (selectedSlotIndex >= 0)
                    {
                        QuickSlotEvents.InvokeQuickSlotUnselected(selectedSlotIndex);
                    }
                    
                    // Select new slot
                    selectedSlotIndex = slotIndex;
                    QuickSlotEvents.InvokeQuickSlotSelected(slotIndex);
                }
            }
            
            lastKeyPressTime = Time.time;
        }
        
        private void ExecuteUse(int slotIndex, ItemInstance item)
        {
            // Log usage
            InventoryLogger.Log("QuickSlotInputHandler", $"Sử dụng item: {item.Definition.ItemId}", enableDebugLogs);

            // Handle based on item type
            switch (item.Definition.ItemType)
            {
                case ItemType.Consumable:
                    // Start consume with progress bar (can be cancelled)
                    QuickSlotEvents.InvokeRequestConsume(item);
                    break;

                case ItemType.Throwable:
                    // Instant equip to hand (like weapon switch)
                    QuickSlotEvents.InvokeRequestEquipThrowable(item);
                    break;

                default:
                    InventoryLogger.LogWarning("QuickSlotInputHandler", $"Item type {item.Definition.ItemType} not supported for quick slots", enableDebugLogs);
                    return;
            }

            // Fire analytics event
            QuickSlotEvents.InvokeQuickSlotDoubleClicked(item, slotIndex);

            // Fire custom event
            OnQuickSlotUsed?.Invoke(slotIndex);

            // Start cooldown
            if (cooldownManager != null)
            {
                cooldownManager.StartCooldown(slotIndex);
            }
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
        
    }
}