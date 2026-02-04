using System.Collections.Generic;
using System.Linq;
using NightHunt.Inventory.Core.Data;

namespace NightHunt.Inventory.Domain.Inventory
{
    /// <summary>
    /// Utility class for auto-stacking inventory items.
    /// </summary>
    public static class InventoryStacker
    {
        /// <summary>
        /// Auto-stacks all stackable items.
        /// Strategy: Merge all instances of same ItemId into first found stack.
        /// </summary>
        public static void AutoStack(List<InventoryData.InventorySlot> slots)
        {
            if (slots == null || slots.Count == 0) return;
            
            // Group stackable items by ItemId
            var stackableGroups = slots
                .Where(s => s.Item != null && s.Item.Definition.IsStackable)
                .GroupBy(s => s.Item.Definition.ItemId);
            
            foreach (var group in stackableGroups)
            {
                var stacks = group.ToList();
                
                // Need at least 2 stacks to merge
                if (stacks.Count <= 1) continue;
                
                // Merge all into first stack
                var firstSlot = stacks[0];
                
                for (int i = 1; i < stacks.Count; i++)
                {
                    var currentSlot = stacks[i];
                    var mergeResult = StackManager.MergeStacks(currentSlot.Item, firstSlot.Item);
                    
                    if (mergeResult.Success)
                    {
                        // If full merge - clear source slot
                        if (mergeResult.ResultSource == null)
                        {
                            currentSlot.Item = null;
                        }
                        // If partial merge resulted in empty source
                        else if (mergeResult.ResultSource.StackSize == 0)
                        {
                            currentSlot.Item = null;
                        }
                        // Partial merge with remaining items - keep in slot
                    }
                }
            }
        }
        
        /// <summary>
        /// Consolidates stacks and fills empty slots at the end.
        /// After stacking, moves all items to front and empties end slots.
        /// </summary>
        public static void ConsolidateAndCompact(List<InventoryData.InventorySlot> slots)
        {
            if (slots == null || slots.Count == 0) return;
            
            // First auto-stack
            AutoStack(slots);
            
            // Then compact - move all items to front
            var items = slots
                .Where(s => s.Item != null)
                .Select(s => s.Item)
                .ToList();
            
            // Clear all slots
            foreach (var slot in slots)
            {
                slot.Item = null;
            }
            
            // Re-assign to front
            for (int i = 0; i < items.Count && i < slots.Count; i++)
            {
                slots[i].Item = items[i];
            }
        }
    }
}