using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NightHunt.Inventory.Core.Data
{
    /// <summary>
    /// List-based inventory data structure.
    /// Each slot can contain one item (or stack).
    /// </summary>
    [Serializable]
    public class InventoryData
    {
        [SerializeField] 
        private int maxSlots = 20;
        
        [SerializeField]
        private List<InventorySlot> slots;
        
        /// <summary>
        /// Represents a single slot in the inventory.
        /// </summary>
        [Serializable]
        public class InventorySlot
        {
            public int Index;
            public ItemInstance Item; // Null if empty
            
            public bool IsEmpty => Item == null;
        }
        
        /// <summary>
        /// Creates a new inventory with the specified number of slots.
        /// </summary>
        public InventoryData(int slotCount)
        {
            maxSlots = slotCount;
            slots = new List<InventorySlot>(maxSlots);
            
            for (int i = 0; i < maxSlots; i++)
            {
                slots.Add(new InventorySlot { Index = i, Item = null });
            }
        }
        
        /// <summary>
        /// Tries to add an item to the first available empty slot.
        /// </summary>
        public bool TryAddItem(ItemInstance item)
        {
            var emptySlot = slots.Find(s => s.Item == null);
            if (emptySlot != null)
            {
                emptySlot.Item = item;
                return true;
            }
            return false; // Inventory full
        }
        
        /// <summary>
        /// Tries to add an item to a specific slot index.
        /// </summary>
        public bool TryAddItemAtIndex(ItemInstance item, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= maxSlots)
                return false;
            
            if (slots[slotIndex].Item != null)
                return false; // Slot occupied
            
            slots[slotIndex].Item = item;
            return true;
        }
        
        /// <summary>
        /// Removes an item from the inventory by instance ID.
        /// </summary>
        public bool RemoveItem(string instanceId)
        {
            var slot = slots.Find(s => s.Item != null && s.Item.InstanceId == instanceId);
            if (slot != null)
            {
                slot.Item = null;
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// Removes an item from a specific slot index.
        /// </summary>
        public ItemInstance RemoveItemAtIndex(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= maxSlots)
                return null;
            
            var item = slots[slotIndex].Item;
            slots[slotIndex].Item = null;
            return item;
        }
        
        /// <summary>
        /// Gets an item by instance ID.
        /// </summary>
        public ItemInstance GetItem(string instanceId)
        {
            var slot = slots.Find(s => s.Item != null && s.Item.InstanceId == instanceId);
            return slot?.Item;
        }
        
        /// <summary>
        /// Gets an item at a specific slot index.
        /// </summary>
        public ItemInstance GetItemAtIndex(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= maxSlots)
                return null;
            
            return slots[slotIndex].Item;
        }
        
        /// <summary>
        /// Checks if the inventory contains an item by instance ID.
        /// </summary>
        public bool HasItem(string instanceId)
        {
            return slots.Any(s => s.Item != null && s.Item.InstanceId == instanceId);
        }
        
        /// <summary>
        /// Gets all slots in the inventory.
        /// </summary>
        public List<InventorySlot> GetAllSlots() => slots;
        
        /// <summary>
        /// Gets all non-empty items in the inventory.
        /// </summary>
        public List<ItemInstance> GetAllItems()
        {
            return slots.Where(s => s.Item != null).Select(s => s.Item).ToList();
        }
        
        /// <summary>
        /// Gets the number of empty slots.
        /// </summary>
        public int GetEmptySlotCount() => slots.Count(s => s.Item == null);
        
        /// <summary>
        /// Gets the total number of slots.
        /// </summary>
        public int GetMaxSlots() => maxSlots;
        
        /// <summary>
        /// Clears all items from the inventory.
        /// </summary>
        public void Clear()
        {
            foreach (var slot in slots)
            {
                slot.Item = null;
            }
        }
        
        /// <summary>
        /// Swaps items between two slots.
        /// </summary>
        public void SwapSlots(int slotA, int slotB)
        {
            if (slotA < 0 || slotA >= maxSlots || slotB < 0 || slotB >= maxSlots)
                return;
            
            (slots[slotA].Item, slots[slotB].Item) = (slots[slotB].Item, slots[slotA].Item);
        }
    }
}