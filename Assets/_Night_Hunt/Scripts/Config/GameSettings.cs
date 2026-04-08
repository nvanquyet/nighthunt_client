using NightHunt.Core;
using UnityEngine;

namespace NightHunt.Config
{
    /// <summary>
    /// Manages graphics and controls settings. Persists to PlayerPrefs.
    /// Audio is managed exclusively by AudioManager + AudioSettingsPanel.
    /// </summary>
    public class GameSettings : SingletonPersistent<GameSettings>
    {
        [Header("Graphics")]
        [SerializeField] private int[] resolutionWidths = { 1920, 1680, 1280, 1024 };
        [SerializeField] private int[] resolutionHeights = { 1080, 1050, 720, 768 };

        // Settings data
        private SettingsData currentSettings;

        protected override void OnSingletonAwake()
        {
            LoadSettings();
        }

        /// <summary>
        /// Load settings from PlayerPrefs
        /// </summary>
        public void LoadSettings()
        {
            currentSettings = new SettingsData
            {
                // Graphics
                QualityLevel = PlayerPrefs.GetInt("QualityLevel", QualitySettings.GetQualityLevel()),
                VSync = PlayerPrefs.GetInt("VSync", 1) == 1,
                Fullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1,
                ResolutionIndex = PlayerPrefs.GetInt("ResolutionIndex", 0),

                // Controls
                MouseSensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 1f),
                InvertY = PlayerPrefs.GetInt("InvertY", 0) == 1
            };

            ApplySettings();
        }

        /// <summary>
        /// Save settings to PlayerPrefs
        /// </summary>
        public void SaveSettings()
        {
            if (currentSettings == null) return;

            // Graphics
            PlayerPrefs.SetInt("QualityLevel", currentSettings.QualityLevel);
            PlayerPrefs.SetInt("VSync", currentSettings.VSync ? 1 : 0);
            PlayerPrefs.SetInt("Fullscreen", currentSettings.Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt("ResolutionIndex", currentSettings.ResolutionIndex);

            // Controls
            PlayerPrefs.SetFloat("MouseSensitivity", currentSettings.MouseSensitivity);
            PlayerPrefs.SetInt("InvertY", currentSettings.InvertY ? 1 : 0);

            PlayerPrefs.Save();
        }

        /// <summary>
        /// Apply current settings
        /// </summary>
        public void ApplySettings()
        {
            if (currentSettings == null) return;

            // Apply graphics
            QualitySettings.SetQualityLevel(currentSettings.QualityLevel);
            QualitySettings.vSyncCount = currentSettings.VSync ? 1 : 0;

            // Apply resolution
            if (currentSettings.ResolutionIndex >= 0 && currentSettings.ResolutionIndex < resolutionWidths.Length)
            {
                int width = resolutionWidths[currentSettings.ResolutionIndex];
                int height = resolutionHeights[currentSettings.ResolutionIndex];
                Screen.SetResolution(width, height, currentSettings.Fullscreen);
            }
        }

        // Getters and Setters
        public int QualityLevel
        {
            get => currentSettings?.QualityLevel ?? 2;
            set { if (currentSettings != null) currentSettings.QualityLevel = value; }
        }

        public bool VSync
        {
            get => currentSettings?.VSync ?? true;
            set { if (currentSettings != null) currentSettings.VSync = value; }
        }

        public bool Fullscreen
        {
            get => currentSettings?.Fullscreen ?? true;
            set { if (currentSettings != null) currentSettings.Fullscreen = value; }
        }

        public float MouseSensitivity
        {
            get => currentSettings?.MouseSensitivity ?? 1f;
            set { if (currentSettings != null) currentSettings.MouseSensitivity = value; }
        }

        public bool InvertY
        {
            get => currentSettings?.InvertY ?? false;
            set { if (currentSettings != null) currentSettings.InvertY = value; }
        }

        /// <summary>
        /// Reset to default settings
        /// </summary>
        public void ResetToDefaults()
        {
            currentSettings = new SettingsData
            {
                QualityLevel = 2,
                VSync = true,
                Fullscreen = true,
                ResolutionIndex = 0,
                MouseSensitivity = 1f,
                InvertY = false
            };

            ApplySettings();
            SaveSettings();
        }
    }

    /// <summary>
    /// Settings data structure
    /// </summary>
    [System.Serializable]
    public class SettingsData
    {
        public int QualityLevel = 2;
        public bool VSync = true;
        public bool Fullscreen = true;
        public int ResolutionIndex = 0;
        public float MouseSensitivity = 1f;
        public bool InvertY = false;
    }
}

