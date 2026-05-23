using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using NightHunt.Gameplay.Core.Events;
using NightHunt.Gameplay.Match;
using NightHunt.Gameplay.Respawn;
using NightHunt.Gameplay.Zone;
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
    ///     the prefab from <see cref="BeaconDefinition.NetworkBeaconPrefab"/> (via
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

        [Header("Settings")]
        [Tooltip("Số beacon tối đa mỗi team. Khi đặt mới vượt limit → beacon cũ nhất bị destroy & thay thế.")]
        [SerializeField] private int _maxActivePerTeam = 1;

        [Tooltip("Reference tới SafeZoneManager để block đặt Beacon ở Final Zone.")]
        // _phaseManager removed -- SafeZoneManager.Instance.IsInFinalZone used directly

        // ── Runtime (server) ──────────────────────────────────────────────────
        /// <summary>teamId → list of active beacon NOs.</summary>
        private readonly Dictionary<int, List<RespawnBeacon>> _activeBeacons = new();

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
        /// Called by <see cref="DeployablePlacementHandler"/> ServerRpc.
        /// </summary>
        /// <param name="definitionId">
        /// ItemID of the <see cref="BeaconDefinition"/> ScriptableObject.
        /// Used to look up <see cref="BeaconDefinition.NetworkBeaconPrefab"/> from
        /// <see cref="ItemDatabase"/>.  Null/empty falls back to
        /// <see cref="_beaconPrefabFallback"/>.
        /// </param>
        /// <returns>True on successful spawn; false if rejected.</returns>
        [Server]
        public bool TryPlaceBeacon(
            int               teamId,
            Vector3           position,
            Quaternion        rotation,
            FishNet.Connection.NetworkConnection ownerConn,
            string            definitionId = null)
        {
            // ── Phase Gate: Block placement in Final Zone ────────────────────
            bool isInFinalZone = SafeZoneManager.Instance?.IsInFinalZone ?? false;
            if (isInFinalZone)
            {
                Debug.Log($"[BeaconManager] Beacon placement blocked: Final zone active.");
                return false;
            }

            // ── Enforce per-team limit: Replace oldest if at cap ──────────────
            int current = GetActiveBeaconCount(teamId);
            if (current >= _maxActivePerTeam)
            {
                // Destroy oldest beacon to make room (Replace, not Reject)
                if (_activeBeacons.TryGetValue(teamId, out var existingList) && existingList.Count > 0)
                {
                    var oldest = existingList[0];
                    Debug.Log($"[BeaconManager] Team {teamId} at limit — destroying oldest beacon to replace.");
                    oldest?.TakeDamage(int.MaxValue); // Force-kill
                }
            }

            // Resolve prefab from ItemDatabase (definition's NetworkBeaconPrefab)
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

            // Resolve definition for stats
            var def = ItemDatabase.GetDefinition(definitionId) as BeaconDefinition;
            int maxHP = (def != null) ? def.BeaconHP : 100; // Expected field or default

            // Initialize SyncVars BEFORE network-spawn so clients receive correct state.
            beacon.Initialize(teamId, maxHP);

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
        /// Resolve the beacon prefab: always requires a valid BeaconDefinition from ItemDatabase.
        /// </summary>
        private GameObject ResolvePrefab(string definitionId)
        {
            if (string.IsNullOrEmpty(definitionId))
            {
                Debug.LogError("[BeaconManager] definitionId is null or empty!");
                return null;
            }

            var def = ItemDatabase.GetDefinition(definitionId) as BeaconDefinition;
            if (def?.NetworkBeaconPrefab == null)
            {
                Debug.LogError($"[BeaconManager] Could not resolve NetworkBeaconPrefab for definitionId: {definitionId}");
                return null;
            }

            return def.NetworkBeaconPrefab;
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
