using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Gameplay.Match;
using NightHunt.Gameplay.Character;
using NightHunt.Networking;
using System.Collections.Generic;
using NightHunt.Gameplay.Player;

namespace NightHunt.Gameplay.Respawn
{
    /// <summary>
    /// Respawn system with phase-based rules
    /// </summary>
    public class RespawnSystem : NetworkBehaviour
    {
        [Header("Respawn Settings")]
        [SerializeField] private float respawnDelay = 5f;
        [SerializeField] private float phase3RespawnDelay = 3f;

        // Synchronized state
        private readonly SyncVar<float> networkRespawnDelay = new SyncVar<float>();

        private MatchPhaseManager phaseManager;
        private Dictionary<NetworkPlayer, float> respawnTimers = new Dictionary<NetworkPlayer, float>();

        private void Awake()
        {
            phaseManager = FindFirstObjectByType<MatchPhaseManager>();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            networkRespawnDelay.OnChange += OnRespawnDelayChanged;
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
            
            // Respawn player
            player.transform.position = respawnPosition;
            
            // Restore player stats
            // var stats = player.GetComponent<PlayerStats>();
            // if (stats != null)
            // {
            //     stats.RestoreHealthToFull();
            //     stats.RestoreStaminaToFull();
            // }

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
            // TODO: Get position from zone system
            return Vector3.zero;
        }

        /// <summary>
        /// Get default spawn position
        /// </summary>
        private Vector3 GetDefaultSpawnPosition()
        {
            // TODO: Get from spawn system
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
        /// Get respawn delay based on phase
        /// </summary>
        private float GetRespawnDelay()
        {
            if (phaseManager != null && phaseManager.CurrentPhase == MatchPhaseState.Lockdown)
            {
                return phase3RespawnDelay;
            }
            return respawnDelay;
        }

        /// <summary>
        /// Check if player is dead
        /// </summary>
        private bool IsPlayerDead(NetworkPlayer player)
        {
            // var stats = player.GetComponent<PlayerStats>();
            // if (stats != null)
            // {
            //     return !stats.IsAlive;
            // }
            return false;
        }

        /// <summary>
        /// Handle player respawned
        /// </summary>
        private void OnPlayerRespawned(NetworkPlayer player)
        {
            Debug.Log($"[RespawnSystem] Player respawned: {player.DisplayName}");
            // Notify other systems
        }

        private void OnRespawnDelayChanged(float oldDelay, float newDelay, bool asServer)
        {
            // Update UI or other systems
        }
    }
}

