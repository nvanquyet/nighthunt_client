#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using NightHunt.InteractionSystem.Core;

namespace NightHunt.InteractionSystem.Editor
{
    public class AttachmentPointGizmo : MonoBehaviour
    {
        [SerializeField] private AttachmentSlotDefinition[] slots;
        [SerializeField] private bool showLabels = true;

        private void OnDrawGizmos()
        {
            if (slots == null) return;

            foreach (var slot in slots)
            {
                Vector3 worldPos = transform.TransformPoint(slot.attachmentPointOffset);

                // Draw axis
                Gizmos.color = Color.red;
                Gizmos.DrawLine(worldPos, worldPos + transform.right * 0.1f);

                Gizmos.color = Color.green;
                Gizmos.DrawLine(worldPos, worldPos + transform.up * 0.1f);

                Gizmos.color = Color.blue;
                Gizmos.DrawLine(worldPos, worldPos + transform.forward * 0.1f);

                // Draw label
                if (showLabels)
                {
                    Handles.Label(worldPos + Vector3.up * 0.15f, slot.slotType.ToString());
                }
            }
        }
    }
}
#endif