using FishNet.Object;
using FOW;
using System.Collections;
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
        private Coroutine _settleRefreshRoutine;

        private void Awake()
        {
            _revealer = GetComponent<FogOfWarRevealer3D>();
            if (_revealer == null)
                _revealer = gameObject.AddComponent<FogOfWarRevealer3D>();

            // Normalize prefab state: start with revealer OFF so we never show
            // vision until server confirms placement + team check passes.
            // (Prevents cases where the prefab was saved with it enabled.)
            _revealer.enabled = false;

            _visibilityBinder = GetComponent<FogTeamVisibilityBinder>();
            if (_visibilityBinder == null)
                _visibilityBinder = gameObject.AddComponent<FogTeamVisibilityBinder>();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _ownerTeamId.OnChange += OnOwnerTeamChanged;
            _isActive.OnChange   += OnIsActiveChanged;
            // Also subscribe _isPlaced directly (base already subscribes via OnIsPlacedChanged
            // override, but we add here for explicit logging during first placement.)
            // Note: base.OnStartNetwork() already wires _isPlaced.OnChange → OnIsPlacedChanged.

            if (SpectateManager.Instance != null)
                SpectateManager.Instance.OnLocalPlayerSet += OnLocalPlayerAvailable;

            SubscribeLocalPlayerTeamChange(SpectateManager.Instance?.GetLocalPlayer());

            // Apply radius BEFORE UpdateVisionState so revealer radius is correct.
            ApplyRevealerRadius();

            // Initial state check. At this point _isPlaced is likely false (set true after StartPlacement).
            Debug.Log($"[VisionWard] OnStartNetwork: isPlaced={_isPlaced.Value} isActive={_isActive.Value} ownerTeam={_ownerTeamId.Value}");
            UpdateVisionState();
            StartSettleRefresh("OnStartNetwork");
        }

        /// <summary>
        /// Server-only init override that also sets the vision radius from the data definition.
        /// Called by DeployablePlacementHandler when VisionRadius > 0 on the DeployableDefinition.
        /// </summary>
        [Server]
        public void InitializeWithRadius(int teamId, int maxHP, float radius)
        {
            base.Initialize(teamId, maxHP);
            if (radius > 0f)
            {
                visionRadius = radius;
                ApplyRevealerRadius();
            }
            Debug.Log($"[VisionWard] InitializeWithRadius [SERVER]: team={teamId} maxHP={maxHP} radius={visionRadius:F1}");
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

            if (_settleRefreshRoutine != null)
            {
                StopCoroutine(_settleRefreshRoutine);
                _settleRefreshRoutine = null;
            }
        }

        [Server]
        protected override void OnDeployablePlaced()
        {
            base.OnDeployablePlaced();

            // _isPlaced is now true (set by StartPlacement before calling this).
            // _isActive is true (set in Initialize).
            // Client will receive _isPlaced SyncVar delta → OnIsPlacedChanged → UpdateVisionState → enable revealer.
            Debug.Log($"[VisionWard] OnDeployablePlaced [SERVER]: team={_ownerTeamId.Value} isPlaced={_isPlaced.Value} isActive={_isActive.Value} radius={visionRadius}");
            ObserversRefreshVisionStateRpc("placed");

            if (lifetimeSeconds > 0f)
                Invoke(nameof(ExpireWard), lifetimeSeconds);
        }

        [ObserversRpc(BufferLast = true)]
        private void ObserversRefreshVisionStateRpc(string reason)
        {
            Debug.Log($"[VisionWard] ObserversRefreshVisionStateRpc: reason={reason} isPlaced={_isPlaced.Value} isActive={_isActive.Value} ownerTeam={_ownerTeamId.Value}");
            UpdateVisionState();
            StartSettleRefresh($"rpc:{reason}");
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
            StartSettleRefresh("localPlayerAvailable");
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
            {
                UpdateVisionState();
                StartSettleRefresh("localTeamChanged");
            }
        }

        private void UpdateVisionState()
        {
            if (_revealer == null)
            {
                Debug.LogWarning("[VisionWard] UpdateVisionState: revealer is null — component missing?");
                return;
            }

            ApplyRevealerRadius();

            bool shouldEnable = _isPlaced.Value && _isActive.Value && !IsEnemyForLocalClient();

            Debug.Log($"[VisionWard] UpdateVisionState: isPlaced={_isPlaced.Value} isActive={_isActive.Value} " +
                      $"ownerTeam={_ownerTeamId.Value} " +
                      $"localTeam={SpectateManager.Instance?.GetLocalPlayer()?.TeamId.ToString() ?? "null(no local player)"} " +
                      $"isEnemy={IsEnemyForLocalClient()} radius={visionRadius:F1} → revealerEnabled={shouldEnable}");

            // Ensure the revealer's own GameObject is active before toggling the component.
            // (FogOfWarRevealer3D only processes when both the GO and component are active.)
            if (shouldEnable && !gameObject.activeInHierarchy)
            {
                Debug.LogWarning("[VisionWard] Cannot enable revealer: GameObject is inactive.");
                return;
            }

            _revealer.enabled = shouldEnable;
        }

        private void StartSettleRefresh(string reason)
        {
            if (!isActiveAndEnabled)
                return;

            if (_settleRefreshRoutine != null)
                StopCoroutine(_settleRefreshRoutine);

            _settleRefreshRoutine = StartCoroutine(SettleRefreshRoutine(reason));
        }

        private IEnumerator SettleRefreshRoutine(string reason)
        {
            for (int i = 0; i < 3; i++)
            {
                yield return null;
                ApplyRevealerRadius();
                UpdateVisionState();
            }

            Debug.Log($"[VisionWard] SettleRefresh complete: reason={reason} revealerEnabled={(_revealer != null && _revealer.enabled)} radius={visionRadius:F1}");
            _settleRefreshRoutine = null;
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

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.15f); // Semi-transparent cyan
            Gizmos.DrawSphere(transform.position, visionRadius);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, visionRadius);
        }
#endif
    }
}
