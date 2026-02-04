using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Domain.Equipment;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NightHunt.Inventory.Domain.Equipment
{
    /// <summary>
    /// Manages equipment slots (Helmet, Armor, Backpack).
    /// Handles equip/unequip operations with stat modifier integration.
    /// </summary>
    public class EquipmentManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private EquipmentSlotsConfig config;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        
        private Dictionary<EquipmentSlotType, ItemInstance> equippedItems;
        
        #region Events
        
        public event Action<ItemInstance, EquipmentSlotType> OnItemEquipped;
        public event Action<ItemInstance, EquipmentSlotType> OnItemUnequipped;
        
        #endregion
        
        #region Lifecycle
        
        void Awake()
        {
            equippedItems = new Dictionary<EquipmentSlotType, ItemInstance>();
            
            if (enableDebugLogs)
                Debug.Log("[EquipmentManager] Initialized");
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Tries to equip an item to a specific equipment slot.
        /// Handles swapping if slot is occupied.
        /// </summary>
        public EquipResult TryEquip(ItemInstance item, EquipmentSlotType targetSlot)
        {
            if (item == null)
            {
                return EquipResult.Fail("Item is null");
            }
            
            // Validate item is equipment type
            if (item.Definition.ItemType != ItemType.Armor)
            {
                return EquipResult.Fail("Item is not equipment");
            }
            
            // Validate slot type matches
            if (item.Definition.EquipmentSlot != targetSlot)
            {
                return EquipResult.Fail($"Item cannot be equipped in {targetSlot} slot (requires {item.Definition.EquipmentSlot})");
            }
            
            // Check if slot occupied
            if (equippedItems.ContainsKey(targetSlot))
            {
                // Swap: unequip old, equip new
                var oldItem = equippedItems[targetSlot];
                equippedItems[targetSlot] = item;
                
                // Fire events for stat changes
                RemoveStatModifiers(oldItem);
                ApplyStatModifiers(item);
                
                OnItemUnequipped?.Invoke(oldItem, targetSlot);
                OnItemEquipped?.Invoke(item, targetSlot);
                
                EquipmentEvents.InvokeEquipmentUnequipped(oldItem, targetSlot);
                EquipmentEvents.InvokeEquipmentEquipped(item, targetSlot);
                
                Log($"Swapped {oldItem.Definition.ItemId} with {item.Definition.ItemId} in {targetSlot}");
                
                return EquipResult.Swapped(oldItem);
            }
            else
            {
                // Direct equip
                equippedItems[targetSlot] = item;
                
                ApplyStatModifiers(item);
                
                OnItemEquipped?.Invoke(item, targetSlot);
                EquipmentEvents.InvokeEquipmentEquipped(item, targetSlot);
                
                Log($"Equipped {item.Definition.ItemId} in {targetSlot}");
                
                return EquipResult.Success();
            }
        }
        
        /// <summary>
        /// Tries to unequip an item from a slot.
        /// </summary>
        public bool TryUnequip(EquipmentSlotType slotType, out ItemInstance unequippedItem)
        {
            if (equippedItems.TryGetValue(slotType, out unequippedItem))
            {
                equippedItems.Remove(slotType);
                
                RemoveStatModifiers(unequippedItem);
                
                OnItemUnequipped?.Invoke(unequippedItem, slotType);
                EquipmentEvents.InvokeEquipmentUnequipped(unequippedItem, slotType);
                
                Log($"Unequipped {unequippedItem.Definition.ItemId} from {slotType}");
                
                return true;
            }
            
            LogWarning($"Cannot unequip - no item in {slotType} slot");
            return false;
        }
        
        /// <summary>
        /// Gets the currently equipped item in a slot.
        /// </summary>
        public ItemInstance GetEquipped(EquipmentSlotType slotType)
        {
            return equippedItems.TryGetValue(slotType, out var item) ? item : null;
        }
        
        /// <summary>
        /// Gets all currently equipped items.
        /// </summary>
        public List<ItemInstance> GetAllEquipped()
        {
            return equippedItems.Values.ToList();
        }
        
        /// <summary>
        /// Checks if a slot is occupied.
        /// </summary>
        public bool IsSlotOccupied(EquipmentSlotType slotType)
        {
            return equippedItems.ContainsKey(slotType);
        }
        
        /// <summary>
        /// Unequips all equipment.
        /// </summary>
        public List<ItemInstance> UnequipAll()
        {
            var items = new List<ItemInstance>();
            
            foreach (var kvp in equippedItems.ToList())
            {
                if (TryUnequip(kvp.Key, out var item))
                {
                    items.Add(item);
                }
            }
            
            Log("Unequipped all equipment");
            return items;
        }
        
        #endregion
        
        #region Stat Modifiers
        
        private void ApplyStatModifiers(ItemInstance item)
        {
            if (item.Definition.CharacterStatModifiers == null) return;
            
            string sourceId = $"Equip:{item.InstanceId}";
            
            foreach (var modifier in item.Definition.CharacterStatModifiers)
            {
                CharacterStatsEvents.InvokeAddModifier(
                    modifier.CharacterStat,
                    modifier.Type,
                    modifier.Value,
                    sourceId
                );
            }
            
            CharacterStatsEvents.InvokeStatsChanged();
            Log($"Applied stat modifiers for {item.Definition.ItemId}");
        }
        
        private void RemoveStatModifiers(ItemInstance item)
        {
            string sourceId = $"Equip:{item.InstanceId}";
            CharacterStatsEvents.InvokeRemoveModifier(sourceId);
            CharacterStatsEvents.InvokeStatsChanged();
            
            Log($"Removed stat modifiers for {item.Definition.ItemId}");
        }
        
        #endregion
        
        #region Helper Methods
        
        private void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[EquipmentManager] {message}");
        }
        
        private void LogWarning(string message)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[EquipmentManager] {message}");
        }
        
        #endregion
    }
  
}