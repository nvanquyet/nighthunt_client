using UnityEngine;
using UnityEditor;
using NightHunt.InteractionSystem.Inventory;

namespace NightHunt.InteractionSystem.Editor.Windows
{
    /// <summary>
    /// Editor window for visualizing inventory grid in Scene View.
    /// </summary>
    public class InventoryGridVisualizer : EditorWindow
    {
        private GridInventoryComponent targetInventory;

        [MenuItem("NightHunt/InteractionSystem/Inventory Grid Visualizer")]
        public static void ShowWindow()
        {
            GetWindow<InventoryGridVisualizer>("Inventory Grid Visualizer");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Inventory Grid Visualizer", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            targetInventory = (GridInventoryComponent)EditorGUILayout.ObjectField("Target Inventory", targetInventory, typeof(GridInventoryComponent), true);

            if (targetInventory == null)
            {
                EditorGUILayout.HelpBox("Select an inventory component to visualize.", MessageType.Info);
                return;
            }

            var (width, height) = targetInventory.GetGridSize();
            EditorGUILayout.LabelField($"Grid Size: {width}x{height}");
            EditorGUILayout.LabelField($"Items: {targetInventory.ItemCount}/{targetInventory.MaxSlots}");
            EditorGUILayout.LabelField($"Weight: {targetInventory.CurrentWeight:F1}/{targetInventory.MaxWeight:F1}");

            // Scene view will show grid via custom gizmos
            SceneView.RepaintAll();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (targetInventory == null)
                return;

            // Draw grid visualization
            // This would require access to internal grid data
            // For now, just show basic info
        }
    }
}
