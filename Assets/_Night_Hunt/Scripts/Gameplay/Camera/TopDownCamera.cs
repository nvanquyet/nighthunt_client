using UnityEngine;
using NightHunt.Networking;
using Cam = UnityEngine.Camera;

namespace NightHunt.Gameplay.Camera
{
    /// <summary>
    /// Top-down camera controller with rotation and zoom
    /// Supports Q/E rotation and mouse wheel zoom
    /// </summary>
    public class TopDownCamera : MonoBehaviour
    {
        [Header("Target Settings")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0, 20, 0);

        [Header("Rotation Settings")]
        [SerializeField] private float rotationSpeed = 90f;
        [SerializeField] private bool allowRotation = true;
        [SerializeField] private KeyCode rotateLeftKey = KeyCode.Q;
        [SerializeField] private KeyCode rotateRightKey = KeyCode.E;

        [Header("Zoom Settings")]
        [SerializeField] private float minZoom = 10f;
        [SerializeField] private float maxZoom = 30f;
        [SerializeField] private float zoomSpeed = 5f;
        [SerializeField] private float currentZoom = 20f;

        [Header("Smooth Settings")]
        [SerializeField] private float followSpeed = 10f;
        [SerializeField] private float rotationSmoothSpeed = 10f;

        private float currentRotationY = 0f;
        private Cam cam;

        private void Awake()
        {
            cam = GetComponent<Cam>();
            if (cam == null)
            {
                cam = Cam.main;
            }

            currentZoom = offset.magnitude;
        }

        private void Start()
        {
            // Find target if not set
            if (target == null)
            {
                // Try to find player
                var player = FindObjectOfType<NightHunt.Networking.NetworkPlayer>();
                if (player != null)
                {
                    target = player.transform;
                }
            }

            // Set initial position
            if (target != null)
            {
                transform.position = target.position + offset;
                transform.LookAt(target.position);
                currentRotationY = transform.eulerAngles.y;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            HandleRotation();
            HandleZoom();
            UpdateCameraPosition();
        }

        /// <summary>
        /// Handle camera rotation with Q/E keys
        /// </summary>
        private void HandleRotation()
        {
            if (!allowRotation) return;

            float rotationInput = 0f;

            if (UnityEngine.Input.GetKey(rotateLeftKey))
            {
                rotationInput = -1f;
            }
            else if (UnityEngine.Input.GetKey(rotateRightKey))
            {
                rotationInput = 1f;
            }

            // Also support mouse right button drag
            if (UnityEngine.Input.GetMouseButton(1))
            {
                rotationInput = UnityEngine.Input.GetAxis("Mouse X");
            }

            if (rotationInput != 0f)
            {
                currentRotationY += rotationInput * rotationSpeed * Time.deltaTime;
            }
        }

        /// <summary>
        /// Handle camera zoom with mouse wheel
        /// </summary>
        private void HandleZoom()
        {
            float scroll = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0f)
            {
                currentZoom -= scroll * zoomSpeed;
                currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);
            }
        }

        /// <summary>
        /// Update camera position and rotation
        /// </summary>
        private void UpdateCameraPosition()
        {
            // Calculate desired position
            float angleRad = currentRotationY * Mathf.Deg2Rad;
            Vector3 desiredOffset = new Vector3(
                Mathf.Sin(angleRad) * currentZoom,
                currentZoom,
                Mathf.Cos(angleRad) * currentZoom
            );

            Vector3 desiredPosition = target.position + desiredOffset;

            // Smoothly move camera
            transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);

            // Look at target
            Vector3 lookDirection = target.position - transform.position;
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSmoothSpeed * Time.deltaTime);
        }

        /// <summary>
        /// Set camera target
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        /// <summary>
        /// Set zoom level
        /// </summary>
        public void SetZoom(float zoom)
        {
            currentZoom = Mathf.Clamp(zoom, minZoom, maxZoom);
        }

        /// <summary>
        /// Set rotation
        /// </summary>
        public void SetRotation(float rotationY)
        {
            currentRotationY = rotationY;
        }

        /// <summary>
        /// Get current zoom
        /// </summary>
        public float GetZoom() => currentZoom;

        /// <summary>
        /// Get current rotation
        /// </summary>
        public float GetRotation() => currentRotationY;
    }
}

