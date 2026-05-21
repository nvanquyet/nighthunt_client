using UnityEngine;

namespace NightHunt.UI.Mobile
{
    /// <summary>
    /// Centralizes mobile/desktop platform detection for the UI layer.
    ///
    /// Place one instance in the persistent scene (e.g. on the HUD canvas root).
    /// Other components call <see cref="IsMobile"/> instead of scattering
    /// <c>Application.isMobilePlatform</c> checks throughout the codebase.
    ///
    /// If no instance exists in the scene, <see cref="IsMobile"/> falls back to
    /// <c>Application.isMobilePlatform</c> so the project builds correctly even
    /// without the singleton present.
    /// </summary>
    public class PlatformManager : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Singleton
        // ─────────────────────────────────────────────────────────────────────

        private static PlatformManager _instance;

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns true when the active input platform is Touch.
        /// Delegates to <see cref="NightHunt.Gameplay.Input.Core.PlatformInputDetector"/> which
        /// handles both real platform detection and debug overrides (forceMobileInput / forceDesktopInput).
        /// Falls back to <c>Application.isMobilePlatform</c> if the detector is not yet alive.
        /// </summary>
        public static bool IsMobile =>
            NightHunt.Gameplay.Input.Core.PlatformInputDetector.Instance != null
                ? NightHunt.Gameplay.Input.Core.PlatformInputDetector.Instance.IsMobile
                : Application.isMobilePlatform;

        // ─────────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[PlatformManager] Duplicate instance detected — destroying extra.");
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
