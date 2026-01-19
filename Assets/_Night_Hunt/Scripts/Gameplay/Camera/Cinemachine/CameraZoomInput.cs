using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;
using NightHunt.Gameplay.Input;
using NightHunt.Gameplay.Camera.Cinemachine;

namespace NightHunt.Gameplay.Camera
{
    /// <summary>
    /// Handles camera zoom input (mouse wheel) for Cinemachine virtual cameras
    /// </summary>
    public class CameraZoomInput : MonoBehaviour
    {
        [Header("Zoom Settings")]
        [SerializeField] private float zoomSpeed = 5f;

        private CinemachineCameraController cameraController;
        private InputAction zoomAction;
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
                    zoomAction = cameraMap.GetAction("Zoom");
                }
            }

            isInitialized = true;
        }

        private void OnEnable()
        {
            if (!isInitialized) return;

            if (zoomAction != null) zoomAction.performed += OnZoomPerformed;
        }

        private void OnDisable()
        {
            if (zoomAction != null) zoomAction.performed -= OnZoomPerformed;
        }

        private void OnZoomPerformed(InputAction.CallbackContext context)
        {
            if (cameraController == null) return;

            float scrollValue = context.ReadValue<Vector2>().y; // Mouse scroll wheel Y-axis
            cameraController.ZoomCamera(scrollValue);
        }
    }
}
