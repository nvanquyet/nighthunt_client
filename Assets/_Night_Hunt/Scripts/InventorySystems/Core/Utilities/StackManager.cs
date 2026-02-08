using NightHunt.Inventory.Core.Data;

namespace NightHunt.Inventory.Core.Utilities
{
    /// <summary>
    /// Utility for stack operations.
    /// </summary>
    public static class StackManager
    {
        /// <summary>
        /// Merges source stack into destination stack.
        /// Returns result indicating success and overflow.
        /// </summary>
        public static StackMergeResult MergeStacks(ItemInstance source, ItemInstance destination)
        {
            if (source == null || destination == null)
                return StackMergeResult.Failed();
            
            if (source.Definition.ItemId != destination.Definition.ItemId)
                return StackMergeResult.Failed();
            
            if (!source.Definition.IsStackable)
                return StackMergeResult.Failed();
            
            int maxStack = destination.Definition.MaxStackSize;
            int availableSpace = maxStack - destination.StackSize;
            
            if (availableSpace <= 0)
                return StackMergeResult.Failed();
            
            int amountToMerge = UnityEngine.Mathf.Min(source.StackSize, availableSpace);
            
            // Transfer
            destination.StackSize += amountToMerge;
            source.StackSize -= amountToMerge;
            
            // Check if source fully consumed
            if (source.StackSize <= 0)
            {
                return StackMergeResult.FullMerge(destination);
            }
            else
            {
                return StackMergeResult.PartialMerge(destination, source);
            }
        }
        
        /// <summary>
        /// Splits amount from source stack into new stack.
        /// </summary>
        public static bool TrySplitStack(ItemInstance source, int amount, out ItemInstance splitStack)
        {
            splitStack = null;
            
            if (source == null || !source.Definition.IsStackable)
                return false;
            
            if (amount <= 0 || amount >= source.StackSize)
                return false;
            
            // Create new stack
            splitStack = new ItemInstance(source.Definition, System.Guid.NewGuid().ToString())
            {
                StackSize = amount,
                CurrentDurability = source.CurrentDurability,
                CurrentAmmo = source.CurrentAmmo
            };
            
            // Reduce source
            source.StackSize -= amount;
            
            return true;
        }
    }
    
    /// <summary>
    /// Result of stack merge operation.
    /// </summary>
    public class StackMergeResult
    {
        public bool Success { get; private set; }
        public ItemInstance ResultDestination { get; private set; }
        public ItemInstance ResultSource { get; private set; } // null if fully merged
        
        public static StackMergeResult FullMerge(ItemInstance destination)
        {
            return new StackMergeResult
            {
                Success = true,
                ResultDestination = destination,
                ResultSource = null
            };
        }
        
        public static StackMergeResult PartialMerge(ItemInstance destination, ItemInstance source)
        {
            return new StackMergeResult
            {
                Success = true,
                ResultDestination = destination,
                ResultSource = source
            };
        }
        
        public static StackMergeResult Failed()
        {
            return new StackMergeResult { Success = false };
        }
    }
}