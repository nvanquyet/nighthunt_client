using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Gameplay.Match;
using NightHunt.Gameplay.Character;
using NightHunt.Gameplay.Spawn;
using NightHunt.Networking;
using System.Collections.Generic;
using NightHunt.Gameplay.Player;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Utilities;

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

            // Update respawn timers
            var playersToRespawn = new List<NetworkPlayer>();
            foreach (var kvp in respawnTimers)
            {
                var player = kvp.Key;
                float timer = kvp.Value;

                if (player != null && IsPlayerDead(player))
                {
                    timer -= Time.deltaTime;
                    respawnTimers[player] = timer;

                    if (timer <= 0f)
                    {
                        playersToRespawn.Add(player);
                    }
                }
                else
                {
                    // Player already respawned or disconnected
                    playersToRespawn.Add(player);
                }
            }

            // Remove completed timers and respawn players
            foreach (var player in playersToRespawn)
            {
                respawnTimers.Remove(player);
                if (player != null && IsPlayerDead(player))
                {
                    RespawnPlayer(player);
                }
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

            // Find respawn location
            Vector3 respawnPosition = GetRespawnPosition(player);
            Quaternion respawnRotation = player.transform.rotation;

            // Use the movement controller's Teleport to properly reset CharacterController
            // and the prediction pipeline.  Direct transform.position assignment would
            // be overwritten by CharacterController on the next FixedUpdate.
            var movement = ComponentResolver.Find<IMovementController>(player)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] IMovementController not found")
                .Resolve();
            if (movement != null)
            {
                movement.Teleport(respawnPosition, respawnRotation);
            }
            else
            {
                // Fallback for objects that don't use IMovementController
                player.transform.position = respawnPosition;
            }

            // Restore player stats via PlayerStatSystem (health)
            var statSystem = ComponentResolver.Find<IPlayerStatSystem>(player)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] IPlayerStatSystem not found")
                .Resolve();
            if (statSystem is NightHunt.Gameplay.StatSystem.Systems.PlayerStatSystem concrete)
            {
                float maxHealth = concrete.GetStat(PlayerStatType.MaxHealth);
                concrete.SetCurrentStat(PlayerStatType.Health, maxHealth);
            }

            // Notify player respawned
            OnPlayerRespawned(player);
        }

        /// <summary>
        /// Get respawn position based on phase
        /// </summary>
        private Vector3 GetRespawnPosition(NetworkPlayer player)
        {
            // Phase 1-2: Use beacon if available
            if (phaseManager != null)
            {
                var currentPhase = phaseManager.CurrentPhase;
                if (currentPhase == MatchPhaseState.Preparation || currentPhase == MatchPhaseState.Hunt)
                {
                    var beacon = FindRespawnBeacon(player);
                    if (beacon != null)
                    {
                        return beacon.transform.position;
                    }
                }
                else if (currentPhase == MatchPhaseState.Lockdown)
                {
                    // Phase 3: Respawn in safe zone
                    return GetSafeZonePosition();
                }
            }

            // Default: Use spawn point
            return GetDefaultSpawnPosition();
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

        /// <summary>
        /// Get safe zone position (Phase 3)
        /// </summary>
        private Vector3 GetSafeZonePosition()
        {
            // ZoneSystem is not yet active — fall back to map center.
            // When LockdownZone is implemented it should provide a center point here.
            Debug.LogWarning("[RespawnSystem] Zone system not yet active — using map center for Phase-3 respawn.");
            return Vector3.zero;
        }

        /// <summary>
        /// Get default spawn position — delegates to SpawnSystem if available
        /// </summary>
        private Vector3 GetDefaultSpawnPosition()
        {
            // Prefer SpawnSystem so the player goes to a real team spawn point
            if (_spawnSystem == null)
                _spawnSystem = SpawnSystem.Instance;

            if (_spawnSystem != null)
            {
                SpawnPoint sp = _spawnSystem.GetRandomSpawnPointForTeam(-1); // neutral fallback
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

            var currentPhase = phaseManager.CurrentPhase;

            switch (currentPhase)
            {
                case MatchPhaseState.Preparation:
                case MatchPhaseState.Hunt:
                    // Phase 1-2: Need beacon
                    return FindRespawnBeacon(player) != null;

                case MatchPhaseState.Lockdown:
                    // Phase 3: Auto-respawn in zone
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Get respawn delay based on phase (reads from RespawnConfig; falls back to hardcoded defaults)
        /// </summary>
        private float GetRespawnDelay()
        {
            if (phaseManager != null && phaseManager.CurrentPhase == MatchPhaseState.Lockdown)
            {
                return _respawnConfig != null ? _respawnConfig.Phase3RespawnDelay : 10f;
            }

            if (phaseManager != null && phaseManager.CurrentPhase == MatchPhaseState.Hunt)
                return _respawnConfig != null ? _respawnConfig.Phase2RespawnDelay : 5f;

            return _respawnConfig != null ? _respawnConfig.Phase1RespawnDelay : 5f;
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
            // Update UI or other systems
        }
    }
}