#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using NightHunt.InteractionSystem.Core.Abstractions;

namespace NightHunt.InteractionSystem.Editor.Gizmos
{
    /// <summary>
    /// Gizmo drawer for interaction range visualization.
    /// </summary>
    [CustomEditor(typeof(InteractableBase), true)]
    public class InteractionRangeGizmo : UnityEditor.Editor
    {
        private void OnSceneGUI()
        {
            InteractableBase interactable = (InteractableBase)target;
            if (interactable == null)
                return;

            Handles.color = Color.cyan;
            float range = interactable.GetInteractionRange();
            Handles.DrawWireDisc(interactable.transform.position, Vector3.up, range);

            // Draw label
            Handles.Label(
                interactable.transform.position + Vector3.up * (range + 0.5f),
                $"{interactable.GetInteractionType()}\n{range}m"
            );
        }
    }
}
#endif
