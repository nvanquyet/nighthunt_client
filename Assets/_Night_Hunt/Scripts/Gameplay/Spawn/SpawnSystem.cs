using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using System.Collections.Generic;
using NightHunt.Gameplay.Team;
using NightHunt.Networking;
using NightHunt.Networking.Player;
using NightHunt.Utilities;

namespace NightHunt.Gameplay.Spawn
{
    /// <summary>
    /// SpawnSystem - Server-authoritative spawn management
    /// </summary>
    public class SpawnSystem : NetworkBehaviour
    {
        public static SpawnSystem Instance { get; private set; }

        [Header("Dependencies")] [SerializeField]
        private TeamAssignmentSystem _teamAssignmentSystem;

        [Header("Spawn Points")] [SerializeField]
        private List<SpawnPoint> _spawnPoints = new();

        private Dictionary<int, List<SpawnPoint>> _teamSpawnPoints = new();
        // Round-robin index per team — đảm bảo 2 player spawn liên tiếp không trùng vị trí.
        private Dictionary<int, int> _spawnPointIndices = new();

        // ===== LIFECYCLE =====

        private void Awake()
        {
            Instance = this;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            OrganizeSpawnPoints();

            Debug.Log($"[SpawnSystem] ✅ Initialized with {_spawnPoints.Count} spawn points");
        }

        private void OrganizeSpawnPoints()
        {
            _teamSpawnPoints.Clear();

            foreach (SpawnPoint sp in _spawnPoints)
            {
                if (sp == null) continue;

                int teamId = sp.TeamId;

                if (!_teamSpawnPoints.ContainsKey(teamId))
                {
                    _teamSpawnPoints[teamId] = new List<SpawnPoint>();
                }

                _teamSpawnPoints[teamId].Add(sp);
            }

            foreach (var kvp in _teamSpawnPoints)
            {
                Debug.Log($"[SpawnSystem] Team {kvp.Key}: {kvp.Value.Count} spawn points");
            }
        }

        // ===== SPAWN PROCESSING =====

        /// <summary>
        /// Server: Process spawn với data từ client
        /// Returns: Updated PlayerRegistryData với team assignment
        /// </summary>
        [Server]
        public PlayerRegistryData ProcessSpawn(GameObject playerObj, NetworkConnection conn,
            PlayerRegistryData clientData)
        {
            int fishnetClientId = conn.ClientId;
            NetworkPlayer player = ComponentResolver.Find<NetworkPlayer>(playerObj)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] NetworkPlayer not found")
                .Resolve();

            Debug.Log(
                $"[SpawnSystem] Processing spawn - Backend ID: {clientData.BackendPlayerId}, Name: {clientData.DisplayName}");

            // STEP 1: Assign team (server authority)
            int teamId = _teamAssignmentSystem.ResolveTeam(fishnetClientId, clientData.TeamId);

            // STEP 2: Update data với server values
            PlayerRegistryData serverData = clientData;
            serverData.TeamId = teamId;
            serverData.Status = PlayerConnectionStatus.InGame;

            Debug.Log($"[SpawnSystem] Team assigned: {teamId}");

            // STEP 3: Position at spawn point
            SpawnPoint spawnPoint = GetSpawnPoint(teamId);

            if (spawnPoint != null)
            {
                playerObj.transform.position = spawnPoint.GetSpawnPosition();
                playerObj.transform.rotation = spawnPoint.GetSpawnRotation();

                Debug.Log($"[SpawnSystem] Positioned at {playerObj.transform.position}");
            }
            else
            {
                Debug.LogWarning($"[SpawnSystem] No spawn point for Team {teamId}, using origin");
                playerObj.transform.position = Vector3.zero;
            }

            Debug.Log($"[SpawnSystem] ✅ Process complete");

            return serverData;
        }

        /// <summary>
        /// Server: Cleanup on disconnect
        /// </summary>
        [Server]
        public void OnPlayerDisconnected(int fishnetClientId)
        {
            Debug.Log($"[SpawnSystem] Cleanup for FishNet ClientId: {fishnetClientId}");
            _teamAssignmentSystem.RemovePlayer(fishnetClientId);
        }

        // ===== SPAWN POINT SELECTION =====

        [Server]
        private SpawnPoint GetSpawnPoint(int teamId)
        {
            // Team-specific first, then neutral (-1) as fallback.
            if (_teamSpawnPoints.TryGetValue(teamId, out List<SpawnPoint> teamPoints) && teamPoints.Count > 0)
            {
                // Round-robin: mỗi lần gọi lấy spawn point kế tiếp trong list.
                // Đảm bảo 2 player cùng team không spawn trùng vị trí.
                if (!_spawnPointIndices.ContainsKey(teamId))
                    _spawnPointIndices[teamId] = 0;

                int idx = _spawnPointIndices[teamId] % teamPoints.Count;
                _spawnPointIndices[teamId] = idx + 1; // advance for next call
                return teamPoints[idx];
            }

            // Fallback: neutral spawn points (also round-robin)
            const int neutralKey = -1;
            if (_teamSpawnPoints.TryGetValue(neutralKey, out List<SpawnPoint> neutralPoints) && neutralPoints.Count > 0)
            {
                if (!_spawnPointIndices.ContainsKey(neutralKey))
                    _spawnPointIndices[neutralKey] = 0;

                int idx = _spawnPointIndices[neutralKey] % neutralPoints.Count;
                _spawnPointIndices[neutralKey] = idx + 1;
                return neutralPoints[idx];
            }

            return null;
        }

        /// <summary>
        /// Public accessor used by RespawnSystem and other systems.
        /// Pass teamId = -1 to get a neutral spawn point.
        /// Returns null if no matching spawn point exists.
        /// </summary>
        public SpawnPoint GetRandomSpawnPointForTeam(int teamId)
        {
            return GetSpawnPoint(teamId);
        }
    }
}