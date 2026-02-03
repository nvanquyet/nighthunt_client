using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NightHunt.Inventory.Core;
using NightHunt.Inventory.Events;
using NightHunt.Inventory.Domain;

namespace NightHunt.Inventory.Domain
{
    /// <summary>
    /// Manages weapon slots (Primary, Secondary).
    /// </summary>
    public class WeaponManager : MonoBehaviour
    {
        private Dictionary<WeaponSlotType, ItemInstance> weaponSlots;
        private WeaponSlotType activeSlot = WeaponSlotType.Primary;
        
        // Events for external weapon/combat system
        public event System.Action<ItemInstance, WeaponSlotType> OnWeaponEquipped;
        public event System.Action<ItemInstance, WeaponSlotType> OnWeaponUnequipped;
        public event System.Action<WeaponSlotType> OnWeaponSwitched;
        
        void Awake()
        {
            weaponSlots = new Dictionary<WeaponSlotType, ItemInstance>();
        }
        
        public EquipResult EquipWeapon(ItemInstance weapon, WeaponSlotType slot)
        {
            if (weapon.Definition.ItemType != ItemType.Weapon)
                return EquipResult.Fail("Item is not a weapon");
            
            // Check if slot occupied
            if (weaponSlots.ContainsKey(slot))
            {
                // Swap
                var oldWeapon = weaponSlots[slot];
                weaponSlots[slot] = weapon;
                
                OnWeaponUnequipped?.Invoke(oldWeapon, slot);
                OnWeaponEquipped?.Invoke(weapon, slot);
                
                WeaponEvents.FireWeaponUnequipped(oldWeapon, slot);
                WeaponEvents.FireWeaponEquipped(weapon, slot);
                
                return EquipResult.Swapped(oldWeapon);
            }
            else
            {
                weaponSlots[slot] = weapon;
                OnWeaponEquipped?.Invoke(weapon, slot);
                WeaponEvents.FireWeaponEquipped(weapon, slot);
                return EquipResult.Success();
            }
        }
        
        public void SwitchToSlot(WeaponSlotType slot)
        {
            if (!weaponSlots.ContainsKey(slot))
            {
                Debug.LogWarning($"No weapon in slot {slot}");
                return;
            }
            
            activeSlot = slot;
            OnWeaponSwitched?.Invoke(slot);
            WeaponEvents.FireWeaponSwitched(slot);
        }
        
        public ItemInstance GetActiveWeapon()
        {
            return weaponSlots.TryGetValue(activeSlot, out var weapon) ? weapon : null;
        }
        
        public List<ItemInstance> GetAllWeapons()
        {
            return weaponSlots.Values.ToList();
        }
    }
}
