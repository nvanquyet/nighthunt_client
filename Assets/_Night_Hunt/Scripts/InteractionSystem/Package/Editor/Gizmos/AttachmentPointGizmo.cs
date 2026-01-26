#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using NightHunt.InteractionSystem.Items.Attachments;

namespace NightHunt.InteractionSystem.Editor.Gizmos
{
    /// <summary>
    /// Gizmo drawer for attachment points visualization.
    /// </summary>
    [CustomEditor(typeof(AttachmentSlot), true)]
    public class AttachmentPointGizmo : UnityEditor.Editor
    {
        private void OnSceneGUI()
        {
            AttachmentSlot slot = (AttachmentSlot)target;
            if (slot == null)
                return;

            Transform attachmentPoint = slot.transform;
            if (attachmentPoint == null)
                return;

            // Draw axis
            Handles.color = Color.red;
            Handles.DrawLine(attachmentPoint.position, attachmentPoint.position + attachmentPoint.right * 0.2f);
            Handles.color = Color.green;
            Handles.DrawLine(attachmentPoint.position, attachmentPoint.position + attachmentPoint.up * 0.2f);
            Handles.color = Color.blue;
            Handles.DrawLine(attachmentPoint.position, attachmentPoint.position + attachmentPoint.forward * 0.2f);

            // Draw label
            Handles.Label(
                attachmentPoint.position + Vector3.up * 0.3f,
                slot.GetSlotType().ToString()
            );
        }
    }
}
#endif
