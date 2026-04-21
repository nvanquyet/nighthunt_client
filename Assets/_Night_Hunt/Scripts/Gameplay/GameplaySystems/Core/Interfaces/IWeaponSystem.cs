using System;
using System.Collections.Generic;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.Core.Interfaces
{
    /// <summary>
    /// Contract for the server-authoritative weapon management system.
    ///
    /// RESPONSIBILITIES:
    ///   - Manages weapon holster slots (Primary, Secondary, Melee).
    ///   - Handles weapon selection, holstering, firing, and reload.
    ///   - Manages ammo state per weapon instance.
    ///   - Supports dropping a weapon directly from its holster slot.
    ///
    /// NETWORK ARCHITECTURE:
    ///   - Server-authoritative — all mutations happen on the server.
    ///   - Clients receive updates via SyncDictionary (slot→instanceID) and SyncVar (activeSlot).
    ///   - Owning client sends requests via [ServerRpc(RequireOwnership = true)].
    /// </summary>
    public interface IWeaponSystem
    {
        #region Getters

        /// <summary>Returns the weapon instance in the given slot, or null if empty.</summary>
        ItemInstance GetWeapon(WeaponSlotType slotType);

        /// <summary>Returns all holstered weapons as a slot → instance dictionary.</summary>
        Dictionary<WeaponSlotType, ItemInstance> GetAllWeapons();

        /// <summary>Returns the currently drawn weapon slot, or null when holstered.</summary>
        WeaponSlotType? GetActiveWeaponSlot();

        /// <summary>Returns the currently drawn weapon instance, or null when holstered.</summary>
        ItemInstance GetActiveWeapon();

        /// <summary>Returns true when the weapon slot contains an item.</summary>
        bool IsSlotOccupied(WeaponSlotType slotType);

        /// <summary>
        /// Returns true when the item with <paramref name="itemDefinitionID"/> can be
        /// holstered into <paramref name="slotType"/>.
        /// </summary>
        bool CanEquipInSlot(string itemDefinitionID, WeaponSlotType slotType);

        #endregion

        #region Equip / Unequip / Drop

        /// <summary>
        /// Equip a weapon from inventory, auto-selecting the first available slot.
        /// Owning client routes to the server automatically.
        /// </summary>
        void EquipWeapon(string instanceID);

        /// <summary>
        /// Equip a weapon from inventory into a specific slot.
        /// If that slot is occupied the existing weapon is unequipped first.
        /// Owning client routes to the server automatically.
        /// </summary>
        void EquipWeaponToSlot(string instanceID, WeaponSlotType targetSlot);

        /// <summary>
        /// Unequip the weapon in <paramref name="slotType"/> and return it to inventory.
        /// Attachment handling follows InventoryConfig.
        /// Owning client routes to the server automatically.
        /// </summary>
        void UnequipWeapon(WeaponSlotType slotType);

        /// <summary>
        /// Swap weapons between two holster slots.
        /// Server-only in the current design.
        /// </summary>
        void SwapWeapons(WeaponSlotType slot1, WeaponSlotType slot2);

        /// <summary>
        /// Holster the weapon, detach all its attachments (per config), then drop it to the world.
        /// Owning client routes to the server automatically.
        /// </summary>
        void DropWeapon(WeaponSlotType slotType);

        #endregion

        #region Selection

        /// <summary>
        /// Draw the weapon in the given slot.
        /// If the slot is already active, holster the weapon instead.
        /// Owning client routes to the server automatically.
        /// </summary>
        void SelectWeapon(WeaponSlotType slotType);

        /// <summary>
        /// Holster the currently drawn weapon.
        /// Owning client routes to the server automatically.
        /// </summary>
        void HolsterWeapon();

        #endregion

        #region Ammo / Reload

        /// <summary>Returns the current magazine ammo for the weapon in the given slot.</summary>
        int GetCurrentMagazine(WeaponSlotType slotType);

        /// <summary>Returns the total reserve ammo for the weapon in the given slot.</summary>
        float GetTotalAmmo(WeaponSlotType slotType);

        /// <summary>Request a reload for the weapon in the given slot.</summary>
        void Reload(WeaponSlotType slotType);

        /// <summary>Returns true when the weapon in <paramref name="slotType"/> can be reloaded.</summary>
        bool CanReload(WeaponSlotType slotType);

        #endregion

        #region Combat (Fire / Reload / FireMode)

        /// <summary>Begin continuous firing (Auto mode) or single shot (Single mode).</summary>
        void StartFire();

        /// <summary>Release the fire input — stops any running auto-fire coroutine.</summary>
        void StopFire();

        /// <summary>Request a reload for the currently active weapon slot.</summary>
        void RequestReload();

        /// <summary>Override the fire mode for the active weapon. Setting is saved to PlayerPrefs.</summary>
        void SetFireMode(FireMode mode);

        /// <summary>Returns the current fire mode of the active weapon.</summary>
        FireMode GetCurrentFireMode();

        #endregion

        #region Aim / Model Wiring

        /// <summary>Supply the current world-space aim direction so OnShotFired carries it.</summary>
        void SetAimDirection(Vector3 worldDirection);

        /// <summary>
        /// Returns the current world-space aim direction stored in WeaponSystem.
        /// Used by VFX controllers (aim trail, muzzle preview) outside of shot events.
        /// </summary>
        Vector3 GetAimDirection();

        /// <summary>
        /// Set the world-space Transform used as the raycast / projectile origin (muzzle tip).
        /// Called by WeaponModelController after each weapon model swap.
        /// </summary>
        void SetFireOrigin(Transform muzzlePoint);

        /// <summary>
        /// Set the WeaponBase component from the currently spawned weapon model prefab.
        /// Called by WeaponModelController after each weapon model swap.
        /// Pass null when the weapon is holstered or the model is destroyed.
        /// </summary>
        void SetCurrentWeaponBase(NightHunt.Gameplay.Character.Combat.Weapons.WeaponBase weaponBase);

        #endregion

        #region Events

        /// <summary>Fired when a weapon is placed into a holster slot. Parameters: (slot, weapon).</summary>
        event Action<WeaponSlotType, ItemInstance> OnWeaponEquipped;

        /// <summary>Fired when a weapon is removed from a holster slot. Parameters: (slot, weapon).</summary>
        event Action<WeaponSlotType, ItemInstance> OnWeaponUnequipped;

        /// <summary>Fired when the active (drawn) weapon slot changes. Parameters: (oldSlot, newSlot) — null = holstered.</summary>
        event Action<WeaponSlotType?, WeaponSlotType?> OnActiveWeaponChanged;

        /// <summary>Fired when a weapon is reloaded. Parameters: (slot, newMagazineAmmo).</summary>
        event Action<WeaponSlotType, int> OnWeaponReloaded;

        /// <summary>
        /// Fired each time the ammo state changes (after a shot or after reload).
        /// Parameters: (currentMagazine, totalAmmoLeft, magazineCapacity).
        /// </summary>
        event Action<int, int, int> OnAmmoChanged;

        /// <summary>Fired when a reload starts (true) or ends (false). Used by HUD reload ring.</summary>
        event Action<bool> OnReloadStateChanged;

        /// <summary>Fired when both magazine and reserve ammo reach zero.</summary>
        event Action<WeaponSlotType> OnWeaponDepleted;

        /// <summary>
        /// Fired immediately after each shot (ammo deducted, hit confirmed).
        /// Parameters: (slot, aimDirection).
        /// </summary>
        event Action<WeaponSlotType, Vector3> OnShotFired;

        /// <summary>
        /// Fired after a hitscan shot with the muzzle origin and the ray endpoint.
        /// Parameters: (slot, muzzleOrigin, rayEndpoint).
        /// </summary>
        event Action<WeaponSlotType, Vector3, Vector3> OnHitscanResult;

        #endregion
    }
}
