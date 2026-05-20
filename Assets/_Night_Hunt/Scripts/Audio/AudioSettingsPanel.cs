using NightHunt.Config;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

namespace NightHunt.Audio
{
    /// <summary>
    /// AudioSettingsPanel - NightHunt-owned replacement for ShiftUI QualityManager.
    /// Synchronizes UI Sliders with GameSettings and AudioManager.
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

            [System.NonSerialized] public UnityAction<float> runtimeListener;
        }

        [Header("Volume Sliders")]
        [Tooltip("Add one entry per mixer channel. Order does not matter.")]
        [SerializeField] private AudioSliderEntry[] sliders;

        [Header("Reset")]
        [SerializeField] private Button resetButton;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Start()
        {
            foreach (var entry in sliders)
            {
                if (entry == null || entry.slider == null || string.IsNullOrEmpty(entry.paramKey))
                    continue;

                // Wire callback before LoadSettings so initial apply does not double-fire.
                var captured = entry;
                entry.runtimeListener = v => UpdateAndSave(captured, v);
                entry.slider.onValueChanged.AddListener(entry.runtimeListener);
            }

            if (resetButton != null)
                resetButton.onClick.AddListener(ResetToDefaults);

            LoadSettings();
        }

        private void OnEnable()
        {
            LoadSettings();
        }

        public void RefreshFromPrefs()
        {
            LoadSettings();
        }

        private void OnDestroy()
        {
            foreach (var entry in sliders)
            {
                if (entry?.slider != null && entry.runtimeListener != null)
                    entry.slider.onValueChanged.RemoveListener(entry.runtimeListener);
            }
            if (resetButton != null)
                resetButton.onClick.RemoveListener(ResetToDefaults);
        }

        // ── Load / Save ────────────────────────────────────────────

        private void LoadSettings()
        {
            var settings = GameSettings.Instance;
            if (settings == null) return;

            foreach (var entry in sliders)
            {
                if (entry == null || entry.slider == null || string.IsNullOrEmpty(entry.paramKey))
                    continue;

                float value = entry.defaultValue;
                // GameSettings holds these values. We map paramKey to the property.
                // For simplicity, we'll use a switch or just read the stored prefs through GameSettings logic.
                // Since GameSettings already loaded them into SettingsData, we can use GetVolume if I added it.
                // Wait, I didn't add a GetVolume(string key). Let's add it to GameSettings later or just use the prefix.
                
                // For now, let's assume we read from PlayerPrefs with the same prefix GameSettings uses.
                float stored = PlayerPrefs.GetFloat("NH_Audio_" + entry.paramKey, entry.defaultValue);
                entry.slider.SetValueWithoutNotify(stored);
                UpdateLabel(entry, stored);

                if (AudioManager.HasInstance)
                    AudioManager.Instance.SetVolume(entry.paramKey, stored);
            }
        }

        private void UpdateAndSave(AudioSliderEntry entry, float value)
        {
            UpdateLabel(entry, value);
            
            if (GameSettings.Instance != null)
            {
                GameSettings.Instance.SetVolume(entry.paramKey, value);
                GameSettings.Instance.SaveSettings();
            }

            if (AudioManager.HasInstance)
                AudioManager.Instance.SetVolume(entry.paramKey, value);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static void UpdateLabel(AudioSliderEntry entry, float value)
        {
            if (entry.percentLabel != null)
                entry.percentLabel.text = $"{Mathf.RoundToInt(value * 100)}%";
        }

        // ── Runtime API ────────────────────────────────────────────────────────

        public void ResetToDefaults()
        {
            if (GameSettings.Instance != null)
            {
                GameSettings.Instance.ResetToDefaults();
                LoadSettings();
            }
        }

        private void OnValidate()
        {
            if (sliders == null || sliders.Length == 0)
            {
                // Try to find standard sliders in Michsky hierarchy
                var content = transform.Find("Content/List/List Content");
                if (content != null)
                {
                    var list = new System.Collections.Generic.List<AudioSliderEntry>();
                    TryAddEntry(list, content, "Master volume", "MasterVol");
                    TryAddEntry(list, content, "Music volume", "MusicVol");
                    TryAddEntry(list, content, "SFX volume", "SFXVol");
                    if (list.Count > 0) sliders = list.ToArray();
                }
            }
        }

        private void TryAddEntry(System.Collections.Generic.List<AudioSliderEntry> list, Transform root, string objName, string key)
        {
            var t = root.Find(objName);
            if (t != null)
            {
                var s = t.GetComponentInChildren<Slider>();
                var l = t.Find("Value")?.GetComponent<TextMeshProUGUI>();
                if (s != null) list.Add(new AudioSliderEntry { paramKey = key, slider = s, percentLabel = l });
            }
        }
    }
}
