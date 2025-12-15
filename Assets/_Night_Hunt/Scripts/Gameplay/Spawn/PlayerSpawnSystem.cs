using UnityEngine;
using FishNet.Object;
using NightHunt.Networking;
using System.Collections.Generic;

namespace NightHunt.Gameplay.Spawn
{
    /// <summary>
    /// Manages player spawning
    /// Handles spawn points, team assignment, spawn protection
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
        }

        /// <summary>
        /// Server: Spawn player at appropriate spawn point
        /// Tránh spawn overlap bằng cách check distance và tìm vị trí an toàn
        /// </summary>
        [Server]
        public Vector3 SpawnPlayer(NetworkPlayer player, int teamId)
        {
            SpawnPoint spawnPoint = GetSpawnPointForTeam(teamId);
            
            if (spawnPoint == null)
            {
                // Fallback to random spawn
                if (spawnPoints.Count > 0)
                {
                    spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Count)];
                }
                else
                {
                    // Default spawn at origin
                    Debug.LogWarning("[PlayerSpawnSystem] No spawn points available, spawning at origin");
                    return Vector3.zero;
                }
            }

            // Tìm vị trí spawn an toàn (không overlap với players khác)
            Vector3 spawnPosition = FindSafeSpawnPosition(spawnPoint);
            
            // Set player position
            player.transform.position = spawnPosition;
            player.transform.rotation = spawnPoint.transform.rotation;

            // Set team
            player.SetTeamId(teamId);

            // Apply spawn protection
            ApplySpawnProtection((uint)player.ObjectId, spawnProtectionDuration);

            // Visual effect
            RpcPlaySpawnEffect(spawnPosition);

            Debug.Log($"[PlayerSpawnSystem] Spawned player {player.PlayerName} at position {spawnPosition}");
            return spawnPosition;
        }

        /// <summary>
        /// Tìm vị trí spawn an toàn (không overlap với players khác)
        /// </summary>
        [Server]
        private Vector3 FindSafeSpawnPosition(SpawnPoint spawnPoint)
        {
            Vector3 basePosition = spawnPoint.GetSpawnPosition();
            
            // Check nếu vị trí base đã an toàn
            if (IsSpawnPositionSafe(basePosition))
            {
                return basePosition;
            }

            // Nếu không an toàn, thử tìm vị trí khác trong spawn radius
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

            // Nếu vẫn không tìm được, thử offset theo các hướng
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

            // Fallback: Dùng base position (có thể overlap nhưng ít nhất không crash)
            Debug.LogWarning($"[PlayerSpawnSystem] Could not find safe spawn position, using base position (may overlap)");
            return basePosition;
        }

        /// <summary>
        /// Check nếu vị trí spawn an toàn (không có players khác gần đó)
        /// </summary>
        [Server]
        private bool IsSpawnPositionSafe(Vector3 position)
        {
            // Check overlap với CharacterController của players khác
            Collider[] overlappingColliders = Physics.OverlapSphere(position, minSpawnDistance, playerLayerMask);
            
            foreach (var collider in overlappingColliders)
            {
                // Check nếu là CharacterController của player khác
                var characterController = collider.GetComponent<CharacterController>();
                if (characterController != null)
                {
                    // Có player khác gần đó → không an toàn
                    return false;
                }
            }

            // Không có players gần đó → an toàn
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
                // Apply visual effect
                // Would need to integrate with character visual system
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
            }
        }

        /// <summary>
        /// Client: Play spawn effect
        /// </summary>
        [ObserversRpc]
        private void RpcPlaySpawnEffect(Vector3 position)
        {
            // Play spawn particle effect, sound, etc.
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
        /// Add spawn point
        /// </summary>
        public void AddSpawnPoint(SpawnPoint point)
        {
            if (!spawnPoints.Contains(point))
            {
                spawnPoints.Add(point);
                OrganizeSpawnPoints();
            }
        }
    }

    /// <summary>
    /// Spawn point component
    /// </summary>
    public class SpawnPoint : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] private int teamId = 0; // -1 for any team
        [SerializeField] private float spawnRadius = 2f;
        [SerializeField] private bool isActive = true;

        public int TeamId => teamId;
        public bool IsActive => isActive;

        /// <summary>
        /// Get spawn position (with random offset)
        /// </summary>
        public Vector3 GetSpawnPosition()
        {
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            return transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = teamId == 0 ? Color.blue : (teamId == 1 ? Color.red : Color.green);
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f);
        }
    }
}

