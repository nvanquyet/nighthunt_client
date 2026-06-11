using UnityEngine;
using UnityEngine.UI;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Gameplay.Input.Handlers.Camera;

namespace NightHunt.UI.Mobile
{
    /// <summary>
    /// Mobile zoom control: a Slider that drives camera FOV via CameraInputHandler.SetMobileZoom.
    /// A toggle button shows/hides the slider panel (expand / collapse).
    ///
    /// PIPELINE:
    ///   Slider value changed → delta → CameraInputHandler.SetMobileZoom(delta * multiplier)
    ///                                → OnZoom event → CameraZoomInput.OnZoomPerformed
    ///                                → playerCamera.Lens.FieldOfView adjusted
    ///
    /// SETUP in Inspector:
    ///   _sliderPanel      — GameObject containing the Slider (shown/hidden on toggle).
    ///   _slider           — the UnityEngine.UI.Slider inside _sliderPanel.
    ///   _toggleButton     — button that calls ToggleExpand when clicked (wire OnClick).
    ///   _iconWhenCollapsed/Expanded — optional GameObjects swapped on toggle (e.g. +/- icons).
    ///   _zoomMultiplier   — scales slider delta before sending to camera (default 10).
    ///                       Negative value inverts zoom direction.
    ///
    /// DESIGN:
    ///   Slider is persistent (holds zoom level); only the DELTA between old and new
    ///   value is forwarded so zoom accumulates naturally in CameraZoomInput.
    ///   Slider range 0–1, center (0.5) = no change from current FOV.
    /// </summary>
    public class MobileZoomSlider : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Container that holds the Slider. Shown when expanded, hidden when collapsed.")]
        [SerializeField] private GameObject _sliderPanel;

        [Tooltip("The Slider inside _sliderPanel.")]
        [SerializeField] private Slider _slider;

        [Header("Toggle Button")]
        [Tooltip("Button that calls ToggleExpand. Wire OnClick in Inspector, or leave blank " +
                 "and call ToggleExpand() from another button's UnityEvent.")]
        [SerializeField] private Button _toggleButton;

        [Tooltip("Icon displayed when the slider is COLLAPSED (e.g. a zoom/expand icon).")]
        [SerializeField] private GameObject _iconWhenCollapsed;

        [Tooltip("Icon displayed when the slider is EXPANDED (e.g. a close/collapse icon).")]
        [SerializeField] private GameObject _iconWhenExpanded;

        [Header("Zoom Settings")]
        [Tooltip("Multiplies slider delta before handing to CameraInputHandler.SetMobileZoom. " +
                 "Increase for stronger zoom effect. Negative to invert direction.")]
        [SerializeField] private float _zoomMultiplier = 10f;

        private CameraInputHandler _handler;
        private float _previousValue;
        private bool _expanded;

        // ─────────────────────────────────────────────────────────────────────
        //  Unity
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            // This slider is only needed on mobile; hide the entire control on desktop.
            if (!IsMobileMode())
            {
                gameObject.SetActive(false);
                return;
            }

            // Start collapsed.
            if (_sliderPanel != null) _sliderPanel.SetActive(false);
            _expanded = false;
            RefreshIcons();
        }

        private void Start()
        {
            _handler = InputManager.Instance?.CameraHandler;

            if (_slider != null)
            {
                _previousValue = _slider.value;
                _slider.onValueChanged.AddListener(OnSliderChanged);
            }

            if (_toggleButton != null && _toggleButton.onClick.GetPersistentEventCount() == 0)
                _toggleButton.onClick.AddListener(ToggleExpand);
        }

        private void OnDestroy()
        {
            if (_slider != null)        _slider.onValueChanged.RemoveListener(OnSliderChanged);
            if (_toggleButton != null)  _toggleButton.onClick.RemoveListener(ToggleExpand);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Toggle slider panel visibility. Safe to call from a Button.OnClick UnityEvent.</summary>
        public void ToggleExpand()
        {
            _expanded = !_expanded;
            if (_sliderPanel != null) _sliderPanel.SetActive(_expanded);
            RefreshIcons();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Private
        // ─────────────────────────────────────────────────────────────────────

        private void OnSliderChanged(float value)
        {
            // Lazy-resolve if InputManager spawned after Start
            if (_handler == null)
                _handler = InputManager.Instance?.CameraHandler;

            float delta = value - _previousValue;
            _previousValue = value;

            if (Mathf.Abs(delta) > 0.001f)
                _handler?.SetMobileZoom(delta * _zoomMultiplier);
        }

        private void RefreshIcons()
        {
            if (_iconWhenCollapsed != null) _iconWhenCollapsed.SetActive(!_expanded);
            if (_iconWhenExpanded  != null) _iconWhenExpanded.SetActive(_expanded);
        }

        private static bool IsMobileMode()
        {
            var platform = PlatformInputDetector.Instance;
            return platform != null ? platform.IsMobile : Application.isMobilePlatform;
        }
    }
}
