using System;
using System.Collections.Generic;
using UnityEngine;
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
        
        #region Combat (Fire / Reload / FireMode)

        /// <summary>Begin continuous firing (Auto) or single shot (Single mode). Call from input handler or HUD.</summary>
        void StartFire();

        /// <summary>Release fire input — stops auto-fire coroutine.</summary>
        void StopFire();

        /// <summary>Request reload for the active weapon slot.</summary>
        void RequestReload();

        /// <summary>Set fire mode override for the active weapon. Saved to PlayerPrefs.</summary>
        void SetFireMode(FireMode mode);

        /// <summary>Get current fire mode of active weapon.</summary>
        FireMode GetCurrentFireMode();

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
        /// Parameters: (oldSlot, newSlot) — null = holstered
        /// </summary>
        event Action<WeaponSlotType?, WeaponSlotType?> OnActiveWeaponChanged;

        /// <summary>
        /// Event fired when weapon reloaded
        /// Parameters: (slotType, newMagazineAmmo)
        /// </summary>
        event Action<WeaponSlotType, int> OnWeaponReloaded;

        /// <summary>
        /// Fired each time ammo state changes (fire or reload complete).
        /// Parameters: (currentMagazine, totalAmmoLeft, magazineCapacity)
        /// Used by HUD ammo display.
        /// </summary>
        event Action<int, int, int> OnAmmoChanged;

        /// <summary>
        /// Fired when reload starts (true) or ends (false).
        /// Used by HUD reload cooldown ring.
        /// </summary>
        event Action<bool> OnReloadStateChanged;

        /// <summary>
        /// Fired when total + magazine ammo = 0.
        /// Hook: force switch, show pickup prompt, disable fire button.
        /// </summary>
        event Action<WeaponSlotType> OnWeaponDepleted;

        /// <summary>
        /// Fired immediately after each shot is processed (ammo deducted, hit confirmed).
        /// Parameters: (slot, aimDirection) — aimDirection is the normalized world-space direction used.
        /// VFX controllers listen to this to spawn muzzle flash / trail / projectile visuals.
        /// </summary>
        event Action<WeaponSlotType, Vector3> OnShotFired;

        /// <summary>Supply the current aim direction to WeaponSystem so OnShotFired carries it.</summary>
        void SetAimDirection(Vector3 worldDirection);

        /// <summary>
        /// Set the world-space Transform used as the raycast / projectile origin (muzzle tip).
        /// Called by WeaponModelController after each weapon model swap.
        /// </summary>
        void SetFireOrigin(Transform muzzlePoint);

        /// <summary>
        /// Fired after a hitscan shot, carrying the muzzle origin and the ray endpoint
        /// (hit surface or max-range fallback point).
        /// WeaponVFXController subscribes to drive bullet-trail start / end positions.
        /// Parameters: (slot, muzzleOrigin, rayEndpoint)
        /// </summary>
        event Action<WeaponSlotType, Vector3, Vector3> OnHitscanResult;

        #endregion
    }
}
