using UnityEngine;
using NightHunt.Networking;
using NightHunt.Networking.Player;
using NightHunt.Gameplay.Spectator;

namespace NightHunt.GameplaySystems.UI.Minimap
{
    /// <summary>
    /// Drives the minimap camera that follows the currently viewed player.
    /// Attach to "MinimapCameraRig" — a child GO that lives only on the local
    /// player's prefab (deactivate the GO on non-local players in NetworkPlayer.OnOwnerReady).
    ///
    /// The camera's cullingMask, depth and clear flags are set in the Inspector.
    /// Recommended settings:
    ///   cullingMask  = Minimap | Terrain | Map  (NOT Default — main cam handles that)
    ///   depth        = -1 (renders before main camera)
    ///   clearFlags   = Solid Color
    ///   orthographic = true (set by this script)
    ///
    /// Spectate support:
    ///   Subscribes SpectateManager.OnCurrentPlayerChanged in OnEnable so the
    ///   camera automatically follows the spectated player without any external call.
    ///   A catch-up call in OnEnable handles the case where the player was already
    ///   set before this component enabled.
    ///
    /// Inspector setup:
    ///   _minimapCamera  — Camera component on this GO (or child)
    ///   _renderTexture  — RenderTexture asset shared with MinimapUI._renderTexture
    ///   _height         — world-space Y above target (default 60)
    ///   _orthoSize      — orthographic half-size in world units (default 80)
    /// </summary>
    public class MinimapCameraController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera        _minimapCamera;
        [SerializeField] private RenderTexture _renderTexture;

        [Header("Camera Settings")]
        [SerializeField] private float _height    = 60f;
        [SerializeField] private float _orthoSize = 80f;

        private Transform _target;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (_minimapCamera == null)
                _minimapCamera = GetComponentInChildren<Camera>();

            if (_minimapCamera != null)
            {
                _minimapCamera.orthographic     = true;
                _minimapCamera.orthographicSize = _orthoSize;

                if (_renderTexture != null)
                    _minimapCamera.targetTexture = _renderTexture;
            }
        }

        private void OnEnable()
        {
            if (SpectateManager.Instance == null) return;

            SpectateManager.Instance.OnCurrentPlayerChanged += SetTarget;

            // Catch-up: player may already be set before this component enabled.
            var current = SpectateManager.Instance.GetCurrentPlayer();
            if (current != null)
                SetTarget(current);
        }

        private void OnDisable()
        {
            if (SpectateManager.Instance != null)
                SpectateManager.Instance.OnCurrentPlayerChanged -= SetTarget;
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            transform.position = _target.position + Vector3.up * _height;
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Switch the followed target. Invoked by SpectateManager.OnCurrentPlayerChanged
        /// and optionally by GameHUD for explicit initialization.
        /// </summary>
        public void SetTarget(NetworkPlayer player)
        {
            _target = player != null ? player.transform : null;
        }
    }
}
