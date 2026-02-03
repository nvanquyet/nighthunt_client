using UnityEngine;
using NightHunt.Inventory.Core;
using NightHunt.Inventory.UI;

namespace NightHunt.Inventory.UI
{
    /// <summary>
    /// Equipment slot UI (Helmet, Armor, Backpack).
    /// Extends InventoryCellUI with equipment-specific behavior.
    /// </summary>
    public class EquipmentSlotUI : InventoryCellUI
    {
        [SerializeField] private EquipmentSlotType slotType;
        
        public EquipmentSlotType SlotType => slotType;
        
        public void InitializeEquipment(ItemInstance item, EquipmentSlotType equipmentSlot, int index)
        {
            Initialize(item, SlotLocationType.Equipment, index);
            slotType = equipmentSlot;
        }
    }
}
