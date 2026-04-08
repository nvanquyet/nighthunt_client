using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NightHunt.UI.Settings
{
    /// <summary>
    /// GraphicsSettingsPanel — mirrors AudioSettingsPanel pattern exactly.
    ///
    /// SETUP in Canvas:
    ///   1. Create "GraphicsSettings" panel with the UI elements below.
    ///   2. Assign each field in the Inspector.
    ///   3. Wire the "Apply" button onClick → GraphicsSettingsPanel.Apply()
    ///   4. Wire the "Reset" button onClick → GraphicsSettingsPanel.ResetToDefaults()
    ///
    /// BEHAVIOUR:
    ///   • Quality / VSync / Fullscreen take effect LIVE (preview as you change).
    ///   • Resolution only applies when the player clicks "Apply" to avoid
    ///     jarring resolution flips while scrolling the dropdown.
    ///   • All values persist to PlayerPrefs (same keys as GameSettings.cs so
    ///     they remain compatible).
    ///
    /// PLAYER PREFS KEYS:
    ///   "QualityLevel", "VSync", "Fullscreen", "ResolutionIndex"
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GraphicsSettingsPanel : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────

        [Header("Quality Preset")]
        [Tooltip("Dropdown populated at runtime with Unity Quality preset names (Low/Medium/High/Ultra).")]
        [SerializeField] private TMP_Dropdown qualityDropdown;

        [Header("Resolution")]
        [Tooltip("Dropdown populated at runtime with available Screen.resolutions.")]
        [SerializeField] private TMP_Dropdown resolutionDropdown;

        [Header("Toggles")]
        [Tooltip("VSync toggle. Changes take effect immediately (preview).")]
        [SerializeField] private Toggle vsyncToggle;

        [Tooltip("Fullscreen toggle. Changes take effect immediately.")]
        [SerializeField] private Toggle fullscreenToggle;

        [Header("Apply / Reset Buttons")]
        [Tooltip("Apply button — commits pending resolution change and saves all settings.")]
        [SerializeField] private Button applyButton;

        [Tooltip("Optional label shown when there are unapplied resolution changes.")]
        [SerializeField] private GameObject pendingLabel;

        // ── Runtime ────────────────────────────────────────────────────────────

        private Resolution[] _availableResolutions;

        // Pending resolution index — set when user changes the dropdown,
        // committed to screen only when Apply() is called.
        private int _pendingResolutionIndex = -1;

        // Applied / saved resolution index
        private int _appliedResolutionIndex;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Start()
        {
            BuildQualityDropdown();
            BuildResolutionDropdown();
            LoadAndApply();
            WireCallbacks();
            SetPendingLabel(false);
        }

        private void OnDestroy()
        {
            if (qualityDropdown    != null) qualityDropdown.onValueChanged.RemoveAllListeners();
            if (resolutionDropdown != null) resolutionDropdown.onValueChanged.RemoveAllListeners();
            if (vsyncToggle        != null) vsyncToggle.onValueChanged.RemoveAllListeners();
            if (fullscreenToggle   != null) fullscreenToggle.onValueChanged.RemoveAllListeners();
            if (applyButton        != null) applyButton.onClick.RemoveAllListeners();
        }

        // ── Build Dropdowns ────────────────────────────────────────────────────

        private void BuildQualityDropdown()
        {
            if (qualityDropdown == null) return;

            qualityDropdown.ClearOptions();
            var names = QualitySettings.names; // e.g. ["Low","Medium","High","Ultra"]
            var options = new List<TMP_Dropdown.OptionData>();
            foreach (var n in names)
                options.Add(new TMP_Dropdown.OptionData(n));
            qualityDropdown.AddOptions(options);
        }

        private void BuildResolutionDropdown()
        {
            if (resolutionDropdown == null) return;

            _availableResolutions = Screen.resolutions;
            resolutionDropdown.ClearOptions();

            var options = new List<TMP_Dropdown.OptionData>();
            foreach (var r in _availableResolutions)
                options.Add(new TMP_Dropdown.OptionData($"{r.width} × {r.height} @ {r.refreshRateRatio.value:F0}Hz"));
            resolutionDropdown.AddOptions(options);
        }

        // ── Load & Apply ───────────────────────────────────────────────────────

        private void LoadAndApply()
        {
            // Quality
            int quality = PlayerPrefs.GetInt("QualityLevel", QualitySettings.GetQualityLevel());
            quality = Mathf.Clamp(quality, 0, QualitySettings.names.Length - 1);
            if (qualityDropdown != null) qualityDropdown.SetValueWithoutNotify(quality);
            QualitySettings.SetQualityLevel(quality, true);

            // VSync
            bool vsync = PlayerPrefs.GetInt("VSync", 1) == 1;
            if (vsyncToggle != null) vsyncToggle.SetIsOnWithoutNotify(vsync);
            QualitySettings.vSyncCount = vsync ? 1 : 0;

            // Fullscreen
            bool fullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
            if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(fullscreen);
            Screen.fullScreen = fullscreen;

            // Resolution — find best match for saved index in current list
            int savedResIdx = PlayerPrefs.GetInt("ResolutionIndex", FindCurrentResolutionIndex());
            savedResIdx = (_availableResolutions != null && savedResIdx < _availableResolutions.Length)
                ? savedResIdx : FindCurrentResolutionIndex();
            _appliedResolutionIndex  = savedResIdx;
            _pendingResolutionIndex  = savedResIdx;
            if (resolutionDropdown != null) resolutionDropdown.SetValueWithoutNotify(savedResIdx);

            // Apply saved resolution immediately on load
            ApplyResolution(savedResIdx, fullscreen);
        }

        // ── Wire UI Callbacks ──────────────────────────────────────────────────

        private void WireCallbacks()
        {
            if (qualityDropdown != null)
                qualityDropdown.onValueChanged.AddListener(HandleQualityChanged);

            if (resolutionDropdown != null)
                resolutionDropdown.onValueChanged.AddListener(HandleResolutionDropdownChanged);

            if (vsyncToggle != null)
                vsyncToggle.onValueChanged.AddListener(HandleVSyncChanged);

            if (fullscreenToggle != null)
                fullscreenToggle.onValueChanged.AddListener(HandleFullscreenChanged);

            if (applyButton != null)
                applyButton.onClick.AddListener(Apply);
        }

        // ── Handlers ──────────────────────────────────────────────────────────

        private void HandleQualityChanged(int index)
        {
            QualitySettings.SetQualityLevel(index, true);  // live preview
            PlayerPrefs.SetInt("QualityLevel", index);
        }

        private void HandleResolutionDropdownChanged(int index)
        {
            // Mark as pending — don't apply yet; user must click Apply.
            _pendingResolutionIndex = index;
            bool hasPending = (_pendingResolutionIndex != _appliedResolutionIndex);
            SetPendingLabel(hasPending);
        }

        private void HandleVSyncChanged(bool on)
        {
            QualitySettings.vSyncCount = on ? 1 : 0;       // live
            PlayerPrefs.SetInt("VSync", on ? 1 : 0);
        }

        private void HandleFullscreenChanged(bool on)
        {
            Screen.fullScreen = on;                          // live
            PlayerPrefs.SetInt("Fullscreen", on ? 1 : 0);
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Commit pending resolution change and save all settings to PlayerPrefs.
        /// Wire this to the "Apply" button onClick.
        /// </summary>
        public void Apply()
        {
            bool fullscreen = fullscreenToggle != null && fullscreenToggle.isOn;

            if (_pendingResolutionIndex != _appliedResolutionIndex)
            {
                ApplyResolution(_pendingResolutionIndex, fullscreen);
                _appliedResolutionIndex = _pendingResolutionIndex;
                PlayerPrefs.SetInt("ResolutionIndex", _appliedResolutionIndex);
                SetPendingLabel(false);
            }

            PlayerPrefs.Save();
        }

        /// <summary>
        /// Reset all graphics settings to defaults and save.
        /// Wire to "Reset to Defaults" button onClick.
        /// </summary>
        public void ResetToDefaults()
        {
            int defaultQuality = Mathf.Clamp(2, 0, QualitySettings.names.Length - 1);
            int defaultResIdx  = FindCurrentResolutionIndex();

            if (qualityDropdown    != null) qualityDropdown.value    = defaultQuality;
            if (vsyncToggle        != null) vsyncToggle.isOn         = true;
            if (fullscreenToggle   != null) fullscreenToggle.isOn    = true;
            if (resolutionDropdown != null) resolutionDropdown.value = defaultResIdx;

            Apply(); // commits resolution + saves
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void ApplyResolution(int index, bool fullscreen)
        {
            if (_availableResolutions == null || index < 0 || index >= _availableResolutions.Length)
                return;

            var r = _availableResolutions[index];
            Screen.SetResolution(r.width, r.height, fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }

        private int FindCurrentResolutionIndex()
        {
            if (_availableResolutions == null) return 0;

            int w = Screen.width;
            int h = Screen.height;
            for (int i = 0; i < _availableResolutions.Length; i++)
            {
                if (_availableResolutions[i].width == w && _availableResolutions[i].height == h)
                    return i;
            }
            return _availableResolutions.Length - 1; // default: highest
        }

        private void SetPendingLabel(bool active)
        {
            if (pendingLabel != null)
                pendingLabel.SetActive(active);
        }
    }
}
