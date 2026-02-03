using System.Collections.Generic;
using System.Linq;
using NightHunt.Inventory.Core;

namespace NightHunt.Inventory.Domain
{
    /// <summary>
    /// Auto-stacks all stackable items.
    /// Strategy: Merge all instances of same ItemId into first found stack.
    /// </summary>
    public static class InventoryStacker
    {
        public static void AutoStack(List<InventorySlot> slots)
        {
            var stackableGroups = slots
                .Where(s => s.Item != null && s.Item.Definition.IsStackable)
                .GroupBy(s => s.Item.Definition.ItemId);
            
            foreach (var group in stackableGroups)
            {
                var stacks = group.ToList();
                if (stacks.Count <= 1) continue;
                
                // Merge all into first stack
                var firstSlot = stacks[0];
                for (int i = 1; i < stacks.Count; i++)
                {
                    var result = StackManager.MergeStacks(stacks[i].Item, firstSlot.Item);
                    
                    if (result.Success && result.ResultSource == null)
                    {
                        // Full merge - clear source slot
                        stacks[i].Item = null;
                    }
                    else if (result.Success && result.ResultSource != null && result.ResultSource.StackSize == 0)
                    {
                        // Partial merge resulted in empty source
                        stacks[i].Item = null;
                    }
                    // If partial merge with remaining items, keep in slot
                }
            }
        }
    }
}
