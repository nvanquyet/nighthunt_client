using System.Collections.Generic;
using UnityEngine;

namespace NightHunt.InteractionSystem.Utilities
{
    public class RaycastVisualizer : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool enableVisualization = true;
    [SerializeField] private float rayDuration = 0.1f;
    
    [Header("Colors")]
    [SerializeField] private Color hitValidColor = Color.green;
    [SerializeField] private Color hitInvalidColor = Color.red;
    [SerializeField] private Color noHitColor = Color.yellow;
    
    [Header("Gizmo Settings")]
    [SerializeField] private float hitPointRadius = 0.1f;
    [SerializeField] private bool showDistanceLabel = true;
    
    private List<RaycastDebugInfo> activeRays = new List<RaycastDebugInfo>();
    
    private struct RaycastDebugInfo
    {
        public Vector3 origin;
        public Vector3 direction;
        public float distance;
        public bool hasHit;
        public Vector3 hitPoint;
        public RaycastHitType hitType;
        public float timestamp;
    }
    
    public enum RaycastHitType
    {
        ValidTarget,
        InvalidTarget,
        NoHit
    }
    
    public void DrawRaycast(Vector3 origin, Vector3 direction, float distance, bool hasHit, RaycastHit hit, RaycastHitType hitType)
    {
        if (!enableVisualization) return;
        
        RaycastDebugInfo info = new RaycastDebugInfo
        {
            origin = origin,
            direction = direction,
            distance = distance,
            hasHit = hasHit,
            hitPoint = hasHit ? hit.point : origin + direction * distance,
            hitType = hitType,
            timestamp = Time.time
        };
        
        activeRays.Add(info);
        
        // Draw immediate
        Color color = GetColorForHitType(hitType);
        Debug.DrawRay(origin, direction * distance, color, rayDuration);
    }
    
    private void Update()
    {
        // Clean old rays
        activeRays.RemoveAll(r => Time.time - r.timestamp > rayDuration);
    }
    
    private void OnDrawGizmos()
    {
        if (!enableVisualization) return;
        
        foreach (var ray in activeRays)
        {
            Color color = GetColorForHitType(ray.hitType);
            Gizmos.color = color;
            
            // Draw ray line
            Gizmos.DrawLine(ray.origin, ray.hitPoint);
            
            // Draw hit point sphere
            if (ray.hasHit)
            {
                Gizmos.DrawWireSphere(ray.hitPoint, hitPointRadius);
            }
            
            // Draw distance label
            #if UNITY_EDITOR
            if (showDistanceLabel)
            {
                float distance = Vector3.Distance(ray.origin, ray.hitPoint);
                UnityEditor.Handles.Label(ray.hitPoint, $"{distance:F2}m");
            }
            #endif
        }
    }
    
    private Color GetColorForHitType(RaycastHitType hitType)
    {
        return hitType switch
        {
            RaycastHitType.ValidTarget => hitValidColor,
            RaycastHitType.InvalidTarget => hitInvalidColor,
            RaycastHitType.NoHit => noHitColor,
            _ => Color.white
        };
    }
}
}