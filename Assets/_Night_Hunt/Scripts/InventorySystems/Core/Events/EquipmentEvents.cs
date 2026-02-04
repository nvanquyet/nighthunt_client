using System;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;

namespace NightHunt.Inventory.Core.Events
{
    /// <summary>
    /// Events for equipment system.
    /// </summary>
    public static class EquipmentEvents
    {
        public static event Action<ItemInstance, EquipmentSlotType> OnEquipmentEquipped;
        public static event Action<ItemInstance, EquipmentSlotType> OnEquipmentUnequipped;
        
        public static void InvokeEquipmentEquipped(ItemInstance item, EquipmentSlotType slot) => OnEquipmentEquipped?.Invoke(item, slot);
        public static void InvokeEquipmentUnequipped(ItemInstance item, EquipmentSlotType slot) => OnEquipmentUnequipped?.Invoke(item, slot);
    }
}