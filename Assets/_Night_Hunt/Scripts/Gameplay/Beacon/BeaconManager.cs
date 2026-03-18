using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using NightHunt.Gameplay.Core.Events;
using NightHunt.Gameplay.Match;
using NightHunt.Gameplay.Respawn;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using UnityEngine;
using NightHunt.Utilities;

namespace NightHunt.Gameplay.Beacon
{
    /// <summary>
    /// Server-authoritative manager for all active <see cref="RespawnBeacon"/>s.
    ///
    /// Responsibilities:
    ///   • Enforce max-active-beacon-per-team limit (from <see cref="maxActivePerTeam"/>).
    ///   • Spawn beacon NetworkObjects when players request placement; resolves
    ///     the prefab from <see cref="BeaconDefinition.BeaconPrefab"/> (via
    ///     ItemDatabase) if a definitionId is supplied, otherwise falls back to
    ///     the inspector-assigned <see cref="_beaconPrefabFallback"/>.
    ///   • Call <see cref="RespawnBeacon.Initialize"/> then
    ///     <see cref="RespawnBeacon.StartPlacement"/> on every newly spawned beacon.
    ///   • Track beacon count per team; fire <see cref="BeaconDestroyedEvent"/>
    ///     via <see cref="GameplayEventBus"/> on destruction.
    ///   • Implement <see cref="IBeaconProvider"/> so
    ///     <see cref="MatchEndManager"/> can query active counts.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BeaconManager : NetworkBehaviour, IBeaconProvider
    {
        // ── Singleton ──────────────────────────────────────────────────────────
        public static BeaconManager Instance { get; private set; }

        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("References")]
        [SerializeField] private MatchEndManager _matchEndManager;

        [Tooltip("Fallback prefab used when a BeaconDefinition cannot be resolved " +
                 "from the ItemDatabase (e.g. legacy calls without a definitionId).")]
        [SerializeField] private GameObject _beaconPrefabFallback;

        [Header("Settings")]
        [Tooltip("Config driving beacon limits and timing. If null, uses built-in defaults.")]
        [SerializeField] private RespawnConfig _respawnConfig;

        // ── Runtime (server) ──────────────────────────────────────────────────
        /// <summary>teamId → list of active beacon NOs.</summary>
        private readonly Dictionary<int, List<RespawnBeacon>> _activeBeacons = new();

        private int _maxActivePerTeam;

        // ──────────────────────────────────────────────────────────────────────
        #region Unity / FishNet Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (_matchEndManager == null)
                _matchEndManager = FindFirstObjectByType<MatchEndManager>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            _maxActivePerTeam = _respawnConfig != null ? _respawnConfig.MaxBeaconsPerTeam : 2;

            _matchEndManager?.RegisterBeaconProvider(this);
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            _activeBeacons.Clear();
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region IBeaconProvider

        /// <summary>Returns how many beacons team <paramref name="teamId"/> has active.</summary>
        public int GetActiveBeaconCount(int teamId)
            => _activeBeacons.TryGetValue(teamId, out var list) ? list.Count : 0;

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region Placement (server)

        /// <summary>
        /// Validate and spawn a beacon for <paramref name="teamId"/> at the given pose.
        /// Called by <see cref="BeaconPlaceable"/> ServerRpc.
        /// </summary>
        /// <param name="definitionId">
        /// ItemID of the <see cref="BeaconDefinition"/> ScriptableObject.
        /// Used to look up <see cref="BeaconDefinition.BeaconPrefab"/> from
        /// <see cref="ItemDatabase"/>.  Null/empty falls back to
        /// <see cref="_beaconPrefabFallback"/>.
        /// </param>
        /// <returns>True on successful spawn; false if rejected.</returns>
        [Server]
        public bool TryPlaceBeacon(
            int               teamId,
            Vector3           position,
            Quaternion        rotation,
            NetworkConnection ownerConn,
            string            definitionId = null)
        {
            // Enforce per-team limit
            int current = GetActiveBeaconCount(teamId);
            if (current >= _maxActivePerTeam)
            {
                Debug.Log($"[BeaconManager] Team {teamId} at beacon limit ({_maxActivePerTeam}). Rejected.");
                return false;
            }

            // Resolve prefab from ItemDatabase (prefers definition's BeaconPrefab)
            GameObject prefab = ResolvePrefab(definitionId);
            if (prefab == null)
            {
                Debug.LogError("[BeaconManager] No beacon prefab available!");
                return false;
            }

            // Instantiate
            var go     = Instantiate(prefab, position, rotation);
            var beacon = ComponentResolver.Find<RespawnBeacon>(go)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] RespawnBeacon not found")
        .Resolve();
            if (beacon == null)
            {
                Debug.LogError("[BeaconManager] Beacon prefab is missing RespawnBeacon component!");
                Destroy(go);
                return false;
            }

            // Initialize SyncVars BEFORE network-spawn so clients receive correct state.
            beacon.Initialize(teamId);

            // Network-spawn (all clients see it; ownerConn gets ownership).
            InstanceFinder.ServerManager.Spawn(go, ownerConn);

            // Start server-side placement timer / activation effects.
            beacon.StartPlacement();

            // Register for destruction callbacks.
            RegisterBeacon(teamId, beacon);

            Debug.Log($"[BeaconManager] Team {teamId} spawned beacon at {position}. " +
                      $"Active: {GetActiveBeaconCount(teamId)}/{_maxActivePerTeam}");
            return true;
        }

        /// <summary>
        /// Resolve the beacon prefab: prefer <see cref="BeaconDefinition.BeaconPrefab"/>
        /// from the ItemDatabase, fall back to the inspector-assigned prefab.
        /// </summary>
        private GameObject ResolvePrefab(string definitionId)
        {
            if (!string.IsNullOrEmpty(definitionId))
            {
                var def = ItemDatabase.GetDefinition(definitionId) as BeaconDefinition;
                if (def?.BeaconPrefab != null)
                    return def.BeaconPrefab;
            }
            return _beaconPrefabFallback;
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region Tracking

        private void RegisterBeacon(int teamId, RespawnBeacon beacon)
        {
            if (!_activeBeacons.ContainsKey(teamId))
                _activeBeacons[teamId] = new List<RespawnBeacon>();

            _activeBeacons[teamId].Add(beacon);
            beacon.Destroyed += () => OnBeaconDestroyed(teamId, beacon);
        }

        private void OnBeaconDestroyed(int teamId, RespawnBeacon beacon)
        {
            if (!IsServerStarted) return;

            if (_activeBeacons.TryGetValue(teamId, out var list))
                list.Remove(beacon);

            int remaining = GetActiveBeaconCount(teamId);
            Debug.Log($"[BeaconManager] Team {teamId} beacon destroyed. Remaining: {remaining}");

            GameplayEventBus.Instance?.Publish(new BeaconDestroyedEvent
            {
                OwnerTeamId          = teamId,
                RemainingBeaconCount = remaining
            });
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region Utility

        /// <summary>Returns all active beacons for a team (server-only read).</summary>
        public IReadOnlyList<RespawnBeacon> GetActiveBeacons(int teamId)
            => _activeBeacons.TryGetValue(teamId, out var list)
                ? list
                : System.Array.Empty<RespawnBeacon>();

        #endregion
    }
}
