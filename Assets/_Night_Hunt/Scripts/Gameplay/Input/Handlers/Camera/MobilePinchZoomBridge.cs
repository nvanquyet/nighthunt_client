using UnityEngine;

namespace NightHunt.Gameplay.Input.Handlers.Camera
{
    /// <summary>
    /// Scene-level bridge that translates a two-finger pinch gesture into zoom
    /// events consumed by the local player's <see cref="CameraInputHandler"/>.
    ///
    /// Hierarchy:
    ///   Attach to any active GameObject in the HUD canvas (or the same GO as
    ///   GameHUD). No child components required.
    ///
    /// Inspector:
    ///   • <c>_zoomSpeedMultiplier</c> — scales pinch distance change before
    ///                                   handing to CameraInputHandler.SetMobileZoom.
    ///                                   Negative values invert pinch direction.
    ///
    /// HUD Wiring:
    ///   Call <see cref="BindHandler"/> from GameHUD once the local player spawns.
    ///   Call <see cref="UnbindHandler"/> when the local player despawns.
    ///
    /// PC behaviour:
    ///   On non-touch platforms (Input.touchCount == 0) this component is a
    ///   no-op — mouse scroll wheel continues to drive zoom via InputSystem.
    ///
    /// Owner-only:
    ///   CameraInputHandler.SetMobileZoom is a no-op when inputEnabled == false,
    ///   so non-owners are automatically excluded.
    /// </summary>
    public class MobilePinchZoomBridge : MonoBehaviour
    {
        [Header("Zoom Sensitivity")]
        [Tooltip("Scales the pinch delta. Increase for faster zoom; negative to invert.")]
        [SerializeField] private float _zoomSpeedMultiplier = 0.05f;

        [Tooltip("Enable two-finger pinch zoom. Leave off when zoom should only come from the mobile slider.")]
        [SerializeField] private bool _enablePinchZoom = false;

        private CameraInputHandler _handler;
        private float _previousPinchDistance;

        // ─────────────────────────────────────────────────────────────────────
        //  Unity
        // ─────────────────────────────────────────────────────────────────────

        private void Update()
        {
            if (_handler == null || !_enablePinchZoom) return;
            if (UnityEngine.Input.touchCount != 2) { _previousPinchDistance = 0f; return; }

            Touch t0 = UnityEngine.Input.GetTouch(0);
            Touch t1 = UnityEngine.Input.GetTouch(1);

            float currentDist = Vector2.Distance(t0.position, t1.position);

            // Skip the first frame of a new two-finger contact to avoid a
            // distance jump from the previous zero value.
            if (_previousPinchDistance == 0f)
            {
                _previousPinchDistance = currentDist;
                return;
            }

            float delta = currentDist - _previousPinchDistance;
            _previousPinchDistance = currentDist;

            if (Mathf.Abs(delta) > 0.01f)
                _handler.SetMobileZoom(delta * _zoomSpeedMultiplier);
        }

        private void OnDisable()
        {
            _previousPinchDistance = 0f;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Bind to the local player's CameraInputHandler.
        /// Call from the HUD orchestrator (GameHUD.Initialize) after the player spawns.
        /// </summary>
        public void BindHandler(CameraInputHandler handler)
        {
            _handler = handler;
            _previousPinchDistance = 0f;
        }

        /// <summary>
        /// Clear the binding. Call when the local player despawns.
        /// </summary>
        public void UnbindHandler()
        {
            _handler = null;
            _previousPinchDistance = 0f;
        }
    }
}
