using UnityEngine;
using UnityEngine.Audio;
using System.IO;

namespace NightHunt.Config
{
    /// <summary>
    /// Manages game settings: graphics, audio, controls
    /// Persists settings to PlayerPrefs
    /// </summary>
    public class GameSettings : MonoBehaviour
    {
        private static GameSettings _instance;
        public static GameSettings Instance => _instance;

        [Header("Audio")]
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private string masterVolumeParam = "MasterVolume";
        [SerializeField] private string musicVolumeParam = "MusicVolume";
        [SerializeField] private string sfxVolumeParam = "SFXVolume";

        [Header("Graphics")]
        [SerializeField] private int[] resolutionWidths = { 1920, 1680, 1280, 1024 };
        [SerializeField] private int[] resolutionHeights = { 1080, 1050, 720, 768 };

        // Settings data
        private SettingsData currentSettings;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                LoadSettings();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Load settings from PlayerPrefs
        /// </summary>
        public void LoadSettings()
        {
            currentSettings = new SettingsData
            {
                // Audio
                MasterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f),
                MusicVolume = PlayerPrefs.GetFloat("MusicVolume", 1f),
                SFXVolume = PlayerPrefs.GetFloat("SFXVolume", 1f),

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

            // Audio
            PlayerPrefs.SetFloat("MasterVolume", currentSettings.MasterVolume);
            PlayerPrefs.SetFloat("MusicVolume", currentSettings.MusicVolume);
            PlayerPrefs.SetFloat("SFXVolume", currentSettings.SFXVolume);

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

            // Apply audio
            if (audioMixer != null)
            {
                audioMixer.SetFloat(masterVolumeParam, Mathf.Log10(currentSettings.MasterVolume) * 20f);
                audioMixer.SetFloat(musicVolumeParam, Mathf.Log10(currentSettings.MusicVolume) * 20f);
                audioMixer.SetFloat(sfxVolumeParam, Mathf.Log10(currentSettings.SFXVolume) * 20f);
            }

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
        public float MasterVolume
        {
            get => currentSettings?.MasterVolume ?? 1f;
            set { if (currentSettings != null) currentSettings.MasterVolume = value; }
        }

        public float MusicVolume
        {
            get => currentSettings?.MusicVolume ?? 1f;
            set { if (currentSettings != null) currentSettings.MusicVolume = value; }
        }

        public float SFXVolume
        {
            get => currentSettings?.SFXVolume ?? 1f;
            set { if (currentSettings != null) currentSettings.SFXVolume = value; }
        }

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
                MasterVolume = 1f,
                MusicVolume = 1f,
                SFXVolume = 1f,
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
        public float MasterVolume = 1f;
        public float MusicVolume = 1f;
        public float SFXVolume = 1f;
        public int QualityLevel = 2;
        public bool VSync = true;
        public bool Fullscreen = true;
        public int ResolutionIndex = 0;
        public float MouseSensitivity = 1f;
        public bool InvertY = false;
    }
}

