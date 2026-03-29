using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Gameplay.Match;
using NightHunt.Gameplay.Character;
using NightHunt.Gameplay.Spawn;
using NightHunt.Networking;
using System.Collections.Generic;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Utilities;
using NightHunt.Gameplay.Core.Events;

namespace NightHunt.Gameplay.Respawn
{
    /// <summary>
    /// Respawn system with phase-based rules
    /// </summary>
    public class RespawnSystem : NetworkBehaviour, IRespawnProvider
    {
        [Header("Respawn Settings")]
        [Tooltip("Config source for all respawn delays and beacon limits. If null, falls back to defaults.")]
        [SerializeField] private RespawnConfig _respawnConfig;

        [Header("Dependencies")]
        [Tooltip("Reference to SpawnSystem for team-based fallback spawn points.")]
        [SerializeField]
        private SpawnSystem _spawnSystem;

        [SerializeField] private MatchEndManager _matchEndManager;

        // Synchronized state
        private readonly SyncVar<float> networkRespawnDelay = new SyncVar<float>();

        private MatchPhaseManager phaseManager;
        private Dictionary<NetworkPlayer, float> respawnTimers = new Dictionary<NetworkPlayer, float>();

        // ── IRespawnProvider ───────────────────────────────────────────────
        /// <summary>True if any player on the given team is waiting for a respawn timer.</summary>
        public bool HasPendingRespawn(int teamId)
        {
            foreach (var kvp in respawnTimers)
            {
                if (kvp.Key != null && kvp.Key.TeamId == teamId && kvp.Value > 0f)
                    return true;
            }

            return false;
        }

        private void Awake()
        {
            phaseManager = FindFirstObjectByType<MatchPhaseManager>();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            networkRespawnDelay.OnChange += OnRespawnDelayChanged;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            // Self-register as respawn provider with MatchEndManager
            if (_matchEndManager == null)
                _matchEndManager = FindFirstObjectByType<MatchEndManager>();
            _matchEndManager?.RegisterRespawnProvider(this);
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            networkRespawnDelay.OnChange -= OnRespawnDelayChanged;
        }

        private void Update()
        {
            if (!IsServer) return;

            var playersToRespawn = new List<NetworkPlayer>();
            var playersFailed    = new List<NetworkPlayer>();

            foreach (var kvp in respawnTimers)
            {
                var player = kvp.Key;
                float timer = kvp.Value;

                if (player == null || !IsPlayerDead(player))
                {
                    // Player already respawned or disconnected
                    playersFailed.Add(player);
                    continue;
                }

                // Issue #4: If beacon was destroyed while waiting → cancel timer
                var phase = phaseManager?.CurrentPhase;
                bool needsBeacon = phase != MatchPhaseState.Lockdown;
                if (needsBeacon && FindRespawnBeacon(player) == null)
                {
                    Debug.Log($"[RespawnSystem] {player.DisplayName}: beacon destroyed during wait — respawn cancelled.");
                    RpcNotifyRespawnFailed(player.Owner, "beacon_destroyed");
                    playersFailed.Add(player);
                    continue;
                }

                timer -= Time.deltaTime;
                respawnTimers[player] = timer;

                if (timer <= 0f)
                    playersToRespawn.Add(player);
            }

            foreach (var p in playersFailed)  respawnTimers.Remove(p);
            foreach (var player in playersToRespawn)
            {
                respawnTimers.Remove(player);
                if (player != null && IsPlayerDead(player))
                    RespawnPlayer(player);
            }
        }

        /// <summary>
        /// Server: Request respawn for player
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        public void RequestRespawn(NetworkPlayer player)
        {
            if (player == null) return;
            if (!IsPlayerDead(player)) return;

            // Check phase-based respawn rules
            if (!CanRespawn(player))
            {
                Debug.Log($"[RespawnSystem] Cannot respawn: Phase restrictions");
                return;
            }

            // Calculate respawn delay based on phase
            float delay = GetRespawnDelay();
            respawnTimers[player] = delay;
            networkRespawnDelay.Value = delay;
        }

        /// <summary>
        /// Server: Respawn player
        /// </summary>
        [Server]
        private void RespawnPlayer(NetworkPlayer player)
        {
            if (player == null) return;

            Vector3 respawnPosition = GetRespawnPosition(player);
            Quaternion respawnRotation = player.transform.rotation;

            // Issue #5: Destroy the beacon used to respawn (single-use per respawn)
            var usedBeacon = FindRespawnBeacon(player);
            usedBeacon?.TakeDamage(int.MaxValue);

            // Teleport via IMovementController to properly reset prediction pipeline
            var movement = ComponentResolver.Find<IMovementController>(player)
                .OnSelf().InChildren()
                .OrLogWarning("[Auto] IMovementController not found").Resolve();
            if (movement != null)
                movement.Teleport(respawnPosition, respawnRotation);
            else
                player.transform.position = respawnPosition;

            // Restore health via PlayerStatSystem
            var statSystem = ComponentResolver.Find<IPlayerStatSystem>(player)
                .OnSelf().InChildren()
                .OrLogWarning("[Auto] IPlayerStatSystem not found").Resolve();
            if (statSystem is NightHunt.Gameplay.StatSystem.Systems.PlayerStatSystem concrete)
            {
                float maxHealth = concrete.GetStat(PlayerStatType.MaxHealth);
                concrete.SetCurrentStat(PlayerStatType.Health, maxHealth);
            }

            OnPlayerRespawned(player);
        }

