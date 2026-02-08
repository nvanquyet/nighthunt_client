using System;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;

namespace NightHunt.Inventory.Core.Events
{
    /// <summary>
    /// Global events for EquipmentSystem.
    /// Subscribe for UI updates and stat recalculation.
    /// </summary>
    public static class EquipmentEvents
    {
        // === Equip/Unequip ===
        /// <summary>Fired when item is equipped. Args: (item, slotType)</summary>
        public static event Action<ItemInstance, EquipmentSlotType> OnItemEquipped;
        
        /// <summary>Fired when item is unequipped. Args: (item, slotType)</summary>
        public static event Action<ItemInstance, EquipmentSlotType> OnItemUnequipped;
        
        /// <summary>Fired when equipment is swapped. Args: (oldItem, newItem, slotType)</summary>
        public static event Action<ItemInstance, ItemInstance, EquipmentSlotType> OnEquipmentSwapped;
        
        // === State Changes ===
        /// <summary>Fired when all equipment is removed</summary>
        public static event Action OnAllEquipmentCleared;
        
        /// <summary>Fired when equipment durability changes. Args: (item, slotType, durability)</summary>
        public static event Action<ItemInstance, EquipmentSlotType, float> OnEquipmentDurabilityChanged;
        
        // === Validation ===
        /// <summary>Fired when equip operation fails. Args: (result, slotType, errorMessage)</summary>
        public static event Action<OperationResult, EquipmentSlotType, string> OnEquipFailed;
        
        // === Invoke Methods ===
        public static void InvokeItemEquipped(ItemInstance item, EquipmentSlotType slotType) 
            => OnItemEquipped?.Invoke(item, slotType);
        
        public static void InvokeItemUnequipped(ItemInstance item, EquipmentSlotType slotType) 
            => OnItemUnequipped?.Invoke(item, slotType);
        
        public static void InvokeEquipmentSwapped(ItemInstance oldItem, ItemInstance newItem, EquipmentSlotType slotType) 
            => OnEquipmentSwapped?.Invoke(oldItem, newItem, slotType);
        
        public static void InvokeAllEquipmentCleared() 
            => OnAllEquipmentCleared?.Invoke();
        
        public static void InvokeEquipmentDurabilityChanged(ItemInstance item, EquipmentSlotType slotType, float durability) 
            => OnEquipmentDurabilityChanged?.Invoke(item, slotType, durability);
        
        public static void InvokeEquipFailed(OperationResult result, EquipmentSlotType slotType, string message) 
            => OnEquipFailed?.Invoke(result, slotType, message);
    }
}