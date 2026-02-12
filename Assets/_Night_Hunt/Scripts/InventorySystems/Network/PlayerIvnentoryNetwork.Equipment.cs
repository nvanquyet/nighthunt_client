// CONTINUATION OF PlayerInventoryNetwork.cs
// Part 3: Equipment, Weapon, and QuickSlot Operations

using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using NightHunt.Inventory.Core.Config;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Structs;

namespace NightHunt.Inventory.Network
{
    public partial class PlayerInventoryNetwork
    {
        // ===== EQUIPMENT OPERATIONS =====
        
        public bool TryEquipToSlot(string instanceId, EquipmentSlotType slotType)
        {
            if (!IsOwner)
            {
                LogWarning("TryEquipToSlot: Not owner!");
                return false;
            }
            
            EquipToSlotServerRpc(instanceId, slotType);
            return true;
        }
        
        [ServerRpc]
        private void EquipToSlotServerRpc(string instanceId, EquipmentSlotType slotType, NetworkConnection sender = null)
        {
            var item = GetItemFromAnywhere(instanceId);
            if (item == null)
            {
                SendOperationFailedObserverRpc("EquipToSlot", "Item not found", sender);
                return;
            }
            
            // Validate can equip
            if (!validator.ValidateEquipmentSlot(item, slotType))
            {
                SendOperationFailedObserverRpc("EquipToSlot", "Item cannot be equipped to this slot", sender);
                return;
            }
            
            // Handle stack behavior
            ItemInstance itemToEquip = item;
            bool needsStackHandling = item.Definition.IsStackable && item.StackSize > 1;
            
            if (needsStackHandling)
            {
                var behavior = config.GetEquipBehavior(item.Definition.ItemType);
                
                if (behavior == EquipStackBehavior.EquipOneReturnRest)
                {
                    // Split stack: equip 1, keep rest in inventory
                    itemToEquip = item.Clone();
                    itemToEquip.StackSize = 1;
                    item.StackSize -= 1;
                    
                    // Update original in inventory
                    UpdateItemInInventory(item);
                }
                // else: EquipFullStack - equip the whole stack
            }
            
            // Check if slot is occupied
            ItemInstance swappedItem = null;
            if (equipmentSlots.TryGetValue(slotType, out ItemInstanceData existingData))
            {
                if (!IsDefaultItemData(existingData))
                {
                    swappedItem = DeserializeItem(existingData);
                    
                    // Return swapped item to inventory
                    swappedItem.IsEquipped = false;
                    swappedItem.InventoryIndex = FindFirstAvailableIndex();
                    AddItemToInventory(swappedItem);
                }
            }
            
            // Remove from inventory if it was there
            RemoveItemFromInventory(instanceId);
            
            // Equip item
            itemToEquip.IsEquipped = true;
            itemToEquip.InventoryIndex = -1; // Not in inventory anymore
            equipmentSlots[slotType] = itemToEquip.Serialize();
            
            // Update stats
            ApplyItemStatModifiers(itemToEquip);
            
            Log($"Equipped {itemToEquip.Definition.DisplayName} to {slotType}");
        }
        
        public bool TryUnequipFromSlot(EquipmentSlotType slotType)
        {
            if (!IsOwner)
            {
                LogWarning("TryUnequipFromSlot: Not owner!");
                return false;
            }
            
            UnequipFromSlotServerRpc(slotType);
            return true;
        }
        
        [ServerRpc]
        private void UnequipFromSlotServerRpc(EquipmentSlotType slotType, NetworkConnection sender = null)
        {
            if (!equipmentSlots.TryGetValue(slotType, out ItemInstanceData itemData))
            {
                SendOperationFailedObserverRpc("UnequipFromSlot", "Slot is empty", sender);
                return;
            }
            
            if (IsDefaultItemData(itemData))
            {
                SendOperationFailedObserverRpc("UnequipFromSlot", "Slot is empty", sender);
                return;
            }
            
            var item = DeserializeItem(itemData);
            if (item == null)
            {
                SendOperationFailedObserverRpc("UnequipFromSlot", "Failed to deserialize item", sender);
                return;
            }
            
            // Remove stat modifiers
            RemoveItemStatModifiers(item);
            
            // Return to inventory
            item.IsEquipped = false;
            item.InventoryIndex = FindFirstAvailableIndex();
            AddItemToInventory(item);
            
            // Clear equipment slot
            equipmentSlots[slotType] = default;
            
            Log($"Unequipped {item.Definition.DisplayName} from {slotType}");
        }
        
        // ===== WEAPON OPERATIONS =====
        
        public bool TryEquipWeapon(string instanceId, int weaponSlotIndex)
        {
            if (!IsOwner)
            {
                LogWarning("TryEquipWeapon: Not owner!");
                return false;
            }
            
            EquipWeaponServerRpc(instanceId, weaponSlotIndex);
            return true;
        }
        
