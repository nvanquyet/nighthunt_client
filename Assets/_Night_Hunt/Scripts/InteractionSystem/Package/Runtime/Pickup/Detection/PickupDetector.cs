using UnityEngine;
using NightHunt.InteractionSystem.Core.Interfaces;
using NightHunt.InteractionSystem.Pickup.Handlers;
using NightHunt.InteractionSystem.Events;
using NightHunt.InteractionSystem.Utilities;

namespace NightHunt.InteractionSystem.Pickup.Detection
{
    /// <summary>
    /// Detects pickupable items via raycast from camera.
    /// Only detects items implementing IPickupable (separate from IInteractable).
    /// </summary>
    public class PickupDetector : MonoBehaviour
    {
        [Header("Settings")] [SerializeField] private PickupSettings settings;

        [Header("Camera")] [SerializeField] private Camera playerCamera;

        [Header("Pickup Handler")] [SerializeField]
        private PickupHandler pickupHandler;
        // Note: UI removed - gameplay UI should subscribe to InteractionEvents.OnPickupTargetChanged

        private IPickupable currentTarget;

        private void Awake()
        {
            if (pickupHandler == null)
            {
                // Use centralized component finder to search in hierarchy
                pickupHandler = ComponentFinder.FindComponentInHierarchy<PickupHandler>(gameObject, includeInactive: false);
                if (pickupHandler == null)
                {
                    Debug.LogWarning("[PickupDetector] PickupHandler not found! Pickup functionality will be limited.");
                }
            }

            // Find settings if not assigned
            if (settings == null)
            {
                settings = FindObjectOfType<PickupSettings>();
                if (settings == null)
                {
                    Debug.LogWarning("[PickupDetector] PickupSettings not found! Using default values.");
                }
            }
        }

        private void Start()
        {
            // Find camera in Start (after network spawn) as fallback
            FindCamera();
        }

        private void Update()
        {
            // Ensure camera is found (in case it wasn't ready during Start)
            if (playerCamera == null)
            {
                FindCamera();
            }

            DetectPickupable();
            // Input handling is done by InteractionInputHandler (New Input System)
        }

        /// <summary>
        /// Find player camera with multiple fallback strategies.
        /// </summary>
        private void FindCamera()
        {
            // If already assigned, use it
            if (playerCamera != null)
                return;

            // Strategy 1: Use Camera.main (if available)
            if (Camera.main != null)
            {
                playerCamera = Camera.main;
                return;
            }

            // Strategy 2: Find Camera in hierarchy (this object, parent, children, root)
            playerCamera = ComponentFinder.FindComponentInHierarchy<Camera>(gameObject, includeInactive: false);
            if (playerCamera != null)
                return;

            // Strategy 4: Find any Camera with "MainCamera" tag
            GameObject mainCamObj = GameObject.FindGameObjectWithTag("MainCamera");
            if (mainCamObj != null)
            {
                playerCamera = mainCamObj.GetComponent<Camera>();
                if (playerCamera != null)
                    return;
            }

            // Strategy 5: Find first active Camera in scene
            Camera[] allCameras = FindObjectsOfType<Camera>();
            foreach (Camera cam in allCameras)
            {
                if (cam.enabled && cam.gameObject.activeInHierarchy)
                {
                    playerCamera = cam;
                    return;
                }
            }

            // If still null, log warning
            if (playerCamera == null)
            {
                Debug.LogWarning(
                    $"[PickupDetector] Could not find player camera! GameObject: {gameObject.name}. Please assign camera manually in Inspector.");
            }
        }

        /// <summary>
        /// Detect pickupable items via raycast.
        /// </summary>
        private void DetectPickupable()
        {
            // Try to find camera if still null
            if (playerCamera == null)
            {
                FindCamera();
                if (playerCamera == null)
                    return;
            }

            IPickupable newTarget = null;
            float closestDistance = float.MaxValue;

            // Get settings values
            float detectionRange = settings != null ? settings.PickupDetectionRange : 5f;
            LayerMask pickupLayers = settings != null ? settings.PickupLayers : -1;

            // Raycast from camera center
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, detectionRange, pickupLayers))
            {
                // Check if hit object implements IPickupable
                IPickupable pickupable = hit.collider.GetComponent<IPickupable>();
                if (pickupable != null)
                {
                    // Check if player can pickup
                    if (pickupable.CanPickup(gameObject))
                    {
                        float distance = Vector3.Distance(transform.position, hit.point);
                        if (distance <= pickupable.GetPickupRange())
                        {
                            newTarget = pickupable;
                            closestDistance = distance;
                        }
                    }
                }
            }

