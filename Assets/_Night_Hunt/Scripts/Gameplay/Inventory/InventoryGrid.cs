using System.Collections.Generic;
using UnityEngine;

namespace NightHunt.Gameplay.Inventory
{
    /// <summary>
    /// Grid layout management for inventory
    /// </summary>
    public class InventoryGrid
    {
        private readonly int width;
        private readonly int height;
        private readonly InventorySlot[,] slots;

        public int Width => width;
        public int Height => height;

        public InventoryGrid(int width, int height)
        {
            this.width = width;
            this.height = height;
            slots = new InventorySlot[width, height];

            // Initialize slots
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    slots[x, y] = new InventorySlot();
                }
            }
        }

        /// <summary>
        /// Get slot at position
        /// </summary>
        public InventorySlot GetSlot(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
                return null;

            return slots[x, y];
        }

        /// <summary>
        /// Check if position is valid
        /// </summary>
        public bool IsValidPosition(int x, int y)
        {
            return x >= 0 && x < width && y >= 0 && y < height;
        }

        /// <summary>
        /// Find empty slot
        /// </summary>
        public bool FindEmptySlot(out int x, out int y)
        {
            for (x = 0; x < width; x++)
            {
                for (y = 0; y < height; y++)
                {
                    if (slots[x, y].IsEmpty)
                    {
                        return true;
                    }
                }
            }

            x = -1;
            y = -1;
            return false;
        }

        /// <summary>
        /// Check if can place item at position
        /// </summary>
        public bool CanPlaceItem(int x, int y)
        {
            if (!IsValidPosition(x, y)) return false;
            return slots[x, y].IsEmpty;
        }

        /// <summary>
        /// Place item at position
        /// </summary>
        public bool PlaceItem(int x, int y, InventorySlot slot)
        {
            if (!CanPlaceItem(x, y)) return false;

            slots[x, y] = slot;
            return true;
        }

        /// <summary>
        /// Remove item at position
        /// </summary>
        public InventorySlot RemoveItem(int x, int y)
        {
            if (!IsValidPosition(x, y)) return null;

            var slot = slots[x, y];
            slots[x, y] = new InventorySlot();
            return slot;
        }

        /// <summary>
        /// Get all items
        /// </summary>
        public List<InventorySlot> GetAllItems()
        {
            var items = new List<InventorySlot>();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (!slots[x, y].IsEmpty)
                    {
                        items.Add(slots[x, y]);
                    }
                }
            }

            return items;
        }

        /// <summary>
        /// Clear all slots
        /// </summary>
        public void Clear()
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    slots[x, y].Clear();
                }
            }
        }
    }
}