        [ServerRpc]
        private void EquipWeaponServerRpc(string instanceId, int weaponSlotIndex, NetworkConnection sender = null)
        {
            if (weaponSlotIndex < 0 || weaponSlotIndex >= config.WeaponSlotCount)
            {
                SendOperationFailedObserverRpc("EquipWeapon", "Invalid weapon slot index", sender);
                return;
            }
            
            var item = GetItemFromAnywhere(instanceId);
            if (item == null)
            {
                SendOperationFailedObserverRpc("EquipWeapon", "Item not found", sender);
                return;
            }
            
            // Validate can equip as weapon
            if (!validator.ValidateWeaponSlot(item))
            {
                SendOperationFailedObserverRpc("EquipWeapon", "Item is not a weapon", sender);
                return;
            }
            
            // Weapons are never stackable, so no stack handling needed
            
            // Check if slot is occupied
            ItemInstance swappedWeapon = null;
            if (weaponSlotIndex < weaponSlots.Count)
            {
                var existingData = weaponSlots[weaponSlotIndex];
                if (!IsDefaultItemData(existingData))
                {
                    swappedWeapon = DeserializeItem(existingData);
                    
                    // Return swapped weapon to inventory
                    swappedWeapon.IsEquipped = false;
                    swappedWeapon.InventoryIndex = FindFirstAvailableIndex();
                    AddItemToInventory(swappedWeapon);
                    
                    // Remove stat modifiers from swapped weapon
                    RemoveItemStatModifiers(swappedWeapon);
                }
            }
            
            // Remove from inventory if it was there
            RemoveItemFromInventory(instanceId);
            
            // Equip weapon
            item.IsEquipped = true;
            item.InventoryIndex = -1;
            weaponSlots[weaponSlotIndex] = item.Serialize();
            
            // Apply stat modifiers (including attachments)
            ApplyItemStatModifiers(item);
            
            Log($"Equipped weapon {item.Definition.DisplayName} to slot {weaponSlotIndex}");
        }
        
        public bool TryUnequipWeapon(int weaponSlotIndex)
        {
            if (!IsOwner)
            {
                LogWarning("TryUnequipWeapon: Not owner!");
                return false;
            }
            
            UnequipWeaponServerRpc(weaponSlotIndex);
            return true;
        }
        
        [ServerRpc]
        private void UnequipWeaponServerRpc(int weaponSlotIndex, NetworkConnection sender = null)
        {
            if (weaponSlotIndex < 0 || weaponSlotIndex >= weaponSlots.Count)
            {
                SendOperationFailedObserverRpc("UnequipWeapon", "Invalid weapon slot index", sender);
                return;
            }
            
            var itemData = weaponSlots[weaponSlotIndex];
            if (IsDefaultItemData(itemData))
            {
                SendOperationFailedObserverRpc("UnequipWeapon", "Weapon slot is empty", sender);
                return;
            }
            
            var weapon = DeserializeItem(itemData);
            if (weapon == null)
            {
                SendOperationFailedObserverRpc("UnequipWeapon", "Failed to deserialize weapon", sender);
                return;
            }
            
            // Remove stat modifiers
            RemoveItemStatModifiers(weapon);
            
            // Return to inventory
            weapon.IsEquipped = false;
            weapon.InventoryIndex = FindFirstAvailableIndex();
            AddItemToInventory(weapon);
            
            // Clear weapon slot
            weaponSlots[weaponSlotIndex] = default;
            
            Log($"Unequipped weapon {weapon.Definition.DisplayName} from slot {weaponSlotIndex}");
        }
        
        // ===== QUICKSLOT OPERATIONS =====
        
        public bool TryAssignToQuickSlot(string instanceId, int quickSlotIndex)
        {
            if (!IsOwner)
            {
                LogWarning("TryAssignToQuickSlot: Not owner!");
                return false;
            }
            
            AssignToQuickSlotServerRpc(instanceId, quickSlotIndex);
            return true;
        }
        
