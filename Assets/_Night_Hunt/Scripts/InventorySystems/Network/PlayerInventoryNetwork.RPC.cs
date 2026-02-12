// CONTINUATION OF PlayerInventoryNetwork.cs
// This file contains RPC methods and IInventoryOperations implementation

using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Structs;

namespace NightHunt.Inventory.Network
{
    public partial class PlayerInventoryNetwork
    {
        // ===== IInventoryOperations: ADD ITEM =====
        
        public bool TryAddItem(ItemInstance item, int targetIndex = -1)
        {
            if (!IsOwner)
            {
                LogWarning("TryAddItem: Not owner!");
                return false;
            }
            
            if (item == null || item.Definition == null)
            {
                LogWarning("TryAddItem: Item or Definition is null");
                return false;
            }
            
            // Client-side prediction: add to cache immediately
            // If server rejects, we'll rollback
            
            AddItemServerRpc(item.Serialize(), targetIndex);
            return true;
        }
        
        [ServerRpc]
        private void AddItemServerRpc(ItemInstanceData itemData, int targetIndex, NetworkConnection sender = null)
        {
            var item = DeserializeItem(itemData);
            if (item == null)
            {
                LogError("AddItemServerRpc: Failed to deserialize item");
                SendOperationFailedObserverRpc("AddItem", "Failed to deserialize item", sender);
                return;
            }
            
            // Validate weight
            float currentWeight = GetCurrentWeight();
            float maxWeight = playerStats != null ? playerStats.GetWeightCapacity() : 100f;
            
            if (!validator.ValidateWeight(item, currentWeight, maxWeight))
            {
                SendOperationFailedObserverRpc("AddItem", "Would exceed weight capacity", sender);
                return;
            }
            
            // Try to stack with existing items
            if (config.AutoStackOnAdd && item.Definition.IsStackable)
            {
                if (TryStackWithExisting(item))
                {
                    Log($"Item {item.Definition.DisplayName} stacked with existing");
                    return;
                }
            }
            
            // Find index
            int index = targetIndex;
            if (index < 0)
            {
                index = FindFirstAvailableIndex();
            }
            
            // Validate index
            if (!validator.ValidateInventoryIndex(index))
            {
                SendOperationFailedObserverRpc("AddItem", "Invalid inventory index", sender);
                return;
            }
            
            // Set inventory index
            item.InventoryIndex = index;
            
            // Add to inventory list (expand if needed)
            if (index >= inventoryList.Count)
            {
                // Fill gaps with default items
                for (int i = inventoryList.Count; i <= index; i++)
                {
                    inventoryList.Add(default);
                }
            }
            
            inventoryList[index] = item.Serialize();
            
            Log($"Added {item.Definition.DisplayName} x{item.StackSize} at index {index}");
        }
        
        private bool TryStackWithExisting(ItemInstance newItem)
        {
            for (int i = 0; i < inventoryList.Count; i++)
            {
                var existingData = inventoryList[i];
                if (IsDefaultItemData(existingData))
                    continue;
                
                var existingItem = DeserializeItem(existingData);
                if (existingItem == null)
                    continue;
                
                if (!validator.CanStack(existingItem, newItem))
                    continue;
                
                // Stack items
                int maxStack = existingItem.Definition.MaxStackSize;
                int spaceAvailable = maxStack - existingItem.StackSize;
                
                if (spaceAvailable >= newItem.StackSize)
                {
                    // Entire new stack fits
                    existingItem.StackSize += newItem.StackSize;
                    inventoryList[i] = existingItem.Serialize();
                    return true;
                }
                else
                {
                    // Partial stack
                    existingItem.StackSize = maxStack;
                    newItem.StackSize -= spaceAvailable;
                    inventoryList[i] = existingItem.Serialize();
                    // Continue to add remainder
                }
            }
            
            return false;
        }
        
        // ===== IInventoryOperations: REMOVE ITEM =====
        
        public bool TryRemoveItem(string instanceId)
        {
            if (!IsOwner)
            {
                LogWarning("TryRemoveItem: Not owner!");
                return false;
            }
            
            RemoveItemServerRpc(instanceId);
            return true;
        }
        
        [ServerRpc]
        private void RemoveItemServerRpc(string instanceId, NetworkConnection sender = null)
        {
            // Find item in inventory
            for (int i = 0; i < inventoryList.Count; i++)
            {
                var itemData = inventoryList[i];
                if (itemData.InstanceId == instanceId)
                {
                    inventoryList[i] = default; // Clear slot
                    Log($"Removed item {instanceId} from inventory index {i}");
                    return;
                }
            }
            
            LogWarning($"RemoveItemServerRpc: Item {instanceId} not found in inventory");
            SendOperationFailedObserverRpc("RemoveItem", "Item not found", sender);
        }
        
