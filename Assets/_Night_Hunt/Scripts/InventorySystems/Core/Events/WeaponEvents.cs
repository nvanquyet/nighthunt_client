using System;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;

namespace NightHunt.Inventory.Core.Events
{
    /// <summary>
    /// Global events for WeaponSystem.
    /// </summary>
    public static class WeaponEvents
    {
        // === Equip/Unequip ===
        /// <summary>Fired when weapon is equipped. Args: (weapon, slotType)</summary>
        public static event Action<ItemInstance, WeaponSlotType> OnWeaponEquipped;
        
        /// <summary>Fired when weapon is unequipped. Args: (weapon, slotType)</summary>
        public static event Action<ItemInstance, WeaponSlotType> OnWeaponUnequipped;
        
        /// <summary>Fired when weapon is swapped. Args: (oldWeapon, newWeapon, slotType)</summary>
        public static event Action<ItemInstance, ItemInstance, WeaponSlotType> OnWeaponSwapped;
        
        // === Weapon Switching ===
        /// <summary>Fired when active weapon changes. Args: (previousWeapon, newWeapon, newSlotType)</summary>
        public static event Action<ItemInstance, ItemInstance, WeaponSlotType> OnActiveWeaponChanged;
        
        // === Ammo ===
        /// <summary>Fired when weapon ammo changes. Args: (weapon, currentAmmo, maxAmmo)</summary>
        public static event Action<ItemInstance, int, int> OnAmmoChanged;
        
        /// <summary>Fired when weapon is reloaded. Args: (weapon, ammoAdded)</summary>
        public static event Action<ItemInstance, int> OnWeaponReloaded;
        
        // === Durability ===
        /// <summary>Fired when weapon durability changes. Args: (weapon, slotType, durability)</summary>
        public static event Action<ItemInstance, WeaponSlotType, float> OnWeaponDurabilityChanged;
        
        // === Validation ===
        /// <summary>Fired when weapon operation fails. Args: (result, slotType, errorMessage)</summary>
        public static event Action<OperationResult, WeaponSlotType, string> OnWeaponOperationFailed;
        
        // === Invoke Methods ===
        public static void InvokeWeaponEquipped(ItemInstance weapon, WeaponSlotType slotType) 
            => OnWeaponEquipped?.Invoke(weapon, slotType);
        
        public static void InvokeWeaponUnequipped(ItemInstance weapon, WeaponSlotType slotType) 
            => OnWeaponUnequipped?.Invoke(weapon, slotType);
        
        public static void InvokeWeaponSwapped(ItemInstance oldWeapon, ItemInstance newWeapon, WeaponSlotType slotType) 
            => OnWeaponSwapped?.Invoke(oldWeapon, newWeapon, slotType);
        
        public static void InvokeActiveWeaponChanged(ItemInstance previous, ItemInstance newWeapon, WeaponSlotType slotType) 
            => OnActiveWeaponChanged?.Invoke(previous, newWeapon, slotType);
        
        public static void InvokeAmmoChanged(ItemInstance weapon, int current, int max) 
            => OnAmmoChanged?.Invoke(weapon, current, max);
        
        public static void InvokeWeaponReloaded(ItemInstance weapon, int ammoAdded) 
            => OnWeaponReloaded?.Invoke(weapon, ammoAdded);
        
        public static void InvokeWeaponDurabilityChanged(ItemInstance weapon, WeaponSlotType slotType, float durability) 
            => OnWeaponDurabilityChanged?.Invoke(weapon, slotType, durability);
        
        public static void InvokeWeaponOperationFailed(OperationResult result, WeaponSlotType slotType, string message) 
            => OnWeaponOperationFailed?.Invoke(result, slotType, message);
    }
}