        [ServerRpc]
        private void AssignToQuickSlotServerRpc(string instanceId, int quickSlotIndex, NetworkConnection sender = null)
        {
            if (quickSlotIndex < 0 || quickSlotIndex >= config.QuickSlotCount)
            {
                SendOperationFailedObserverRpc("AssignToQuickSlot", "Invalid quickslot index", sender);
                return;
            }
            
            var item = GetItemFromAnywhere(instanceId);
            if (item == null)
            {
                SendOperationFailedObserverRpc("AssignToQuickSlot", "Item not found", sender);
                return;
            }
            
            // Validate can assign to quickslot
            if (!validator.ValidateQuickSlot(item, quickSlotIndex))
            {
                SendOperationFailedObserverRpc("AssignToQuickSlot", "Item cannot be assigned to quickslot", sender);
                return;
            }
            
            // Handle stack behavior
            ItemInstance itemToAssign = item;
            var behavior = config.GetEquipBehavior(item.Definition.ItemType);
            
            if (item.Definition.IsStackable && item.StackSize > 1)
            {
                if (behavior == EquipStackBehavior.EquipFullStack)
                {
                    // Assign full stack
                    itemToAssign = item;
                }
                else
                {
                    // Assign only 1, keep rest in inventory
                    itemToAssign = item.Clone();
                    itemToAssign.StackSize = 1;
                    item.StackSize -= 1;
                    UpdateItemInInventory(item);
                }
            }
            
            // Check if quickslot is occupied
            ItemInstance swappedItem = null;
            if (quickSlotIndex < quickSlots.Count)
            {
                var existingData = quickSlots[quickSlotIndex];
                if (!IsDefaultItemData(existingData))
                {
                    swappedItem = DeserializeItem(existingData);
                    
                    // Return swapped item to inventory
                    swappedItem.IsEquipped = false;
                    swappedItem.InventoryIndex = FindFirstAvailableIndex();
                    AddItemToInventory(swappedItem);
                }
            }
            
            // Remove from inventory if it was there
            RemoveItemFromInventory(instanceId);
            
            // Assign to quickslot
            itemToAssign.IsEquipped = true;
            itemToAssign.InventoryIndex = -1;
            quickSlots[quickSlotIndex] = itemToAssign.Serialize();
            
            Log($"Assigned {itemToAssign.Definition.DisplayName} to quickslot {quickSlotIndex}");
        }
        
        public bool TryRemoveFromQuickSlot(int quickSlotIndex)
        {
            if (!IsOwner)
            {
                LogWarning("TryRemoveFromQuickSlot: Not owner!");
                return false;
            }
            
            RemoveFromQuickSlotServerRpc(quickSlotIndex);
            return true;
        }
        
        [ServerRpc]
        private void RemoveFromQuickSlotServerRpc(int quickSlotIndex, NetworkConnection sender = null)
        {
            if (quickSlotIndex < 0 || quickSlotIndex >= quickSlots.Count)
            {
                SendOperationFailedObserverRpc("RemoveFromQuickSlot", "Invalid quickslot index", sender);
                return;
            }
            
            var itemData = quickSlots[quickSlotIndex];
            if (IsDefaultItemData(itemData))
            {
                SendOperationFailedObserverRpc("RemoveFromQuickSlot", "Quickslot is empty", sender);
                return;
            }
            
            var item = DeserializeItem(itemData);
            if (item == null)
            {
                SendOperationFailedObserverRpc("RemoveFromQuickSlot", "Failed to deserialize item", sender);
                return;
            }
            
            // Return to inventory
            item.IsEquipped = false;
            item.InventoryIndex = FindFirstAvailableIndex();
            AddItemToInventory(item);
            
            // Clear quickslot
            quickSlots[quickSlotIndex] = default;
            
            Log($"Removed {item.Definition.DisplayName} from quickslot {quickSlotIndex}");
        }
        
        // ===== HELPER METHODS =====
        
        private void AddItemToInventory(ItemInstance item)
        {
            int index = item.InventoryIndex >= 0 ? item.InventoryIndex : FindFirstAvailableIndex();
            
            while (inventoryList.Count <= index)
            {
                inventoryList.Add(default);
            }
            
            inventoryList[index] = item.Serialize();
        }
        
        private void RemoveItemFromInventory(string instanceId)
        {
            for (int i = 0; i < inventoryList.Count; i++)
            {
                if (inventoryList[i].InstanceId == instanceId)
                {
                    inventoryList[i] = default;
                    return;
                }
            }
        }
        
        private void UpdateItemInInventory(ItemInstance item)
        {
            for (int i = 0; i < inventoryList.Count; i++)
            {
                if (inventoryList[i].InstanceId == item.InstanceId)
                {
                    inventoryList[i] = item.Serialize();
                    return;
                }
            }
        }
        
        private void ApplyItemStatModifiers(ItemInstance item)
        {
            if (playerStats == null)
                return;
            
            // TODO: Apply stat modifiers to PlayerStats
            // This will be implemented when integrating with stat system
            
            InventoryEvents.RaiseStatModifiersChanged(new StatModifiersChangedEvent
            {
                OwnerId = ObjectId,
                IsLocalPlayer = IsOwner,
                SourceId = item.GetModifierSourceId()
            });
        }
        
        private void RemoveItemStatModifiers(ItemInstance item)
        {
            if (playerStats == null)
                return;
            
            // TODO: Remove stat modifiers from PlayerStats
            
            InventoryEvents.RaiseStatModifiersChanged(new StatModifiersChangedEvent
            {
                OwnerId = ObjectId,
                IsLocalPlayer = IsOwner,
                SourceId = item.GetModifierSourceId()
            });
        }
    }
}