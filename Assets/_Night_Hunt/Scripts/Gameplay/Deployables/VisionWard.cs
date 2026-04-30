using FishNet.Object;
using FOW;
using NightHunt.Gameplay.FogOfWar;
using NightHunt.Gameplay.Spectator;
using NightHunt.Networking.Player;
using UnityEngine;

namespace NightHunt.Gameplay.Deployables
{
    /// <summary>
    /// Team-owned deployable revealer. Allied clients enable the FOW revealer, enemy
    /// clients keep it disabled. Health, placement state, and despawn come from BaseDeployable.
    /// </summary>
    [RequireComponent(typeof(FogOfWarRevealer3D))]
    public class VisionWard : BaseDeployable
    {
        [Header("Vision Config")]
        [SerializeField, Min(0f)] private float visionRadius = 15f;
        [SerializeField, Min(0f)] private float lifetimeSeconds = 120f;

        private FogOfWarRevealer3D _revealer;
        private FogTeamVisibilityBinder _visibilityBinder;
        private NetworkPlayer _subscribedLocalPlayer;

        private void Awake()
        {
            _revealer = GetComponent<FogOfWarRevealer3D>();
            if (_revealer == null)
                _revealer = gameObject.AddComponent<FogOfWarRevealer3D>();

            _visibilityBinder = GetComponent<FogTeamVisibilityBinder>();
            if (_visibilityBinder == null)
                _visibilityBinder = gameObject.AddComponent<FogTeamVisibilityBinder>();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _ownerTeamId.OnChange += OnOwnerTeamChanged;
            _isActive.OnChange += OnIsActiveChanged;

            if (SpectateManager.Instance != null)
                SpectateManager.Instance.OnLocalPlayerSet += OnLocalPlayerAvailable;

            SubscribeLocalPlayerTeamChange(SpectateManager.Instance?.GetLocalPlayer());
            ApplyRevealerRadius();
            UpdateVisionState();
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _ownerTeamId.OnChange -= OnOwnerTeamChanged;
            _isActive.OnChange -= OnIsActiveChanged;

            if (SpectateManager.Instance != null)
                SpectateManager.Instance.OnLocalPlayerSet -= OnLocalPlayerAvailable;

            if (_subscribedLocalPlayer != null)
            {
                _subscribedLocalPlayer.OnPublicDataChanged -= OnLocalTeamChanged;
                _subscribedLocalPlayer = null;
            }
        }

        [Server]
        protected override void OnDeployablePlaced()
        {
            base.OnDeployablePlaced();

            Debug.Log($"[VisionWard] Team {_ownerTeamId.Value} placed vision ward. hp={_currentHP.Value} radius={visionRadius}");

            if (lifetimeSeconds > 0f)
                Invoke(nameof(ExpireWard), lifetimeSeconds);
        }

        [Server]
        private void ExpireWard()
        {
            if (_isActive.Value)
                OnDeployableDestroyed();
        }

        protected override void OnIsPlacedChanged(bool oldVal, bool newVal, bool asServer)
        {
            base.OnIsPlacedChanged(oldVal, newVal, asServer);
            UpdateVisionState();
        }

        private void OnOwnerTeamChanged(int oldVal, int newVal, bool asServer) => UpdateVisionState();

        private void OnIsActiveChanged(bool oldVal, bool newVal, bool asServer) => UpdateVisionState();

        private void OnLocalPlayerAvailable(NetworkPlayer localPlayer)
        {
            SubscribeLocalPlayerTeamChange(localPlayer);
            UpdateVisionState();
        }

        private void SubscribeLocalPlayerTeamChange(NetworkPlayer localPlayer)
        {
            if (localPlayer == null || localPlayer == _subscribedLocalPlayer)
                return;

            if (_subscribedLocalPlayer != null)
                _subscribedLocalPlayer.OnPublicDataChanged -= OnLocalTeamChanged;

            _subscribedLocalPlayer = localPlayer;
            _subscribedLocalPlayer.OnPublicDataChanged += OnLocalTeamChanged;
        }

        private void OnLocalTeamChanged(PlayerPublicData prev, PlayerPublicData next)
        {
            if (prev.TeamId != next.TeamId)
                UpdateVisionState();
        }

        private void UpdateVisionState()
        {
            if (_revealer == null)
                return;

            ApplyRevealerRadius();

            if (!_isPlaced.Value || !_isActive.Value)
            {
                _revealer.enabled = false;
                return;
            }

            _revealer.enabled = !IsEnemyForLocalClient();
        }

        private void ApplyRevealerRadius()
        {
            if (_revealer != null)
                _revealer.ViewRadius = visionRadius;
        }

        private bool IsEnemyForLocalClient()
        {
            var localPlayer = SpectateManager.Instance?.GetLocalPlayer();
            if (localPlayer == null)
                return false;

            return localPlayer.TeamId != _ownerTeamId.Value;
        }
    }
}
