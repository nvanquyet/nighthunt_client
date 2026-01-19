using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;
using NightHunt.Gameplay.Input;
using NightHunt.Gameplay.Camera.Cinemachine;

namespace NightHunt.Gameplay.Camera
{
    /// <summary>
    /// Handles camera rotation input (Q/E keys) for Cinemachine virtual cameras
    /// </summary>
    public class CameraRotationInput : MonoBehaviour
    {
        [Header("Rotation Settings")]
        [SerializeField] private float rotationSpeed = 90f;

        private CinemachineCameraController cameraController;
        private InputAction rotateLeftAction;
        private InputAction rotateRightAction;
        private InputAction lookAction; // For mouse drag rotation

        private float currentRotationY = 0f;
        private bool isInitialized = false;

        /// <summary>
        /// Initialize with camera controller
        /// </summary>
        public void Initialize(CinemachineCameraController controller)
        {
            cameraController = controller;
            
            if (InputLayerManager.Instance != null)
            {
                var cameraMap = InputLayerManager.Instance.GetController(InputState.Camera);
                if (cameraMap != null)
                {
                    rotateLeftAction = cameraMap.GetAction("RotateLeft");
                    rotateRightAction = cameraMap.GetAction("RotateRight");
                    lookAction = cameraMap.GetAction("Look"); // Assuming 'Look' action is used for mouse drag
                }
            }

            isInitialized = true;
        }

        private void OnEnable()
        {
            if (!isInitialized) return;

            if (rotateLeftAction != null) rotateLeftAction.performed += OnRotateLeft;
            if (rotateRightAction != null) rotateRightAction.performed += OnRotateRight;
            if (lookAction != null) lookAction.performed += OnLookPerformed;
        }

        private void OnDisable()
        {
            if (rotateLeftAction != null) rotateLeftAction.performed -= OnRotateLeft;
            if (rotateRightAction != null) rotateRightAction.performed -= OnRotateRight;
            if (lookAction != null) lookAction.performed -= OnLookPerformed;
        }

        private void Update()
        {
            if (cameraController == null || !isInitialized) return;

            // Apply rotation to camera
            if (currentRotationY != 0f)
            {
                cameraController.RotateCamera(currentRotationY);
                currentRotationY = 0f; // Reset after applying
            }
        }

        private void OnRotateLeft(InputAction.CallbackContext context)
        {
            currentRotationY = -rotationSpeed * Time.deltaTime; // Negative for left rotation
        }

        private void OnRotateRight(InputAction.CallbackContext context)
        {
            currentRotationY = rotationSpeed * Time.deltaTime; // Positive for right rotation
        }

        private void OnLookPerformed(InputAction.CallbackContext context)
        {
            // Mouse X for horizontal rotation
            Vector2 lookDelta = context.ReadValue<Vector2>();
            currentRotationY += lookDelta.x * rotationSpeed * Time.deltaTime; // Adjust sensitivity as needed
        }
    }
}
