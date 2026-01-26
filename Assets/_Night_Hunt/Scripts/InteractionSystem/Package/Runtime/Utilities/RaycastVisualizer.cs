using UnityEngine;

namespace NightHunt.InteractionSystem.Utilities
{
    /// <summary>
    /// Visualizes raycasts in Scene View for debugging.
    /// </summary>
    public class RaycastVisualizer : MonoBehaviour
    {
        [Header("Visualization Settings")]
        [SerializeField] private bool showRays = true;
        [SerializeField] private float rayLength = 5f;
        [SerializeField] private Color validHitColor = Color.green;
        [SerializeField] private Color invalidHitColor = Color.red;
        [SerializeField] private Color noHitColor = Color.yellow;

        private Camera targetCamera;
        private RaycastHit lastHit;
        private bool lastHitValid = false;

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        /// <summary>
        /// Update raycast visualization.
        /// </summary>
        public void UpdateRaycast(RaycastHit hit, bool isValid)
        {
            lastHit = hit;
            lastHitValid = isValid;
        }

        /// <summary>
        /// Clear raycast visualization.
        /// </summary>
        public void ClearRaycast()
        {
            lastHitValid = false;
        }

        private void OnDrawGizmos()
        {
            if (!showRays || targetCamera == null)
                return;

            Vector3 start = targetCamera.transform.position;
            Vector3 direction = targetCamera.transform.forward;
            Vector3 end = start + direction * rayLength;

            Color rayColor = noHitColor;
            if (lastHitValid)
            {
                rayColor = lastHit.collider != null ? validHitColor : invalidHitColor;
                end = lastHit.point;
            }

            Gizmos.color = rayColor;
            Gizmos.DrawLine(start, end);

            // Draw hit point
            if (lastHitValid && lastHit.collider != null)
            {
                Gizmos.DrawSphere(lastHit.point, 0.1f);
            }
        }
    }
}
