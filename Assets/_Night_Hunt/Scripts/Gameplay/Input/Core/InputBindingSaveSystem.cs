using UnityEngine;
using UnityEngine.InputSystem;

namespace NightHunt.Gameplay.Input.Core
{
    /// <summary>
    /// Persists all InputAction binding overrides across play sessions via PlayerPrefs.
    ///
    /// USAGE:
    ///   • Call <see cref="LoadBindings"/> once, BEFORE any ActionMap is enabled
    ///     (hooked into <see cref="InputLayerManager"/> after BuildLayerCache).
    ///   • Call <see cref="SaveBindings"/> after every interactive rebind
    ///     (hooked into <see cref="NightHunt.UI.Settings.RebindActionUI.FinishRebind"/>).
    ///   • Call <see cref="ResetAllBindings"/> from the Settings "Reset to Defaults" button.
    ///
    /// SAVE FORMAT:
    ///   Single JSON string via InputActionAsset.SaveBindingOverridesAsJson().
    ///   PlayerPrefs key: "NH_InputBindings_v1" — bump version if schema changes.
    /// </summary>
    public static class InputBindingSaveSystem
    {
        private const string PrefsKey = "NH_InputBindings_v2";
        private const string LegacyPrefsKey = "NH_InputBindings_v1";

        // ── Save ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// Serialise all binding overrides on the asset to PlayerPrefs.
        /// Call after every successful rebind.
        /// </summary>
        public static void SaveBindings(InputActionAsset asset)
        {
            if (asset == null)
            {
                Debug.LogWarning("[InputBindings] SaveBindings called with null asset.");
                return;
            }

            string json = asset.SaveBindingOverridesAsJson();
            PlayerPrefs.SetString(PrefsKey, json);
            PlayerPrefs.Save();
            Debug.Log($"[InputBindings] Saved {json.Length} chars to PlayerPrefs key='{PrefsKey}'.");
        }

        // ── Load ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// Restore all binding overrides from PlayerPrefs onto the asset.
        /// Call BEFORE the first ActionMap.Enable() — typically inside BuildLayerCache().
        /// </summary>
        public static void LoadBindings(InputActionAsset asset)
        {
            if (asset == null)
            {
                Debug.LogWarning("[InputBindings] LoadBindings called with null asset.");
                return;
            }

            string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                if (PlayerPrefs.HasKey(LegacyPrefsKey))
                {
                    PlayerPrefs.DeleteKey(LegacyPrefsKey);
                    PlayerPrefs.Save();
                    Debug.Log("[InputBindings] Removed legacy v1 binding overrides so updated defaults can apply.");
                }

                Debug.Log("[InputBindings] No saved bindings found — using defaults.");
                return;
            }

            try
            {
                asset.LoadBindingOverridesFromJson(json);
                Debug.Log($"[InputBindings] Loaded binding overrides from PlayerPrefs ({json.Length} chars).");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[InputBindings] Failed to load bindings — resetting. Error: {ex.Message}");
                PlayerPrefs.DeleteKey(PrefsKey);
            }
        }

        // ── Reset ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Remove all binding overrides from the asset and clear the saved data.
        /// Use for "Reset to Defaults" in the Controls settings panel.
        /// </summary>
        public static void ResetAllBindings(InputActionAsset asset)
        {
            if (asset == null)
            {
                Debug.LogWarning("[InputBindings] ResetAllBindings called with null asset.");
                return;
            }

            asset.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.DeleteKey(LegacyPrefsKey);
            PlayerPrefs.Save();
            Debug.Log("[InputBindings] All binding overrides removed and PlayerPrefs cleared.");
        }

        // ── Query ──────────────────────────────────────────────────────────────────

        /// <summary>Returns true if there are saved binding overrides in PlayerPrefs.</summary>
        public static bool HasSavedBindings()
            => PlayerPrefs.HasKey(PrefsKey);
    }
}
