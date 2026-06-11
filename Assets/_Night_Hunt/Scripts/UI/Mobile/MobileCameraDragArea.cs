using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using NightHunt.Config;
using NightHunt.Gameplay.Input.Handlers.Camera;
using NightHunt.Gameplay.Camera;

namespace NightHunt.UI.Mobile
{
    /// <summary>
    /// Full-screen invisible touch-drag area for mobile camera rotation.
    /// Drag anywhere on this panel and the Cinemachine camera receives mouse-delta input.
    ///
    /// Keep this object behind buttons/joysticks in the HUD hierarchy. Buttons consume their
    /// own touches; empty HUD space reaches this drag area.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class MobileCameraDragArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Sensitivity")]
        [Tooltip("Input delta injected per screen pixel dragged horizontally. Increase for faster spin.")]
        [SerializeField] private float _degreesPerPixel = 0.25f;

        private Mouse _mouse;
        private CameraInputHandler _handler;
        private CameraStateManager _cameraStateManager;
        private bool _addedVirtualMouse;
        private bool _dragging;
        private bool _missingHandlerLogged;
        private float _nextDragLogTime;
        private float _effectiveDegreesPerPixel;

        private void Awake()
        {
            ConfigureRaycastImage();
            ApplySettingsFromConfig();
        }

        private void OnEnable()
        {
            ConfigureRaycastImage();
            ApplySettingsFromConfig();
            GameSettings.OnSettingsChanged += ApplySettingsFromConfig;
            EnsureVirtualMouse();
        }

        private void Start()
        {
            ConfigureRaycastImage();
            EnsureVirtualMouse();
        }

        private void OnDisable()
        {
            GameSettings.OnSettingsChanged -= ApplySettingsFromConfig;
            _dragging = false;
        }

        private void OnDestroy()
        {
            UnbindHandler();

            if (_addedVirtualMouse && _mouse != null)
            {
                InputSystem.RemoveDevice(_mouse);
                _mouse = null;
                _addedVirtualMouse = false;
            }
        }

        public void BindHandler(CameraInputHandler handler)
        {
            _handler = handler;
            _missingHandlerLogged = false;
            Debug.Log($"[MobileCameraDragArea] Bind camera handler={(handler != null ? "ok" : "null")} inputEnabled={handler?.IsInputEnabled.ToString() ?? "n/a"} active={isActiveAndEnabled}");
        }

        public void BindCameraStateManager(CameraStateManager cameraStateManager)
        {
            _cameraStateManager = cameraStateManager;
            Debug.Log($"[MobileCameraDragArea] Bind camera state manager={(cameraStateManager != null ? cameraStateManager.name : "null")} state={cameraStateManager?.CurrentState.ToString() ?? "n/a"}");
        }

        public void UnbindHandler()
        {
            _handler = null;
            _cameraStateManager = null;
            _dragging = false;
            _missingHandlerLogged = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (NightHunt.GameplaySystems.UI.Combat.ThrowableAimController.IsAnyAimingActive)
            {
                _dragging = false;
                return;
            }

            _dragging = true;
            EnsureVirtualMouse();

            if (Time.unscaledTime >= _nextDragLogTime)
            {
                _nextDragLogTime = Time.unscaledTime + 0.75f;
                Debug.Log($"[MobileCameraDragArea] PointerDown pointer={eventData.pointerId} pos={eventData.position:F1} handler={(_handler != null ? "ok" : "null")} inputEnabled={_handler?.IsInputEnabled.ToString() ?? "n/a"} virtualMouse={(_mouse != null ? "ok" : "null")}");
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _dragging = false;

            // FIX: Move the virtual cursor off-screen so no UI element stays in a hovered
            // (blue-highlighted) state after the finger is lifted.  Without this, the last
            // drag position leaves hover events on whatever button/panel was under the finger,
            // producing persistent blue highlight "traces" on the HUD.
            if (_mouse != null)
            {
                InputSystem.QueueStateEvent(_mouse,
                    new MouseState { position = new Vector2(-9999f, -9999f) });
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (NightHunt.GameplaySystems.UI.Combat.ThrowableAimController.IsAnyAimingActive)
            {
                _dragging = false;
                return;
            }

            if (!_dragging)
                return;

            EnsureVirtualMouse();
            if (_mouse == null)
                return;

            // Pinch zoom owns two-finger gestures.
            if (UnityEngine.Input.touchCount > 1)
                return;

            if (_handler == null && !_missingHandlerLogged)
            {
                _missingHandlerLogged = true;
                Debug.LogWarning("[MobileCameraDragArea] CameraInputHandler is not bound. Drag still injects virtual mouse delta, but MobileHUDPanel should bind it for diagnostics.");
            }

            if (_handler != null && !_handler.IsInputEnabled && Time.unscaledTime >= _nextDragLogTime)
            {
                _nextDragLogTime = Time.unscaledTime + 0.75f;
                Debug.LogWarning("[MobileCameraDragArea] Drag received while CameraInputHandler input is disabled. If the camera does not rotate, check CameraStateManager and active input layer.");
            }

            if (_cameraStateManager != null && _cameraStateManager.IsRotationLocked() && Time.unscaledTime >= _nextDragLogTime)
            {
                _nextDragLogTime = Time.unscaledTime + 0.75f;
                Debug.LogWarning($"[MobileCameraDragArea] Drag received while camera state is {_cameraStateManager.CurrentState}. CinemachineInputAxisController is locked, so mobile camera rotation will not move until the state returns to Free.");
            }

            Vector2 delta = new Vector2(eventData.delta.x * _effectiveDegreesPerPixel, 0f);
            if (delta.sqrMagnitude < 0.0001f)
                return;

            InputSystem.QueueStateEvent(_mouse, new MouseState { delta = delta });

            if (Time.unscaledTime >= _nextDragLogTime)
            {
                _nextDragLogTime = Time.unscaledTime + 0.75f;
                Debug.Log($"[MobileCameraDragArea] Drag delta={eventData.delta:F1} injected={delta:F2} sensitivity={_effectiveDegreesPerPixel:F2} handler={(_handler != null ? "ok" : "null")} inputEnabled={_handler?.IsInputEnabled.ToString() ?? "n/a"}");
            }
        }

        private void ApplySettingsFromConfig()
        {
            _effectiveDegreesPerPixel = GameSettings.Instance != null
                ? GameSettings.Instance.MobileCameraDegreesPerPixel
                : _degreesPerPixel;
        }

        private void ConfigureRaycastImage()
        {
            var img = GetComponent<Image>();
            if (img == null)
                return;

            var c = img.color;
            c.a = 0f;
            img.color = c;
            img.raycastTarget = true;
        }

        private void EnsureVirtualMouse()
        {
            if (_mouse != null)
                return;

            string existingMouse = Mouse.current != null ? Mouse.current.displayName : "none";
            _mouse = InputSystem.AddDevice<Mouse>();
            _addedVirtualMouse = true;
            Debug.Log($"[MobileCameraDragArea] Virtual mouse device added for camera rotation. previousMouse={existingMouse}");
        }
    }
}
