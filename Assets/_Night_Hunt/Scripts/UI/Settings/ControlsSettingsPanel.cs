using NightHunt.Config;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

        [Header("Reset Button")]
        [Tooltip("Optional — wire onClick in Inspector to ResetToDefaults().")]
        [SerializeField] private Button resetButton;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Start()
        {
            LoadAndApply();
            WireCallbacks();
        }

        private void OnDestroy()
        {
            if (mouseSensitivitySlider != null)
                mouseSensitivitySlider.onValueChanged.RemoveAllListeners();
            if (invertYToggle != null)
                invertYToggle.onValueChanged.RemoveAllListeners();
            if (resetButton != null)
                resetButton.onClick.RemoveListener(ResetToDefaults);
        }

        // ── Load & Apply ───────────────────────────────────────────────────────

        private void LoadAndApply()
        {
            float sensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 1f);
            bool  invertY     = PlayerPrefs.GetInt("InvertY", 0) == 1;

            if (mouseSensitivitySlider != null)
                mouseSensitivitySlider.SetValueWithoutNotify(sensitivity);
            UpdateSensitivityLabel(sensitivity);

            if (invertYToggle != null)
                invertYToggle.SetIsOnWithoutNotify(invertY);

            // Sync to GameSettings runtime state
            if (GameSettings.Instance != null)
            {
                GameSettings.Instance.MouseSensitivity = sensitivity;
                GameSettings.Instance.InvertY          = invertY;
            }
        }

        // ── Wire UI Callbacks ──────────────────────────────────────────────────

        private void WireCallbacks()
        {
            if (mouseSensitivitySlider != null)
                mouseSensitivitySlider.onValueChanged.AddListener(HandleSensitivityChanged);

            if (invertYToggle != null)
                invertYToggle.onValueChanged.AddListener(HandleInvertYChanged);

            if (resetButton != null)
                resetButton.onClick.AddListener(ResetToDefaults);
        }

        // ── Handlers ──────────────────────────────────────────────────────────

        private void HandleSensitivityChanged(float value)
        {
            value = Mathf.Clamp(value, 0.1f, 5f);
            UpdateSensitivityLabel(value);

            PlayerPrefs.SetFloat("MouseSensitivity", value);
            PlayerPrefs.Save();

            if (GameSettings.Instance != null)
                GameSettings.Instance.MouseSensitivity = value;
        }

        private void HandleInvertYChanged(bool on)
        {
            PlayerPrefs.SetInt("InvertY", on ? 1 : 0);
            PlayerPrefs.Save();

            if (GameSettings.Instance != null)
                GameSettings.Instance.InvertY = on;
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Reset controls settings to defaults and save.
        /// Wire to "Reset to Defaults" button onClick in Inspector.
        /// </summary>
        public void ResetToDefaults()
        {
            const float defaultSensitivity = 1f;
            const bool  defaultInvertY     = false;

            if (mouseSensitivitySlider != null) mouseSensitivitySlider.value = defaultSensitivity;
            if (invertYToggle          != null) invertYToggle.isOn           = defaultInvertY;

            // Callbacks above persist to PlayerPrefs + GameSettings automatically.
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void UpdateSensitivityLabel(float value)
        {
            if (sensitivityValueLabel != null)
                sensitivityValueLabel.text = value.ToString("F1");
        }
    }
}
