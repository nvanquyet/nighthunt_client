using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using System.Collections.Generic;
using NightHunt.Gameplay.Team;
using NightHunt.Common;
using NightHunt.Networking;
using NightHunt.Networking.Player;
using NightHunt.State;
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

        [Header("Ground Snap")]
        [Tooltip("Raycast downward from this height above the spawn point to find the exact terrain surface. " +
                 "Prevents spawning inside terrain on DS where timing between physics and spawn differs from host.")]
        [SerializeField] private float _groundSnapRayHeight = 50f;

        [Tooltip("Offset above the detected terrain surface so the player's feet are ON the ground, not inside it.")]
        [SerializeField] private float _groundSnapSurfaceOffset = 0.05f;

        // ===== LIFECYCLE =====

        private void Awake()
        {
            Instance = this;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (_teamAssignmentSystem == null)
            {
                _teamAssignmentSystem = FindFirstObjectByType<TeamAssignmentSystem>(FindObjectsInactive.Include);
                if (_teamAssignmentSystem == null)
                    Debug.LogError("[SpawnSystem] ❌ TeamAssignmentSystem not found in scene! Add it to the scene.");
                else
                    Debug.LogWarning("[SpawnSystem] ⚠️ TeamAssignmentSystem was not assigned — resolved via FindFirstObjectByType.");
            }

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

            // STEP 1: Assign team (server authority). Room roster is authoritative for
            // custom parties; the client-sent team is only a fallback hint.
            int requestedTeamId = ResolveAuthoritativeGameplayTeamId(clientData, out string teamSource);
            int teamId = _teamAssignmentSystem.ResolveTeam(fishnetClientId, requestedTeamId);

            // STEP 2: Update data với server values
            PlayerRegistryData serverData = clientData;
            serverData.TeamId = teamId;
            serverData.Status = PlayerConnectionStatus.InGame;

            Debug.Log($"[SpawnSystem] Team assigned: {teamId} source={teamSource} clientRequested={clientData.TeamId}");

            // STEP 3: Position at spawn point
            SpawnPoint spawnPoint = GetSpawnPoint(teamId);

            if (spawnPoint != null)
            {
                Vector3 rawPos = spawnPoint.GetSpawnPosition();
                // Snap Y to actual terrain surface: raycast downward from above the spawn point.
                // Critical for DS builds: on a dedicated server the spawn packet is sent immediately
                // after ServerManager.Spawn(), and any position error (spawn point Y slightly inside
                // terrain) causes the client's prediction to think the player is airborne, leading to
                // rapid free-fall before the first reconcile can correct it.
                Vector3 snappedPos = SnapToGround(rawPos);
                playerObj.transform.position = snappedPos;
                playerObj.transform.rotation = spawnPoint.GetSpawnRotation();

                Debug.Log($"[SpawnSystem] Positioned at {playerObj.transform.position} (raw={rawPos} snapped={snappedPos})");
            }
            else
            {
                Debug.LogWarning($"[SpawnSystem] No spawn point for Team {teamId}, using origin");
                playerObj.transform.position = Vector3.zero;
            }

            Debug.Log($"[SpawnSystem] ✅ Process complete");

            return serverData;
        }

        private static int ResolveAuthoritativeGameplayTeamId(PlayerRegistryData clientData, out string source)
        {
            source = "client";

            if (!string.IsNullOrEmpty(clientData.BackendPlayerId)
                && long.TryParse(clientData.BackendPlayerId, out long backendUserId)
                && RoomState.Instance?.CurrentRoom?.players != null)
            {
                var roomPlayer = RoomState.Instance.CurrentRoom.players.Find(p => p.userId == backendUserId);
                if (roomPlayer != null)
                {
                    source = $"room-roster:{roomPlayer.team}";
                    return NormalizeRoomTeamToGameplayTeam(roomPlayer.team);
                }
            }

            return clientData.TeamId;
        }

        private static int NormalizeRoomTeamToGameplayTeam(int roomTeam)
        {
            if (roomTeam == Constants.TEAM_1)
                return 0;
            if (roomTeam == Constants.TEAM_2)
                return 1;

            return roomTeam >= 0 ? roomTeam : -1;
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

        [Server]
        public void OnPlayerReconnected(int previousFishNetId, int newFishNetId)
        {
            Debug.Log($"[SpawnSystem] Reconnect remap FishNet ClientId: {previousFishNetId} -> {newFishNetId}");
            _teamAssignmentSystem.RemapPlayerClientId(previousFishNetId, newFishNetId);
        }

        // ===== GROUND SNAP =====

        /// <summary>
        /// Raycasts straight down from (position + snapHeight * up) using ALL layers to find the
        /// first non-trigger surface beneath the spawn point.  Returns a position on that surface
        /// plus <see cref="_groundSnapSurfaceOffset"/> so the character's feet are ON the ground.
        ///
        /// Intentionally uses LayerMask ~0 (Everything) so the result is independent of the
        /// groundLayer inspector field on the movement component — that field only controls the
        /// grounded-detection CheckSphere, not actual physics collision.
        ///
        /// Falls back to the original position if no surface is found (open-air or sky spawn).
        /// </summary>
        private Vector3 SnapToGround(Vector3 position)
        {
            Vector3 rayOrigin = position + Vector3.up * _groundSnapRayHeight;
            float   maxDist   = _groundSnapRayHeight + 20f; // cast well past the spawn point

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, maxDist,
                    ~0, QueryTriggerInteraction.Ignore))
            {
                return new Vector3(position.x, hit.point.y + _groundSnapSurfaceOffset, position.z);
            }

            Debug.LogWarning($"[SpawnSystem] SnapToGround: no surface found below {position} — using original Y.");
            return position;
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
