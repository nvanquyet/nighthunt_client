using UnityEngine;
using NightHunt.Inventory.Core;

namespace NightHunt.Inventory.Domain
{
    /// <summary>
    /// Manages stack merging logic.
    /// Strategy: Fill target to max first, leave overflow in source.
    /// </summary>
    public static class StackManager
    {
        /// <summary>
        /// Merge source stack into target stack.
        /// Example: Source(30/50) + Target(40/50) → Source(20/50) + Target(50/50)
        /// </summary>
        public static StackMergeResult MergeStacks(ItemInstance source, ItemInstance target)
        {
            if (source.Definition.ItemId != target.Definition.ItemId) 
                return StackMergeResult.IncompatibleItems();
            
            if (!source.Definition.IsStackable)
                return StackMergeResult.NotStackable();
            
            int maxStack = source.Definition.MaxStackSize;
            int spaceInTarget = maxStack - target.StackSize;
            
            if (spaceInTarget <= 0)
                return StackMergeResult.TargetFull();
            
            int amountToTransfer = Mathf.Min(source.StackSize, spaceInTarget);
            
            target.StackSize += amountToTransfer;
            source.StackSize -= amountToTransfer;
            
            if (source.StackSize == 0)
            {
                return StackMergeResult.FullMerge(target);
            }
            else
            {
                return StackMergeResult.PartialMerge(source, target);
            }
        }
    }
    
    /// <summary>
    /// Result of a stack merge operation.
    /// </summary>
    public struct StackMergeResult
    {
        public bool Success;
        public string FailReason;
        public ItemInstance ResultSource;
        public ItemInstance ResultTarget;
        
        public static StackMergeResult FullMerge(ItemInstance merged)
        {
            return new StackMergeResult 
            { 
                Success = true, 
                ResultSource = null, 
                ResultTarget = merged 
            };
        }
        
        public static StackMergeResult PartialMerge(ItemInstance source, ItemInstance target)
        {
            return new StackMergeResult 
            { 
                Success = true, 
                ResultSource = source, 
                ResultTarget = target 
            };
        }
        
        public static StackMergeResult IncompatibleItems()
        {
            return new StackMergeResult { Success = false, FailReason = "Items are not the same type" };
        }
        
        public static StackMergeResult NotStackable()
        {
            return new StackMergeResult { Success = false, FailReason = "Item is not stackable" };
        }
        
        public static StackMergeResult TargetFull()
        {
            return new StackMergeResult { Success = false, FailReason = "Target stack is full" };
        }
    }
}
