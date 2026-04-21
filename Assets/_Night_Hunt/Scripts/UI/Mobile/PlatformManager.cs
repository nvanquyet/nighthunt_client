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
        //  Inspector
        // ─────────────────────────────────────────────────────────────────────

        [Tooltip("Force mobile UI in the Editor for testing without a real mobile device.")]
        [SerializeField] private bool _forceMobile;

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns true when running on a mobile platform or when
        /// <see cref="_forceMobile"/> is set in the Inspector.
        /// Safe to call before Awake (falls back to <c>Application.isMobilePlatform</c>).
        /// </summary>
        public static bool IsMobile =>
            Application.isMobilePlatform || (_instance != null && _instance._forceMobile);

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
