using System.Collections.Generic;
using FishNet.Object;
using NightHunt.InteractionSystem.Core;
using NightHunt.InteractionSystem.Items;
using UnityEngine;

namespace NightHunt.InteractionSystem.Inventory
{
    public class GridInventoryComponent : InventoryComponentBase
    {
        [Header("Grid Settings")] [SerializeField]
        private int gridWidth = 10;

        [SerializeField] private int gridHeight = 5;

        private bool[,] occupiedCells;
        private Dictionary<string, (Vector2Int position, Vector2Int size)> itemPositions;

        public override int Capacity => gridWidth * gridHeight;
        public int GridWidth => gridWidth;
        public int GridHeight => gridHeight;

        public override void OnStartServer()
        {
            base.OnStartServer();

            occupiedCells = new bool[gridWidth, gridHeight];
            itemPositions = new Dictionary<string, (Vector2Int, Vector2Int)>();
        }

        [Server]
        public bool TryAddItemAt(ItemInstance item, Vector2Int position)
        {
            ItemDataBase data = ItemDatabaseManager.Instance.GetItemData(item.itemDataId);
            if (data == null) return false;

            Vector2Int size = data.gridSize;

            // Check if fits
            if (!CanPlaceAt(position, size))
            {
                return false;
            }

            // Mark cells as occupied
            MarkCells(position, size, true);

            // Add item
            items.Add(item);
            itemPositions[item.instanceId] = (position, size);

            OnItemAdded(item);
            return true;
        }

        [Server]
        public override bool TryAddItem(ItemInstance item)
        {
        // Auto-find position
            Vector2Int? position = FindEmptyPosition(item);
            if (!position.HasValue)
            {
                return false;
            }

            return TryAddItemAt(item, position.Value);
        }

        [Server]
        public override bool TryRemoveItem(string instanceId)
        {
            if (!itemPositions.TryGetValue(instanceId, out var positionData))
            {
                return false;
            }

            // Free cells
            MarkCells(positionData.position, positionData.size, false);

            // Remove item
            itemPositions.Remove(instanceId);

            return base.TryRemoveItem(instanceId);
        }

        private bool CanPlaceAt(Vector2Int position, Vector2Int size)
        {
            // Check bounds
            if (position.x + size.x > gridWidth || position.y + size.y > gridHeight)
            {
                return false;
            }

            // Check if cells are free
            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    if (occupiedCells[position.x + x, position.y + y])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private Vector2Int? FindEmptyPosition(ItemInstance item)
        {
            ItemDataBase data = ItemDatabaseManager.Instance.GetItemData(item.itemDataId);
            if (data == null) return null;

            Vector2Int size = data.gridSize;

            // Try to find empty spot (top-left to bottom-right)
            for (int y = 0; y <= gridHeight - size.y; y++)
            {
                for (int x = 0; x <= gridWidth - size.x; x++)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    if (CanPlaceAt(pos, size))
                    {
                        return pos;
                    }
                }
            }

            return null;
        }

        private void MarkCells(Vector2Int position, Vector2Int size, bool occupied)
        {
            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    occupiedCells[position.x + x, position.y + y] = occupied;
                }
            }
        }

        public Vector2Int? GetItemPosition(string instanceId)
        {
            if (itemPositions.TryGetValue(instanceId, out var data))
            {
                return data.position;
            }

            return null;
        }
        
        public void ClearInventory()
        {
            occupiedCells = new bool[gridWidth, gridHeight];
            itemPositions.Clear();
            items.Clear();
        }
    }
}