using System;
using System.Collections.Generic;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.Core.Interfaces
{
    /// <summary>
    /// Interface for weapon management system
    /// 
    /// RESPONSIBILITIES:
    /// - Manages weapon slots (primary, secondary, melee)
    /// - Handles weapon selection and holstering
    /// - Manages weapon ammo and reloading
    /// - Implemented by WeaponSystem (NetworkBehaviour)
    /// 
    /// NETWORK ARCHITECTURE:
    /// - Server-authoritative: All operations on server
    /// - Client receives updates via SyncDictionary and SyncVar
    /// </summary>
    public interface IWeaponSystem
    {
        #region Getters
        
        /// <summary>
        /// Get weapon in specific slot
        /// Returns null if slot empty
        /// </summary>
        ItemInstance GetWeapon(WeaponSlotType slotType);
        
        /// <summary>
        /// Get all equipped weapons
        /// </summary>
        Dictionary<WeaponSlotType, ItemInstance> GetAllWeapons();
        
        /// <summary>
        /// Get currently active weapon slot
        /// Returns null if no weapon equipped/holstered
        /// </summary>
        WeaponSlotType? GetActiveWeaponSlot();
        
        /// <summary>
        /// Get currently active weapon instance
        /// Returns null if holstered
        /// </summary>
        ItemInstance GetActiveWeapon();
        
        /// <summary>
        /// Check if weapon slot is occupied
        /// </summary>
        bool IsSlotOccupied(WeaponSlotType slotType);
        
        /// <summary>
        /// Check if weapon can be equipped in slot
        /// </summary>
        bool CanEquipInSlot(string itemDefinitionID, WeaponSlotType slotType);
        
        #endregion
        
        #region Equip/Unequip
        
        /// <summary>
        /// Equip weapon from inventory
        /// Auto-finds appropriate slot or swaps
        /// Server-side only
        /// </summary>
        void EquipWeapon(string instanceID);

        /// <summary>
        /// Equip weapon from inventory into a SPECIFIC slot.
        /// If slot is already occupied the existing weapon is unequipped first.
        /// Used by drag-and-drop when the player explicitly chooses a target slot.
        /// Server-side only.
        /// </summary>
        void EquipWeaponToSlot(string instanceID, WeaponSlotType targetSlot);
        
        /// <summary>
        /// Unequip weapon from slot back to inventory
        /// Server-side only
        /// </summary>
        void UnequipWeapon(WeaponSlotType slotType);
        
        /// <summary>
        /// Swap weapons between two slots
        /// </summary>
        void SwapWeapons(WeaponSlotType slot1, WeaponSlotType slot2);
        
        #endregion
        
        #region Selection
        
        /// <summary>
        /// Select weapon slot (draw weapon)
        /// If already selected, holsters weapon
        /// </summary>
        void SelectWeapon(WeaponSlotType slotType);
        
        /// <summary>
        /// Holster current weapon (hands free)
        /// </summary>
        void HolsterWeapon();
        
        #endregion
        
        #region Ammo/Reload
        
        /// <summary>
        /// Get current magazine ammo
        /// </summary>
        int GetCurrentMagazine(WeaponSlotType slotType);
        
        /// <summary>
        /// Get total ammo remaining
        /// </summary>
        float GetTotalAmmo(WeaponSlotType slotType);
        
        /// <summary>
        /// Reload weapon
        /// </summary>
        void Reload(WeaponSlotType slotType);
        
        /// <summary>
        /// Check if can reload
        /// </summary>
        bool CanReload(WeaponSlotType slotType);
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Event fired when weapon equipped
        /// Parameters: (slotType, weapon)
        /// </summary>
        event Action<WeaponSlotType, ItemInstance> OnWeaponEquipped;
        
        /// <summary>
        /// Event fired when weapon unequipped
        /// Parameters: (slotType, weapon)
        /// </summary>
        event Action<WeaponSlotType, ItemInstance> OnWeaponUnequipped;
        
        /// <summary>
        /// Event fired when active weapon changed
        /// Parameters: (oldSlot, newSlot)
        /// null = holstered
        /// </summary>
        event Action<WeaponSlotType?, WeaponSlotType?> OnActiveWeaponChanged;
        
        /// <summary>
        /// Event fired when weapon reloaded
        /// Parameters: (slotType, newMagazineAmmo)
        /// </summary>
        event Action<WeaponSlotType, int> OnWeaponReloaded;
        
        #endregion
    }
}
