using FOW;
using NightHunt.Gameplay.Character;
using NightHunt.Gameplay.Spectator;
using NightHunt.Networking;
using NightHunt.Networking.Player;
using NightHunt.Utilities;
using UnityEngine;

namespace NightHunt.Gameplay.FogOfWar
{
    /// <summary>
    /// Controls whether this object should be hidden by Fog of War for the local client.
    ///
    /// VISIBILITY RULES (applied to every networked object in the scene):
    ///
    ///   1. AlwaysVisible (IFogTeamOwned.FogAlwaysVisible == true OR no IFogTeamOwned found):
    ///      → No FogOfWarHider attached  → object always rendered
    ///      → Examples: map geometry, neutral pickups, ally players, ally deployables
    ///
    ///   2. Same team as local player (FogOwnerTeamId == localTeamId):
    ///      → No FogOfWarHider          → always visible (ally)
    ///      → Examples: ally grenade, ally VisionWard, ally player
    ///
    ///   3. Different team (FogOwnerTeamId != localTeamId):
    ///      → FogOfWarHider + HiderDisableRenderers attached
    ///      → Object hidden outside any FogOfWarRevealer3D radius
    ///      → Examples: enemy player, enemy grenade, enemy VisionWard
    ///
    /// TEAM IDENTITY SOURCE (via IFogTeamOwned interface):
    ///   • NetworkPlayer       → IFogTeamOwned.FogOwnerTeamId = NetworkPlayer.TeamId
    ///   • BaseDeployable      → IFogTeamOwned.FogOwnerTeamId = BaseDeployable.OwnerTeamId
    ///   • ProjectileNetworked → IFogTeamOwned.FogOwnerTeamId = thrower's TeamId (set in Initialize)
    ///   • Any custom object   → implement IFogTeamOwned and this binder handles it automatically
    ///
    /// SETUP on prefabs:
    ///   Add FogTeamVisibilityBinder to the root NetworkBehaviour GO.
    ///   Ensure the GO (or a child/parent) implements IFogTeamOwned.
    ///   This binder finds it via ComponentResolver at Awake.
    /// </summary>
    [DisallowMultipleComponent]
    public class FogTeamVisibilityBinder : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool _logDecisions;

        private FogOfWarHider       _hider;
        private HiderDisableRenderers _hiderBehavior;

        // Primary team source: unified interface (preferred over legacy fields).
        private IFogTeamOwned _teamOwned;

        // Legacy fallback references — kept only for spectate-override logic on NetworkPlayer.
        private NetworkPlayer      _networkPlayer;
        private PlayerModelLoader  _modelLoader;

        // Tracks local player subscription for team-change callbacks.
        private NetworkPlayer _subscribedLocalPlayer;

        public bool IsEnemyToLocal { get; private set; }
        public event System.Action<bool> OnEnemyStateChanged;

        // ─────────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            // Resolve the unified team identity interface first.
            // ComponentResolver searches Self → Children → Parent so it handles any prefab structure.
            _teamOwned = ComponentResolver.Find<IFogTeamOwned>(this)
                .OnSelf().InChildren().InParent()
                .Resolve();

            // NetworkPlayer is still needed for:
            //   (a) OnPublicDataChanged event (re-run when team changes mid-game)
            //   (b) Spectate-override in FogVisionBinder
            _networkPlayer = ComponentResolver.Find<NetworkPlayer>(this)
                .OnSelf().InChildren().InParent()
                .Resolve();

            if (_networkPlayer != null)
            {
                // When the player has a NetworkPlayer, it IS the IFogTeamOwned source.
                // If for some reason it was not resolved via the interface (old prefab), fall back.
                if (_teamOwned == null && _networkPlayer is IFogTeamOwned np)
                    _teamOwned = np;
            }

            // Existing FogOfWarHider on the prefab (if any) — adopt rather than duplicate.
            _hider = ComponentResolver.Find<FogOfWarHider>(this)
                .OnSelf().InChildren().InParent()
                .Resolve();

