using UnityEngine;
using NightHunt.Inventory.Core;

namespace NightHunt.Inventory.Interaction
{
    /// <summary>
    /// Dual raycast detection for interactables.
    /// 1. Raycast from camera center (screen center)
    /// 2. Sphere overlap from player position
    /// 3. Pick closest to player if both hit
    /// </summary>
    public class InteractionDetector : MonoBehaviour
    {
        [Header("Raycast Config")]
        [SerializeField] private float cameraRaycastMaxDistance = 5f;
        [SerializeField] private float playerOverlapRadius = 2f;
        [SerializeField] private LayerMask interactableLayer;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugDraw = true;
        
        /// <summary>
        /// Detect interactables using dual raycast strategy.
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
            if (Camera.main == null) return null;
            
            Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width/2, Screen.height/2));
            
            if (enableDebugDraw)
            {
                Debug.DrawLine(ray.origin, ray.origin + ray.direction * cameraRaycastMaxDistance, Color.green);
            }
            
            if (Physics.Raycast(ray, out RaycastHit hit, cameraRaycastMaxDistance, interactableLayer))
            {
                return hit.collider.GetComponent<IInteractable>();
            }
            return null;
        }
        
        private IInteractable PlayerOverlapCheck()
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, playerOverlapRadius, interactableLayer);
            
            if (enableDebugDraw)
            {
                // Debug draw handled in OnDrawGizmos
            }
            
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
            
            return closest;
        }
        
        private void OnDrawGizmos()
        {
            if (!enableDebugDraw) return;
            
            // Draw camera raycast
            if (Camera.main != null)
            {
                Gizmos.color = Color.green;
                Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width/2, Screen.height/2));
                Gizmos.DrawLine(ray.origin, ray.origin + ray.direction * cameraRaycastMaxDistance);
            }
            
            // Draw player overlap radius
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, playerOverlapRadius);
        }
    }
}
