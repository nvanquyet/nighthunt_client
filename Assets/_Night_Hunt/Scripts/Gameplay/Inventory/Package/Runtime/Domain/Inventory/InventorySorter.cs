using System.Collections.Generic;
using System.Linq;
using NightHunt.Inventory.Core;

namespace NightHunt.Inventory.Domain
{
    /// <summary>
    /// Sorts inventory by item type.
    /// Order: Weapon → Armor → Consumable → Throwable → Attachment → Misc
    /// </summary>
    public static class InventorySorter
    {
        public static void SortByItemType(List<InventorySlot> slots)
        {
            var items = slots.Where(s => s.Item != null).Select(s => s.Item).ToList();
            
            // Sort by ItemType enum value
            items = items.OrderBy(i => (int)i.Definition.ItemType).ToList();
            
            // Clear all slots
            foreach (var slot in slots)
            {
                slot.Item = null;
            }
            
            // Re-assign sorted items
            for (int i = 0; i < items.Count; i++)
            {
                slots[i].Item = items[i];
            }
        }
    }
}