        public bool TryRemoveQuantity(string instanceId, int quantity, out ItemInstance remainder)
        {
            remainder = null;
            
            if (!IsOwner)
            {
                LogWarning("TryRemoveQuantity: Not owner!");
                return false;
            }
            
            // For now, just remove the item
            // TODO: Implement proper quantity splitting
            RemoveQuantityServerRpc(instanceId, quantity);
            return true;
        }
        
        [ServerRpc]
        private void RemoveQuantityServerRpc(string instanceId, int quantity, NetworkConnection sender = null)
        {
            for (int i = 0; i < inventoryList.Count; i++)
            {
                var itemData = inventoryList[i];
                if (itemData.InstanceId == instanceId)
                {
                    var item = DeserializeItem(itemData);
                    if (item == null)
                        continue;
                    
                    if (quantity >= item.StackSize)
                    {
                        // Remove entire stack
                        inventoryList[i] = default;
                    }
                    else
                    {
                        // Reduce stack
                        item.StackSize -= quantity;
                        inventoryList[i] = item.Serialize();
                    }
                    
                    Log($"Removed {quantity} from {item.Definition.DisplayName}");
                    return;
                }
            }
            
            LogWarning($"RemoveQuantityServerRpc: Item {instanceId} not found");
            SendOperationFailedObserverRpc("RemoveQuantity", "Item not found", sender);
        }
        
        // ===== IInventoryOperations: DROP ITEM =====
        
        public bool TryDropItem(string instanceId, int quantity)
        {
            if (!IsOwner)
            {
                LogWarning("TryDropItem: Not owner!");
                return false;
            }
            
            DropItemServerRpc(instanceId, quantity);
            return true;
        }
        
        [ServerRpc]
        private void DropItemServerRpc(string instanceId, int quantity, NetworkConnection sender = null)
        {
            // Find item
            ItemInstance item = GetItemFromAnywhere(instanceId);
            if (item == null)
            {
                LogWarning($"DropItemServerRpc: Item {instanceId} not found");
                SendOperationFailedObserverRpc("DropItem", "Item not found", sender);
                return;
            }
            
            // TODO: Spawn item in world (for now just log and remove)
            Log($"DROP: {item.Definition.DisplayName} x{quantity} (would spawn in world)");
            
            // Remove from inventory
            TryRemoveQuantity(instanceId, quantity, out _);
        }
        
        // ===== IInventoryOperations: MOVE ITEM =====
        
        public bool TryMoveItem(string instanceId, int newIndex)
        {
            if (!IsOwner)
            {
                LogWarning("TryMoveItem: Not owner!");
                return false;
            }
            
            MoveItemServerRpc(instanceId, newIndex);
            return true;
        }
        
        [ServerRpc]
        private void MoveItemServerRpc(string instanceId, int newIndex, NetworkConnection sender = null)
        {
            if (!validator.ValidateInventoryIndex(newIndex))
            {
                SendOperationFailedObserverRpc("MoveItem", "Invalid index", sender);
                return;
            }
            
            // Find item's current index
            int currentIndex = -1;
            for (int i = 0; i < inventoryList.Count; i++)
            {
                if (inventoryList[i].InstanceId == instanceId)
                {
                    currentIndex = i;
                    break;
                }
            }
            
            if (currentIndex < 0)
            {
                SendOperationFailedObserverRpc("MoveItem", "Item not found", sender);
                return;
            }
            
            if (currentIndex == newIndex)
            {
                Log("MoveItem: Same index, ignoring");
                return;
            }
            
            // Expand list if needed
            while (inventoryList.Count <= newIndex)
            {
                inventoryList.Add(default);
            }
            
            // Swap items
            var movingItem = inventoryList[currentIndex];
            var targetItem = inventoryList[newIndex];
            
            // Update indices
            movingItem.InventoryIndex = newIndex;
            if (!IsDefaultItemData(targetItem))
            {
                targetItem.InventoryIndex = currentIndex;
                inventoryList[currentIndex] = targetItem;
            }
            else
            {
                inventoryList[currentIndex] = default;
            }
            
            inventoryList[newIndex] = movingItem;
            
            Log($"Moved item {instanceId} from index {currentIndex} to {newIndex}");
        }
        
        // ===== QUERY METHODS =====
        
        public ItemInstance GetItem(string instanceId)
        {
            if (runtimeItemCache.TryGetValue(instanceId, out ItemInstance item))
                return item;
            
            return null;
        }
        
