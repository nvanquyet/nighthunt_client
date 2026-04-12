using NightHunt.Core;
using UnityEngine;

namespace NightHunt.Config
{
    /// <summary>
    /// Manages controls settings and applies quality/vsync/fullscreen on startup.
    /// Persists to PlayerPrefs.
    ///
    /// Resolution is intentionally NOT managed here — it is handled exclusively
    /// by GraphicsSettingsPanel which uses Screen.resolutions[] and the same
    /// "ResolutionIndex" key. Owning resolution in two places with different
    /// index semantics caused conflicts.
    ///
    /// Audio is managed exclusively by AudioManager + AudioSettingsPanel.
    /// </summary>
    public class GameSettings : SingletonPersistent<GameSettings>
    {
        // Settings data
        private SettingsData currentSettings;

        protected override void OnSingletonAwake()
        {
            LoadSettings();
        }

        /// <summary>Load settings from PlayerPrefs and apply.</summary>
        public void LoadSettings()
        {
            currentSettings = new SettingsData
            {
                // Graphics (non-resolution)
                QualityLevel = PlayerPrefs.GetInt("QualityLevel", QualitySettings.GetQualityLevel()),
                VSync        = PlayerPrefs.GetInt("VSync",        1) == 1,
                Fullscreen   = PlayerPrefs.GetInt("Fullscreen",   1) == 1,

                // Controls
                MouseSensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 1f),
                InvertY          = PlayerPrefs.GetInt("InvertY", 0) == 1
            };

            ApplySettings();
        }

        /// <summary>Save settings to PlayerPrefs.</summary>
        public void SaveSettings()
        {
            if (currentSettings == null) return;

            PlayerPrefs.SetInt("QualityLevel", currentSettings.QualityLevel);
            PlayerPrefs.SetInt("VSync",        currentSettings.VSync      ? 1 : 0);
            PlayerPrefs.SetInt("Fullscreen",   currentSettings.Fullscreen ? 1 : 0);

            PlayerPrefs.SetFloat("MouseSensitivity", currentSettings.MouseSensitivity);
            PlayerPrefs.SetInt("InvertY",            currentSettings.InvertY ? 1 : 0);

            PlayerPrefs.Save();
        }

        /// <summary>Apply current settings (quality, vsync, fullscreen). Resolution managed by GraphicsSettingsPanel.</summary>
        public void ApplySettings()
        {
            if (currentSettings == null) return;

            QualitySettings.SetQualityLevel(currentSettings.QualityLevel);
            QualitySettings.vSyncCount = currentSettings.VSync ? 1 : 0;
            Screen.fullScreen          = currentSettings.Fullscreen;
        }

        // ── Properties ─────────────────────────────────────────────────────────

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

        /// <summary>Reset to default settings and save.</summary>
        public void ResetToDefaults()
        {
            currentSettings = new SettingsData
            {
                QualityLevel     = 2,
                VSync            = true,
                Fullscreen       = true,
                MouseSensitivity = 1f,
                InvertY          = false
            };

            ApplySettings();
            SaveSettings();
        }
    }

    [System.Serializable]
    public class SettingsData
    {
        public int   QualityLevel     = 2;
        public bool  VSync            = true;
        public bool  Fullscreen       = true;
        public float MouseSensitivity = 1f;
        public bool  InvertY          = false;
    }
}

