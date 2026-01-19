using UnityEngine;
using Unity.Cinemachine;
using NightHunt.Networking;

namespace NightHunt.Gameplay.Camera.Cinemachine
{
    /// <summary>
    /// Main camera controller with Cinemachine 3.x
    /// Manages third-person camera with 45 degree angle
    /// </summary>
    public class CinemachineCameraController : MonoBehaviour
    {
        [Header("Camera References")]
        [SerializeField] private CinemachineBrain cinemachineBrain;
        [SerializeField] private CinemachineCamera playerCamera;

        [Header("Camera Settings")]
        [SerializeField] private float defaultDistance = 15f;
        [SerializeField] private float defaultHeight = 10f;
        [SerializeField] private float rotationSpeed = 90f;
        [SerializeField] private float zoomSpeed = 2f;
        [SerializeField] private float minZoom = 5f;
        [SerializeField] private float maxZoom = 25f;

        private NetworkPlayer networkPlayer;
        private CameraRotationInput rotationInput;
        private CameraZoomInput zoomInput;
        private bool isInitialized = false;

        // Cinemachine 3.x components
        private CinemachineOrbitalFollow followComponent;
        private CinemachineRotationComposer rotationComposer;

        private void Awake()
        {
            networkPlayer = GetComponentInParent<NetworkPlayer>();
            
            // Find or create CinemachineBrain
            if (cinemachineBrain == null)
            {
                cinemachineBrain = FindFirstObjectByType<CinemachineBrain>();
                if (cinemachineBrain == null)
                {
                    var mainCamera = UnityEngine.Camera.main;
                    if (mainCamera != null)
                    {
                        cinemachineBrain = mainCamera.GetComponent<CinemachineBrain>();
                        if (cinemachineBrain == null)
                        {
                            cinemachineBrain = mainCamera.gameObject.AddComponent<CinemachineBrain>();
                        }
                    }
                }
            }
        }

        private void Start()
        {
            InitializeCamera();
        }

        /// <summary>
        /// Initialize camera setup
        /// </summary>
        private void InitializeCamera()
        {
            if (isInitialized) return;

            // Setup player camera if not assigned
            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<CinemachineCamera>();
            }

            if (playerCamera == null)
            {
                Debug.LogError("[CinemachineCameraController] CinemachineCamera not found!");
                return;
            }

            // Get Cinemachine 3.x components
            followComponent = playerCamera.GetComponent<CinemachineOrbitalFollow>();
            rotationComposer = playerCamera.GetComponent<CinemachineRotationComposer>();

            // Add components if missing
            if (followComponent == null)
            {
                followComponent = playerCamera.gameObject.AddComponent<CinemachineOrbitalFollow>();
            }

            if (rotationComposer == null)
            {
                rotationComposer = playerCamera.gameObject.AddComponent<CinemachineRotationComposer>();
            }

            // Setup camera with 45 degree angle
            SetupCameraAngle();

            // Initialize input handlers
            rotationInput = GetComponent<CameraRotationInput>();
            if (rotationInput == null)
            {
                rotationInput = gameObject.AddComponent<CameraRotationInput>();
            }

            zoomInput = GetComponent<CameraZoomInput>();
            if (zoomInput == null)
            {
                zoomInput = gameObject.AddComponent<CameraZoomInput>();
            }

            rotationInput.Initialize(this);
            zoomInput.Initialize(this);

            isInitialized = true;
        }

        /// <summary>
        /// Setup camera with 45 degree angle (isometric-like)
        /// </summary>
        private void SetupCameraAngle()
        {
            if (playerCamera == null || followComponent == null) return;

            // Set camera offset for 45 degree angle
            //followComponent.FollowOffset = new Vector3(0, defaultHeight, -defaultDistance);

            // Set rotation composer
            if (rotationComposer != null)
            {
                rotationComposer.TargetOffset = Vector3.zero;
            }

            // Set camera rotation to 45 degrees down
            var cameraTransform = playerCamera.transform;
            cameraTransform.rotation = Quaternion.Euler(45f, 0f, 0f);
        }

        /// <summary>
        /// Rotate camera (Q/E input)
        /// </summary>
        public void RotateCamera(float direction)
        {
            if (playerCamera == null || followComponent == null) return;
            Debug.Log($"[CinemachineCameraController] RotateCamera called with delta: {direction}");

            // Rotate around Y axis
            //Vector3 offset = followComponent.FollowOffset;
            // float angle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            // angle += direction * rotationSpeed * Time.deltaTime;
            //
            // float distance = new Vector3(offset.x, 0, offset.z).magnitude;
            // offset.x = Mathf.Sin(angle * Mathf.Deg2Rad) * distance;
            // offset.z = Mathf.Cos(angle * Mathf.Deg2Rad) * distance;
            // offset.y = defaultHeight; // Keep height constant
            
            //followComponent.FollowOffset = offset;
        }

        /// <summary>
        /// Zoom camera in/out
        /// </summary>
        public void ZoomCamera(float zoomDelta)
        {
            if (playerCamera == null || followComponent == null) return;
            Debug.Log($"[CinemachineCameraController] ZoomCamera called with delta: {zoomDelta}");
            // if (playerCamera == null || followComponent == null) return;
            //
            // Vector3 offset = followComponent.FollowOffset;
            // float currentDistance = new Vector3(offset.x, 0, offset.z).magnitude;
            // float newDistance = Mathf.Clamp(currentDistance - zoomDelta * zoomSpeed, minZoom, maxZoom);
            //
            // // Maintain 45 degree angle
            // float angle = Mathf.Atan2(offset.x, offset.z);
            // offset.x = Mathf.Sin(angle) * newDistance;
            // offset.z = Mathf.Cos(angle) * newDistance;
            //
            // followComponent.FollowOffset = offset;
        }

        /// <summary>
        /// Set camera follow target
        /// </summary>
        public void SetFollowTarget(Transform target)
        {
            if (playerCamera != null)
            {
                // In Cinemachine 3.x, use Target property
                playerCamera.Target.TrackingTarget = target;
                playerCamera.Target.LookAtTarget = target;
            }
        }

        /// <summary>
        /// Enable/disable camera
        /// </summary>
        public void SetCameraEnabled(bool enabled)
        {
            if (playerCamera != null)
            {
                playerCamera.enabled = enabled;
            }
        }

        /// <summary>
        /// Get camera component
        /// </summary>
        public CinemachineCamera GetCamera() => playerCamera;
    }
}