            // Model loader for refreshing child renderers after model hot-swap.
            _modelLoader = ComponentResolver.Find<PlayerModelLoader>(this)
                .OnSelf().OnRoot().InRootChildren()
                .Resolve();
        }

        private void Start()
        {
            // Retry if IFogTeamOwned not found (network not initialized yet).
            if (_teamOwned == null)
                StartCoroutine(WaitForTeamOwned());

            // Subscribe to NetworkPlayer team changes.
            if (_networkPlayer != null)
                _networkPlayer.OnPublicDataChanged += OnNetworkPlayerDataChanged;

            // Subscribe to local player availability (late-join / spectate changes).
            if (SpectateManager.Instance != null)
                SpectateManager.Instance.OnLocalPlayerSet += OnLocalPlayerAvailable;

            // Initial refresh.
            RefreshVisibilityForLocalTeam();
            var local = SpectateManager.Instance?.GetLocalPlayer();
            if (local != null)
                SubscribeLocalPlayerTeamChange(local);

            // Refresh renderers when player model loads.
            if (_modelLoader != null)
            {
                _modelLoader.OnModelReady += OnModelReadyForFog;
                if (_modelLoader.CurrentModelInstance != null)
                    OnModelReadyForFog(_modelLoader.CurrentModelInstance);
            }
        }

        private System.Collections.IEnumerator WaitForTeamOwned()
        {
            int maxFrames = 60;
            for (int i = 0; i < maxFrames; i++)
            {
                yield return null;

                _teamOwned = ComponentResolver.Find<IFogTeamOwned>(this)
                    .OnSelf().InChildren().InParent()
                    .Resolve();

                if (_networkPlayer == null)
                    _networkPlayer = ComponentResolver.Find<NetworkPlayer>(this)
                        .OnSelf().InChildren().InParent()
                        .Resolve();

                if (_teamOwned != null)
                {
                    if (_networkPlayer != null)
                        _networkPlayer.OnPublicDataChanged += OnNetworkPlayerDataChanged;

                    var local = SpectateManager.Instance?.GetLocalPlayer();
                    if (local != null)
                        SubscribeLocalPlayerTeamChange(local);

                    RefreshVisibilityForLocalTeam();
                    yield break;
                }
            }

            Log("IFogTeamOwned not found after 60 frames — treating object as always visible (neutral).");
            RemoveHiderIfExists();
            SetEnemyState(false);
        }

        private void OnDestroy()
        {
            if (_networkPlayer != null)
                _networkPlayer.OnPublicDataChanged -= OnNetworkPlayerDataChanged;

            if (SpectateManager.Instance != null)
                SpectateManager.Instance.OnLocalPlayerSet -= OnLocalPlayerAvailable;

            if (_subscribedLocalPlayer != null)
                _subscribedLocalPlayer.OnPublicDataChanged -= OnLocalTeamChanged;

            if (_modelLoader != null)
                _modelLoader.OnModelReady -= OnModelReadyForFog;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Event Handlers
        // ─────────────────────────────────────────────────────────────────────

        private void OnNetworkPlayerDataChanged(PlayerPublicData prev, PlayerPublicData next)
        {
            if (prev.TeamId != next.TeamId)
                RefreshVisibilityForLocalTeam();
        }

        private void OnLocalPlayerAvailable(NetworkPlayer localPlayer)
        {
            if (SpectateManager.Instance != null)
                SpectateManager.Instance.OnLocalPlayerSet -= OnLocalPlayerAvailable;

            SubscribeLocalPlayerTeamChange(localPlayer);
            RefreshVisibilityForLocalTeam();
        }

        private void SubscribeLocalPlayerTeamChange(NetworkPlayer local)
        {
            if (local == null || local == _subscribedLocalPlayer) return;
            if (_subscribedLocalPlayer != null)
                _subscribedLocalPlayer.OnPublicDataChanged -= OnLocalTeamChanged;
            _subscribedLocalPlayer = local;
            _subscribedLocalPlayer.OnPublicDataChanged += OnLocalTeamChanged;
        }

        private void OnLocalTeamChanged(PlayerPublicData prev, PlayerPublicData next)
        {
            if (prev.TeamId != next.TeamId)
                RefreshVisibilityForLocalTeam();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Core Visibility Logic
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Re-evaluates team relationship and applies/removes FogOfWarHider accordingly.
        /// Call this whenever local team or object team may have changed.
        /// Returns true if team was resolved; false if still waiting for data.
        /// </summary>
        public bool RefreshVisibilityForLocalTeam()
        {
            // Rule 1: AlwaysVisible or no IFogTeamOwned found → never hide.
            if (_teamOwned == null || _teamOwned.FogAlwaysVisible)
            {
                Log(_teamOwned == null
                    ? "No IFogTeamOwned found — defaulting to always visible."
                    : $"FogAlwaysVisible=true (teamId={_teamOwned.FogOwnerTeamId}) — never hiding.");
                RemoveHiderIfExists();
                SetEnemyState(false);
                return _teamOwned != null;
            }

            // Rule 2: Neutral (TeamId == -1) → always visible.
            int objectTeamId = _teamOwned.FogOwnerTeamId;
            if (objectTeamId < 0)
            {
                Log($"Neutral object (teamId={objectTeamId}) — always visible.");
                RemoveHiderIfExists();
                SetEnemyState(false);
                return true;
            }

            // Rule 3: Wait for local player.
            if (!TryGetLocalTeamId(out int localTeamId))
            {
                Log("Local player not ready — defaulting to visible, waiting for OnLocalPlayerSet.");
                RemoveHiderIfExists();
                SetEnemyState(false);
                return false;
            }

            // Rule 4: Team comparison.
            bool isEnemy = objectTeamId != localTeamId;
            SetEnemyState(isEnemy);

            if (isEnemy)
            {
                EnsureHiderExists();
                Log($"ENEMY (localTeam={localTeamId}, objectTeam={objectTeamId}) → FogOfWarHider ON.");
            }
            else
            {
                RemoveHiderIfExists();
                Log($"ALLY  (localTeam={localTeamId}, objectTeam={objectTeamId}) → FogOfWarHider OFF.");
            }

            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        private void SetEnemyState(bool isEnemy)
        {
            if (IsEnemyToLocal != isEnemy)
            {
                IsEnemyToLocal = isEnemy;
                OnEnemyStateChanged?.Invoke(IsEnemyToLocal);
            }
        }

        private bool TryGetLocalTeamId(out int teamId)
        {
            teamId = -1;
            if (SpectateManager.Instance == null) return false;
            var local = SpectateManager.Instance.GetLocalPlayer();
            if (local == null) return false;
            teamId = local.TeamId;
            return teamId >= 0;
        }

        private void EnsureHiderExists()
        {
            var host = GetHiderHost();
            if (_hider != null && _hider.transform != host)
                RemoveHiderIfExists();

            if (_hider != null) return;

            _hider = host.gameObject.AddComponent<FogOfWarHider>();
            _hiderBehavior = host.gameObject.AddComponent<HiderDisableRenderers>();
            RefreshHiderRenderers();
        }

        private void RemoveHiderIfExists()
        {
            if (_hider == null) return;

            // Disable first so FogOfWarHider.OnDisable() fires synchronously and restores renderers.
            _hider.enabled = false;

            if (_hiderBehavior != null)
            {
                Destroy(_hiderBehavior);
                _hiderBehavior = null;
            }

            Destroy(_hider);
            _hider = null;
        }

        private void OnModelReadyForFog(GameObject _) => RefreshHiderRenderers();

        private void RefreshHiderRenderers()
        {
            if (_hiderBehavior == null) return;
            var host = GetHiderHost();
            _hiderBehavior.ModifyHiddenRenderers(
                host.GetComponentsInChildren<Renderer>(includeInactive: true));
        }

        private Transform GetHiderHost()
        {
            // Use the NetworkPlayer transform as host when available (player prefab).
            if (_networkPlayer != null) return _networkPlayer.transform;

            // For other objects (deployables, projectiles), use this transform.
            return transform;
        }

        private void Log(string msg)
        {
            if (_logDecisions)
                Debug.Log($"[FogTeamVisibilityBinder:{name}] {msg}", this);
        }
    }
}
