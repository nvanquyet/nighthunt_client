using System;
using System.Collections.Generic;
using NightHunt.Inventory.Core.Data;

namespace NightHunt.Inventory.Core.Utilities
{
    public static class InventorySorter
    {
        public static void SortByItemType(List<ItemInstance> items)
        {
            items.Sort((a, b) =>
            {
                int typeCompare = a.Definition.ItemType.CompareTo(b.Definition.ItemType);
                if (typeCompare != 0) return typeCompare;
                
                return String.CompareOrdinal(a.Definition.ItemId, b.Definition.ItemId);
            });
        }
    }
}