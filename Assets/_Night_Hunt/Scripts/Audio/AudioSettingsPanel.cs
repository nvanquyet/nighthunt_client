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

                // Load stored value (same key format as ShiftUI SliderManager)
                float stored = PlayerPrefs.GetFloat(entry.paramKey + "SliderValue", entry.defaultValue);
                stored = Mathf.Clamp01(stored);

                // Apply without firing callback first
                entry.slider.SetValueWithoutNotify(stored);
                UpdateLabel(entry, stored);

                // Apply to mixer via AudioManager (triggers dB conversion + PlayerPrefs write)
                if (AudioManager.HasInstance)
                    AudioManager.Instance.SetVolume(entry.paramKey, stored);

                // Wire callback — capture entry by value for lambda safety
                var captured = entry;
                entry.slider.onValueChanged.AddListener(v => HandleSliderChanged(captured, v));
            }
        }

        private void OnDestroy()
        {
            foreach (var entry in sliders)
            {
                if (entry?.slider != null)
                    entry.slider.onValueChanged.RemoveAllListeners();
            }
        }

        // ── Slider Callback ────────────────────────────────────────────────────

        private void HandleSliderChanged(AudioSliderEntry entry, float value)
        {
            UpdateLabel(entry, value);

            if (!AudioManager.HasInstance) return;
            AudioManager.Instance.SetVolume(entry.paramKey, value);
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
                entry.slider.value = entry.defaultValue; // fires onValueChanged → HandleSliderChanged
            }
        }
    }
}
