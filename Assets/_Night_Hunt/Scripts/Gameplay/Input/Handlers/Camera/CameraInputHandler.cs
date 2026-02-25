using UnityEngine;
using UnityEngine.InputSystem;
using NightHunt.Gameplay.Input.Core;

namespace NightHunt.Gameplay.Input.Handlers.Camera
{
    /// <summary>
    /// Handles camera input (Rotation, Zoom)
    /// Used for both normal gameplay and spectator mode
    /// </summary>
    public class CameraInputHandler : MonoBehaviour, IInputHandler
    {
        [Header("Camera Settings")]
        [SerializeField] private float rotationSpeed = 90f;
        [SerializeField] private float zoomSpeed = 5f;

        private InputActionMap cameraActionMap;
        private InputAction rotateLeftAction;
        private InputAction rotateRightAction;
        private InputAction lookAction; // Mouse drag rotation
        private InputAction zoomAction;

        private float currentRotationY = 0f;
        private bool inputEnabled = false;

        // Events
        public event System.Action<float> OnRotate; // Rotation delta
        public event System.Action<float> OnZoom;   // Zoom delta

        #region Lifecycle

        private void Awake()
        {
            InitializeActions();
        }

        private void OnEnable()
        {
            RegisterWithManager();
        }

        private void OnDisable()
        {
            DisableInput();
            UnregisterFromManager();
        }

        private void Update()
        {
            if (!inputEnabled) return;

            // Apply rotation if any
            if (currentRotationY != 0f)
            {
                OnRotate?.Invoke(currentRotationY);
                currentRotationY = 0f;
            }
        }

        #endregion

        #region Initialization

        private void InitializeActions()
        {
            if (InputLayerManager.Instance == null)
            {
                Debug.LogError("[CameraInputHandler] InputLayerManager.Instance is null!");
                return;
            }

            cameraActionMap = InputLayerManager.Instance.CameraMap;

            if (cameraActionMap != null)
            {
                rotateLeftAction = cameraActionMap.FindAction("RotateLeft");
                rotateRightAction = cameraActionMap.FindAction("RotateRight");
                lookAction = cameraActionMap.FindAction("Look");
                zoomAction = cameraActionMap.FindAction("Zoom");
            }
            else
            {
                Debug.LogError("[CameraInputHandler] 'Camera' action map not found!");
            }
        }

        private void RegisterWithManager()
        {
            InputLayerManager.Instance?.RegisterHandler(this);
        }

        private void UnregisterFromManager()
        {
            InputLayerManager.Instance?.UnregisterHandler(this);
        }

        #endregion

        #region IInputHandler Implementation

        public bool IsInputEnabled => inputEnabled;

        public InputActionMap GetActionMap() => cameraActionMap;

        public void EnableInput()
        {
            if (inputEnabled) return;

            // Retry nếu Awake chạy trước khi InputLayerManager sẵn sàng
            if (cameraActionMap == null) InitializeActions();
            if (cameraActionMap == null)
            {
                Debug.LogError("[CameraInputHandler] cameraActionMap null – không thể EnableInput!");
                return;
            }

            inputEnabled = true;

            if (rotateLeftAction != null)
                rotateLeftAction.performed += OnRotateLeftPerformed;

            if (rotateRightAction != null)
                rotateRightAction.performed += OnRotateRightPerformed;

            if (lookAction != null)
                lookAction.performed += OnLookPerformed;

            if (zoomAction != null)
                zoomAction.performed += OnZoomPerformed;

            Debug.Log("[CameraInputHandler] Input enabled");
        }

        public void DisableInput()
        {
            if (!inputEnabled) return;

            inputEnabled = false;

            if (rotateLeftAction != null)
                rotateLeftAction.performed -= OnRotateLeftPerformed;

            if (rotateRightAction != null)
                rotateRightAction.performed -= OnRotateRightPerformed;

            if (lookAction != null)
                lookAction.performed -= OnLookPerformed;

            if (zoomAction != null)
                zoomAction.performed -= OnZoomPerformed;

            currentRotationY = 0f;

            Debug.Log("[CameraInputHandler] Input disabled");
        }

        #endregion

        #region Input Event Handlers

        private void OnRotateLeftPerformed(InputAction.CallbackContext context)
        {
            currentRotationY = -rotationSpeed * Time.deltaTime;
        }

        private void OnRotateRightPerformed(InputAction.CallbackContext context)
        {
            currentRotationY = rotationSpeed * Time.deltaTime;
        }

        private void OnLookPerformed(InputAction.CallbackContext context)
        {
            Vector2 lookDelta = context.ReadValue<Vector2>();
            currentRotationY += lookDelta.x * rotationSpeed * Time.deltaTime;
        }

        private void OnZoomPerformed(InputAction.CallbackContext context)
        {
            float scrollValue = context.ReadValue<Vector2>().y;
            OnZoom?.Invoke(scrollValue);
        }

        #endregion
    }
}