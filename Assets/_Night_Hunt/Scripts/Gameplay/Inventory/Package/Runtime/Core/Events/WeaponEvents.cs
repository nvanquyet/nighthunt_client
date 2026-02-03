using System;
using NightHunt.Inventory.Core;

namespace NightHunt.Inventory.Events
{
    /// <summary>
    /// Events for weapon system.
    /// </summary>
    public static class WeaponEvents
    {
        public static event Action<ItemInstance, WeaponSlotType> OnWeaponEquipped;
        public static event Action<ItemInstance, WeaponSlotType> OnWeaponUnequipped;
        public static event Action<WeaponSlotType> OnWeaponSwitched;
        
        public static void FireWeaponEquipped(ItemInstance weapon, WeaponSlotType slot) => OnWeaponEquipped?.Invoke(weapon, slot);
        public static void FireWeaponUnequipped(ItemInstance weapon, WeaponSlotType slot) => OnWeaponUnequipped?.Invoke(weapon, slot);
        public static void FireWeaponSwitched(WeaponSlotType slot) => OnWeaponSwitched?.Invoke(slot);
    }
}
