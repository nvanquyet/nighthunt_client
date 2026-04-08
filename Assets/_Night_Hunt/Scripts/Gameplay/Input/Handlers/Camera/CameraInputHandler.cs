using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
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
        [FormerlySerializedAs("rotationSpeed")]
        [SerializeField] private float _rotationSpeed = 90f;
#pragma warning disable CS0414
        [FormerlySerializedAs("zoomSpeed")]
        [SerializeField] private float _zoomSpeed = 5f;
#pragma warning restore CS0414

        private InputActionMap cameraActionMap;
        private InputAction rotateLeftAction;
        private InputAction rotateRightAction;
        private InputAction lookAction; // Mouse drag rotation
        private InputAction zoomAction;

        private float currentRotationY = 0f;
        private bool inputEnabled = false;

        // Raw look value polled every Update (works for both mouse delta and analog stick).
        private Vector2 _rawLookValue;

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

            // Poll lookAction every frame so analog stick (OnScreenStick → rightStick)
            // produces continuous rotation while held, and mouse delta works identically.
            if (lookAction != null)
                _rawLookValue = lookAction.ReadValue<Vector2>();

            currentRotationY += _rawLookValue.x * _rotationSpeed * Time.deltaTime;

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
                // InputSystem_Actions uses "MouseDelta" + "ZoomInOut" in Camera map.
                lookAction = cameraActionMap.FindAction("MouseDelta");
                zoomAction = cameraActionMap.FindAction("ZoomInOut");
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

        public InputActionMap GetActionMap()
        {
            if (cameraActionMap == null && InputLayerManager.Instance != null)
                cameraActionMap = InputLayerManager.Instance.CameraMap;
            return cameraActionMap;
        }

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
            else
            {
                Debug.Log("[CameraInputHandler] Enabling input with action map: " + cameraActionMap.name);
            }

            inputEnabled = true;

            if (rotateLeftAction != null)
                rotateLeftAction.performed += OnRotateLeftPerformed;

            if (rotateRightAction != null)
                rotateRightAction.performed += OnRotateRightPerformed;

            // lookAction is polled in Update — no callback subscription needed.

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

            // lookAction was not subscribed — nothing to unsubscribe.

            if (zoomAction != null)
                zoomAction.performed -= OnZoomPerformed;

            currentRotationY = 0f;
            _rawLookValue = Vector2.zero;

            Debug.Log("[CameraInputHandler] Input disabled");
        }

        #endregion

        #region Input Event Handlers

        private void OnRotateLeftPerformed(InputAction.CallbackContext context)
        {
            currentRotationY = -_rotationSpeed * Time.deltaTime;
        }

        private void OnRotateRightPerformed(InputAction.CallbackContext context)
        {
            currentRotationY = _rotationSpeed * Time.deltaTime;
        }

        private void OnZoomPerformed(InputAction.CallbackContext context)
        {
            // ZoomInOut is an Axis (float)
            float scrollValue = context.ReadValue<float>();
            OnZoom?.Invoke(scrollValue);
        }

        #endregion

        #region Mobile API

        /// <summary>
        /// Fire a one-shot zoom event from MobilePinchZoomBridge.
        /// No-op when input is disabled so it is safe to call unconditionally.
        /// </summary>
        public void SetMobileZoom(float delta)
        {
            if (inputEnabled) OnZoom?.Invoke(delta);
        }

        #endregion

    }
}