        public float GetCurrentWeight()
        {
            float total = 0f;
            
            // Inventory items always count
            foreach (var itemData in inventoryList)
            {
                if (!IsDefaultItemData(itemData))
                {
                    var item = DeserializeItem(itemData);
                    if (item != null)
                        total += item.GetTotalWeight();
                }
            }
            
            // Equipment items (config-based)
            if (config.EquipmentAddsWeight)
            {
                foreach (var kvp in equipmentSlots)
                {
                    if (!IsDefaultItemData(kvp.Value))
                    {
                        var item = DeserializeItem(kvp.Value);
                        if (item != null)
                            total += item.GetTotalWeight();
                    }
                }
            }
            
            // Weapon items (config-based)
            if (config.WeaponsAddWeight)
            {
                foreach (var itemData in weaponSlots)
                {
                    if (!IsDefaultItemData(itemData))
                    {
                        var item = DeserializeItem(itemData);
                        if (item != null)
                            total += item.GetTotalWeight();
                    }
                }
            }
            
            // Quickslot items (config-based)
            if (config.QuickSlotItemsAddWeight)
            {
                foreach (var itemData in quickSlots)
                {
                    if (!IsDefaultItemData(itemData))
                    {
                        var item = DeserializeItem(itemData);
                        if (item != null)
                            total += item.GetTotalWeight();
                    }
                }
            }
            
            return total;
        }
        
        public bool CanAddItem(ItemInstance item)
        {
            float currentWeight = GetCurrentWeight();
            float maxWeight = playerStats != null ? playerStats.GetWeightCapacity() : 100f;
            
            return validator.ValidateWeight(item, currentWeight, maxWeight);
        }
        
        public ItemInstance GetItemAtIndex(int index)
        {
            if (index < 0 || index >= inventoryList.Count)
                return null;
            
            var itemData = inventoryList[index];
            if (IsDefaultItemData(itemData))
                return null;
            
            return DeserializeItem(itemData);
        }
        
        public ItemInstance GetEquippedItem(EquipmentSlotType slotType)
        {
            if (equipmentSlots.TryGetValue(slotType, out ItemInstanceData itemData))
            {
                if (!IsDefaultItemData(itemData))
                    return DeserializeItem(itemData);
            }
            
            return null;
        }
        
        public ItemInstance GetWeapon(int weaponSlotIndex)
        {
            if (weaponSlotIndex < 0 || weaponSlotIndex >= weaponSlots.Count)
                return null;
            
            var itemData = weaponSlots[weaponSlotIndex];
            if (IsDefaultItemData(itemData))
                return null;
            
            return DeserializeItem(itemData);
        }
        
        public ItemInstance GetQuickSlotItem(int quickSlotIndex)
        {
            if (quickSlotIndex < 0 || quickSlotIndex >= quickSlots.Count)
                return null;
            
            var itemData = quickSlots[quickSlotIndex];
            if (IsDefaultItemData(itemData))
                return null;
            
            return DeserializeItem(itemData);
        }
        
        public int FindFirstAvailableIndex()
        {
            for (int i = 0; i < inventoryList.Count; i++)
            {
                if (IsDefaultItemData(inventoryList[i]))
                    return i;
            }
            
            // No gaps, return next index
            return inventoryList.Count;
        }
        
        private ItemInstance GetItemFromAnywhere(string instanceId)
        {
            // Check inventory
            foreach (var itemData in inventoryList)
            {
                if (itemData.InstanceId == instanceId)
                    return DeserializeItem(itemData);
            }
            
            // Check equipment
            foreach (var kvp in equipmentSlots)
            {
                if (kvp.Value.InstanceId == instanceId)
                    return DeserializeItem(kvp.Value);
            }
            
            // Check weapons
            foreach (var itemData in weaponSlots)
            {
                if (itemData.InstanceId == instanceId)
                    return DeserializeItem(itemData);
            }
            
            // Check quickslots
            foreach (var itemData in quickSlots)
            {
                if (itemData.InstanceId == instanceId)
                    return DeserializeItem(itemData);
            }
            
            return null;
        }
        
        // ===== ERROR HANDLING =====
        
        [ObserversRpc]
        private void SendOperationFailedObserverRpc(string operation, string reason, NetworkConnection target)
        {
            if (!IsOwner)
                return;
            
            LogWarning($"Operation '{operation}' failed: {reason}");
            
            InventoryEvents.RaiseOperationFailed(new OperationFailedEvent
            {
                OwnerId = ObjectId,
                IsLocalPlayer = true,
                Operation = operation,
                Reason = reason
            });
        }
    }
}