        /// <summary>
        /// Get respawn position based on phase
        /// </summary>
        private Vector3 GetRespawnPosition(NetworkPlayer player)
        {
            var phase = phaseManager?.CurrentPhase;

            if (phase == MatchPhaseState.Lockdown)
                return GetSafeZonePosition();

            // Phase 1-2: Beacon → Default spawn point
            var beacon = FindRespawnBeacon(player);
            if (beacon != null)
                return beacon.transform.position;

            return GetDefaultSpawnPosition(player);
        }

        /// <summary>
        /// Find respawn beacon for player
        /// </summary>
        private RespawnBeacon FindRespawnBeacon(NetworkPlayer player)
        {
            var beacons = FindObjectsByType<RespawnBeacon>(FindObjectsSortMode.None);
            foreach (var beacon in beacons)
            {
                if (beacon != null && beacon.IsActive && player != null && beacon.CanRespawnHere(player.TeamId))
                {
                    return beacon;
                }
            }

            return null;
        }

        /// Issue #6: Find the active LockdownZone center; fallback to first team spawn
        private Vector3 GetSafeZonePosition()
        {
            // Try to find the Lockdown zone center if implemented
            // var lockdownZone = FindFirstObjectByType<LockdownZone>();
            // if (lockdownZone != null) return lockdownZone.Center;

            // Fallback: Use neutral team spawn point
            return GetDefaultSpawnPosition(null);
        }

        /// <summary>
        /// Get default spawn position — delegates to SpawnSystem if available.
        /// Ưu tiên spawn point của đúng team player, fallback sang neutral (-1).
        /// </summary>
        private Vector3 GetDefaultSpawnPosition(NetworkPlayer player = null)
        {
            if (_spawnSystem == null)
                _spawnSystem = SpawnSystem.Instance;

            if (_spawnSystem != null)
            {
                // Thử team của player trước (vd. Team 0 → team-0 spawn zone)
                int teamId = player != null ? player.TeamId : -1;
                SpawnPoint sp = _spawnSystem.GetRandomSpawnPointForTeam(teamId);

                // Fallback sang neutral nếu team không có spawn point riêng
                if (sp == null && teamId != -1)
                    sp = _spawnSystem.GetRandomSpawnPointForTeam(-1);

                if (sp != null)
                    return sp.GetSpawnPosition();
            }

            Debug.LogWarning("[RespawnSystem] No SpawnSystem or spawn points found — using Vector3.zero!");
            return Vector3.zero;
        }

        /// <summary>
        /// Check if player can respawn based on phase rules
        /// </summary>
        private bool CanRespawn(NetworkPlayer player)
        {
            if (phaseManager == null) return true;

            var config = phaseManager.GetCurrentPhaseConfig();
            if (config == null) return false;

            // Rules by Phase Config
            if (!config.RespawnEnabled) return false;

            // If respawn is enabled, check placement logic (Beacons vs Auto-Zone)
            if (phaseManager.CurrentPhase == MatchPhaseState.Lockdown)
                return true; // Auto-spawn in Safe Zone in Phase 3

            // Phase 1-2: Need beacon
            return FindRespawnBeacon(player) != null;
        }

        /// <summary>
        /// Get respawn delay based on phase (reads from RespawnConfig; falls back to hardcoded defaults)
        /// </summary>
        private float GetRespawnDelay()
        {
            if (phaseManager == null) return 5f;
            
            var config = phaseManager.GetCurrentPhaseConfig();
            return config?.RespawnDelay ?? 5f;
        }

        /// <summary>
        /// Check if player is dead based on stat system (Health <= 0)
        /// </summary>
        private bool IsPlayerDead(NetworkPlayer player)
        {
            if (player == null)
                return false;

            var statSystem = ComponentResolver.Find<IPlayerStatSystem>(player)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] IPlayerStatSystem not found")
                .Resolve();
            if (statSystem == null)
                return false;

            float health = statSystem.GetStat(PlayerStatType.Health);
            return health <= 0f;
        }

        /// <summary>
        /// Handle player respawned
        /// </summary>
        private void OnPlayerRespawned(NetworkPlayer player)
        {
            Debug.Log($"[RespawnSystem] Player respawned: {player.DisplayName}");

            // Mark alive via NetworkPlayer so RegistryService.GetAliveCount is accurate
            player.SetAlive(true);

            // Ask MatchEndManager to re-evaluate the player's team (Phase 3 guard)
            _matchEndManager?.RecheckEliminationForTeam(player.TeamId);
        }

        private void OnRespawnDelayChanged(float oldDelay, float newDelay, bool asServer)
        {
            // Clients: Forward to HUD so countdown timer can be shown
            if (!asServer)
                GameplayEventBus.Instance?.Publish(new NightHunt.Gameplay.Core.Events.RespawnTimerEvent
                {
                    DelaySeconds = newDelay
                });
        }

        /// <summary>Notify the owning client their respawn was cancelled (beacon destroyed).</summary>
        [TargetRpc]
        private void RpcNotifyRespawnFailed(FishNet.Connection.NetworkConnection conn, string reason)
        {
            Debug.Log($"[RespawnSystem] Respawn cancelled: {reason}");
            GameplayEventBus.Instance?.Publish(new NightHunt.Gameplay.Core.Events.RespawnCancelledEvent
            {
                Reason = reason
            });
        }
    }
}