using UnityEngine;
using NightHunt.Inventory.Core.Interfaces;
using NightHunt.Inventory.Core.Events;
using System.Linq;

namespace NightHunt.Inventory.Interaction
{
    /// <summary>
    /// Detects interactable objects using dual raycast strategy:
    /// 1. Raycast from camera center (screen center)
    /// 2. Sphere overlap from player position
    /// 3. Pick closest to player if both hit
    /// </summary>
    public class InteractionDetector : MonoBehaviour
    {
        [Header("Raycast Configuration")]
        [SerializeField] private float cameraRaycastMaxDistance = 5f;
        [SerializeField] private float playerOverlapRadius = 2f;
        [SerializeField] private LayerMask interactableLayer;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugDraw = true;
        [SerializeField] private bool enableDebugLogs = false;
        
        private Camera mainCamera;
        private IInteractable currentInteractable;
        
        #region Lifecycle
        
        void Start()
        {
            mainCamera = Camera.main;
            
            if (mainCamera == null)
            {
                Debug.LogError("[InteractionDetector] No main camera found!");
            }
        }
        
        void Update()
        {
            DetectAndUpdateInteractable();
        }
        
        void OnDrawGizmos()
        {
            if (!enableDebugDraw) return;
            
            // Draw camera raycast
            if (mainCamera != null)
            {
                Gizmos.color = Color.green;
                Ray ray = mainCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2));
                Gizmos.DrawLine(ray.origin, ray.origin + ray.direction * cameraRaycastMaxDistance);
            }
            
            // Draw player overlap radius
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, playerOverlapRadius);
        }
        
        #endregion
        
        #region Detection
        
        /// <summary>
        /// Detects interactables using dual raycast.
        /// </summary>
        public IInteractable DetectInteractable()
        {
            IInteractable cameraHit = CameraRaycast();
            IInteractable playerHit = PlayerOverlapCheck();
            
            // Return closest to player
            if (cameraHit != null && playerHit != null)
            {
                float cameraDist = Vector3.Distance(transform.position, cameraHit.GetPosition());
                float playerDist = Vector3.Distance(transform.position, playerHit.GetPosition());
                
                return cameraDist < playerDist ? cameraHit : playerHit;
            }
            
            return cameraHit ?? playerHit;
        }
        
        private IInteractable CameraRaycast()
        {
            if (mainCamera == null) return null;
            
            Ray ray = mainCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2));
            
            if (enableDebugDraw)
            {
                Debug.DrawLine(ray.origin, ray.origin + ray.direction * cameraRaycastMaxDistance, Color.green);
            }
            
            if (Physics.Raycast(ray, out RaycastHit hit, cameraRaycastMaxDistance, interactableLayer))
            {
                var interactable = hit.collider.GetComponent<IInteractable>();
                
                if (interactable != null && enableDebugLogs)
                {
                    Debug.Log($"[InteractionDetector] Camera raycast hit: {hit.collider.name}");
                }
                
                return interactable;
            }
            
            return null;
        }
        
        private IInteractable PlayerOverlapCheck()
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, playerOverlapRadius, interactableLayer);
            
            if (colliders.Length == 0) return null;
            
            IInteractable closest = null;
            float closestDist = float.MaxValue;
            
            foreach (var col in colliders)
            {
                var interactable = col.GetComponent<IInteractable>();
                if (interactable != null)
                {
                    float dist = Vector3.Distance(transform.position, col.transform.position);
                    if (dist < closestDist)
                    {
                        closest = interactable;
                        closestDist = dist;
                    }
                }
            }
            
            if (closest != null && enableDebugLogs)
            {
                Debug.Log($"[InteractionDetector] Overlap found closest: {closestDist:F2}m");
            }
            
            return closest;
        }
        
        private void DetectAndUpdateInteractable()
        {
            var detected = DetectInteractable();
            
            // Check if changed
            if (detected != currentInteractable)
            {
                // Lost previous
                if (currentInteractable != null)
                {
                    InteractionEvents.InvokeInteractableLost();
                    
                    if (enableDebugLogs)
                        Debug.Log("[InteractionDetector] Lost interactable");
                }
                
                // Found new
                if (detected != null)
                {
                    InteractionEvents.InvokeInteractableDetected(detected);
                    
                    if (enableDebugLogs)
                        Debug.Log($"[InteractionDetector] Detected interactable");
                }
                
                currentInteractable = detected;
            }
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Gets the currently detected interactable.
        /// </summary>
        public IInteractable GetCurrentInteractable() => currentInteractable;
        
        /// <summary>
        /// Checks if any interactable is detected.
        /// </summary>
        public bool HasInteractable() => currentInteractable != null;
        
        #endregion
    }
}