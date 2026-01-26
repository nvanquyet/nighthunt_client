using NightHunt.InteractionSystem.Core;
using NightHunt.InteractionSystem.Inventory;
using NightHunt.InteractionSystem.Items;

namespace NightHunt.InteractionSystem.Editor
{
#if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine;

    public class InventoryGridVisualizer : EditorWindow
    {
        private GridInventoryComponent targetInventory;
        private Vector2 scrollPosition;
        private float cellSize = 40f;

        [MenuItem("NightHunt/Tools/Inventory Grid Visualizer")]
        public static void ShowWindow()
        {
            GetWindow<InventoryGridVisualizer>("Inventory Grid");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Inventory Grid Visualizer", EditorStyles.boldLabel);

            targetInventory = (GridInventoryComponent)EditorGUILayout.ObjectField(
                "Target Inventory",
                targetInventory,
                typeof(GridInventoryComponent),
                true
            );

            if (targetInventory == null)
            {
                EditorGUILayout.HelpBox("Select a GridInventoryComponent to visualize", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            cellSize = EditorGUILayout.Slider("Cell Size", cellSize, 20f, 80f);

            EditorGUILayout.Space();
            DrawGrid();
        }

        private void DrawGrid()
        {
            int width = targetInventory.GridWidth;
            int height = targetInventory.GridHeight;

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            GUILayout.BeginVertical();

            for (int y = 0; y < height; y++)
            {
                GUILayout.BeginHorizontal();

                for (int x = 0; x < width; x++)
                {
                    DrawCell(x, y);
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void DrawCell(int x, int y)
        {
            bool isOccupied = IsCellOccupied(x, y, out ItemInstance? item);

            Color cellColor = isOccupied ? new Color(1f, 0.5f, 0.5f) : new Color(0.8f, 0.8f, 0.8f);

            GUI.backgroundColor = cellColor;

            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.alignment = TextAnchor.MiddleCenter;

            string label = "";
            if (isOccupied && item.HasValue)
            {
                label = GetItemDisplayName(item.Value);
            }

            GUILayout.Box(label, style, GUILayout.Width(cellSize), GUILayout.Height(cellSize));

            GUI.backgroundColor = Color.white;
        }

        private bool IsCellOccupied(int x, int y, out ItemInstance? item)
        {
            item = null;

            foreach (var itemInstance in targetInventory.Items)
            {
                Vector2Int? position = targetInventory.GetItemPosition(itemInstance.instanceId);
                if (!position.HasValue) continue;

                ItemDataBase itemData = ItemDatabaseManager.Instance?.GetItemData(itemInstance.itemDataId);
                if (itemData == null) continue;

                Vector2Int size = itemData.gridSize;

                if (x >= position.Value.x && x < position.Value.x + size.x &&
                    y >= position.Value.y && y < position.Value.y + size.y)
                {
                    item = itemInstance;
                    return true;
                }
            }

            return false;
        }

        private string GetItemDisplayName(ItemInstance item)
        {
            ItemDataBase data = ItemDatabaseManager.Instance?.GetItemData(item.itemDataId);
            if (data != null)
            {
                return data.displayName.Substring(0, Mathf.Min(3, data.displayName.Length));
            }

            return "?";
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }
    }
#endif
}