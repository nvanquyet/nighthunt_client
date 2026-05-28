using System.Collections.Generic;
using NightHunt.Core;
using UnityEngine;

namespace NightHunt.Gameplay.FogOfWar
{
    /// <summary>
    /// Server-only service that computes per-tick line-of-sight (LoS) visibility between
    /// all players, then exposes a fast lookup used by <see cref="FogOfWarObserverCondition"/>.
    ///
    /// ══════════════════════════════════════════════════════
    ///  ARCHITECTURE
    /// ══════════════════════════════════════════════════════
    ///  NetworkPlayer.OnStartServer   → RegisterPlayer(...)
    ///  NetworkPlayer.OnStopServer    → UnregisterPlayer(...)
    ///  NetworkPlayer.SetPublicData   → UpdatePlayerData(...)  ← team / vision changes
    ///
    ///  Every _updateInterval seconds (default 0.15 s ≈ every 7 ticks @ 45 Hz):
    ///    1. Iterate all (observer, target) player pairs.
    ///    2. Same team → visible.  AlwaysVisible flag → visible.
    ///    3. Different team → Physics.Linecast with NightHuntLayers.MaskFOWObstacles
    ///       (Wall + MapStatic + MapObstacle) from observer eye to target eye.
    ///       If unobstructed AND within VisionRange → visible.
    ///    4. Store result in _visibility[(observerId, targetId)].
    ///
    ///  FogOfWarObserverCondition.ConditionMet() calls IsVisible() on every FishNet
    ///  observer tick to decide whether to add or remove the connection from the
    ///  target object's observer set (= whether to send network updates to that client).
    ///
    /// ══════════════════════════════════════════════════════
    ///  VISIBILITY RULES (mirrored from FogTeamVisibilityBinder for server parity)
    /// ══════════════════════════════════════════════════════
    ///  • Observer == target             → always true  (own object)
    ///  • Same team                      → always true  (allies)
    ///  • AlwaysVisible == true          → always true  (neutral / dropped loot)
    ///  • Different team + in LoS range  → true
    ///  • Different team + out of range or blocked → false
    ///
    /// ══════════════════════════════════════════════════════
    ///  SCENE SETUP
    /// ══════════════════════════════════════════════════════
    ///  Add this MonoBehaviour to the server scene root (e.g. on the NetworkManager GO
    ///  or a dedicated "ServerServices" object that is active on the Dedicated Server).
    ///  It does nothing on clients (only <see cref="FogOfWarObserverCondition"/> runs server-side
    ///  anyway, but keeping this off clients avoids Update overhead).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class ServerFogVisibilityService : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────
        public static ServerFogVisibilityService Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Tuning")]
        [Tooltip("Seconds between full LoS recomputes. Lower = more accurate, higher = cheaper.")]
        [SerializeField] private float _updateInterval = 0.15f;

        [Tooltip("VisionRange (world units) used when a player's stat system is unavailable.")]
        [SerializeField] private float _defaultVisionRange = 20f;

        // ── Internal data structures ───────────────────────────────────────────
        private struct PlayerEntry
        {
            public Transform Transform;
            public int       TeamId;
            public float     VisionRange;
        }

        private struct TrackedEntry
        {
            public Transform Transform;
            public int       TeamId;
            public bool      AlwaysVisible;
        }

        // NetObjId → registration data
        private readonly Dictionary<int, PlayerEntry>  _players    = new();
        private readonly Dictionary<int, TrackedEntry> _tracked    = new();

        // (observerNetObjId, targetNetObjId) → can observer see target?
        private readonly Dictionary<(int, int), bool> _visibility = new();

        // Reused list to avoid GC in PurgeEntriesFor
        private readonly List<(int, int)> _purgeBuffer = new();

