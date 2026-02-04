using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Domain.Equipment;
using System.Collections.Generic;

namespace NightHunt.Inventory.UI.Panels
{
    /// <summary>
    /// Equipment panel UI with helmet, armor, backpack slots.
    /// Fully connected to EquipmentManager and network sync.
    /// </summary>
    public class EquipmentPanelUI : MonoBehaviour
    {
        [Header("Spawn Configuration")]
        [SerializeField] private Transform slotContainer;
        [SerializeField] private GameObject slotPrefab;
        [SerializeField] private EquipmentSlotsConfig config;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        // Runtime-only fields (not shown in Inspector)
        private EquipmentManager equipmentManager;
        private Dictionary<EquipmentSlotType, EquipmentSlotUI> slotMap;
        
        #region Lifecycle
        
        void Awake()
        {
            // Initialize slot map
            slotMap = new Dictionary<EquipmentSlotType, EquipmentSlotUI>();
            
            // Spawn slots from config (required)
            if (config == null)
            {
                Debug.LogError("[EquipmentPanelUI] EquipmentSlotsConfig is required! Please assign config in Inspector.");
                return;
            }
            
            if (slotContainer == null)
            {
                Debug.LogError("[EquipmentPanelUI] SlotContainer is required! Please assign container Transform in Inspector.");
                return;
            }
            
            if (slotPrefab == null)
            {
                Debug.LogError("[EquipmentPanelUI] SlotPrefab is required! Please assign slot prefab in Inspector.");
                return;
            }
            
            SpawnSlotsFromConfig();
        }
        
        /// <summary>
        /// Sets the EquipmentManager reference (from local player).
        /// Called by parent controller or player setup.
        /// </summary>
        public void SetEquipmentManager(EquipmentManager manager)
        {
            equipmentManager = manager;
            
            if (enableDebugLogs)
                Debug.Log("[EquipmentPanelUI] EquipmentManager injected");
            
            // Refresh slots after manager is set
            if (slotMap.Count > 0)
            {
                RefreshAllSlots();
            }
        }
        
        private void SpawnSlotsFromConfig()
        {
            if (config.Slots == null || config.Slots.Length == 0)
            {
                Debug.LogError("[EquipmentPanelUI] Config has no slots defined! Please configure EquipmentSlotsConfig.");
                return;
            }
            
            // Spawn slots from config
            foreach (var slotData in config.Slots)
            {
                var slotObj = Instantiate(slotPrefab, slotContainer);
                var slotUI = slotObj.GetComponent<EquipmentSlotUI>();
                
                if (slotUI != null)
                {
                    slotUI.Initialize(slotData.SlotType, this);
                    slotMap[slotData.SlotType] = slotUI;
                    
                    if (enableDebugLogs)
                        Debug.Log($"[EquipmentPanelUI] Spawned {slotData.SlotType} slot from config");
                }
                else
                {
                    Debug.LogError($"[EquipmentPanelUI] Slot prefab doesn't have EquipmentSlotUI component!");
                    Destroy(slotObj);
                }
            }
        }
        
        void OnEnable()
        {
            // Subscribe to equipment events
            EquipmentEvents.OnEquipmentEquipped += HandleEquipmentEquipped;
            EquipmentEvents.OnEquipmentUnequipped += HandleEquipmentUnequipped;
        }
        
        void OnDisable()
        {
            EquipmentEvents.OnEquipmentEquipped -= HandleEquipmentEquipped;
            EquipmentEvents.OnEquipmentUnequipped -= HandleEquipmentUnequipped;
        }
        
        void Start()
        {
            RefreshAllSlots();
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Called when player drops item on equipment slot.
        /// Validates and equips item, handles server sync.
        /// </summary>
        public void OnItemDroppedOnSlot(ItemInstance item, EquipmentSlotType targetSlot)
        {
            if (equipmentManager == null)
            {
                Debug.LogError("[EquipmentPanelUI] EquipmentManager not assigned!");
                return;
            }
            
            // Validate item type
            if (item.Definition.ItemType != ItemType.Armor)
            {
                UIEvents.InvokeShowError("Only armor can be equipped here");
                return;
            }
            
            // Validate slot compatibility
            if (item.Definition.EquipmentSlot != targetSlot)
            {
                UIEvents.InvokeShowError($"This item cannot be equipped in {targetSlot} slot");
                return;
            }
            
            // Try equip (handles swap internally)
            var result = equipmentManager.TryEquip(item, targetSlot);
            
            if (result.IsSuccess)
            {
                // If swapped, add old item back to inventory
                if (result.SwappedItem != null)
                {
                    InventoryEvents.InvokeRequestAddItem(result.SwappedItem);
                }
                
                // Remove from inventory
                InventoryEvents.InvokeRequestRemoveItem(item.InstanceId);
                
                // Server sync happens automatically via EquipmentManager events
                if (enableDebugLogs)
                    Debug.Log($"[EquipmentPanelUI] Equipped {item.Definition.ItemId} in {targetSlot}");
            }
            else
            {
                UIEvents.InvokeShowError(result.FailReason);
            }
        }
        
        /// <summary>
        /// Called when player clicks unequip button.
        /// </summary>
        public void OnUnequipRequested(EquipmentSlotType slotType)
        {
            if (equipmentManager == null) return;
            
            if (equipmentManager.TryUnequip(slotType, out ItemInstance unequippedItem))
            {
                // Add back to inventory
                InventoryEvents.InvokeRequestAddItem(unequippedItem);
                
                if (enableDebugLogs)
                    Debug.Log($"[EquipmentPanelUI] Unequipped {unequippedItem.Definition.ItemId}");
            }
        }
        
        /// <summary>
        /// Refreshes all equipment slots from EquipmentManager.
        /// </summary>
        public void RefreshAllSlots()
        {
            if (equipmentManager == null) return;
            
            foreach (var kvp in slotMap)
            {
                var equipped = equipmentManager.GetEquipped(kvp.Key);
                kvp.Value.SetItem(equipped);
            }
        }
        
        #endregion
        
        #region Event Handlers
        
        private void HandleEquipmentEquipped(ItemInstance item, EquipmentSlotType slotType)
        {
            if (slotMap.TryGetValue(slotType, out var slot))
            {
                slot.SetItem(item);
            }
            
            if (enableDebugLogs)
                Debug.Log($"[EquipmentPanelUI] UI updated - equipped {item.Definition.ItemId}");
        }
        
        private void HandleEquipmentUnequipped(ItemInstance item, EquipmentSlotType slotType)
        {
            if (slotMap.TryGetValue(slotType, out var slot))
            {
                slot.SetItem(null);
            }
            
            if (enableDebugLogs)
                Debug.Log($"[EquipmentPanelUI] UI updated - unequipped {item.Definition.ItemId}");
        }
        
        #endregion
    }
}