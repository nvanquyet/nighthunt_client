using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Gameplay.Zone;

namespace NightHunt.UI
{
    /// <summary>
    /// Client HUD for the PUBG-style safe zone ring.
    ///
    /// Subscribes to SafeZoneHUDProxy static events — decoupled from server objects.
    ///
    /// Inspector setup:
    ///   _zoneRingImage   : RawImage that represents the white ring circle on the minimap/world
    ///   _countdownText   : TMP label showing seconds until next shrink (or "CLOSING" while shrinking)
    ///   _zoneLabel       : TMP label showing "ZONE 1" / "ZONE 2" / "FINAL ZONE"
    ///   _shrinkWarning   : GameObject shown only while zone is actively closing
    ///   _damageVignette  : Image shown at screen edges when outside zone (toggled externally or here)
    /// </summary>
    public class SafeZoneHUD : MonoBehaviour
    {
        [Header("Zone Ring (minimap or world overlay)")]
        [Tooltip("RawImage rendered as the safe-zone white circle. Scale driven by radius.")]
        [SerializeField] private RectTransform _zoneRingTransform;

        [Header("Text Labels")]
        [SerializeField] private TextMeshProUGUI _zoneLabel;
        [SerializeField] private TextMeshProUGUI _countdownText;

        [Header("Warning")]
        [Tooltip("Activated while zone is actively shrinking.")]
        [SerializeField] private GameObject _shrinkWarning;

        [Header("Damage Vignette")]
        [Tooltip("Screen-edge vignette shown when local player is outside the zone.")]
        [SerializeField] private GameObject _damageVignette;

        // ── Cached values ─────────────────────────────────────────────────────
        private float   _currentRadius;
        private Vector3 _currentCenter;
        private int     _zoneIndex = -1;
        private bool    _isShrinking;
        private float   _countdown;

        // ── Map scale helper (optional) ───────────────────────────────────────
        [Header("Minimap Scale (optional)")]
        [Tooltip("World-unit diameter that maps to the full minimap width. E.g. 800 for an 800m map.")]
        [SerializeField] private float _worldMapDiameter = 800f;
        [Tooltip("Pixel width of the minimap panel. Ring RectTransform scale is derived from this.")]
        [SerializeField] private float _minimapPixelSize = 256f;

        // ── Unity Lifecycle ───────────────────────────────────────────────────

        private void OnEnable()
        {
            SafeZoneHUDProxy.OnRadiusChanged     += OnRadiusChanged;
            SafeZoneHUDProxy.OnZoneIndexChanged  += OnZoneIndexChanged;
            SafeZoneHUDProxy.OnShrinkStateChanged += OnShrinkStateChanged;
            SafeZoneHUDProxy.OnCountdownChanged  += OnCountdownChanged;
            SafeZoneManager.Instance?.ReplayCurrentHudState();
        }

        private void OnDisable()
        {
            SafeZoneHUDProxy.OnRadiusChanged     -= OnRadiusChanged;
            SafeZoneHUDProxy.OnZoneIndexChanged  -= OnZoneIndexChanged;
            SafeZoneHUDProxy.OnShrinkStateChanged -= OnShrinkStateChanged;
            SafeZoneHUDProxy.OnCountdownChanged  -= OnCountdownChanged;
        }

        private void Update()
        {
            if (_isShrinking && _countdown > 0f)
            {
                // Count down in real time between server sync packets
                _countdown -= Time.deltaTime;
                RefreshCountdownText();
            }
        }

        // ── SafeZoneHUDProxy callbacks ────────────────────────────────────────

        private void OnRadiusChanged(float radius, Vector3 center)
        {
            _currentRadius = radius;
            _currentCenter = center;
            RefreshRing();
        }

        private void OnZoneIndexChanged(int idx)
        {
            _zoneIndex = idx;
            bool isFinal = SafeZoneManager.Instance?.IsInFinalZone ?? false;
            if (_zoneLabel != null)
                _zoneLabel.text = isFinal ? "FINAL ZONE" : $"ZONE {idx + 1}";
        }

        private void OnShrinkStateChanged(bool shrinking)
        {
            _isShrinking = shrinking;
            if (_shrinkWarning != null)
                _shrinkWarning.SetActive(shrinking);
            RefreshCountdownText();
        }

        private void OnCountdownChanged(float seconds)
        {
            _countdown = seconds;
            RefreshCountdownText();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void RefreshRing()
        {
            if (_zoneRingTransform == null) return;

            // Scale ring size proportional to radius vs world map size
            float scale = (_currentRadius * 2f / _worldMapDiameter) * _minimapPixelSize;
            _zoneRingTransform.sizeDelta = new Vector2(scale, scale);

            // Optionally reposition ring center on minimap (requires MinimapUI integration)
        }

        private void RefreshCountdownText()
        {
            if (_countdownText == null) return;

            if (_isShrinking)
            {
                _countdownText.text = "ZONE CLOSING";
                return;
            }

            float remaining = Mathf.Max(0f, _countdown);
            int   minutes   = Mathf.FloorToInt(remaining / 60f);
            int   seconds   = Mathf.FloorToInt(remaining % 60f);
            _countdownText.text = $"NEXT ZONE: {minutes:00}:{seconds:00}";
        }

        /// <summary>
        /// Call externally (e.g., from PlayerHealthComponent or ZoneDamageReceiver)
        /// when the local player enters/exits the zone.
        /// </summary>
        public void SetOutsideZone(bool outside)
        {
            if (_damageVignette != null)
                _damageVignette.SetActive(outside);
        }
    }
}
