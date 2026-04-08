using UnityEngine;
using NightHunt.Networking;
using NightHunt.Networking.Player;
using NightHunt.Gameplay.Team;

namespace NightHunt.GameplaySystems.UI.Minimap
{
    /// <summary>
    /// Self-contained minimap marker. Attach to the "MinimapMarker" child GO inside
    /// the player prefab.
    ///
    /// On Awake:
    ///   • Forces gameObject.layer = LayerMask.NameToLayer("Minimap") so only
    ///     the MinimapCamera (cullingMask = Minimap) renders this marker.
    ///   • Reads the owning NetworkPlayer via GetComponentInParent.
    ///
    /// On Start:
    ///   • Reads team color from TeamService and applies it to the SpriteRenderer.
    ///
    /// No dependency on MinimapUI or MinimapCameraController.
    ///
    /// Inspector setup:
    ///   _markerRenderer — SpriteRenderer on this GO (assign a small circle sprite)
    ///   _markerScale    — uniform local scale multiplier applied in Awake
    /// </summary>
    public class MinimapMarkerController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _markerRenderer;
        [SerializeField] private float          _markerScale = 1f;

        private NetworkPlayer _owner;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            gameObject.layer = LayerMask.NameToLayer("Minimap");

            _owner = GetComponentInParent<NetworkPlayer>();

            if (_markerScale != 1f)
                transform.localScale = Vector3.one * _markerScale;
        }

        private void Start()
        {
            RefreshColor();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void RefreshColor()
        {
            if (_owner == null || TeamService.Instance == null) return;

            var color = TeamService.Instance.GetTeamColor(_owner.TeamId);
            if (_markerRenderer != null)
                _markerRenderer.color = color;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Show or hide this marker (player death, despawn, out-of-vision, etc.).
        /// </summary>
        public void SetVisible(bool visible) =>
            gameObject.SetActive(visible);

        /// <summary>
        /// Override the marker color — e.g. to tint the local player's own dot
        /// differently from teammates.
        /// </summary>
        public void SetColor(Color color)
        {
            if (_markerRenderer != null)
                _markerRenderer.color = color;
        }
    }
}
