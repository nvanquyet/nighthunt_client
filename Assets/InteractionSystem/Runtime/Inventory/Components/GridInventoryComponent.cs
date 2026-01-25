// using System.Collections.Generic;
// using FishNet.Object;
// using NightHunt.InteractionSystem.Core;
// using NightHunt.InteractionSystem.Items;
// using UnityEngine;
//
// namespace NightHunt.InteractionSystem.Inventory
// {
//     public class GridInventoryComponent : InventoryComponentBase
//     {
//         [Header("Grid Settings")]
//         [SerializeField] private int gridWidth = 10;
//         [SerializeField] private int gridHeight = 5;
//     
//         private bool[,] occupiedCells;
//         private Dictionary<string, (Vector2Int position, Vector2Int size)> itemPositions;
//     
//         public override int Capacity => gridWidth * gridHeight;
//         public int GridWidth => gridWidth;
//         public int GridHeight => gridHeight;
//     
//         public override void OnStartServer()
//         {
//             base.OnStartServer();
//         
//             occupiedCells = new bool[gridWidth, gridHeight];
//             itemPositions = new Dictionary<string, (Vector2Int, Vector2Int)>();
//         }
//     
//         [Server]
//         public bool TryAddItemAt(ItemInstance item, Vector2Int position)
//         {
//             ItemDataBase data = ItemDatabaseManager.Instance.GetItemData(item.itemDataId);
//             if (data == null) return false;
//         
//             Vector2Int size = data.gridSize;
//         
//             // Check if fits
//             if (!CanPlaceAt(position, size))
//             {
//                 return false;
//             }
//         
//             // Mark cells as occupied
//             MarkCells(position, size, true);
//         
//             // Add item
//             items.Add(item);
//             itemPositions[item.instanceId] = (position, size);
//         
//             OnItemAdded(item);
//             return true;
//         }
//     
//         [Server]
//         public override bool Try
// }