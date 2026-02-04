using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Enums;
using System.Collections.Generic;
using System.Linq;

namespace NightHunt.Inventory.Domain.Inventory
{
    /// <summary>
    /// Main inventory controller.
    /// Manages inventory operations and fires events on state changes.
    /// Server-authoritative with event-driven architecture.
    /// </summary>
    public class InventoryManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private int maxSlots = 20;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        
        private InventoryData inventory;
        
        #region Lifecycle
        
        void Awake()
        {
            inventory = new InventoryData(maxSlots);
            
            if (enableDebugLogs)
                Debug.Log($"[InventoryManager] Initialized with {maxSlots} slots");
        }
        
        void OnEnable()
        {
            // Subscribe to request events from UI
            InventoryEvents.OnRequestAddItem += HandleRequestAddItem;
            InventoryEvents.OnRequestRemoveItem += HandleRequestRemoveItem;
            InventoryEvents.OnRequestDropStack += HandleRequestDropStack;
            InventoryEvents.OnRequestTrashItem += HandleRequestTrashItem;
            InventoryEvents.OnRequestSortInventory += HandleRequestSort;
            InventoryEvents.OnRequestAutoStack += HandleRequestAutoStack;
        }
        
        void OnDisable()
        {
            InventoryEvents.OnRequestAddItem -= HandleRequestAddItem;
            InventoryEvents.OnRequestRemoveItem -= HandleRequestRemoveItem;
            InventoryEvents.OnRequestDropStack -= HandleRequestDropStack;
            InventoryEvents.OnRequestTrashItem -= HandleRequestTrashItem;
            InventoryEvents.OnRequestSortInventory -= HandleRequestSort;
            InventoryEvents.OnRequestAutoStack -= HandleRequestAutoStack;
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Tries to add an item to the first available slot.
        /// Fires events on success/failure.
        /// </summary>
        public bool TryAddItem(ItemInstance item)
        {
            if (item == null)
            {
                LogWarning("Cannot add null item");
                return false;
            }
            
            // Try find existing stack if stackable
            if (item.Definition.IsStackable)
            {
                var existingStack = FindStackableItem(item.Definition.ItemId);
                if (existingStack != null)
                {
                    var mergeResult = StackManager.MergeStacks(item, existingStack);
                    if (mergeResult.Success)
                    {
                        Log($"Merged {item.Definition.ItemId} into existing stack");
                        
                        // If full merge, item is consumed
                        if (mergeResult.ResultSource == null)
                        {
                            InventoryEvents.InvokeStackSizeChanged(existingStack);
                            BroadcastInventoryChanged();
                            return true;
                        }
                        else
                        {
                            // Partial merge - continue with overflow
                            item = mergeResult.ResultSource;
                        }
                    }
                }
            }
            
            // Try add to empty slot
            bool success = inventory.TryAddItem(item);
            
            if (success)
            {
                Log($"Added {item.Definition.ItemId} to inventory");
                InventoryEvents.InvokeItemAdded(item, -1);
                BroadcastInventoryChanged();
            }
            else
            {
                LogWarning("Inventory full - cannot add item");
                InventoryEvents.InvokeInventoryFull();
            }
            
            return success;
        }
        
        /// <summary>
        /// Tries to add an item to a specific slot index.
        /// </summary>
        public bool TryAddItemAtIndex(ItemInstance item, int slotIndex)
        {
            if (item == null) return false;
            
            bool success = inventory.TryAddItemAtIndex(item, slotIndex);
            
            if (success)
            {
                Log($"Added {item.Definition.ItemId} at slot {slotIndex}");
                InventoryEvents.InvokeItemAdded(item, slotIndex);
                BroadcastInventoryChanged();
            }
            
            return success;
        }
        
        /// <summary>
        /// Removes an item by instance ID.
        /// </summary>
        public bool RemoveItem(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return false;
            
            var item = inventory.GetItem(instanceId);
            if (item == null)
            {
                LogWarning($"Cannot remove - item not found: {instanceId}");
                return false;
            }
            
            bool success = inventory.RemoveItem(instanceId);
            
            if (success)
            {
                Log($"Removed item: {instanceId}");
                InventoryEvents.InvokeItemRemoved(instanceId, -1);
                BroadcastInventoryChanged();
            }
            
            return success;
        }
        
        /// <summary>
        /// Removes item from a specific slot.
        /// </summary>
        public ItemInstance RemoveItemAtIndex(int slotIndex)
        {
            var item = inventory.RemoveItemAtIndex(slotIndex);
            
            if (item != null)
            {
                Log($"Removed {item.Definition.ItemId} from slot {slotIndex}");
                InventoryEvents.InvokeItemRemoved(item.InstanceId, slotIndex);
                BroadcastInventoryChanged();
            }
            
            return item;
        }
        
        /// <summary>
        /// Moves an item from one slot to another.
        /// Handles swapping if target is occupied.
        /// </summary>
        public bool MoveItem(int fromIndex, int toIndex)
        {
            var fromItem = inventory.GetItemAtIndex(fromIndex);
            var toItem = inventory.GetItemAtIndex(toIndex);
            
            if (fromItem == null)
            {
                LogWarning($"Cannot move - no item at slot {fromIndex}");
                return false;
            }
            
            // If target empty - simple move
            if (toItem == null)
            {
                inventory.RemoveItemAtIndex(fromIndex);
                inventory.TryAddItemAtIndex(fromItem, toIndex);
                
                Log($"Moved {fromItem.Definition.ItemId} from {fromIndex} to {toIndex}");
                BroadcastInventoryChanged();
                return true;
            }
            
            // Target occupied - check if stackable
            if (fromItem.Definition.IsStackable && 
                fromItem.Definition.ItemId == toItem.Definition.ItemId)
            {
                var mergeResult = StackManager.MergeStacks(fromItem, toItem);
                
                if (mergeResult.Success)
                {
                    Log($"Merged stacks at {fromIndex} and {toIndex}");
                    
                    // If full merge, remove source
                    if (mergeResult.ResultSource == null)
                    {
                        inventory.RemoveItemAtIndex(fromIndex);
                    }
                    
                    InventoryEvents.InvokeStackSizeChanged(toItem);
                    BroadcastInventoryChanged();
                    return true;
                }
            }
            
            // Swap items
            inventory.SwapSlots(fromIndex, toIndex);
            Log($"Swapped items at {fromIndex} and {toIndex}");
            BroadcastInventoryChanged();
            return true;
        }
        
        /// <summary>
        /// Gets an item by instance ID.
        /// </summary>
        public ItemInstance GetItem(string instanceId)
        {
            return inventory.GetItem(instanceId);
        }
        
        /// <summary>
        /// Checks if inventory contains an item.
        /// </summary>
        public bool HasItem(string instanceId)
        {
            return inventory.HasItem(instanceId);
        }
        
        /// <summary>
        /// Gets all items in inventory.
        /// </summary>
        public List<ItemInstance> GetAllItems()
        {
            return inventory.GetAllItems();
        }
        
        /// <summary>
        /// Gets the inventory data.
        /// </summary>
        public InventoryData GetInventoryData() => inventory;
        
        /// <summary>
        /// Gets number of empty slots.
        /// </summary>
        public int GetEmptySlotCount() => inventory.GetEmptySlotCount();
        
        /// <summary>
        /// Clears all items from inventory.
        /// </summary>
        public void Clear()
        {
            inventory.Clear();
            Log("Inventory cleared");
            BroadcastInventoryChanged();
        }
        
        #endregion
        
        #region Event Handlers
        
        private void HandleRequestAddItem(ItemInstance item)
        {
            TryAddItem(item);
        }
        
        private void HandleRequestRemoveItem(string instanceId)
        {
            RemoveItem(instanceId);
        }
        
        private void HandleRequestDropStack(ItemInstance item, int amount)
        {
            if (item == null) return;
            
            // If dropping entire stack or item is not stackable
            if (amount >= item.StackSize || !item.Definition.IsStackable)
            {
                // Remove from inventory
                RemoveItem(item.InstanceId);
                
                // Fire world drop event (handled by WorldDropSpawner)
                // TODO: Fire world drop event with item
                Log($"Dropped entire stack of {item.Definition.ItemId}");
                return;
            }
            
            // Split stack
            if (StackManager.TrySplitStack(item, amount, out ItemInstance splitStack))
            {
                // Update original stack
                InventoryEvents.InvokeStackSizeChanged(item);
                
                // Drop the split stack
                // TODO: Fire world drop event with splitStack
                Log($"Dropped {amount} of {item.Definition.ItemId}");
                BroadcastInventoryChanged();
            }
        }
        
        private void HandleRequestTrashItem(ItemInstance item)
        {
            if (item == null) return;
            
            // Remove from inventory
            bool removed = RemoveItem(item.InstanceId);
            
            if (removed)
            {
                // Remove stat modifiers if equipped
                string sourceId = $"Equip:{item.InstanceId}";
                CharacterStatsEvents.InvokeRemoveModifier(sourceId);
                
                // Remove attachment modifiers
                sourceId = $"Attach:{item.InstanceId}";
                CharacterStatsEvents.InvokeRemoveModifier(sourceId);
                
                Log($"Trashed item: {item.Definition.ItemId}");
            }
        }
        
        private void HandleRequestSort()
        {
            InventorySorter.SortByItemType(inventory.GetAllSlots());
            Log("Inventory sorted by item type");
            BroadcastInventoryChanged();
        }
        
        private void HandleRequestAutoStack()
        {
            InventoryStacker.AutoStack(inventory.GetAllSlots());
            Log("Auto-stacked inventory");
            BroadcastInventoryChanged();
        }
        
        #endregion
        
        #region Helper Methods
        
        private ItemInstance FindStackableItem(string itemId)
        {
            return inventory.GetAllItems()
                .FirstOrDefault(i => i.Definition.ItemId == itemId && 
                                    i.StackSize < i.Definition.MaxStackSize);
        }
        
        private void BroadcastInventoryChanged()
        {
            InventoryEvents.InvokeInventoryChanged(inventory);
        }
        
        private void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[InventoryManager] {message}");
        }
        
        private void LogWarning(string message)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[InventoryManager] {message}");
        }
        
        #endregion
    }
}