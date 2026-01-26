#if UNITY_EDITOR
using NightHunt.InteractionSystem.Core;
using UnityEditor;
using UnityEngine;

namespace NightHunt.InteractionSystem.Editor
{
    public class InteractionRangeGizmo : MonoBehaviour
    {
        [SerializeField] private bool showGizmo = true;
        [SerializeField] private Color gizmoColor = new Color(0, 1, 0, 0.3f);

        private void OnDrawGizmos()
        {
            if (!showGizmo) return;

            InteractableBase interactable = GetComponent<InteractableBase>();
            if (interactable == null) return;

            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position, interactable.InteractionDistance);

            // Draw type label
            Handles.Label(
                transform.position + Vector3.up * 1.5f,
                $"{interactable.InteractionType}\n{interactable.InteractionDistance}m"
            );
        }

        private void OnDrawGizmosSelected()
        {
            InteractableBase interactable = GetComponent<InteractableBase>();
            if (interactable == null) return;

            // Draw detailed gizmo when selected
            Handles.color = Color.green;
            Handles.DrawWireDisc(transform.position, Vector3.up, interactable.InteractionDistance);

            // Draw prompt
            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.white;
            style.fontSize = 14;
            style.fontStyle = FontStyle.Bold;

            Handles.Label(
                transform.position + Vector3.up * 2f,
                interactable.InteractionPrompt,
                style
            );
        }
    }
#endif
}