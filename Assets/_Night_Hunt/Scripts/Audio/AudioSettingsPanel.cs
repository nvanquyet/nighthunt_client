using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NightHunt.Audio
{
    /// <summary>
    /// AudioSettingsPanel — mirrors ShiftUI QualityManager exactly.
    ///
    /// PATTERN (from QualityManager.Start()):
    ///   mixer.SetFloat("Master", Mathf.Log10(PlayerPrefs.GetFloat(sliderTag + "SliderValue")) * 20)
    ///
    /// HOW THIS WORKS:
    ///   • Each slider has a "sliderTag" (= AudioManager param key, e.g. "MasterVol").
    ///   • On Start(): reads stored 0–1 value from PlayerPrefs → applies dB to mixer.
    ///   • On slider change: calls AudioManager.SetVolume(param, value) → mixer + PlayerPrefs.
    ///
    /// SETUP in Canvas:
    ///   1. Create "AudioSettings" panel with Slider components.
    ///   2. Assign each slider below.
    ///   3. AudioSettingsPanel.Start() auto-wires OnValueChanged callbacks.
    ///   4. No further code needed — fully declarative.
    ///
    /// HINT: Use ShiftUI's SliderManager if available — just replace its sliderTag with
    ///   the AudioManager param key (e.g. "MasterVol") so PlayerPrefs keys match.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioSettingsPanel : MonoBehaviour
    {
        [System.Serializable]
        public class AudioSliderEntry
        {
            [Tooltip("AudioManager exposed param key, e.g. 'MasterVol', 'MusicVol', 'SFXVol'.")]
            public string paramKey;

            [Tooltip("Unity UI Slider for this volume channel.")]
            public Slider slider;

            [Tooltip("Optional text label showing percentage (e.g. '80%'). Leave null to skip.")]
            public TextMeshProUGUI percentLabel;

            [Tooltip("Default value (0–1) applied on first run (no saved preference yet).")]
            [Range(0f, 1f)]
            public float defaultValue = 1f;
        }

        [Header("Volume Sliders")]
        [Tooltip("Add one entry per mixer channel. Order does not matter.")]
        [SerializeField] private AudioSliderEntry[] sliders;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Start()
        {
            foreach (var entry in sliders)
            {
                if (entry == null || entry.slider == null || string.IsNullOrEmpty(entry.paramKey))
                    continue;

                // Wire callback before LoadSettings so initial apply does not double-fire.
                var captured = entry;
                entry.slider.onValueChanged.AddListener(v => SaveSettings(captured, v));
            }

            LoadSettings();
        }

        private void OnEnable()
        {
            // Re-apply saved values when the panel becomes visible
            // (e.g. the settings screen is reopened).
            LoadSettings();
        }

        private void OnDestroy()
        {
            foreach (var entry in sliders)
            {
                if (entry?.slider != null)
                    entry.slider.onValueChanged.RemoveAllListeners();
            }
        }

        // ── Load / Save ────────────────────────────────────────────

        /// <summary>
        /// Read saved volumes from PlayerPrefs and apply to all sliders and the mixer.
        /// Uses prefix "NH_Audio_" + paramKey.
        /// </summary>
        private void LoadSettings()
        {
            foreach (var entry in sliders)
            {
                if (entry == null || entry.slider == null || string.IsNullOrEmpty(entry.paramKey))
                    continue;

                float stored = PlayerPrefs.GetFloat("NH_Audio_" + entry.paramKey, entry.defaultValue);
                stored = Mathf.Clamp01(stored);

                entry.slider.SetValueWithoutNotify(stored);
                UpdateLabel(entry, stored);

                if (AudioManager.HasInstance)
                    AudioManager.Instance.SetVolume(entry.paramKey, stored);
            }
        }

        /// <summary>
        /// Persist a single channel volume to PlayerPrefs and apply to the mixer.
        /// Called by slider.onValueChanged.
        /// </summary>
        private void SaveSettings(AudioSliderEntry entry, float value)
        {
            UpdateLabel(entry, value);
            if (!AudioManager.HasInstance) return;
            AudioManager.Instance.SetVolume(entry.paramKey, value);
            // AudioManager.SetVolume already writes PlayerPrefs.SetFloat("NH_Audio_" + param, value).
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static void UpdateLabel(AudioSliderEntry entry, float value)
        {
            if (entry.percentLabel != null)
                entry.percentLabel.text = $"{Mathf.RoundToInt(value * 100)}%";
        }

        // ── Runtime API ────────────────────────────────────────────────────────

        /// <summary>Reset all sliders back to their default values and persist.</summary>
        public void ResetToDefaults()
        {
            foreach (var entry in sliders)
            {
                if (entry?.slider == null) continue;
                entry.slider.value = entry.defaultValue; // fires onValueChanged → SaveSettings
            }
        }
    }
}
