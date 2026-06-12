using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using NightHunt.Diagnostics;
using NightHunt.Gameplay.Character;
using NightHunt.Gameplay.Core.Events;
using NightHunt.Gameplay.Match;
using NightHunt.Gameplay.Spawn;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.Zone;
using NightHunt.Networking;
using NightHunt.Networking.Player;
using NightHunt.Utilities;

namespace NightHunt.Gameplay.Respawn
{
    /// <summary>
    /// Server-authoritative respawn system. Normal respawns require a beacon.
    /// An optional one-time final-zone revival can revive players waiting without one.
    /// </summary>
    public class RespawnSystem : NetworkBehaviour, IRespawnProvider
    {
        [Header("Respawn Settings")]
        [SerializeField] private RespawnConfig _respawnConfig;

        [Header("Dependencies")]
        [SerializeField] private SpawnSystem _spawnSystem;
        [SerializeField] private MatchEndManager _matchEndManager;

        private readonly Dictionary<NetworkPlayer, float> _respawnTimers = new();
        private readonly HashSet<NetworkPlayer> _waitingForFinalZone = new();
        private bool _finalZoneRevivalProcessed;
        private bool _hasLocalDisposition;
        private int _localDispositionPlayerObjectId;
        private RespawnDisposition _localDisposition;
        private float _localDispositionDelay;
        private float _localDispositionReceivedAt;
        private string _localDispositionReason;

        public bool HasPendingRespawn(int teamId)
        {
            foreach (var player in _respawnTimers.Keys)
            {
                if (player != null && player.TeamId == teamId)
                    return true;
            }

            foreach (var player in _waitingForFinalZone)
            {
                if (player != null && player.TeamId == teamId)
                    return true;
            }

            return false;
        }

        [Server]
        public void ForceFinalZoneRespawn(float delaySeconds, string reason)
        {
            _finalZoneRevivalProcessed = true;
            _waitingForFinalZone.Clear();
            _respawnTimers.Clear();

            NetworkPlayer[] players = RegistryService.Instance?.GetAllPlayers();
            if (players == null)
                return;

            float delay = Mathf.Max(0f, delaySeconds);
            int queued = 0;
            foreach (NetworkPlayer player in players)
            {
                if (player == null || !IsPlayerDead(player))
                    continue;

                QueueRespawn(player, delay, string.IsNullOrEmpty(reason) ? "forced_final_zone" : reason);
                queued++;
            }

            PhaseTestLog.Log(
                PhaseTestLogCategory.Death,
                "ForcedFinalZoneRespawnQueued",
                $"deadPlayers={queued} delay={delay:F2} reason={reason}",
                this);
        }

        public bool TryGetLocalDisposition(
            NetworkPlayer player,
            out RespawnDisposition disposition,
            out float remainingDelay,
            out string reason)
        {
            disposition = _localDisposition;
            remainingDelay = 0f;
            reason = _localDispositionReason;

            if (!_hasLocalDisposition || player == null || _localDispositionPlayerObjectId != (int)player.ObjectId)
                return false;

            remainingDelay = Mathf.Max(0f, _localDispositionDelay - (Time.unscaledTime - _localDispositionReceivedAt));
            return true;
        }

        public void ClearLocalDisposition()
        {
            _hasLocalDisposition = false;
            _localDispositionReason = string.Empty;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (_matchEndManager == null)
                _matchEndManager = FindFirstObjectByType<MatchEndManager>();
            _matchEndManager?.RegisterRespawnProvider(this);
        }

        private void Update()
        {
            if (!IsServerInitialized)
                return;

            bool isInFinalZone = SafeZoneManager.Instance?.IsInFinalZone ?? false;
            if (isInFinalZone && !_finalZoneRevivalProcessed)
                ProcessFinalZoneRevival();

            _waitingForFinalZone.RemoveWhere(player => player == null || !IsPlayerDead(player));

            var playersToRespawn = new List<NetworkPlayer>();
            var playersToRemove = new List<NetworkPlayer>();
            var teamsToRecheck = new HashSet<int>();

            foreach (var kvp in new List<KeyValuePair<NetworkPlayer, float>>(_respawnTimers))
            {
                NetworkPlayer player = kvp.Key;
                if (player == null || !IsPlayerDead(player))
                {
                    playersToRemove.Add(player);
                    continue;
                }

                // Final-zone revival timers do not consume or require a beacon.
                if (!isInFinalZone && FindRespawnBeacon(player) == null)
                {
                    playersToRemove.Add(player);
                    if (ShouldWaitForFinalZone())
                    {
                        WaitForFinalZone(player, "beacon_destroyed");
                    }
                    else
                    {
                        NotifyDisposition(player, RespawnDisposition.Eliminated, 0f, "beacon_destroyed");
                        teamsToRecheck.Add(player.TeamId);
                    }
                    continue;
                }

                float timer = kvp.Value - Time.deltaTime;
                _respawnTimers[player] = timer;
                if (timer <= 0f)
                    playersToRespawn.Add(player);
            }

            foreach (var player in playersToRemove)
                _respawnTimers.Remove(player);

            foreach (var player in playersToRespawn)
            {
                _respawnTimers.Remove(player);
                if (player != null && IsPlayerDead(player))
                    RespawnPlayer(player);
            }

            foreach (int teamId in teamsToRecheck)
                _matchEndManager?.RecheckEliminationForTeam(teamId);
        }