        private LayerMask _losMask;
        private float     _nextUpdateTime;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            _losMask = NightHuntLayers.MaskFOWObstacles;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (Time.time < _nextUpdateTime) return;
            _nextUpdateTime = Time.time + _updateInterval;
            RebuildVisibilityMap();
        }

        // ── Registration API ──────────────────────────────────────────────────

        /// <summary>
        /// Register a player with the service.
        /// Called from NetworkPlayer.OnStartServer once the object is spawned.
        /// </summary>
        /// <param name="netObjId">NetworkObject.ObjectId of the player.</param>
        /// <param name="t">Root transform used for position sampling.</param>
        /// <param name="teamId">Player team ID (from SyncVar).</param>
        /// <param name="visionRange">Player VisionRange stat (world units).</param>
        public void RegisterPlayer(int netObjId, Transform t, int teamId, float visionRange)
        {
            _players[netObjId] = new PlayerEntry
            {
                Transform   = t,
                TeamId      = teamId,
                VisionRange = visionRange > 0.1f ? visionRange : _defaultVisionRange
            };
            // Immediate rebuild so the condition has valid data on the first observer check.
            RebuildVisibilityMap();
        }

        /// <summary>Unregister a player. Called from NetworkPlayer.OnStopServer.</summary>
        public void UnregisterPlayer(int netObjId)
        {
            _players.Remove(netObjId);
            PurgeEntriesFor(netObjId);
        }

        /// <summary>
        /// Update team and vision range for a registered player.
        /// Called from NetworkPlayer.SetPublicData (server-side) whenever player data changes.
        /// </summary>
        public void UpdatePlayerData(int netObjId, int teamId, float visionRange)
        {
            if (!_players.TryGetValue(netObjId, out var e)) return;
            _players[netObjId] = new PlayerEntry
            {
                Transform   = e.Transform,
                TeamId      = teamId,
                VisionRange = visionRange > 0.1f ? visionRange : _defaultVisionRange
            };
        }

        /// <summary>
        /// Register a non-player networked object (deployables, VisionWards, etc.)
        /// that should also be gated by FoW visibility.
        /// </summary>
        public void RegisterTracked(int netObjId, Transform t, int teamId, bool alwaysVisible)
        {
            _tracked[netObjId] = new TrackedEntry
            {
                Transform     = t,
                TeamId        = teamId,
                AlwaysVisible = alwaysVisible
            };
        }

        /// <summary>Unregister a tracked non-player object.</summary>
        public void UnregisterTracked(int netObjId)
        {
            _tracked.Remove(netObjId);
            PurgeEntriesFor(netObjId);
        }

        // ── Query API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Returns whether the player identified by <paramref name="observerNetObjId"/>
        /// can currently see the object identified by <paramref name="targetNetObjId"/>.
        ///
        /// Fails open (returns true) if either party is not registered — this prevents
        /// accidentally culling objects that were not set up (map geometry, UI, etc.).
        /// </summary>
        public bool IsVisible(int observerNetObjId, int targetNetObjId)
        {
            if (_visibility.TryGetValue((observerNetObjId, targetNetObjId), out bool v))
                return v;
            return true;
        }

        // ── Core Compute ──────────────────────────────────────────────────────

        private void RebuildVisibilityMap()
        {
            _visibility.Clear();

            foreach (var (oid, observer) in _players)
            {
                if (observer.Transform == null) continue;

                // ── Player ↔ Player ──────────────────────────────────────────
                foreach (var (tid, target) in _players)
                {
                    if (tid == oid)
                    {
                        _visibility[(oid, tid)] = true;
                        continue;
                    }

                    if (target.Transform == null) continue;

                    _visibility[(oid, tid)] = observer.TeamId == target.TeamId
                        || HasLoS(observer.Transform.position, observer.VisionRange,
                                  target.Transform.position);
                }

                // ── Player ↔ Tracked non-player objects ──────────────────────
                foreach (var (tid, obj) in _tracked)
                {
                    if (obj.Transform == null) continue;

                    _visibility[(oid, tid)] = obj.AlwaysVisible
                        || observer.TeamId == obj.TeamId
                        || HasLoS(observer.Transform.position, observer.VisionRange,
                                  obj.Transform.position);
                }
            }
        }

        /// <summary>
        /// Returns true if a straight-line Physics.Linecast from the observer's eye-level
        /// to the target's eye-level is unobstructed and within VisionRange.
        /// Uses <see cref="NightHuntLayers.MaskFOWObstacles"/> (Wall + MapStatic + MapObstacle).
        /// +1 m eye offset avoids ground-level misses on uneven terrain.
        /// </summary>
        private bool HasLoS(Vector3 observerPos, float visionRange, Vector3 targetPos)
        {
            const float eyeHeight = 1f;
            Vector3 from = observerPos + Vector3.up * eyeHeight;
            Vector3 to   = targetPos   + Vector3.up * eyeHeight;

            if (Vector3.Distance(from, to) > visionRange)
                return false;

            return !Physics.Linecast(from, to, _losMask);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void PurgeEntriesFor(int id)
        {
            _purgeBuffer.Clear();
            foreach (var k in _visibility.Keys)
                if (k.Item1 == id || k.Item2 == id)
                    _purgeBuffer.Add(k);
            foreach (var k in _purgeBuffer)
                _visibility.Remove(k);
        }
    }
}