            // Update current target
            if (currentTarget != newTarget)
            {
                if (currentTarget != null)
                {
                    InteractionEvents.InvokePickupTargetLost();
                }

                currentTarget = newTarget;

                if (currentTarget != null)
                {
                    string promptText = $"Press F to Pickup {currentTarget.GetDisplayName()}";
                    InteractionEvents.InvokePickupTargetChanged(currentTarget, promptText);
                }
            }
        }


        /// <summary>
        /// Try to pickup the current target.
        /// </summary>
        public void TryPickup()
        {
            Debug.Log($"[PickupDetector] TryPickup called - currentTarget={currentTarget != null}, pickupHandler={pickupHandler != null}");
            
            if (currentTarget == null)
            {
                Debug.LogWarning("[PickupDetector] currentTarget is null! Cannot pickup.");
                return;
            }

            if (!currentTarget.CanPickup(gameObject))
            {
                Debug.LogWarning($"[PickupDetector] Cannot pickup {currentTarget.GetDisplayName()} - CanPickup returned false");
                return;
            }

            if (pickupHandler != null)
            {
                Debug.Log($"[PickupDetector] Calling RequestPickup for {currentTarget.GetDisplayName()}");
                pickupHandler.RequestPickup(currentTarget);
            }
            else
            {
                Debug.LogWarning("[PickupDetector] PickupHandler not available!");
            }
        }

        // Note: UI methods removed - gameplay UI subscribes to InteractionEvents

        /// <summary>
        /// Get the current pickup target.
        /// </summary>
        public IPickupable GetCurrentTarget()
        {
            return currentTarget;
        }

        /// <summary>
        /// Get raycast info for gizmo drawing (Editor only).
        /// </summary>
        public void GetRaycastInfo(out Vector3 start, out Vector3 end, out bool hasHit, out RaycastHit hit)
        {
            if (playerCamera == null)
            {
                start = Vector3.zero;
                end = Vector3.zero;
                hasHit = false;
                hit = new RaycastHit();
                return;
            }

            float detectionRange = settings != null ? settings.PickupDetectionRange : 5f;
            LayerMask pickupLayers = settings != null ? settings.PickupLayers : -1;

            start = playerCamera.transform.position;
            Vector3 direction = playerCamera.transform.forward;
            Ray ray = new Ray(start, direction);
            hasHit = Physics.Raycast(ray, out hit, detectionRange, pickupLayers);
            end = hasHit ? hit.point : (start + direction * detectionRange);
        }

        /// <summary>
        /// Get detection settings for gizmo drawing (Editor only).
        /// </summary>
        public void GetDetectionSettings(out float range, out LayerMask layers, out Camera cam)
        {
            range = settings != null ? settings.PickupDetectionRange : 5f;
            layers = settings != null ? settings.PickupLayers : -1;
            cam = playerCamera;
        }

        [Header("Debug")] [SerializeField] private bool showGizmos = true;
        [SerializeField] private bool showGizmosOnlyWhenSelected = false;

        private RaycastHit lastHit;
        private bool lastHitValid = false;

        private void OnDrawGizmos()
        {
            if (!showGizmos || showGizmosOnlyWhenSelected)
                return;

            DrawRaycastGizmo();
        }

        private void OnDrawGizmosSelected()
        {
            if (!showGizmos)
                return;

            DrawRaycastGizmo();
        }

        private void DrawRaycastGizmo()
        {
            if (playerCamera == null)
                return;

            float detectionRange = settings != null ? settings.PickupDetectionRange : 5f;
            LayerMask pickupLayers = settings != null ? settings.PickupLayers : -1;

            Vector3 start = playerCamera.transform.position;
            Vector3 direction = playerCamera.transform.forward;
            Vector3 end = start + direction * detectionRange;

            // Perform raycast to get hit point
            Ray ray = new Ray(start, direction);
            RaycastHit hit;
            bool hasHit = Physics.Raycast(ray, out hit, detectionRange, pickupLayers);

            if (hasHit)
            {
                end = hit.point;
                lastHit = hit;
                lastHitValid = true;

                // Check if hit object is pickupable
                IPickupable pickupable = hit.collider.GetComponent<IPickupable>();
                if (pickupable != null && pickupable.CanPickup(gameObject))
                {
                    float distance = Vector3.Distance(transform.position, hit.point);
                    if (distance <= pickupable.GetPickupRange())
                    {
                        // Valid pickupable target - green
                        Gizmos.color = Color.green;
                        Gizmos.DrawLine(start, end);
                        Gizmos.DrawWireSphere(end, 0.2f);
                        return;
                    }
                    else
                    {
                        // Out of range - yellow
                        Gizmos.color = Color.yellow;
                    }
                }
                else
                {
                    // Hit something but not pickupable - red
                    Gizmos.color = Color.red;
                }
            }
            else
            {
                // No hit - cyan
                Gizmos.color = Color.cyan;
                lastHitValid = false;
            }

            // Draw raycast line
            Gizmos.DrawLine(start, end);

            // Draw hit point if valid
            if (hasHit)
            {
                Gizmos.DrawWireSphere(end, 0.15f);
            }

            // Draw detection range sphere at end
            Gizmos.DrawWireSphere(end, 0.1f);
        }
    }
}