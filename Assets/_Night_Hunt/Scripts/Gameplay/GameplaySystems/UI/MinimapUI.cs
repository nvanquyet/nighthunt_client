using UnityEngine;
using UnityEngine.UI;
using NightHunt.Networking;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.UI
{
    /// <summary>
    /// Minimap UI — renders a top-down camera feed to a RawImage in the HUD.
    ///
    /// Inspector setup
    ///   1. Create a child Camera GameObject (MinimapCamera) under the player prefab
    ///      with renderMode = Depth, cullingMask = Minimap layer, Orthographic, clear flags = Solid Color.
    ///   2. Create a RenderTexture asset (e.g. 256×256) and assign to MinimapCamera.targetTexture.
    ///   3. Assign the same RenderTexture to <see cref="_minimapTexture"/> here.
    ///   4. The <see cref="_minimapRawImage"/> displays that RenderTexture.
    ///   5. Call <see cref="SetLocalPlayer"/> from GameHUD.Initialize.
    ///
    /// Zone circle
    ///   • Assign a <see cref="GameObject"/> with a thin ring Image to <see cref="_zoneCircleImage"/>.
    ///   • Call <see cref="SetZone"/> from your zone system when radius/centre changes.
    ///
    /// Teammate dots
    ///   • Assign a small circle prefab to <see cref="_teammateDotPrefab"/>.
    ///   • Call <see cref="RefreshTeammates"/> with the current NetworkPlayer list.
    /// </summary>
    public class MinimapUI : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Display")]
        [SerializeField] private RawImage          _minimapRawImage;
        [SerializeField] private RenderTexture     _minimapTexture;

        [Header("Minimap Camera")]
        [Tooltip("Camera that renders the top-down view. " +
                 "Should be a child of the player OR follow-cam logic runs here.")]
        [SerializeField] private Camera _minimapCamera;
        [Tooltip("Height above player at which minimap camera sits")]
        [SerializeField] private float  _cameraHeight   = 50f;
        [Tooltip("Orthographic size (half the world units visible vertically)")]
        [SerializeField] private float  _orthoSize      = 60f;

        [Header("Player indicator")]
        [Tooltip("Small arrow/dot image representing the local player (always at centre)")]
        [SerializeField] private RectTransform _playerIndicator;

        [Header("Zone circle")]
        [Tooltip("Ring image whose RectTransform.sizeDelta is scaled to match world zone radius")]
        [SerializeField] private RectTransform _zoneCircleRect;

        [Header("Teammates")]
        [Tooltip("Parent transform for teammate dot instances")]
        [SerializeField] private Transform  _teammateDotParent;
        [Tooltip("Small circle prefab spawned per teammate")]
        [SerializeField] private GameObject _teammateDotPrefab;
        [SerializeField] private Color      _teammateColor = Color.cyan;

        [Header("Minimap panel border clip")]
        [Tooltip("RawImage's RectTransform: controls the minimap panel size on screen")]
        [SerializeField] private RectTransform _minimapPanelRect;

        // ── Runtime ───────────────────────────────────────────────────────────

        private NetworkPlayer _localPlayer;
        private Vector3       _zoneCenter;
        private float         _zoneRadius;

        // ── Public API ────────────────────────────────────────────────────────

        public void SetLocalPlayer(NetworkPlayer player)
        {
            _localPlayer = player;

            if (_minimapCamera != null)
            {
                // Make sure RenderTexture is assigned
                if (_minimapTexture != null)
                    _minimapCamera.targetTexture = _minimapTexture;

                _minimapCamera.orthographic     = true;
                _minimapCamera.orthographicSize = _orthoSize;
            }

            if (_minimapRawImage != null && _minimapTexture != null)
                _minimapRawImage.texture = _minimapTexture;
        }

        /// <summary>
        /// Update zone circle overlay.
        /// <paramref name="worldRadius"/> in world-space units.
        /// </summary>
        public void SetZone(Vector3 centre, float worldRadius)
        {
            _zoneCenter = centre;
            _zoneRadius = worldRadius;
            UpdateZoneCircle();
        }

        /// <summary>
        /// Refresh teammate dots. Pass all players in the same team (excluding local).
        /// </summary>
        public void RefreshTeammates(NetworkPlayer[] teammates)
        {
            if (_teammateDotParent == null || _teammateDotPrefab == null) return;

            // Clear old
            foreach (Transform child in _teammateDotParent)
                Destroy(child.gameObject);

            if (teammates == null) return;

            foreach (var tm in teammates)
            {
                if (tm == null || tm == _localPlayer) continue;

                var dot    = Instantiate(_teammateDotPrefab, _teammateDotParent);
                var img    = ComponentResolver.Find<UnityEngine.UI.Image>(dot)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] UnityEngine.UI.Image not found")
        .Resolve();
                if (img != null) img.color = _teammateColor;
                // Position updated each frame in Update
                dot.name = $"Dot_{tm.name}";
            }
        }

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void LateUpdate()
        {
            if (_localPlayer == null) return;

            FollowPlayer();
            UpdateZoneCircle();   // recalcs each frame in case scale changes
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void FollowPlayer()
        {
            if (_minimapCamera == null) return;

            Vector3 playerPos = _localPlayer.transform.position;

            // Camera follows player on X/Z, fixed height
            _minimapCamera.transform.position = new Vector3(playerPos.x, playerPos.y + _cameraHeight, playerPos.z);
            _minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // top-down

            // Rotate player indicator to match player's Y rotation
            if (_playerIndicator != null)
                _playerIndicator.localEulerAngles = new Vector3(0f, 0f,
                    -_localPlayer.transform.eulerAngles.y);
        }

        private void UpdateZoneCircle()
        {
            if (_zoneCircleRect == null) return;

            if (_zoneRadius <= 0f)
            {
                _zoneCircleRect.gameObject.SetActive(false);
                return;
            }

            _zoneCircleRect.gameObject.SetActive(true);

            // Scale the zone circle in minimap pixel space:
            //   worldUnitsPerPixel = (orthoSize * 2) / minimapHeight
            //   pixelRadius = zoneRadius / worldUnitsPerPixel
            float mapHeight = _minimapPanelRect != null ? _minimapPanelRect.rect.height : 256f;
            float unitsPerPixel = (_orthoSize * 2f) / mapHeight;
            float pixelRadius   = _zoneRadius / unitsPerPixel;
            _zoneCircleRect.sizeDelta = new Vector2(pixelRadius * 2f, pixelRadius * 2f);

            // Centre offset in pixels
            if (_localPlayer != null && _minimapPanelRect != null)
            {
                Vector3 offset = _zoneCenter - _localPlayer.transform.position;
                float px = offset.x / unitsPerPixel;
                float pz = offset.z / unitsPerPixel;
                _zoneCircleRect.anchoredPosition = new Vector2(px, pz);
            }
        }
    }
}