        [Server]
        public void ServerInitiateRespawn(NetworkPlayer player)
        {
            if (player == null || !IsPlayerDead(player))
                return;

            if (_respawnTimers.TryGetValue(player, out float remaining))
            {
                NotifyDisposition(player, RespawnDisposition.Queued, remaining, "already_queued");
                return;
            }

            if (_waitingForFinalZone.Contains(player))
            {
                NotifyDisposition(player, RespawnDisposition.WaitingForFinalZone, 0f, "waiting_final_zone");
                return;
            }

            bool isInFinalZone = SafeZoneManager.Instance?.IsInFinalZone ?? false;
            if (isInFinalZone)
            {
                if (!_finalZoneRevivalProcessed && IsFinalZoneRevivalEnabled())
                    WaitForFinalZone(player, "waiting_final_zone");
                else
                    EliminatePlayer(player, "respawn_disabled");
                return;
            }

            if (FindRespawnBeacon(player) != null)
            {
                QueueRespawn(player, GetRespawnDelay(), "beacon");
                return;
            }

            if (ShouldWaitForFinalZone())
                WaitForFinalZone(player, "no_beacon");
            else
                EliminatePlayer(player, "no_beacon");
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestRespawn(NetworkPlayer player, NetworkConnection conn = null)
        {
            if (player == null || !IsPlayerDead(player))
                return;

            if (conn == null)
                conn = player.Owner;

            if (conn != null && player.Owner != conn)
            {
                Debug.LogWarning($"[RespawnSystem] Reject respawn request: caller does not own {player.DisplayName}.");
                return;
            }

            ServerInitiateRespawn(player);
        }

        [Server]
        private void ProcessFinalZoneRevival()
        {
            _finalZoneRevivalProcessed = true;

            if (!IsFinalZoneRevivalEnabled())
            {
                var teamsToRecheck = new HashSet<int>();
                foreach (var player in _respawnTimers.Keys)
                {
                    if (player == null)
                        continue;
                    NotifyDisposition(player, RespawnDisposition.Eliminated, 0f, "final_zone_respawn_disabled");
                    teamsToRecheck.Add(player.TeamId);
                }

                foreach (var player in _waitingForFinalZone)
                {
                    if (player == null)
                        continue;
                    NotifyDisposition(player, RespawnDisposition.Eliminated, 0f, "final_zone_respawn_disabled");
                    teamsToRecheck.Add(player.TeamId);
                }

                _respawnTimers.Clear();
                _waitingForFinalZone.Clear();
                foreach (int teamId in teamsToRecheck)
                    _matchEndManager?.RecheckEliminationForTeam(teamId);
                return;
            }

            _waitingForFinalZone.Clear();
            _respawnTimers.Clear();

            float delay = _respawnConfig != null
                ? Mathf.Max(0f, _respawnConfig.FinalZoneReviveDelaySeconds)
                : 3f;

            NetworkPlayer[] players = RegistryService.Instance?.GetAllPlayers();
            if (players == null)
                return;

            foreach (NetworkPlayer player in players)
            {
                if (player != null && IsPlayerDead(player))
                    QueueRespawn(player, delay, "final_zone_revive");
            }

            PhaseTestLog.Log(
                PhaseTestLogCategory.Death,
                "FinalZoneRevivalQueued",
                $"deadPlayers={_respawnTimers.Count} delay={delay:F2}",
                this);
        }

        [Server]
        private void QueueRespawn(NetworkPlayer player, float delay, string reason)
        {
            _waitingForFinalZone.Remove(player);
            _respawnTimers[player] = Mathf.Max(0f, delay);
            NotifyDisposition(player, RespawnDisposition.Queued, delay, reason);

            PhaseTestLog.Log(
                PhaseTestLogCategory.Death,
                "RespawnQueued",
                $"player={player.DisplayName} team={player.TeamId} delay={delay:F2} reason={reason}",
                this);
        }

        [Server]
        private void WaitForFinalZone(NetworkPlayer player, string reason)
        {
            _respawnTimers.Remove(player);
            _waitingForFinalZone.Add(player);
            NotifyDisposition(player, RespawnDisposition.WaitingForFinalZone, 0f, reason);

            PhaseTestLog.Log(
                PhaseTestLogCategory.Death,
                "RespawnWaitingFinalZone",
                $"player={player.DisplayName} team={player.TeamId} reason={reason}",
                this);
        }

        [Server]
        private void EliminatePlayer(NetworkPlayer player, string reason)
        {
            _respawnTimers.Remove(player);
            _waitingForFinalZone.Remove(player);
            NotifyDisposition(player, RespawnDisposition.Eliminated, 0f, reason);

            PhaseTestLog.Log(
                PhaseTestLogCategory.Death,
                "RespawnUnavailable",
                $"player={player.DisplayName} team={player.TeamId} reason={reason}",
                this);
        }

        [Server]
        private void RespawnPlayer(NetworkPlayer player)
        {
            if (player == null)
                return;

            bool isInFinalZone = SafeZoneManager.Instance?.IsInFinalZone ?? false;
            Vector3 respawnPosition = GetRespawnPosition(player);
            Quaternion respawnRotation = player.transform.rotation;

            if (!isInFinalZone)
                FindRespawnBeacon(player)?.TakeDamage(int.MaxValue);

            var movement = ComponentResolver.Find<IMovementController>(player)
                .OnSelf().InChildren()
                .OrLogWarning("[Auto] IMovementController not found").Resolve();
            if (movement != null)
                movement.Teleport(respawnPosition, respawnRotation);
            else
                player.transform.position = respawnPosition;

            var statSystem = ComponentResolver.Find<IPlayerStatSystem>(player)
                .OnSelf().InChildren()
                .OrLogWarning("[Auto] IPlayerStatSystem not found").Resolve();
            if (statSystem is NightHunt.Gameplay.StatSystem.Systems.PlayerStatSystem concrete)
            {
                float maxHealth = concrete.GetStat(PlayerStatType.MaxHealth);
                concrete.SetCurrentStat(PlayerStatType.Health, maxHealth);
            }

            _waitingForFinalZone.Remove(player);
            player.SetAlive(true);
            _matchEndManager?.RecheckEliminationForTeam(player.TeamId);

            PhaseTestLog.Log(
                PhaseTestLogCategory.Death,
                "RespawnApplyComplete",
                $"player={player.DisplayName} team={player.TeamId} pos={player.transform.position:F2}",
                this);
        }

        private Vector3 GetRespawnPosition(NetworkPlayer player)
        {
            if (SafeZoneManager.Instance?.IsInFinalZone ?? false)
                return GetSafeZonePosition();

            RespawnBeacon beacon = FindRespawnBeacon(player);
            Vector3 basePosition = beacon != null ? beacon.transform.position : GetDefaultSpawnPosition(player);

            // Loop-death guard: a beacon/spawn point may now sit OUTSIDE the shrunk safe
            // zone. Respawning there means the next zone damage tick kills the player again
            // immediately — looking like the player is "immortal but bleeding out". Pull the
            // respawn point back inside the current safe zone so the player gets a fair start.
            return ClampInsideSafeZone(basePosition);
        }

        /// <summary>
        /// If <paramref name="worldPos"/> lies outside the current safe-zone circle, returns the
        /// closest point a safe margin inside the zone edge. Otherwise returns it unchanged.
        /// Server-only; no-op when no SafeZoneManager / zone radius is active.
        /// </summary>
        private Vector3 ClampInsideSafeZone(Vector3 worldPos)
        {
            SafeZoneManager manager = SafeZoneManager.Instance;
            if (manager == null || manager.CurrentRadius <= 0f)
                return worldPos;

            if (manager.IsInsideSafeZone(worldPos))
                return worldPos;

            // Keep a margin inside the edge so the player isn't clipped by the ring instantly.
            float safeRadius = Mathf.Max(0f, manager.CurrentRadius * 0.85f);
            Vector3 center = manager.CurrentCenter;
            Vector3 flatDelta = worldPos - center;
            flatDelta.y = 0f;

            // Degenerate case (respawn point == center): jitter so we don't stack everyone on one spot.
            if (flatDelta.sqrMagnitude < 0.01f)
            {
                Vector2 jitter = Random.insideUnitCircle * safeRadius;
                flatDelta = new Vector3(jitter.x, 0f, jitter.y);
            }

            Vector3 clampedFlat = Vector3.ClampMagnitude(flatDelta, safeRadius);
            Vector3 result = center + clampedFlat;
            result.y = worldPos.y; // preserve original ground height

            PhaseTestLog.Log(
                PhaseTestLogCategory.Death,
                "RespawnClampedToZone",
                $"from={worldPos:F1} to={result:F1} zoneCenter={center:F1} zoneRadius={manager.CurrentRadius:F1}",
                this);

            return result;
        }

        private RespawnBeacon FindRespawnBeacon(NetworkPlayer player)
        {
            foreach (RespawnBeacon beacon in RespawnBeacon.All)
            {
                if (beacon != null && beacon.IsActive && player != null && beacon.CanRespawnHere(player.TeamId))
                    return beacon;
            }

            return null;
        }

        private Vector3 GetSafeZonePosition()
        {
            SafeZoneManager manager = SafeZoneManager.Instance;
            if (manager != null)
            {
                float configuredRadius = _respawnConfig != null ? _respawnConfig.SafeZoneRespawnRadius : 20f;
                float radius = Mathf.Max(0f, Mathf.Min(configuredRadius, manager.CurrentRadius * 0.75f));
                Vector2 offset = Random.insideUnitCircle * radius;
                return manager.CurrentCenter + new Vector3(offset.x, 0f, offset.y);
            }

            return GetDefaultSpawnPosition();
        }

        private Vector3 GetDefaultSpawnPosition(NetworkPlayer player = null)
        {
            if (_spawnSystem == null)
                _spawnSystem = SpawnSystem.Instance;

            if (_spawnSystem != null)
            {
                int teamId = player != null ? player.TeamId : -1;
                SpawnPoint spawnPoint = _spawnSystem.GetRandomSpawnPointForTeam(teamId);
                if (spawnPoint == null && teamId != -1)
                    spawnPoint = _spawnSystem.GetRandomSpawnPointForTeam(-1);
                if (spawnPoint != null)
                    return spawnPoint.GetSpawnPosition();
            }

            Debug.LogWarning("[RespawnSystem] No SpawnSystem or spawn points found; using Vector3.zero.");
            return Vector3.zero;
        }

        private bool ShouldWaitForFinalZone()
        {
            bool isInFinalZone = SafeZoneManager.Instance?.IsInFinalZone ?? false;
            return !isInFinalZone && IsFinalZoneRevivalEnabled() && !_finalZoneRevivalProcessed;
        }

        private bool IsFinalZoneRevivalEnabled()
        {
            return _respawnConfig != null && _respawnConfig.ReviveAllDeadPlayersOnFinalZoneStart;
        }

        private float GetRespawnDelay()
        {
            return _respawnConfig != null ? Mathf.Max(0f, _respawnConfig.RespawnDelaySeconds) : 5f;
        }

        private bool IsPlayerDead(NetworkPlayer player)
        {
            if (player == null)
                return false;

            if (!player.IsAlive)
                return true;

            var statSystem = ComponentResolver.Find<IPlayerStatSystem>(player)
                .OnSelf().InChildren().Resolve();
            return statSystem != null && statSystem.GetStat(PlayerStatType.Health) <= 0f;
        }

        [Server]
        private void NotifyDisposition(NetworkPlayer player, RespawnDisposition disposition, float delay, string reason)
        {
            if (player?.Owner != null)
                RpcNotifyRespawnDisposition(
                    player.Owner,
                    (int)player.ObjectId,
                    (int)disposition,
                    Mathf.Max(0f, delay),
                    reason);
        }

        [TargetRpc]
        private void RpcNotifyRespawnDisposition(
            NetworkConnection conn,
            int playerObjectId,
            int disposition,
            float delay,
            string reason)
        {
            _hasLocalDisposition = true;
            _localDispositionPlayerObjectId = playerObjectId;
            _localDisposition = (RespawnDisposition)disposition;
            _localDispositionDelay = delay;
            _localDispositionReceivedAt = Time.unscaledTime;
            _localDispositionReason = reason;

            GameplayEventBus.Instance?.Publish(new RespawnDispositionEvent
            {
                Disposition = _localDisposition,
                DelaySeconds = delay,
                Reason = reason
            });
        }
    }
}
