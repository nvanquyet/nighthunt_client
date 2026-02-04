using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Domain.Weapon;
using System.Collections.Generic;

namespace NightHunt.Inventory.UI.Panels
{
    /// <summary>
    /// Weapon panel UI with Primary/Secondary slots.
    /// Shows active weapon, handles switching, displays ammo.
    /// </summary>
    public class WeaponPanelUI : MonoBehaviour
    {
        [Header("Weapon Slots")]
        [SerializeField] private WeaponSlotUI primarySlot;
        [SerializeField] private WeaponSlotUI secondarySlot;
        
        [Header("References")]
        [SerializeField] private WeaponManager weaponManager;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        private Dictionary<WeaponSlotType, WeaponSlotUI> slotMap;
        
        #region Lifecycle
        
        void Awake()
        {
            slotMap = new Dictionary<WeaponSlotType, WeaponSlotUI>
            {
                { WeaponSlotType.Primary, primarySlot },
                { WeaponSlotType.Secondary, secondarySlot }
            };
            
            foreach (var kvp in slotMap)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.Initialize(kvp.Key, this);
                }
            }
        }
        
        void OnEnable()
        {
            WeaponEvents.OnWeaponEquipped += HandleWeaponEquipped;
            WeaponEvents.OnWeaponUnequipped += HandleWeaponUnequipped;
            WeaponEvents.OnWeaponSwitched += HandleWeaponSwitched;
            WeaponEvents.OnAmmoChanged += HandleAmmoChanged;
        }
        
        void OnDisable()
        {
            WeaponEvents.OnWeaponEquipped -= HandleWeaponEquipped;
            WeaponEvents.OnWeaponUnequipped -= HandleWeaponUnequipped;
            WeaponEvents.OnWeaponSwitched -= HandleWeaponSwitched;
            WeaponEvents.OnAmmoChanged -= HandleAmmoChanged;
        }
        
        void Start()
        {
            RefreshAllSlots();
        }
        
        #endregion
        
        #region Public API
        
        public void OnItemDroppedOnSlot(ItemInstance item, WeaponSlotType targetSlot)
        {
            if (weaponManager == null)
            {
                Debug.LogError("[WeaponPanelUI] WeaponManager not assigned!");
                return;
            }
            
            // Validate item type
            if (item.Definition.ItemType != ItemType.Weapon)
            {
                UIEvents.InvokeShowError("Only weapons can be equipped here");
                return;
            }
            
            // Try equip
            var result = weaponManager.EquipWeapon(item, targetSlot);
            
            if (result.IsSuccess)
            {
                // If swapped, add old weapon back to inventory
                if (result.SwappedItem != null)
                {
                    InventoryEvents.InvokeRequestAddItem(result.SwappedItem);
                }
                
                // Remove from inventory
                InventoryEvents.InvokeRequestRemoveItem(item.InstanceId);
                
                if (enableDebugLogs)
                    Debug.Log($"[WeaponPanelUI] Equipped {item.Definition.ItemId} in {targetSlot}");
            }
            else
            {
                UIEvents.InvokeShowError(result.FailReason);
            }
        }
        
        public void OnUnequipRequested(WeaponSlotType slotType)
        {
            if (weaponManager == null) return;
            
            if (weaponManager.TryUnequipWeapon(slotType, out ItemInstance unequippedWeapon))
            {
                InventoryEvents.InvokeRequestAddItem(unequippedWeapon);
                
                if (enableDebugLogs)
                    Debug.Log($"[WeaponPanelUI] Unequipped {unequippedWeapon.Definition.ItemId}");
            }
        }
        
        public void OnSwitchRequested(WeaponSlotType slotType)
        {
            if (weaponManager == null) return;
            
            weaponManager.SwitchToSlot(slotType);
        }
        
        public void RefreshAllSlots()
        {
            if (weaponManager == null) return;
            
            foreach (var kvp in slotMap)
            {
                var weapon = weaponManager.GetWeapon(kvp.Key);
                kvp.Value.SetWeapon(weapon);
                
                // Update active state
                bool isActive = (weaponManager.GetActiveSlot() == kvp.Key);
                kvp.Value.SetActiveState(isActive);
            }
        }
        
        #endregion
        
        #region Event Handlers
        
        private void HandleWeaponEquipped(ItemInstance weapon, WeaponSlotType slotType)
        {
            if (slotMap.TryGetValue(slotType, out var slot))
            {
                slot.SetWeapon(weapon);
            }
            
            if (enableDebugLogs)
                Debug.Log($"[WeaponPanelUI] UI updated - equipped {weapon.Definition.ItemId}");
        }
        
        private void HandleWeaponUnequipped(ItemInstance weapon, WeaponSlotType slotType)
        {
            if (slotMap.TryGetValue(slotType, out var slot))
            {
                slot.SetWeapon(null);
                slot.SetActiveState(false);
            }
            
            if (enableDebugLogs)
                Debug.Log($"[WeaponPanelUI] UI updated - unequipped {weapon.Definition.ItemId}");
        }
        
        private void HandleWeaponSwitched(WeaponSlotType newActiveSlot)
        {
            // Update all slots' active state
            foreach (var kvp in slotMap)
            {
                bool isActive = (kvp.Key == newActiveSlot);
                kvp.Value.SetActiveState(isActive);
            }
            
            if (enableDebugLogs)
                Debug.Log($"[WeaponPanelUI] Active weapon changed to {newActiveSlot}");
        }
        
        private void HandleAmmoChanged(ItemInstance weapon, int newAmmo)
        {
            // Find slot with this weapon and update ammo display
            foreach (var kvp in slotMap)
            {
                if (kvp.Value.GetWeapon() == weapon)
                {
                    kvp.Value.UpdateAmmoDisplay(newAmmo);
                    break;
                }
            }
        }
        
        #endregion
    }
}