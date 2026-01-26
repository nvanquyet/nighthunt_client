using System.Collections.Generic;
using UnityEngine;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Events;

namespace NightHunt.InteractionSystem.Inventory
{
    /// <summary>
    /// Grid-based inventory component.
    /// </summary>
    public class GridInventoryComponent : InventoryComponentBase
    {
        [Header("Grid Settings")]
        [SerializeField] private int gridWidth = 4;
        [SerializeField] private int gridHeight = 3;

        private ItemInstance?[,] grid;
        private Dictionary<string, ItemDataBase> itemDataCache = new Dictionary<string, ItemDataBase>();

        protected override void Awake()
        {
            base.Awake();
            InitializeGrid();
        }

        /// <summary>
        /// Initialize the grid.
        /// </summary>
        private void InitializeGrid()
        {
            grid = new ItemInstance?[gridWidth, gridHeight];
            maxSlots = gridWidth * gridHeight;
            
            // Sync items list with grid
            SyncItemsListFromGrid();
        }

        /// <summary>
        /// Sync items list from grid (for event system).
        /// </summary>
        private void SyncItemsListFromGrid()
        {
            items.Clear();
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    if (grid[x, y].HasValue)
                    {
                        items.Add(grid[x, y].Value);
                    }
                }
            }
        }

        /// <summary>
        /// Add an item to the inventory.
        /// </summary>
        public override bool AddItem(ItemInstance item)
        {
            if (!CanAddItem(item))
                return false;

            ItemDataBase itemData = GetItemData(item.itemDataId);
            if (itemData == null)
                return false;

            // Try to stack with existing item
            if (itemData.IsStackable)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    for (int y = 0; y < gridHeight; y++)
                    {
                        if (grid[x, y].HasValue)
                        {
                            var existing = grid[x, y].Value;
                            if (existing.itemDataId == item.itemDataId)
                            {
                                int canAdd = itemData.MaxStack - existing.quantity;
                                if (canAdd > 0)
                                {
                                    int toAdd = Mathf.Min(item.quantity, canAdd);
                                    var updated = existing.WithQuantity(existing.quantity + toAdd);
                                    grid[x, y] = updated;

                                    if (toAdd < item.quantity)
                                    {
                                        // Still have items left, create new stack
                                        var remaining = item.WithQuantity(item.quantity - toAdd);
                                        return AddItem(remaining);
                                    }

                                    UpdateWeight();
                                    InventoryEvents.InvokeItemQuantityChanged(updated, updated.quantity);
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            // Find empty slot
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    if (!grid[x, y].HasValue)
                    {
                        grid[x, y] = item;
                        currentWeight += itemData.GetTotalWeight(item.quantity);
                        items.Add(item);
                        UpdateWeight();
                        InventoryEvents.InvokeItemAdded(item);
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Remove an item from the inventory.
        /// </summary>
        public override bool RemoveItem(string itemId, int quantity = 1)
        {
            ItemDataBase itemData = GetItemData(itemId);
            if (itemData == null)
                return false;

            int remaining = quantity;

            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    if (grid[x, y].HasValue)
                    {
                        var item = grid[x, y].Value;
                        if (item.itemDataId == itemId)
                        {
                            int toRemove = Mathf.Min(remaining, item.quantity);
                            int newQuantity = item.quantity - toRemove;
                            remaining -= toRemove;

                            if (newQuantity <= 0)
                            {
                                grid[x, y] = null;
                                items.Remove(item);
                                InventoryEvents.InvokeItemRemoved(item, toRemove);
                            }
                            else
                            {
                                var updatedItem = item.WithQuantity(newQuantity);
                                grid[x, y] = updatedItem;
                                // Update in items list
                                int index = items.IndexOf(item);
                                if (index >= 0)
                                {
                                    items[index] = updatedItem;
                                }
                                InventoryEvents.InvokeItemQuantityChanged(updatedItem, newQuantity);
                            }

                            currentWeight -= itemData.GetTotalWeight(toRemove);

                            if (remaining <= 0)
                            {
                                UpdateWeight();
                                return true;
                            }
                        }
                    }
                }
            }

            UpdateWeight();
            return remaining < quantity; // Return true if at least some items were removed
        }

        /// <summary>
        /// Check if an item can be added.
        /// </summary>
        public override bool CanAddItem(ItemInstance item)
        {
            ItemDataBase itemData = GetItemData(item.itemDataId);
            if (itemData == null)
                return false;

            return HasSpace(item, itemData);
        }

        /// <summary>
        /// Get item at grid position.
        /// </summary>
        public ItemInstance? GetItemAt(int x, int y)
        {
            if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight)
                return null;

            return grid[x, y];
        }

        /// <summary>
        /// Place item at grid position.
        /// </summary>
        public bool PlaceItemAt(int x, int y, ItemInstance item)
        {
            if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight)
                return false;

            if (grid[x, y].HasValue)
                return false;

            grid[x, y] = item;
            ItemDataBase itemData = GetItemData(item.itemDataId);
            if (itemData != null)
            {
                currentWeight += itemData.GetTotalWeight(item.quantity);
            }
            UpdateWeight();
            return true;
        }

        /// <summary>
        /// Remove item from grid position.
        /// </summary>
        public bool RemoveItemAt(int x, int y)
        {
            if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight)
                return false;

            if (!grid[x, y].HasValue)
                return false;

            var item = grid[x, y].Value;
            ItemDataBase itemData = GetItemData(item.itemDataId);
            if (itemData != null)
            {
                currentWeight -= itemData.GetTotalWeight(item.quantity);
            }

            grid[x, y] = null;
            UpdateWeight();
            return true;
        }

        /// <summary>
        /// Get item data (with caching).
        /// </summary>
        private ItemDataBase GetItemData(string itemId)
        {
            if (itemDataCache.ContainsKey(itemId))
                return itemDataCache[itemId];

            // Load from resources or database
            // TODO: Implement proper item data loading
            return null;
        }

        /// <summary>
        /// Get grid dimensions.
        /// </summary>
        public (int width, int height) GetGridSize()
        {
            return (gridWidth, gridHeight);
        }
    }
}
