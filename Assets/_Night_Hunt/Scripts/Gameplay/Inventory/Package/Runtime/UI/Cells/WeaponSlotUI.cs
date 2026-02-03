using UnityEngine;
using NightHunt.Inventory.Core;
using NightHunt.Inventory.UI;

namespace NightHunt.Inventory.UI
{
    /// <summary>
    /// Weapon slot UI (Primary, Secondary).
    /// Extends InventoryCellUI with weapon-specific behavior.
    /// </summary>
    public class WeaponSlotUI : InventoryCellUI
    {
        [SerializeField] private WeaponSlotType slotType;
        
        public WeaponSlotType SlotType => slotType;
        
        public void InitializeWeapon(ItemInstance item, WeaponSlotType weaponSlot, int index)
        {
            Initialize(item, SlotLocationType.Weapon, index);
            slotType = weaponSlot;
        }
    }
}
