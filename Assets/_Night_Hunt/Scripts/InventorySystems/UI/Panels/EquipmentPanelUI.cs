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
        [Header("Equipment Slots")]
        [SerializeField] private EquipmentSlotUI helmetSlot;
        [SerializeField] private EquipmentSlotUI armorSlot;
        [SerializeField] private EquipmentSlotUI backpackSlot;
        
        [Header("References")]
        [SerializeField] private EquipmentManager equipmentManager;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        private Dictionary<EquipmentSlotType, EquipmentSlotUI> slotMap;
        
        #region Lifecycle
        
        void Awake()
        {
            // Map slots
            slotMap = new Dictionary<EquipmentSlotType, EquipmentSlotUI>
            {
                { EquipmentSlotType.Helmet, helmetSlot },
                { EquipmentSlotType.Armor, armorSlot },
                { EquipmentSlotType.Backpack, backpackSlot }
            };
            
            // Initialize slots
            foreach (var kvp in slotMap)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.Initialize(kvp.Key, this);
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