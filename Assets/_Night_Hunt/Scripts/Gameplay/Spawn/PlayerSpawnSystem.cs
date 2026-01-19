using UnityEngine;
using FishNet.Object;
using NightHunt.Networking;
using System.Collections.Generic;

namespace NightHunt.Gameplay.Spawn
{
    /// <summary>
    /// Manages player spawning mechanics
    /// Handles spawn points, safe positioning, spawn protection
    /// </summary>
    public class PlayerSpawnSystem : NetworkBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] private List<SpawnPoint> spawnPoints = new List<SpawnPoint>();
        [SerializeField] private float spawnProtectionDuration = 3f;
        [SerializeField] private bool useTeamBasedSpawning = true;
        [SerializeField] private float minSpawnDistance = 3f; // Minimum distance between spawned players
        [SerializeField] private LayerMask playerLayerMask = -1; // Layer mask for checking player overlap

        [Header("Spawn Protection Visual")]
        [SerializeField] private Material spawnProtectionMaterial;
        [SerializeField] private GameObject spawnProtectionEffect;

        private Dictionary<uint, float> spawnProtectionTimes = new Dictionary<uint, float>();
        private Dictionary<int, List<SpawnPoint>> teamSpawnPoints = new Dictionary<int, List<SpawnPoint>>();

        public override void OnStartServer()
        {
            base.OnStartServer();
            OrganizeSpawnPoints();
        }

        /// <summary>
        /// Organize spawn points by team
        /// </summary>
        private void OrganizeSpawnPoints()
        {
            teamSpawnPoints.Clear();

            foreach (var spawnPoint in spawnPoints)
            {
                if (spawnPoint == null) continue;

                int teamId = spawnPoint.TeamId;
                if (!teamSpawnPoints.ContainsKey(teamId))
                {
                    teamSpawnPoints[teamId] = new List<SpawnPoint>();
                }
                teamSpawnPoints[teamId].Add(spawnPoint);
            }

            Debug.Log($"[PlayerSpawnSystem] Organized {spawnPoints.Count} spawn points for {teamSpawnPoints.Count} teams");
        }

        /// <summary>
        /// Server: Spawn player at appropriate spawn point
        /// Avoids spawn overlap by checking distance and finding safe position
        /// </summary>
        [Server]
        public Vector3 SpawnPlayer(NetworkPlayer player, int teamId)
        {
            // Validate player
            if (player == null)
            {
                Debug.LogError("[PlayerSpawnSystem] Player is null!");
                return Vector3.zero;
            }

            if (!player.IsSpawned)
            {
                Debug.LogError($"[PlayerSpawnSystem] Player {player.PlayerName} is not spawned yet! Cannot set position/team.");
                return Vector3.zero;
            }

            // Get spawn point for team
            SpawnPoint spawnPoint = GetSpawnPointForTeam(teamId);
            
            if (spawnPoint == null)
            {
                if (spawnPoints.Count > 0)
                {
                    spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Count)];
                }
                else
                {
                    Debug.LogWarning("[PlayerSpawnSystem] No spawn points available, spawning at origin");
                    return Vector3.zero;
                }
            }

            // Find safe spawn position (no overlap with other players)
            Vector3 spawnPosition = FindSafeSpawnPosition(spawnPoint);
            
            // Set player position and rotation
            player.transform.position = spawnPosition;
            player.transform.rotation = spawnPoint.transform.rotation;

            // Set team (if not already set)
            if (player.TeamId != teamId)
            {
                player.SetTeamId(teamId);
            }

            // Apply spawn protection
            ApplySpawnProtection((uint)player.ObjectId, spawnProtectionDuration);

            // Visual effect
            RpcPlaySpawnEffect(spawnPosition);

            Debug.Log($"[PlayerSpawnSystem] Spawned player {player.PlayerName} at position {spawnPosition} on team {teamId}");
            return spawnPosition;
        }

        /// <summary>
        /// Find safe spawn position (no overlap with other players)
        /// </summary>
        [Server]
        private Vector3 FindSafeSpawnPosition(SpawnPoint spawnPoint)
        {
            Vector3 basePosition = spawnPoint.GetSpawnPosition();
            
            // Check if base position is safe
            if (IsSpawnPositionSafe(basePosition))
            {
                return basePosition;
            }

            // If not safe, try to find alternative position within spawn radius
            int maxAttempts = 10;
            for (int i = 0; i < maxAttempts; i++)
            {
                Vector3 testPosition = spawnPoint.GetSpawnPosition();
                if (IsSpawnPositionSafe(testPosition))
                {
                    Debug.Log($"[PlayerSpawnSystem] Found safe spawn position after {i + 1} attempts");
                    return testPosition;
                }
            }

            // Try offset positions in cardinal directions
            Vector3[] offsets = new Vector3[]
            {
                new Vector3(minSpawnDistance, 0, 0),
                new Vector3(-minSpawnDistance, 0, 0),
                new Vector3(0, 0, minSpawnDistance),
                new Vector3(0, 0, -minSpawnDistance),
                new Vector3(minSpawnDistance, 0, minSpawnDistance),
                new Vector3(-minSpawnDistance, 0, -minSpawnDistance),
            };

            foreach (var offset in offsets)
            {
                Vector3 testPosition = basePosition + offset;
                if (IsSpawnPositionSafe(testPosition))
                {
                    Debug.Log($"[PlayerSpawnSystem] Found safe spawn position with offset");
                    return testPosition;
                }
            }

            // Fallback: Use base position (may overlap but won't crash)
            Debug.LogWarning($"[PlayerSpawnSystem] Could not find safe spawn position, using base position (may overlap)");
            return basePosition;
        }

        /// <summary>
        /// Check if spawn position is safe (no other players nearby)
        /// </summary>
        [Server]
        private bool IsSpawnPositionSafe(Vector3 position)
        {
            // Check overlap with CharacterController of other players
            Collider[] overlappingColliders = Physics.OverlapSphere(position, minSpawnDistance, playerLayerMask);
            
            foreach (var collider in overlappingColliders)
            {
                // Check if it's a CharacterController of another player
                var characterController = collider.GetComponent<CharacterController>();
                if (characterController != null)
                {
                    // Another player is nearby → not safe
                    return false;
                }
            }

            // No players nearby → safe
            return true;
        }

        /// <summary>
        /// Get spawn point for team
        /// </summary>
        private SpawnPoint GetSpawnPointForTeam(int teamId)
        {
            if (!useTeamBasedSpawning)
            {
                // Random spawn
                if (spawnPoints.Count > 0)
                {
                    return spawnPoints[Random.Range(0, spawnPoints.Count)];
                }
                return null;
            }

            // Team-based spawn
            if (teamSpawnPoints.ContainsKey(teamId) && teamSpawnPoints[teamId].Count > 0)
            {
                var teamPoints = teamSpawnPoints[teamId];
                return teamPoints[Random.Range(0, teamPoints.Count)];
            }

            // Fallback to any spawn point
            if (spawnPoints.Count > 0)
            {
                return spawnPoints[Random.Range(0, spawnPoints.Count)];
            }

            return null;
        }

        /// <summary>
        /// Server: Apply spawn protection to player
        /// </summary>
        [Server]
        private void ApplySpawnProtection(uint playerId, float duration)
        {
            spawnProtectionTimes[playerId] = Time.time + duration;
            RpcApplySpawnProtection(playerId, duration);
            
            Debug.Log($"[PlayerSpawnSystem] Applied spawn protection for player {playerId} ({duration}s)");
        }

        /// <summary>
        /// Client: Apply spawn protection visual
        /// </summary>
        [ObserversRpc]
        private void RpcApplySpawnProtection(uint playerId, float duration)
        {
            NetworkPlayer player = GetPlayerById(playerId);
            if (player != null)
            {
                // Apply visual effect (shield, glow, etc.)
                // This would integrate with your character visual system
                Debug.Log($"[Client] Spawn protection applied to player {player.PlayerName}");
            }
        }

        /// <summary>
        /// Check if player has spawn protection
        /// </summary>
        public bool HasSpawnProtection(uint playerId)
        {
            if (!spawnProtectionTimes.ContainsKey(playerId))
                return false;

            return Time.time < spawnProtectionTimes[playerId];
        }

        /// <summary>
        /// Remove spawn protection
        /// </summary>
        [Server]
        public void RemoveSpawnProtection(uint playerId)
        {
            if (spawnProtectionTimes.ContainsKey(playerId))
            {
                spawnProtectionTimes.Remove(playerId);
                Debug.Log($"[PlayerSpawnSystem] Removed spawn protection for player {playerId}");
            }
        }

        /// <summary>
        /// Client: Play spawn effect
        /// </summary>
        [ObserversRpc]
        private void RpcPlaySpawnEffect(Vector3 position)
        {
            // Play spawn particle effect, sound, etc.
            if (spawnProtectionEffect != null)
            {
                Instantiate(spawnProtectionEffect, position, Quaternion.identity);
            }
        }

        /// <summary>
        /// Get player by network ID
        /// </summary>
        private NetworkPlayer GetPlayerById(uint playerId)
        {
#if UNITY_2023_1_OR_NEWER
            NetworkPlayer[] players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
#else
            NetworkPlayer[] players = FindObjectsOfType<NetworkPlayer>();
#endif
            foreach (var player in players)
            {
                if (player.ObjectId == playerId)
                {
                    return player;
                }
            }
            return null;
        }

        /// <summary>
        /// Add spawn point dynamically
        /// </summary>
        public void AddSpawnPoint(SpawnPoint point)
        {
            if (!spawnPoints.Contains(point))
            {
                spawnPoints.Add(point);
                OrganizeSpawnPoints();
                Debug.Log($"[PlayerSpawnSystem] Added spawn point: {point.name}");
            }
        }

        /// <summary>
        /// Remove spawn point
        /// </summary>
        public void RemoveSpawnPoint(SpawnPoint point)
        {
            if (spawnPoints.Contains(point))
            {
                spawnPoints.Remove(point);
                OrganizeSpawnPoints();
                Debug.Log($"[PlayerSpawnSystem] Removed spawn point: {point.name}");
            }
        }
    }
}