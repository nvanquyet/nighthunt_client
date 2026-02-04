using UnityEngine;
using NightHunt.Inventory.Core.Data;

namespace NightHunt.Inventory.Domain.Inventory
{
    /// <summary>
    /// Handles stack merging logic.
    /// Strategy: Fill target to max first, leave overflow in source.
    /// </summary>
    public static class StackManager
    {
        /// <summary>
        /// Merges source stack into target stack.
        /// Example: Source(30/50) + Target(40/50) → Source(20/50) + Target(50/50)
        /// </summary>
        public static StackMergeResult MergeStacks(ItemInstance source, ItemInstance target)
        {
            // Validate same item type
            if (source.Definition.ItemId != target.Definition.ItemId)
            {
                return StackMergeResult.IncompatibleItems();
            }
            
            // Validate stackable
            if (!source.Definition.IsStackable)
            {
                return StackMergeResult.NotStackable();
            }
            
            int maxStack = source.Definition.MaxStackSize;
            int spaceInTarget = maxStack - target.StackSize;
            
            // Check if target is full
            if (spaceInTarget <= 0)
            {
                return StackMergeResult.TargetFull();
            }
            
            // Calculate transfer amount
            int amountToTransfer = Mathf.Min(source.StackSize, spaceInTarget);
            
            // Perform transfer
            target.StackSize += amountToTransfer;
            source.StackSize -= amountToTransfer;
            
            // Check if full merge (source depleted)
            if (source.StackSize == 0)
            {
                return StackMergeResult.FullMerge(target);
            }
            else
            {
                return StackMergeResult.PartialMerge(source, target);
            }
        }
        
        /// <summary>
        /// Splits a stack into two stacks.
        /// </summary>
        public static bool TrySplitStack(ItemInstance source, int splitAmount, out ItemInstance newStack)
        {
            newStack = null;
            
            // Validate
            if (!source.Definition.IsStackable)
                return false;
            
            if (splitAmount <= 0 || splitAmount >= source.StackSize)
                return false;
            
            // Create new stack
            newStack = new ItemInstance(source.Definition, System.Guid.NewGuid().ToString())
            {
                StackSize = splitAmount,
                CurrentDurability = source.CurrentDurability,
                CurrentAmmo = 0 // Don't split ammo
            };
            
            // Reduce source stack
            source.StackSize -= splitAmount;
            
            return true;
        }
    }
    
    /// <summary>
    /// Result of a stack merge operation.
    /// </summary>
    public struct StackMergeResult
    {
        public bool Success;
        public string FailReason;
        public ItemInstance ResultSource; // Null if fully merged
        public ItemInstance ResultTarget;
        
        public static StackMergeResult FullMerge(ItemInstance merged)
        {
            return new StackMergeResult
            {
                Success = true,
                ResultSource = null, // Source depleted
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
            return new StackMergeResult
            {
                Success = false,
                FailReason = "Items are not the same type"
            };
        }
        
        public static StackMergeResult NotStackable()
        {
            return new StackMergeResult
            {
                Success = false,
                FailReason = "Item is not stackable"
            };
        }
        
        public static StackMergeResult TargetFull()
        {
            return new StackMergeResult
            {
                Success = false,
                FailReason = "Target stack is full"
            };
        }
    }
}