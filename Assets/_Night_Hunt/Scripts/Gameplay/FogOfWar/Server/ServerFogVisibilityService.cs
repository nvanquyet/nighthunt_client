using System.Collections.Generic;
using NightHunt.Core;
using NightHunt.GameplaySystems.Core.Configs;
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
        private readonly Dictionary<(int, int), bool> _lastLoggedVisibility = new();

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
            float resolvedVision = visionRange > 0.1f ? visionRange : _defaultVisionRange;
            _players[netObjId] = new PlayerEntry
            {
                Transform   = t,
                TeamId      = teamId,
                VisionRange = resolvedVision
            };
            LogNetwork($"[NH_FOW][REGISTER] player={netObjId} team={teamId} vision={resolvedVision:F1} pos={FormatTransform(t)}");
            // Immediate rebuild so the condition has valid data on the first observer check.
            RebuildVisibilityMap();
        }

        /// <summary>Unregister a player. Called from NetworkPlayer.OnStopServer.</summary>
        public void UnregisterPlayer(int netObjId)
        {
            if (_players.Remove(netObjId))
                LogNetwork($"[NH_FOW][UNREGISTER] player={netObjId}");
            PurgeEntriesFor(netObjId);
        }

        /// <summary>
        /// Update team and vision range for a registered player.
        /// Called from NetworkPlayer.SetPublicData (server-side) whenever player data changes.
        /// </summary>
        public void UpdatePlayerData(int netObjId, int teamId, float visionRange)
        {
            if (!_players.TryGetValue(netObjId, out var e))
            {
                LogNetwork($"[NH_FOW][DATA_MISS] UpdatePlayerData before RegisterPlayer player={netObjId} team={teamId} vision={visionRange:F1}");
                return;
            }

            float resolvedVision = visionRange > 0.1f ? visionRange : _defaultVisionRange;
            _players[netObjId] = new PlayerEntry
            {
                Transform   = e.Transform,
                TeamId      = teamId,
                VisionRange = resolvedVision
            };
            LogNetwork($"[NH_FOW][DATA] player={netObjId} team={teamId} vision={resolvedVision:F1} pos={FormatTransform(e.Transform)}");
            RebuildVisibilityMap();
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
            LogNetwork($"[NH_FOW][REGISTER] tracked={netObjId} team={teamId} always={alwaysVisible} pos={FormatTransform(t)}");
        }

        /// <summary>Unregister a tracked non-player object.</summary>
        public void UnregisterTracked(int netObjId)
        {
            if (_tracked.Remove(netObjId))
                LogNetwork($"[NH_FOW][UNREGISTER] tracked={netObjId}");
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
                        SetVisibility(oid, tid, true, "self", 0f, false);
                        continue;
                    }

                    if (target.Transform == null) continue;

                    if (observer.TeamId == target.TeamId)
                    {
                        SetVisibility(oid, tid, true, "same-team", 0f, false);
                        continue;
                    }

                    bool visible = HasLoS(
                        observer.Transform.position,
                        observer.VisionRange,
                        target.Transform.position,
                        out float distance,
                        out bool blocked,
                        out string blockedBy);
                    SetVisibility(oid, tid, visible, visible ? "los" : (blocked ? "blocked" : "out-of-range"), distance, blocked, blockedBy);
                }

                // ── Player ↔ Tracked non-player objects ──────────────────────
                foreach (var (tid, obj) in _tracked)
                {
                    if (obj.Transform == null) continue;

                    if (obj.AlwaysVisible)
                    {
                        SetVisibility(oid, tid, true, "always", 0f, false);
                        continue;
                    }

                    if (observer.TeamId == obj.TeamId)
                    {
                        SetVisibility(oid, tid, true, "same-team", 0f, false);
                        continue;
                    }

                    bool visible = HasLoS(
                        observer.Transform.position,
                        observer.VisionRange,
                        obj.Transform.position,
                        out float distance,
                        out bool blocked,
                        out string blockedBy);
                    SetVisibility(oid, tid, visible, visible ? "los" : (blocked ? "blocked" : "out-of-range"), distance, blocked, blockedBy);
                }
            }
        }

        /// <summary>
        /// Returns true if a straight-line Physics.Linecast from the observer's eye-level
        /// to the target's eye-level is unobstructed and within VisionRange.
        /// Uses <see cref="NightHuntLayers.MaskFOWObstacles"/> (Wall + MapStatic + MapObstacle).
        /// +1 m eye offset avoids ground-level misses on uneven terrain.
        /// </summary>
        private bool HasLoS(
            Vector3 observerPos,
            float visionRange,
            Vector3 targetPos,
            out float distance,
            out bool blocked,
            out string blockedBy)
        {
            const float eyeHeight = 1f;
            Vector3 from = observerPos + Vector3.up * eyeHeight;
            Vector3 to   = targetPos   + Vector3.up * eyeHeight;

            distance = Vector3.Distance(from, to);
            blocked = false;
            blockedBy = "";
            if (distance > visionRange)
                return false;

            blocked = Physics.Linecast(from, to, out RaycastHit hit, _losMask, QueryTriggerInteraction.Ignore);
            if (blocked && hit.collider != null)
            {
                GameObject hitObject = hit.collider.gameObject;
                blockedBy = $"{hitObject.name}@{LayerMask.LayerToName(hitObject.layer)}";
            }
            return !blocked;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SetVisibility(
            int observerId,
            int targetId,
            bool visible,
            string reason,
            float distance,
            bool blocked,
            string blockedBy = "")
        {
            var key = (observerId, targetId);
            _visibility[key] = visible;

            if (!IsNetworkDebugEnabled())
                return;

            if (_lastLoggedVisibility.TryGetValue(key, out bool previous) && previous == visible)
                return;

            _lastLoggedVisibility[key] = visible;
            Debug.Log(
                $"[NH_FOW][VIS] observer={observerId} target={targetId} visible={visible} reason={reason} " +
                $"dist={distance:F1} blocked={blocked} blockedBy={blockedBy}");
        }

        private void PurgeEntriesFor(int id)
        {
            _purgeBuffer.Clear();
            foreach (var k in _visibility.Keys)
                if (k.Item1 == id || k.Item2 == id)
                    _purgeBuffer.Add(k);
            foreach (var k in _purgeBuffer)
            {
                _visibility.Remove(k);
                _lastLoggedVisibility.Remove(k);
            }
        }

        private static bool IsNetworkDebugEnabled()
        {
            var cfg = NightHuntDebugConfig.Instance;
            return cfg != null && cfg.EnableNetworkDebugLogs;
        }

        private static void LogNetwork(string message)
        {
            if (IsNetworkDebugEnabled())
                Debug.Log(message);
        }

        private static string FormatTransform(Transform t)
        {
            if (t == null)
                return "null";

            Vector3 p = t.position;
            return $"({p.x:F1},{p.y:F1},{p.z:F1})";
        }
    }
}
