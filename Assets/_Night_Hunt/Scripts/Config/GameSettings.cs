using NightHunt.Core;
using NightHunt.Audio;
using UnityEngine;
using UnityEngine.InputSystem;

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
                // Graphics
                QualityLevel = PlayerPrefs.GetInt("NH_QualityLevel", QualitySettings.GetQualityLevel()),
                VSync        = PlayerPrefs.GetInt("NH_VSync",        1) == 1,
                Fullscreen   = PlayerPrefs.GetInt("NH_Fullscreen",   1) == 1,
                ResolutionIndex = PlayerPrefs.GetInt("NH_ResolutionIndex", -1),

                // Extended Graphics
                AntiAliasing         = PlayerPrefs.GetInt("NH_AntiAliasing", 0),
                AnisotropicFiltering = PlayerPrefs.GetInt("NH_AnisotropicFiltering", 0),
                ShadowQuality        = PlayerPrefs.GetInt("NH_ShadowQuality", 2),
                TextureQuality       = PlayerPrefs.GetInt("NH_TextureQuality", 0),
                DrawDistance         = PlayerPrefs.GetFloat("NH_DrawDistance", 500f),
                Bloom                = PlayerPrefs.GetInt("NH_Bloom", 1) == 1,
                MotionBlur           = PlayerPrefs.GetInt("NH_MotionBlur", 1) == 1,

                // Extended Gameplay
                Language             = PlayerPrefs.GetString("NH_Language", "English"),
                EnableTutorials      = PlayerPrefs.GetInt("NH_EnableTutorials", 1) == 1,
                EnableDevConsole     = PlayerPrefs.GetInt("NH_EnableDevConsole", 0) == 1,
                QuickSwap            = PlayerPrefs.GetInt("NH_QuickSwap", 1) == 1,
                ShowFPS              = PlayerPrefs.GetInt("NH_ShowFPS", 1) == 1,
                ShowPing             = PlayerPrefs.GetInt("NH_ShowPing", 1) == 1,

                // Audio
                MasterVol = PlayerPrefs.GetFloat("NH_Audio_MasterVol", 1f),
                MusicVol  = PlayerPrefs.GetFloat("NH_Audio_MusicVol",  0.8f),
                SFXVol    = PlayerPrefs.GetFloat("NH_Audio_SFXVol",    1f),
                UIVol     = PlayerPrefs.GetFloat("NH_Audio_UIVol",     1f),

                // Controls
                MouseSensitivity           = Mathf.Clamp(PlayerPrefs.GetFloat("NH_MouseSensitivity", 1f), 0.1f, 5f),
                InvertY                    = PlayerPrefs.GetInt("NH_InvertY", 0) == 1,
                MobileCameraDegreesPerPixel = Mathf.Clamp(PlayerPrefs.GetFloat("NH_MobileCameraDegreesPerPixel", 3), 1, 5),

                // Gameplay
                FOV              = PlayerPrefs.GetFloat("NH_FOV",          70f),
                UIScale          = PlayerPrefs.GetFloat("NH_UIScale",      1f),
                CrosshairType    = PlayerPrefs.GetInt("NH_CrosshairType",  0)
            };

            // If first run (no saved UIScale), apply platform defaults
            if (!PlayerPrefs.HasKey("NH_UIScale"))
                ApplyPlatformDefaults();

            LoadInputBindings();
            ApplySettings();
        }

        private void ApplyPlatformDefaults()
        {
            if (UnityEngine.Application.isMobilePlatform)
            {
                UIScale = 1.2f; // Larger UI for mobile
                FOV = 60f;      // Narrower FOV often better for small screens
            }
            else
            {
                UIScale = 1.0f;
                FOV = 70f;
            }
        }

        public void LoadInputBindings()
        {
            string rebinds = PlayerPrefs.GetString("NH_InputRebinds", string.Empty);
            if (string.IsNullOrEmpty(rebinds)) return;

            var inputManager = NightHunt.Gameplay.Input.Core.InputLayerManager.Instance;
            var asset = inputManager != null ? inputManager.Config?.InputActionAsset : null;
            
            if (asset != null)
            {
                asset.LoadBindingOverridesFromJson(rebinds);
                Debug.Log("[GameSettings] Input bindings loaded.");
            }
        }

        public void SaveInputBindings()
        {
            var inputManager = NightHunt.Gameplay.Input.Core.InputLayerManager.Instance;
            var asset = inputManager != null ? inputManager.Config?.InputActionAsset : null;

            if (asset != null)
            {
                string rebinds = asset.SaveBindingOverridesAsJson();
                PlayerPrefs.SetString("NH_InputRebinds", rebinds);
                PlayerPrefs.Save();
                Debug.Log("[GameSettings] Input bindings saved.");
            }
        }

        /// <summary>Save settings to PlayerPrefs.</summary>
        public void SaveSettings()
        {
            if (currentSettings == null) return;

            PlayerPrefs.SetInt("NH_QualityLevel", currentSettings.QualityLevel);
            PlayerPrefs.SetInt("NH_VSync",        currentSettings.VSync      ? 1 : 0);
            PlayerPrefs.SetInt("NH_Fullscreen",   currentSettings.Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt("NH_ResolutionIndex", currentSettings.ResolutionIndex);

            PlayerPrefs.SetInt("NH_AntiAliasing",         currentSettings.AntiAliasing);
            PlayerPrefs.SetInt("NH_AnisotropicFiltering", currentSettings.AnisotropicFiltering);
            PlayerPrefs.SetInt("NH_ShadowQuality",        currentSettings.ShadowQuality);
            PlayerPrefs.SetInt("NH_TextureQuality",       currentSettings.TextureQuality);
            PlayerPrefs.SetFloat("NH_DrawDistance",       currentSettings.DrawDistance);
            PlayerPrefs.SetInt("NH_Bloom",                currentSettings.Bloom ? 1 : 0);
            PlayerPrefs.SetInt("NH_MotionBlur",           currentSettings.MotionBlur ? 1 : 0);

            PlayerPrefs.SetString("NH_Language",          currentSettings.Language);
            PlayerPrefs.SetInt("NH_EnableTutorials",      currentSettings.EnableTutorials ? 1 : 0);
            PlayerPrefs.SetInt("NH_EnableDevConsole",     currentSettings.EnableDevConsole ? 1 : 0);
            PlayerPrefs.SetInt("NH_QuickSwap",            currentSettings.QuickSwap ? 1 : 0);
            PlayerPrefs.SetInt("NH_ShowFPS",              currentSettings.ShowFPS ? 1 : 0);
            PlayerPrefs.SetInt("NH_ShowPing",             currentSettings.ShowPing ? 1 : 0);

            PlayerPrefs.SetFloat("NH_Audio_MasterVol", currentSettings.MasterVol);
            PlayerPrefs.SetFloat("NH_Audio_MusicVol",  currentSettings.MusicVol);
            PlayerPrefs.SetFloat("NH_Audio_SFXVol",    currentSettings.SFXVol);
            PlayerPrefs.SetFloat("NH_Audio_UIVol",     currentSettings.UIVol);

            PlayerPrefs.SetFloat("NH_MouseSensitivity", currentSettings.MouseSensitivity);
            PlayerPrefs.SetInt("NH_InvertY",            currentSettings.InvertY ? 1 : 0);
            PlayerPrefs.SetFloat("NH_MobileCameraDegreesPerPixel", currentSettings.MobileCameraDegreesPerPixel);

            PlayerPrefs.SetFloat("NH_FOV",              currentSettings.FOV);
            PlayerPrefs.SetFloat("NH_UIScale",          currentSettings.UIScale);
            PlayerPrefs.SetInt("NH_CrosshairType",      currentSettings.CrosshairType);

            SaveInputBindings();
            PlayerPrefs.Save();
            OnSettingsChanged?.Invoke();
        }

        public static event System.Action OnSettingsChanged;

        /// <summary>Apply current settings (quality, vsync, fullscreen). </summary>
        public void ApplySettings()
        {
            if (currentSettings == null) return;

        #if !UNITY_SERVER
            QualitySettings.SetQualityLevel(currentSettings.QualityLevel);
            QualitySettings.vSyncCount = currentSettings.VSync ? 1 : 0;
            
            QualitySettings.antiAliasing = currentSettings.AntiAliasing;
            QualitySettings.anisotropicFiltering = (AnisotropicFiltering)currentSettings.AnisotropicFiltering;
            QualitySettings.shadows = (ShadowQuality)currentSettings.ShadowQuality;
            QualitySettings.globalTextureMipmapLimit = currentSettings.TextureQuality;

            // Apply resolution if valid
            if (currentSettings.ResolutionIndex >= 0 && currentSettings.ResolutionIndex < Screen.resolutions.Length)
            {
                var res = Screen.resolutions[currentSettings.ResolutionIndex];
                Screen.SetResolution(res.width, res.height, currentSettings.Fullscreen);
            }
            else
            {
                Screen.fullScreen = currentSettings.Fullscreen;
            }

            // Apply Audio
            if (AudioManager.HasInstance)
            {
                AudioManager.Instance.SetVolume("MasterVol", currentSettings.MasterVol);
                AudioManager.Instance.SetVolume("MusicVol",  currentSettings.MusicVol);
                AudioManager.Instance.SetVolume("SFXVol",    currentSettings.SFXVol);
                AudioManager.Instance.SetVolume("UIVol",     currentSettings.UIVol);
            }
        #endif
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

        public int ResolutionIndex
        {
            get => currentSettings?.ResolutionIndex ?? -1;
            set { if (currentSettings != null) currentSettings.ResolutionIndex = value; }
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

        public float MobileCameraDegreesPerPixel
        {
            get => currentSettings?.MobileCameraDegreesPerPixel ?? 3f;
            set { if (currentSettings != null) currentSettings.MobileCameraDegreesPerPixel = Mathf.Clamp(value, 1f, 5f); }
        }

        public float FOV
        {
            get => currentSettings?.FOV ?? 70f;
            set { if (currentSettings != null) currentSettings.FOV = value; }
        }

        public float UIScale
        {
            get => currentSettings?.UIScale ?? 1f;
            set { if (currentSettings != null) currentSettings.UIScale = value; }
        }

        public int CrosshairType
        {
            get => currentSettings?.CrosshairType ?? 0;
            set { if (currentSettings != null) currentSettings.CrosshairType = value; }
        }

        public int AntiAliasing
        {
            get => currentSettings?.AntiAliasing ?? 0;
            set { if (currentSettings != null) currentSettings.AntiAliasing = value; }
        }

        public int AnisotropicFiltering
        {
            get => currentSettings?.AnisotropicFiltering ?? 0;
            set { if (currentSettings != null) currentSettings.AnisotropicFiltering = value; }
        }

        public int ShadowQuality
        {
            get => currentSettings?.ShadowQuality ?? 2;
            set { if (currentSettings != null) currentSettings.ShadowQuality = value; }
        }

        public int TextureQuality
        {
            get => currentSettings?.TextureQuality ?? 0;
            set { if (currentSettings != null) currentSettings.TextureQuality = value; }
        }

        public float DrawDistance
        {
            get => currentSettings?.DrawDistance ?? 500f;
            set { if (currentSettings != null) currentSettings.DrawDistance = value; }
        }

        public bool Bloom
        {
            get => currentSettings?.Bloom ?? true;
            set { if (currentSettings != null) currentSettings.Bloom = value; }
        }

        public bool MotionBlur
        {
            get => currentSettings?.MotionBlur ?? true;
            set { if (currentSettings != null) currentSettings.MotionBlur = value; }
        }

        public string Language
        {
            get => currentSettings?.Language ?? "English";
            set { if (currentSettings != null) currentSettings.Language = value; }
        }

        public bool EnableTutorials
        {
            get => currentSettings?.EnableTutorials ?? true;
            set { if (currentSettings != null) currentSettings.EnableTutorials = value; }
        }

        public bool EnableDevConsole
        {
            get => currentSettings?.EnableDevConsole ?? false;
            set { if (currentSettings != null) currentSettings.EnableDevConsole = value; }
        }

        public bool QuickSwap
        {
            get => currentSettings?.QuickSwap ?? true;
            set { if (currentSettings != null) currentSettings.QuickSwap = value; }
        }

        public bool ShowFPS
        {
            get => currentSettings?.ShowFPS ?? true;
            set { if (currentSettings != null) currentSettings.ShowFPS = value; }
        }

        public bool ShowPing
        {
            get => currentSettings?.ShowPing ?? true;
            set { if (currentSettings != null) currentSettings.ShowPing = value; }
        }

        public void SetVolume(string key, float value)
        {
            if (currentSettings == null) return;
            switch (key)
            {
                case "MasterVol": currentSettings.MasterVol = value; break;
                case "MusicVol":  currentSettings.MusicVol = value; break;
                case "SFXVol":    currentSettings.SFXVol = value; break;
                case "UIVol":     currentSettings.UIVol = value; break;
            }
        }

        /// <summary>Reset to default settings and save.</summary>
        public void ResetToDefaults()
        {
            currentSettings = new SettingsData();
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
        public int   ResolutionIndex  = -1;

        // Extended Graphics
        public int   AntiAliasing         = 0; // 0, 2, 4, 8
        public int   AnisotropicFiltering = 0; // 0=Disable, 1=Enable, 2=Force
        public int   ShadowQuality        = 2; // 0=Disable, 1=Hard, 2=All
        public int   TextureQuality       = 0; // 0=Full, 1=Half, 2=Quarter
        public float DrawDistance         = 500f;
        public bool  Bloom                = true;
        public bool  MotionBlur           = true;

        // Extended Gameplay
        public string Language            = "English";
        public bool   EnableTutorials     = true;
        public bool   EnableDevConsole    = false;
        public bool   QuickSwap           = true;
        public bool   ShowFPS             = true;
        public bool   ShowPing            = true;

        public float MasterVol = 1f;
        public float MusicVol  = 0.8f;
        public float SFXVol    = 1f;
        public float UIVol     = 1f;

        public float MouseSensitivity            = 1f;
        public bool  InvertY                     = false;
        public float MobileCameraDegreesPerPixel = 3f;
        public float FOV              = 70f;
        public float UIScale          = 1f;
        public int   CrosshairType    = 0;
    }
}

