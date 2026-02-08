using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;

namespace _Night_Hunt.Scripts.InventorySystems.Core.Interfaces
{
    /// <summary>
    /// Public API for InventorySystem.
    /// Manages main player inventory (20-50 slots).
    /// </summary>
    public interface IInventorySystem
    {
        // === Query ===
        
        /// <summary>Get item at specific slot index</summary>
        ItemInstance GetItemAtSlot(int slotIndex);
        
        /// <summary>Get total slot count</summary>
        int GetSlotCount();
        
        /// <summary>Get number of empty slots</summary>
        int GetEmptySlotCount();
        
        /// <summary>Get current total weight of all items</summary>
        float GetCurrentWeight();
        
        /// <summary>Get maximum weight capacity</summary>
        float GetMaxWeight();
        
        /// <summary>Check if player is overweight</summary>
        bool IsOverweight();
        
        // === Add/Remove ===
        
        /// <summary>Add item to first available slot</summary>
        OperationResult AddItem(ItemInstance item, out int assignedSlot);
        
        /// <summary>Add item to specific slot</summary>
        OperationResult AddItemAtSlot(ItemInstance item, int slotIndex);
        
        /// <summary>Remove item by instance ID</summary>
        OperationResult RemoveItem(string instanceId);
        
        /// <summary>Remove item from specific slot</summary>
        OperationResult RemoveItemAtSlot(int slotIndex, out ItemInstance removedItem);
        
        // === Move/Swap ===
        
        /// <summary>Move item from one slot to another (empty target slot)</summary>
        OperationResult MoveItem(int fromSlot, int toSlot);
        
        /// <summary>Swap items between two slots</summary>
        OperationResult SwapItems(int slotA, int slotB);
        
        // === Stack Operations ===
        
        /// <summary>Split stack into two stacks</summary>
        OperationResult SplitStack(int slotIndex, int amount, out ItemInstance splitItem);
        
        /// <summary>Merge two stacks together</summary>
        OperationResult MergeStacks(int sourceSlot, int targetSlot);
        
        // === Utility ===
        
        /// <summary>Check if inventory has item with instance ID</summary>
        bool HasItem(string instanceId);
        
        /// <summary>Find item by instance ID</summary>
        ItemInstance FindItem(string instanceId);
        
        /// <summary>Clear entire inventory</summary>
        void Clear();
    }
}