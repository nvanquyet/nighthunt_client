using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Domain.Weapon;
using System;
using System.Collections.Generic;
using System.Linq;
using NightHunt.Inventory.Domain.Equipment;

namespace NightHunt.Inventory.Domain.Weapon
{
    /// <summary>
    /// Manages weapon slots (Primary, Secondary).
    /// Handles weapon switching and ammo tracking.
    /// </summary>
    public class WeaponManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private WeaponSlotConfig config;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        
        private Dictionary<WeaponSlotType, ItemInstance> weaponSlots;
        private WeaponSlotType activeSlot = WeaponSlotType.Primary;
        
        #region Events
        
        public event Action<ItemInstance, WeaponSlotType> OnWeaponEquipped;
        public event Action<ItemInstance, WeaponSlotType> OnWeaponUnequipped;
        public event Action<WeaponSlotType> OnWeaponSwitched;
        
        #endregion
        
        #region Lifecycle
        
        void Awake()
        {
            weaponSlots = new Dictionary<WeaponSlotType, ItemInstance>();
            
            if (enableDebugLogs)
                Debug.Log("[WeaponManager] Initialized");
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Equips a weapon to a specific slot.
        /// Handles swapping if slot is occupied.
        /// </summary>
        public EquipResult EquipWeapon(ItemInstance weapon, WeaponSlotType slot)
        {
            if (weapon == null)
            {
                return EquipResult.Fail("Weapon is null");
            }
            
            // Validate item is a weapon
            if (weapon.Definition.ItemType != ItemType.Weapon)
            {
                return EquipResult.Fail("Item is not a weapon");
            }
            
            // Check if slot occupied
            if (weaponSlots.ContainsKey(slot))
            {
                // Swap
                var oldWeapon = weaponSlots[slot];
                weaponSlots[slot] = weapon;
                
                // Fire events
                OnWeaponUnequipped?.Invoke(oldWeapon, slot);
                OnWeaponEquipped?.Invoke(weapon, slot);
                
                WeaponEvents.InvokeWeaponUnequipped(oldWeapon, slot);
                WeaponEvents.InvokeWeaponEquipped(weapon, slot);
                
                Log($"Swapped {oldWeapon.Definition.ItemId} with {weapon.Definition.ItemId} in {slot}");
                
                return EquipResult.Swapped(oldWeapon);
            }
            else
            {
                // Direct equip
                weaponSlots[slot] = weapon;
                
                OnWeaponEquipped?.Invoke(weapon, slot);
                WeaponEvents.InvokeWeaponEquipped(weapon, slot);
                
                Log($"Equipped {weapon.Definition.ItemId} in {slot}");
                
                return EquipResult.Success();
            }
        }
        
        /// <summary>
        /// Unequips a weapon from a slot.
        /// </summary>
        public bool TryUnequipWeapon(WeaponSlotType slot, out ItemInstance unequippedWeapon)
        {
            if (weaponSlots.TryGetValue(slot, out unequippedWeapon))
            {
                weaponSlots.Remove(slot);
                
                OnWeaponUnequipped?.Invoke(unequippedWeapon, slot);
                WeaponEvents.InvokeWeaponUnequipped(unequippedWeapon, slot);
                
                Log($"Unequipped {unequippedWeapon.Definition.ItemId} from {slot}");
                
                // If unequipped active weapon, switch to other slot
                if (slot == activeSlot)
                {
                    SwitchToOtherSlot();
                }
                
                return true;
            }
            
            LogWarning($"Cannot unequip - no weapon in {slot} slot");
            return false;
        }
        
        /// <summary>
        /// Switches to a specific weapon slot.
        /// </summary>
        public void SwitchToSlot(WeaponSlotType slot)
        {
            if (!weaponSlots.ContainsKey(slot))
            {
                LogWarning($"No weapon in slot {slot}");
                return;
            }
            
            if (activeSlot == slot)
            {
                Log($"Already using {slot} slot");
                return;
            }
            
            activeSlot = slot;
            
            OnWeaponSwitched?.Invoke(slot);
            WeaponEvents.InvokeWeaponSwitched(slot);
            
            Log($"Switched to {slot} slot: {weaponSlots[slot].Definition.ItemId}");
            
            // Combat system should subscribe to this event to handle weapon switching
        }
        
        /// <summary>
        /// Switches to the next weapon slot.
        /// </summary>
        public void SwitchToNext()
        {
            // Get all equipped weapon slots
            var equippedSlots = weaponSlots.Keys.ToList();
            
            if (equippedSlots.Count == 0)
            {
                LogWarning("No weapons equipped");
                return;
            }
            
            if (equippedSlots.Count == 1)
            {
                Log("Only one weapon equipped");
                return;
            }
            
            // Find next slot
            int currentIndex = equippedSlots.IndexOf(activeSlot);
            int nextIndex = (currentIndex + 1) % equippedSlots.Count;
            WeaponSlotType nextSlot = equippedSlots[nextIndex];
            
            SwitchToSlot(nextSlot);
        }
        
        /// <summary>
        /// Gets the currently active weapon.
        /// </summary>
        public ItemInstance GetActiveWeapon()
        {
            return weaponSlots.TryGetValue(activeSlot, out var weapon) ? weapon : null;
        }
        
        /// <summary>
        /// Gets the active weapon slot type.
        /// </summary>
        public WeaponSlotType GetActiveSlot() => activeSlot;
        
        /// <summary>
        /// Gets weapon in a specific slot.
        /// </summary>
        public ItemInstance GetWeapon(WeaponSlotType slot)
        {
            return weaponSlots.TryGetValue(slot, out var weapon) ? weapon : null;
        }
        
        /// <summary>
        /// Gets all equipped weapons.
        /// </summary>
        public List<ItemInstance> GetAllWeapons()
        {
            return weaponSlots.Values.ToList();
        }
        
        /// <summary>
        /// Checks if a slot has a weapon.
        /// </summary>
        public bool HasWeapon(WeaponSlotType slot)
        {
            return weaponSlots.ContainsKey(slot);
        }
        
        /// <summary>
        /// Unequips all weapons.
        /// </summary>
        public List<ItemInstance> UnequipAll()
        {
            var weapons = new List<ItemInstance>();
            
            foreach (var kvp in weaponSlots.ToList())
            {
                if (TryUnequipWeapon(kvp.Key, out var weapon))
                {
                    weapons.Add(weapon);
                }
            }
            
            Log("Unequipped all weapons");
            return weapons;
        }
        
        #endregion
        
        #region Ammo Management
        
        /// <summary>
        /// Updates ammo count for a weapon.
        /// </summary>
        public void UpdateAmmo(ItemInstance weapon, int newAmmo)
        {
            if (weapon == null) return;
            
            weapon.CurrentAmmo = newAmmo;
            WeaponEvents.InvokeAmmoChanged(weapon, newAmmo);
            
            Log($"Updated ammo for {weapon.Definition.ItemId}: {newAmmo}");
        }
        
        /// <summary>
        /// Gets current ammo of active weapon.
        /// </summary>
        public int GetActiveWeaponAmmo()
        {
            var activeWeapon = GetActiveWeapon();
            return activeWeapon?.CurrentAmmo ?? 0;
        }
        
        #endregion
        
        #region Helper Methods
        
        private void SwitchToOtherSlot()
        {
            // Find any other equipped weapon
            var otherSlot = weaponSlots.Keys.FirstOrDefault(s => s != activeSlot);
            
            if (otherSlot != default(WeaponSlotType))
            {
                SwitchToSlot(otherSlot);
            }
            else
            {
                Log("No other weapons equipped");
            }
        }
        
        private void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[WeaponManager] {message}");
        }
        
        private void LogWarning(string message)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[WeaponManager] {message}");
        }
        
        #endregion
    }
}