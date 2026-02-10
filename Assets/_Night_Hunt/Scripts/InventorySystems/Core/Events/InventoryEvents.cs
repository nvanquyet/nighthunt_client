using System;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;

namespace NightHunt.Inventory.Core.Events
{
    /// <summary>
    /// Global events for InventorySystem.
    /// Subscribe to these for UI updates and gameplay logic.
    /// </summary>
    public static class InventoryEvents
    {
        // === Item Added/Removed ===
        /// <summary>Fired when item is added to inventory. Args: (item, slotIndex)</summary>
        public static event Action<ItemInstance, int> OnItemAdded;
        
        /// <summary>Fired when item is removed from inventory. Args: (item, slotIndex)</summary>
        public static event Action<ItemInstance, int> OnItemRemoved;
        
        /// <summary>Fired when item is moved. Args: (item, fromSlot, toSlot)</summary>
        public static event Action<ItemInstance, int, int> OnItemMoved;
        
        /// <summary>Fired when items are swapped. Args: (itemA, slotA, itemB, slotB)</summary>
        public static event Action<ItemInstance, int, ItemInstance, int> OnItemsSwapped;
        
        // === Stack Operations ===
        /// <summary>Fired when stack is split. Args: (originalItem, originalSlot, splitItem, newSlot, splitAmount)</summary>
        public static event Action<ItemInstance, int, ItemInstance, int, int> OnStackSplit;
        
        /// <summary>Fired when stacks are merged. Args: (sourceItem, sourceSlot, targetItem, targetSlot, mergedAmount)</summary>
        public static event Action<ItemInstance, int, ItemInstance, int, int> OnStacksMerged;
        
        // === Weight ===
        /// <summary>Fired when total weight changes. Args: (currentWeight, maxWeight, isOverweight)</summary>
        public static event Action<float, float, bool> OnWeightChanged;
        
        // === Container State ===
        /// <summary>Fired when inventory is cleared</summary>
        public static event Action OnInventoryCleared;
        
        /// <summary>Fired when inventory size changes (e.g., backpack equipped). Args: (newSize)</summary>
        public static event Action<int> OnInventorySizeChanged;
        
        // === Validation ===
        /// <summary>Fired when operation fails. Args: (result, errorMessage)</summary>
        public static event Action<OperationResult, string> OnOperationFailed;
        
        // === Invoke Methods ===
        public static void InvokeItemAdded(ItemInstance item, int slotIndex)
        {
            UnityEngine.Debug.Log($"[InventoryEvents] InvokeItemAdded: {item?.Definition?.DisplayName ?? "null"} at slot {slotIndex}, Subscribers: {OnItemAdded?.GetInvocationList()?.Length ?? 0}");
            OnItemAdded?.Invoke(item, slotIndex);
        }
        
        public static void InvokeItemRemoved(ItemInstance item, int slotIndex) 
            => OnItemRemoved?.Invoke(item, slotIndex);
        
        public static void InvokeItemMoved(ItemInstance item, int fromSlot, int toSlot) 
            => OnItemMoved?.Invoke(item, fromSlot, toSlot);
        
        public static void InvokeItemsSwapped(ItemInstance itemA, int slotA, ItemInstance itemB, int slotB) 
            => OnItemsSwapped?.Invoke(itemA, slotA, itemB, slotB);
        
        public static void InvokeStackSplit(ItemInstance original, int originalSlot, ItemInstance split, int newSlot, int amount) 
            => OnStackSplit?.Invoke(original, originalSlot, split, newSlot, amount);
        
        public static void InvokeStacksMerged(ItemInstance source, int sourceSlot, ItemInstance target, int targetSlot, int amount) 
            => OnStacksMerged?.Invoke(source, sourceSlot, target, targetSlot, amount);
        
        public static void InvokeWeightChanged(float current, float max, bool isOverweight) 
            => OnWeightChanged?.Invoke(current, max, isOverweight);
        
        public static void InvokeInventoryCleared() 
            => OnInventoryCleared?.Invoke();
        
        public static void InvokeInventorySizeChanged(int newSize) 
            => OnInventorySizeChanged?.Invoke(newSize);
        
        public static void InvokeOperationFailed(OperationResult result, string message) 
            => OnOperationFailed?.Invoke(result, message);
    }
}