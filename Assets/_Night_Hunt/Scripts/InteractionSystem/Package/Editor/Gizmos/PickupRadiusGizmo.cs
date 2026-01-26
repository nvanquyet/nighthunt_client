#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using NightHunt.InteractionSystem.Pickup.Detection;

namespace NightHunt.InteractionSystem.Editor.Gizmos
{
    /// <summary>
    /// Gizmo drawer for auto pickup radius visualization.
    /// </summary>
    [CustomEditor(typeof(AutoPickupTrigger), true)]
    public class PickupRadiusGizmo : UnityEditor.Editor
    {
        private void OnSceneGUI()
        {
            AutoPickupTrigger trigger = (AutoPickupTrigger)target;
            if (trigger == null)
                return;

            // Get radius from trigger
            SphereCollider collider = trigger.GetComponent<SphereCollider>();
            if (collider == null)
                return;

            float radius = collider.radius;
            Handles.color = new Color(0f, 1f, 0f, 0.3f);
            Handles.DrawWireDisc(trigger.transform.position, Vector3.up, radius);
            
            // Draw label
            Handles.Label(
                trigger.transform.position + Vector3.up * (radius + 0.5f),
                $"Auto Pickup Radius: {radius}m"
            );
        }
    }
}
#endif
