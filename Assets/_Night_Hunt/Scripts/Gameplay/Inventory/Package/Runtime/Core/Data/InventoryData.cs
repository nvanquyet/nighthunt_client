using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NightHunt.Inventory.Core
{
    /// <summary>
    /// List-based inventory data structure.
    /// NOT grid-based - each item occupies exactly one cell.
    /// </summary>
    [System.Serializable]
    public class InventoryData
    {
        [SerializeField] private int maxSlots = 20; // Config
        private List<InventorySlot> slots;
        
        public int MaxSlots => maxSlots;
        public int Count => slots.Count(s => s.Item != null);
        
        public InventoryData(int slotCount)
        {
            maxSlots = slotCount;
            slots = new List<InventorySlot>(maxSlots);
            
            for (int i = 0; i < maxSlots; i++)
            {
                slots.Add(new InventorySlot(i));
            }
        }
        
        public bool TryAddItem(ItemInstance item)
        {
            // Find empty slot
            var emptySlot = slots.FirstOrDefault(s => s.Item == null);
            if (emptySlot != null)
            {
                emptySlot.Item = item;
                return true;
            }
            return false; // Inventory full
        }
        
        public bool TryRemoveItem(string instanceId)
        {
            var slot = slots.FirstOrDefault(s => s.Item != null && s.Item.InstanceId == instanceId);
            if (slot != null)
            {
                slot.Item = null;
                return true;
            }
            return false;
        }
        
        public ItemInstance GetItem(string instanceId)
        {
            var slot = slots.FirstOrDefault(s => s.Item != null && s.Item.InstanceId == instanceId);
            return slot?.Item;
        }
        
        public bool HasItem(string instanceId)
        {
            return slots.Any(s => s.Item != null && s.Item.InstanceId == instanceId);
        }
        
        public List<InventorySlot> GetAllSlots() => slots;
        public int GetEmptySlotCount() => slots.Count(s => s.Item == null);
        
        public InventorySlot GetSlot(int index)
        {
            if (index >= 0 && index < slots.Count)
                return slots[index];
            return null;
        }
    }
}
