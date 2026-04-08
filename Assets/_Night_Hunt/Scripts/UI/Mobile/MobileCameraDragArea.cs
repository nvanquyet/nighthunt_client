using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace NightHunt.UI.Mobile
{
    /// <summary>
    /// Full-screen invisible touch-drag area for mobile camera rotation.
    /// Drag anywhere on this panel → camera rotates.
    ///
    /// Works by injecting simulated mouse-delta state events into InputSystem so that
    /// CinemachineInputAxisController transparently picks them up via the Camera/"MouseDelta"
    /// action (bound to &lt;Mouse&gt;/delta). No Cinemachine internals are touched.
    ///
    /// SETUP in HUD Canvas:
    ///   1. Create an Image GameObject → anchors Stretch/Stretch (full screen).
    ///   2. Image color alpha = 0 (invisible). Raycast Target = CHECKED.
    ///   3. Attach this component.
    ///   4. Make it the FIRST child in the Canvas (lowest sibling index = rendered
    ///      behind all buttons/joysticks). Touches on joysticks go to the joystick
    ///      (EventSystem gives priority to the topmost element that handles the event);
    ///      touches on empty space fall through to this panel.
    ///
    /// PC:
    ///   On PC a real Mouse device already exists; the drag area silently does nothing
    ///   on PC (camera rotation is handled by real mouse movement as normal).
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class MobileCameraDragArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Sensitivity")]
        [Tooltip("Rotation degrees per screen pixel dragged horizontally. Increase for faster spin.")]
        [SerializeField] private float _degreesPerPixel = 0.25f;

        private Mouse _mouse;
        private bool _addedVirtualMouse;
        private bool _dragging;

        // ─────────────────────────────────────────────────────────────────────
        //  Unity
        // ─────────────────────────────────────────────────────────────────────

        private void Start()
        {
            // Make Image transparent but keep Raycast Target so EventSystem routes touches here.
            var img = GetComponent<Image>();
            if (img != null)
            {
                var c = img.color;
                c.a = 0f;
                img.color = c;
                img.raycastTarget = true;
            }

            // On mobile there is no physical Mouse device.
            // Adding a virtual one lets any InputAction bound to <Mouse>/delta
            // receive our injected events — including CinemachineInputAxisController.
            if (Mouse.current != null)
            {
                // PC: real mouse present, drag area is a no-op (camera already driven by mouse move).
                _mouse = null;
                _addedVirtualMouse = false;
            }
            else
            {
                _mouse = InputSystem.AddDevice<Mouse>();
                _addedVirtualMouse = true;
                Debug.Log("[MobileCameraDragArea] Virtual mouse device added for camera rotation.");
            }
        }

        private void OnDestroy()
        {
            if (_addedVirtualMouse && _mouse != null)
            {
                InputSystem.RemoveDevice(_mouse);
                _mouse = null;
                _addedVirtualMouse = false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  EventSystem handlers
        // ─────────────────────────────────────────────────────────────────────

        public void OnPointerDown(PointerEventData eventData) => _dragging = true;

        public void OnPointerUp(PointerEventData eventData) => _dragging = false;

        public void OnDrag(PointerEventData eventData)
        {
            // No-op on PC (no virtual mouse) or when not in a drag gesture.
            if (!_dragging || _mouse == null) return;

            // When two fingers are on screen (e.g. about to pinch), suppress rotation
            // to avoid chaotic input mixing.
            if (UnityEngine.Input.touchCount > 1) return;

            Vector2 delta = new Vector2(eventData.delta.x * _degreesPerPixel, 0f);
            if (delta.sqrMagnitude < 0.0001f) return;

            // Inject the delta into InputSystem. CinemachineInputAxisController reads
            // <Mouse>/delta from here and drives camera yaw automatically.
            InputSystem.QueueStateEvent(_mouse, new MouseState { delta = delta });
        }
    }
}
