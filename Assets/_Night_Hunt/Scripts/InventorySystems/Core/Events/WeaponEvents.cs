using System;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;

namespace NightHunt.Inventory.Core.Events
{
    /// <summary>
    /// Events for weapon system.
    /// </summary>
    public static class WeaponEvents
    {
        public static event Action<ItemInstance, WeaponSlotType> OnWeaponEquipped;
        public static event Action<ItemInstance, WeaponSlotType> OnWeaponUnequipped;
        public static event Action<WeaponSlotType> OnWeaponSwitched;
        public static event Action<ItemInstance> OnWeaponReloaded;
        public static event Action<ItemInstance, int> OnAmmoChanged; // weapon, newAmmo
        
        public static void InvokeWeaponEquipped(ItemInstance weapon, WeaponSlotType slot) => OnWeaponEquipped?.Invoke(weapon, slot);
        public static void InvokeWeaponUnequipped(ItemInstance weapon, WeaponSlotType slot) => OnWeaponUnequipped?.Invoke(weapon, slot);
        public static void InvokeWeaponSwitched(WeaponSlotType slot) => OnWeaponSwitched?.Invoke(slot);
        public static void InvokeWeaponReloaded(ItemInstance weapon) => OnWeaponReloaded?.Invoke(weapon);
        public static void InvokeAmmoChanged(ItemInstance weapon, int newAmmo) => OnAmmoChanged?.Invoke(weapon, newAmmo);
    }
}