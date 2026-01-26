#if UNITY_EDITOR
using NightHunt.InteractionSystem.Pickup;
using UnityEditor;
using UnityEngine;

namespace NightHunt.InteractionSystem.Editor
{
    public class PickupRadiusGizmo : MonoBehaviour
    {
        [SerializeField] private bool showAutoPickupRadius = true;
    
        private void OnDrawGizmos()
        {
            if (!showAutoPickupRadius) return;
        
            AutoPickupTrigger trigger = GetComponent<AutoPickupTrigger>();
            if (trigger == null) return;
        
            PickupSettings settings = GetComponentInChildren<PickupSettings>();
            if (settings == null) return;
        
            Color color = settings.autoPickupEnabled 
                ? new Color(0, 1, 0, 0.2f) 
                : new Color(1, 0, 0, 0.2f);
        
            Gizmos.color = color;
            Gizmos.DrawSphere(transform.position, settings.autoPickupRadius);
        
            Handles.color = settings.autoPickupEnabled ? Color.green : Color.red;
            Handles.DrawWireDisc(transform.position, Vector3.up, settings.autoPickupRadius);
        
            Handles.Label(
                transform.position + Vector3.up * 2f,
                $"Auto Pickup: {(settings.autoPickupEnabled ? "ON" : "OFF")}\nRadius: {settings.autoPickupRadius}m"
            );
        }
    }
#endif
}