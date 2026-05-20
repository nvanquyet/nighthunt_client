using NightHunt.Config;
using Michsky.UI.Shift;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using NightHunt.Gameplay.Input.Core;

namespace NightHunt.UI.Settings
{
    /// <summary>
    /// ControlsSettingsPanel — connects Controls UI to <see cref="GameSettings"/>.
    ///
    /// SETUP in Canvas:
    ///   1. Create "ControlsSettings" panel with the UI elements below.
    ///   2. Assign each field in the Inspector.
    ///   3. Wire "Reset" button onClick → ControlsSettingsPanel.ResetToDefaults()
    ///   4. No "Apply" button needed — all changes take effect live (stored on change).
    ///
    /// PLAYER PREFS KEYS (shared with GameSettings.cs):
    ///   "MouseSensitivity"  — float 0.1–5.0
    ///   "InvertY"           — int  0/1
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ControlsSettingsPanel : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────

        [Header("Mouse Sensitivity")]
        [Tooltip("Slider range should be set to 0.1–5.0 in Inspector.")]
        [SerializeField] private Slider mouseSensitivitySlider;

        [Tooltip("Optional TMP label showing current value, e.g. '1.5'. Leave null to skip.")]
        [SerializeField] private TextMeshProUGUI sensitivityValueLabel;

        [Header("Invert Y")]
        [Tooltip("Toggle for inverting vertical camera/aim axis.")]
        [SerializeField] private Toggle invertYToggle;
        [Tooltip("Optional Shift UI switch used as the visual/input source for Invert Y.")]
        [SerializeField] private SwitchManager invertYSwitch;

        [Header("Reset Button")]
        [Tooltip("Optional — wire onClick in Inspector to ResetToDefaults().")]
        [SerializeField] private Button resetButton;
        [Tooltip("Optional — resets ALL key bindings to defaults. Wire onClick to ResetAllBindings().")]
        [SerializeField] private Button _resetAllBindingsButton;
        [SerializeField] private RebindActionUI[] rebindableActions;

        private UnityAction _invertSwitchOnListener;
        private UnityAction _invertSwitchOffListener;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Start()
        {
            LoadAndApply();
            WireCallbacks();

            if (rebindableActions != null)
            {
                foreach (var rebind in rebindableActions)
                {
                    if (rebind != null)
                        rebind.OnBindingChanged += () => GameSettings.Instance?.SaveSettings();
                }
            }
        }

        private void OnValidate()
        {
            if (rebindableActions == null || rebindableActions.Length == 0)
                rebindableActions = GetComponentsInChildren<RebindActionUI>(true);
        }

        public void RefreshFromPrefs()
        {
            LoadAndApply();
        }

        private void OnDestroy()
        {
            if (mouseSensitivitySlider != null)
                mouseSensitivitySlider.onValueChanged.RemoveListener(HandleSensitivityChanged);
            if (invertYToggle != null)
                invertYToggle.onValueChanged.RemoveListener(HandleInvertYChanged);
            if (invertYSwitch != null)
            {
                if (_invertSwitchOnListener != null)
                    invertYSwitch.OnEvents.RemoveListener(_invertSwitchOnListener);
                if (_invertSwitchOffListener != null)
                    invertYSwitch.OffEvents.RemoveListener(_invertSwitchOffListener);
            }
            if (resetButton != null)
                resetButton.onClick.RemoveListener(ResetToDefaults);
            if (_resetAllBindingsButton != null)
                _resetAllBindingsButton.onClick.RemoveListener(ResetAllBindings);
        }

        // ── Load & Apply ───────────────────────────────────────────────────────

        private void LoadAndApply()
        {
            var settings = GameSettings.Instance;
            if (settings == null) return;

            float sensitivity = settings.MouseSensitivity;
            bool  invertY     = settings.InvertY;

            if (mouseSensitivitySlider != null)
                mouseSensitivitySlider.SetValueWithoutNotify(sensitivity);
            UpdateSensitivityLabel(sensitivity);

            if (invertYToggle != null)
                invertYToggle.SetIsOnWithoutNotify(invertY);
            ShiftUIBridge.SetSwitchSilently(invertYSwitch, invertY);
        }

        // ── Wire UI Callbacks ──────────────────────────────────────────────────

        private void WireCallbacks()
        {
            if (mouseSensitivitySlider != null)
                mouseSensitivitySlider.onValueChanged.AddListener(HandleSensitivityChanged);

            if (invertYToggle != null)
                invertYToggle.onValueChanged.AddListener(HandleInvertYChanged);

            if (invertYSwitch != null)
            {
                _invertSwitchOnListener ??= () => HandleInvertYChanged(true);
                _invertSwitchOffListener ??= () => HandleInvertYChanged(false);
                invertYSwitch.OnEvents.RemoveListener(_invertSwitchOnListener);
                invertYSwitch.OffEvents.RemoveListener(_invertSwitchOffListener);
                invertYSwitch.OnEvents.AddListener(_invertSwitchOnListener);
                invertYSwitch.OffEvents.AddListener(_invertSwitchOffListener);
            }

            if (resetButton != null)
                resetButton.onClick.AddListener(ResetToDefaults);
            if (_resetAllBindingsButton != null)
                _resetAllBindingsButton.onClick.AddListener(ResetAllBindings);
        }

        // ── Handlers ──────────────────────────────────────────────────────────

        private void HandleSensitivityChanged(float value)
        {
            value = Mathf.Clamp(value, 0.1f, 5f);
            UpdateSensitivityLabel(value);

            if (GameSettings.Instance != null)
            {
                GameSettings.Instance.MouseSensitivity = value;
                GameSettings.Instance.SaveSettings();
            }
        }

        private void HandleInvertYChanged(bool on)
        {
            if (GameSettings.Instance != null)
            {
                GameSettings.Instance.InvertY = on;
                GameSettings.Instance.SaveSettings();
            }

            if (invertYToggle != null && invertYToggle.isOn != on)
                invertYToggle.SetIsOnWithoutNotify(on);
            ShiftUIBridge.SetSwitchSilently(invertYSwitch, on);
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Reset controls settings to defaults and save.
        /// Wire to "Reset to Defaults" button onClick in Inspector.
        /// </summary>
        public void ResetToDefaults()
        {
            if (GameSettings.Instance != null)
            {
                GameSettings.Instance.ResetToDefaults();
                LoadAndApply();
            }
        }

        /// <summary>
        /// Reset all key/button bindings to defaults and refresh every RebindActionUI display.
        /// Wire to a "Reset All Bindings" button onClick in Inspector.
        /// </summary>
        public void ResetAllBindings()
        {
            var asset = InputLayerManager.Instance?.Config?.InputActionAsset;
            if (asset != null)
            {
                InputBindingSaveSystem.ResetAllBindings(asset);
            }
            else
            {
                Debug.LogWarning("[ControlsSettingsPanel] InputLayerManager not found — cannot reset bindings.");
            }

            // Refresh every rebind entry label
            if (rebindableActions != null)
            {
                foreach (var rebind in rebindableActions)
                    rebind?.RefreshDisplay();
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void UpdateSensitivityLabel(float value)
        {
            if (sensitivityValueLabel != null)
                sensitivityValueLabel.text = value.ToString("F1");
        }

    }
}
