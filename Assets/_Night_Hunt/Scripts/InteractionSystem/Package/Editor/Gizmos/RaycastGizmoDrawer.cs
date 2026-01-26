using UnityEngine;
using UnityEditor;
using NightHunt.InteractionSystem.Utilities;

namespace NightHunt.InteractionSystem.Editor.Gizmos
{
    /// <summary>
    /// Custom gizmo drawer for raycast visualization in Scene View.
    /// </summary>
    [CustomEditor(typeof(RaycastVisualizer))]
    public class RaycastGizmoDrawer : UnityEditor.Editor
    {
        private void OnSceneGUI()
        {
            RaycastVisualizer visualizer = (RaycastVisualizer)target;
            if (visualizer == null)
                return;

            // Draw distance label if needed
            // This is handled by RaycastVisualizer.OnDrawGizmos
        }
    }
}
