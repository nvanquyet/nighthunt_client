using System.Collections.Generic;
using System.Linq;
using NightHunt.Inventory.Core.Data;

namespace NightHunt.Inventory.Core.Utilities
{
    public static class InventoryStacker
    {
        public static void AutoStack(List<ItemInstance> items)
        {
            // Group stackable items by ItemId
            var stackableGroups = items
                .Where(i => i.Definition.IsStackable)
                .GroupBy(i => i.Definition.ItemId);
            
            foreach (var group in stackableGroups)
            {
                var itemList = group.ToList();
                if (itemList.Count <= 1) continue;
                
                // Merge all into first stack
                var primaryStack = itemList[0];
                for (int i = 1; i < itemList.Count; i++)
                {
                    var result = StackManager.MergeStacks(itemList[i], primaryStack);
                    if (result.Success && result.ResultSource == null)
                    {
                        // Fully merged - remove empty stack
                        items.Remove(itemList[i]);
                    }
                }
            }
        }
    }
}