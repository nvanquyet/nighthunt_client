using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Network;

namespace NightHunt.Inventory.Core.Behaviors
{
    /// <summary>
    /// Service that handles double-click behavior for items based on their type and context.
    /// CONTEXT:
    /// - Inventory UI: Double-click to equip/use
    /// - Equipment/Weapon slots: Double-click to unequip
    /// - QuickSlot (in Inventory UI): Double-click to remove from quickslot
    /// - QuickSlot (in HUD): Double-click to use
    /// </summary>
    public class ItemDoubleClickHandler
    {
        private readonly PlayerInventoryNetwork inventory;
        
        public ItemDoubleClickHandler(PlayerInventoryNetwork inventory)
        {
            this.inventory = inventory;
        }
        
        // ===== INVENTORY UI CONTEXT =====
        
        /// <summary>
        /// Handle double-click on item in main inventory list.
        /// Behavior: Equip or Use based on item type.
        /// </summary>
        public void HandleInventoryDoubleClick(ItemInstance item)
        {
            if (item == null || item.Definition == null)
            {
                UnityEngine.Debug.LogWarning("[ItemDoubleClickHandler] Item or Definition is null");
                return;
            }
            
            var behavior = GetDoubleClickBehavior(item);
            
            switch (behavior)
            {
                case DoubleClickBehavior.Use:
                    inventory.TryUseItem(item.InstanceId);
                    break;
                
                case DoubleClickBehavior.QuickEquip:
                    HandleQuickEquip(item);
                    break;
                
                case DoubleClickBehavior.None:
                    UnityEngine.Debug.Log($"[ItemDoubleClickHandler] No double-click behavior for {item.Definition.DisplayName}");
                    break;
            }
        }
        
        /// <summary>
        /// Handle double-click on equipped item in Equipment slot.
        /// Behavior: Unequip back to inventory.
        /// </summary>
        public void HandleEquippedItemDoubleClick(ItemInstance item, EquipmentSlotType slotType)
        {
            if (item == null)
            {
                UnityEngine.Debug.LogWarning("[ItemDoubleClickHandler] Item is null");
                return;
            }
            
            inventory.TryUnequipFromSlot(slotType);
        }
        
        /// <summary>
        /// Handle double-click on equipped weapon in Weapon slot.
        /// Behavior: Unequip back to inventory.
        /// </summary>
        public void HandleEquippedWeaponDoubleClick(ItemInstance weapon, int weaponSlotIndex)
        {
            if (weapon == null)
            {
                UnityEngine.Debug.LogWarning("[ItemDoubleClickHandler] Weapon is null");
                return;
            }
            
            inventory.TryUnequipWeapon(weaponSlotIndex);
        }
        
        /// <summary>
        /// Handle double-click on item in QuickSlot (from Inventory UI).
        /// Behavior: Remove from quickslot back to inventory.
        /// </summary>
        public void HandleInventoryQuickSlotDoubleClick(ItemInstance item, int quickSlotIndex)
        {
            if (item == null)
            {
                UnityEngine.Debug.LogWarning("[ItemDoubleClickHandler] Item is null");
                return;
            }
            
            inventory.TryRemoveFromQuickSlot(quickSlotIndex);
        }
        
        // ===== HUD CONTEXT =====
        
        /// <summary>
        /// Handle double-click on QuickSlot in HUD (outside Inventory UI).
        /// Behavior: Use the item.
        /// </summary>
        public void HandleHUDQuickSlotDoubleClick(int quickSlotIndex)
        {
            var item = inventory.GetQuickSlotItem(quickSlotIndex);
            
            if (item == null)
            {
                UnityEngine.Debug.LogWarning($"[ItemDoubleClickHandler] QuickSlot {quickSlotIndex} is empty");
                return;
            }
            
            inventory.TryUseItem(item.InstanceId);
        }
        
        /// <summary>
        /// Handle weapon slot click in HUD.
        /// Behavior: 
        /// - First click: Select weapon (show model)
        /// - Click on already selected: Holster weapon (hide model)
        /// </summary>
        public void HandleHUDWeaponSlotClick(int weaponSlotIndex, int currentlySelectedWeaponIndex)
        {
            if (weaponSlotIndex == currentlySelectedWeaponIndex)
            {
                // Same weapon clicked - holster
                // TODO: Notify weapon system to holster weapon
                UnityEngine.Debug.Log($"[ItemDoubleClickHandler] Holster weapon in slot {weaponSlotIndex}");
            }
            else
            {
                // Different weapon clicked - select
                // TODO: Notify weapon system to show this weapon
                UnityEngine.Debug.Log($"[ItemDoubleClickHandler] Select weapon in slot {weaponSlotIndex}");
            }
        }
        
        // ===== HELPER METHODS =====
        
        private DoubleClickBehavior GetDoubleClickBehavior(ItemInstance item)
        {
            // Check if item has explicit behavior override
            if (item.Definition.InventoryDoubleClickBehavior != DoubleClickBehavior.AutoDetermine)
            {
                return item.Definition.InventoryDoubleClickBehavior;
            }
            
            // Auto-determine based on item type
            switch (item.Definition.ItemType)
            {
                case ItemType.Consumable:
                    return DoubleClickBehavior.Use;
                
                case ItemType.Weapon:
                case ItemType.Equipment:
                    return DoubleClickBehavior.QuickEquip;
                
                default:
                    return DoubleClickBehavior.None;
            }
        }
        
        private void HandleQuickEquip(ItemInstance item)
        {
            switch (item.Definition.ItemType)
            {
                case ItemType.Weapon:
                    QuickEquipWeapon(item);
                    break;
                
                case ItemType.Equipment:
                    QuickEquip(item);
                    break;
                
                default:
                    UnityEngine.Debug.LogWarning($"[ItemDoubleClickHandler] Cannot quick-equip item type: {item.Definition.ItemType}");
                    break;
            }
        }
        
        private void QuickEquipWeapon(ItemInstance weapon)
        {
            // Find first available weapon slot
            for (int i = 0; i < 2; i++) // Assume 2 weapon slots
            {
                var existingWeapon = inventory.GetWeapon(i);
                if (existingWeapon == null)
                {
                    // Found empty slot
                    inventory.TryEquipWeapon(weapon.InstanceId, i);
                    return;
                }
            }
            
            // No empty slot, swap with first slot
            inventory.TryEquipWeapon(weapon.InstanceId, 0);
        }
        
        private void QuickEquip(ItemInstance item)
        {
            // Equip to appropriate equipment slot based on item's EquipmentSlot
            inventory.TryEquipToSlot(item.InstanceId, item.Definition.EquipmentSlot);
        }
    }
}