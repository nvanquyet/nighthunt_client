using System.Collections.Generic;
using System.Linq;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;

namespace NightHunt.Inventory.Domain.Inventory
{
    /// <summary>
    /// Utility class for sorting inventory items.
    /// </summary>
    public static class InventorySorter
    {
        /// <summary>
        /// Sorts inventory by item type.
        /// Order: Weapon → Armor → Consumable → Throwable → Attachment → Misc
        /// </summary>
        public static void SortByItemType(List<InventoryData.InventorySlot> slots)
        {
            if (slots == null || slots.Count == 0) return;
            
            // Extract all items
            var items = slots
                .Where(s => s.Item != null)
                .Select(s => s.Item)
                .OrderBy(i => (int)i.Definition.ItemType)
                .ThenBy(i => i.Definition.ItemId)
                .ToList();
            
            // Clear all slots
            foreach (var slot in slots)
            {
                slot.Item = null;
            }
            
            // Re-assign sorted items
            for (int i = 0; i < items.Count && i < slots.Count; i++)
            {
                slots[i].Item = items[i];
            }
        }
        
        /// <summary>
        /// Sorts inventory by weight (heaviest first).
        /// </summary>
        public static void SortByWeightDescending(List<InventoryData.InventorySlot> slots)
        {
            if (slots == null || slots.Count == 0) return;
            
            var items = slots
                .Where(s => s.Item != null)
                .Select(s => s.Item)
                .OrderByDescending(i => i.GetTotalWeight())
                .ToList();
            
            foreach (var slot in slots)
            {
                slot.Item = null;
            }
            
            for (int i = 0; i < items.Count && i < slots.Count; i++)
            {
                slots[i].Item = items[i];
            }
        }
        
        /// <summary>
        /// Sorts inventory by name alphabetically.
        /// </summary>
        public static void SortByNameAlphabetical(List<InventoryData.InventorySlot> slots)
        {
            if (slots == null || slots.Count == 0) return;
            
            var items = slots
                .Where(s => s.Item != null)
                .Select(s => s.Item)
                .OrderBy(i => i.Definition.ItemId)
                .ToList();
            
            foreach (var slot in slots)
            {
                slot.Item = null;
            }
            
            for (int i = 0; i < items.Count && i < slots.Count; i++)
            {
                slots[i].Item = items[i];
            }
        }
    }
}