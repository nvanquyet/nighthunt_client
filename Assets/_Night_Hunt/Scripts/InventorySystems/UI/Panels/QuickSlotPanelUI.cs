using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Utilities;
using NightHunt.Inventory.Domain.QuickSlot;
using System.Collections.Generic;

namespace NightHunt.Inventory.UI.Panels
{
    /// <summary>
    /// Quick slot panel UI for Inventory Panel.
    /// Handles drag & drop only (no use functionality).
    /// Use functionality is handled by QuickSlotHUDController.
    /// </summary>
    public class QuickSlotPanelUI : MonoBehaviour
    {
        [Header("Spawn Configuration")]
        [SerializeField] private Transform slotContainer;
        [SerializeField] private GameObject slotPrefab;
        [SerializeField] private QuickSlotConfig config;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        // Runtime-only fields (not shown in Inspector)
        private QuickSlotManager quickSlotManager;
        private Dictionary<int, QuickSlotSlotUI> slotMap;
        
        #region Lifecycle
        
        void Awake()
        {
            slotMap = new Dictionary<int, QuickSlotSlotUI>();
            
            if (config == null)
            {
                InventoryLogger.LogError("QuickSlotPanelUI", "QuickSlotConfig is required! Please assign config in Inspector.");
                return;
            }
            
            if (slotContainer == null)
            {
                InventoryLogger.LogError("QuickSlotPanelUI", "SlotContainer is required! Please assign container Transform in Inspector.");
                return;
            }
            
            if (slotPrefab == null)
            {
                InventoryLogger.LogError("QuickSlotPanelUI", "SlotPrefab is required! Please assign slot prefab in Inspector.");
                return;
            }
            
            InitializeSlots();
        }
        
        /// <summary>
        /// Sets the QuickSlotManager reference (from local player).
        /// Called by parent controller or player setup.
        /// </summary>
        public void SetQuickSlotManager(QuickSlotManager manager)
        {
            quickSlotManager = manager;
            
            InventoryLogger.Log("QuickSlotPanelUI", "QuickSlotManager injected", enableDebugLogs);
            
            // Refresh slots after manager is set
            if (slotMap.Count > 0)
            {
                RefreshAllSlots();
            }
        }
        
        void OnEnable()
        {
            // Subscribe to quick slot changes
            QuickSlotEvents.OnQuickSlotChanged += HandleQuickSlotChanged;
        }
        
        void OnDisable()
        {
            QuickSlotEvents.OnQuickSlotChanged -= HandleQuickSlotChanged;
        }
        
        void Start()
        {
            RefreshAllSlots();
        }
        
        #endregion
        
        #region Initialization
        
        private void InitializeSlots()
        {
            if (config == null || slotContainer == null || slotPrefab == null)
                return;
            
            // Spawn slots based on config
            for (int i = 0; i < config.SlotCount; i++)
            {
                var slotObj = Instantiate(slotPrefab, slotContainer);
                var slotUI = slotObj.GetComponent<QuickSlotSlotUI>();
                
                if (slotUI != null)
                {
                    slotUI.Initialize(i, this);
                    slotMap[i] = slotUI;
                    
                    InventoryLogger.Log("QuickSlotPanelUI", $"Spawned quick slot {i}", enableDebugLogs);
                }
                else
                {
                    InventoryLogger.LogError("QuickSlotPanelUI", "Slot prefab doesn't have QuickSlotSlotUI component!");
                    Destroy(slotObj);
                }
            }
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Called when player drops item on quick slot.
        /// Validates item type and assigns to slot.
        /// </summary>
        public void OnItemDroppedOnSlot(ItemInstance item, int slotIndex)
        {
            if (quickSlotManager == null)
            {
                InventoryLogger.LogError("QuickSlotPanelUI", "QuickSlotManager not assigned!");
                return;
            }
            
            // Validate item type
            if (item.Definition.ItemType != ItemType.Consumable &&
                item.Definition.ItemType != ItemType.Throwable)
            {
                UIEvents.InvokeShowError("Only consumables and throwables can be assigned to quick slots");
                return;
            }
            
            // Get old item for swap
            var oldItem = quickSlotManager.GetItem(slotIndex);
            
            // Try add item to slot
            bool success = quickSlotManager.TryAddItem(item, slotIndex);
            
            if (success)
            {
                // Remove from inventory
                InventoryEvents.InvokeRequestRemoveItem(item.InstanceId);
                
                // If swapped, add old item back to inventory
                if (oldItem != null)
                {
                    InventoryEvents.InvokeRequestAddItem(oldItem);
                }
                
                InventoryLogger.Log("QuickSlotPanelUI", $"Assigned {item.Definition.ItemId} to slot {slotIndex}", enableDebugLogs);
            }
            else
            {
                UIEvents.InvokeShowError("Cannot assign item to quick slot");
            }
        }
        
        /// <summary>
        /// Refreshes all quick slot UIs from QuickSlotManager.
        /// </summary>
        public void RefreshAllSlots()
        {
            if (quickSlotManager == null) return;
            
            foreach (var kvp in slotMap)
            {
                var item = quickSlotManager.GetItem(kvp.Key);
                kvp.Value.SetItem(item);
            }
        }
        
        #endregion
        
        #region Event Handlers
        
        private void HandleQuickSlotChanged(int slotIndex, ItemInstance item)
        {
            if (slotMap.TryGetValue(slotIndex, out var slot))
            {
                slot.SetItem(item);
            }
            
            InventoryLogger.Log("QuickSlotPanelUI", $"UI updated - slot {slotIndex} changed", enableDebugLogs);
        }
        
        #endregion
    }
}
