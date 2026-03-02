using UnityEngine;
using FishNet.Object;
using System.Collections.Generic;
using NightHunt.Networking;

namespace NightHunt.Gameplay.Team
{
    /// <summary>
    /// TeamAssignmentSystem - Server-authoritative team assignment
    /// </summary>
    public class TeamAssignmentSystem : NetworkBehaviour
    {
        [Header("Team Settings")]
        [SerializeField] private int _maxTeams = 2;
        
        // FishNet ClientId → TeamId
        private Dictionary<int, int> _playerTeamMap = new();
        
        // ===== LIFECYCLE =====
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            Debug.Log("[TeamAssignmentSystem] ✅ Initialized");
        }
        
        // ===== TEAM ASSIGNMENT =====
        
        /// <summary>
        /// Server: Assign team cho player
        /// </summary>
        [Server]
        public int AssignTeam(int fishnetClientId, string backendPlayerId)
        {
            Debug.Log($"[TeamAssignmentSystem] Assigning team - FishNet ID: {fishnetClientId}, Backend ID: {backendPlayerId}");
            
            // Team assignment uses load-balancing by default.
            // If persistent team preferences are added in future, query backend here.
            int teamId = GetSmallestTeam();
            _playerTeamMap[fishnetClientId] = teamId;
            
            // Notify TeamService (passive data provider)
            TeamService.Instance?.OnPlayerAssignedToTeam(fishnetClientId, teamId);
            
            Debug.Log($"[TeamAssignmentSystem] ✅ Assigned Team {teamId}");
            
            return teamId;
        }
        
        [Server]
        public int ResolveTeam(
            int fishnetClientId,
            int requestedTeamId
        )
        {
            Debug.Log($"[TeamAssignmentSystem] Resolve team - FishNet ID: {fishnetClientId}, Requested: {requestedTeamId}");

            // STEP 1: Validate requested team
            if (IsValidTeam(requestedTeamId))
            {
                _playerTeamMap[fishnetClientId] = requestedTeamId;
                TeamService.Instance?.OnPlayerAssignedToTeam(fishnetClientId, requestedTeamId);

                Debug.Log($"[TeamAssignmentSystem] ✅ Accepted requested team {requestedTeamId}");
                return requestedTeamId;
            }

            // STEP 2: Fallback (balance)
            int fallbackTeam = GetSmallestTeam();
            _playerTeamMap[fishnetClientId] = fallbackTeam;
            TeamService.Instance?.OnPlayerAssignedToTeam(fishnetClientId, fallbackTeam);

            Debug.Log($"[TeamAssignmentSystem] ⚠ Invalid request, fallback to team {fallbackTeam}");
            return fallbackTeam;
        }

        private bool IsValidTeam(int teamId)
        {
            return teamId >= 0 && teamId < _maxTeams;
        }
        
        /// <summary>
        /// Server: Switch player team
        /// </summary>
        [Server]
        public void SwitchTeam(int fishnetClientId, int newTeam)
        {
            int oldTeam = _playerTeamMap.TryGetValue(fishnetClientId, out int t) ? t : 0;
            
            Debug.Log($"[TeamAssignmentSystem] Switching - FishNet ID: {fishnetClientId}, {oldTeam} → {newTeam}");
            
            _playerTeamMap[fishnetClientId] = newTeam;
            
            // Notify TeamService
            TeamService.Instance?.OnPlayerSwitchedTeam(fishnetClientId, oldTeam, newTeam);
            
            Debug.Log($"[TeamAssignmentSystem] ✅ Team switched");
        }
        
        /// <summary>
        /// Server: Remove player
        /// </summary>
        [Server]
        public void RemovePlayer(int fishnetClientId)
        {
            if (_playerTeamMap.TryGetValue(fishnetClientId, out int team))
            {
                Debug.Log($"[TeamAssignmentSystem] Removing FishNet ID: {fishnetClientId}");
                
                _playerTeamMap.Remove(fishnetClientId);
                TeamService.Instance?.OnPlayerRemovedFromTeam(fishnetClientId, team);
            }
        }
        
        // ===== HELPERS =====
        
        private int GetSmallestTeam()
        {
            TeamService teamService = TeamService.Instance;
            if (teamService == null) return 0;
            
            int smallestTeam = 0;
            int smallestCount = int.MaxValue;
            
            for (int i = 0; i < _maxTeams; i++)
            {
                int count = teamService.GetTeamPlayerCount(i);
                if (count < smallestCount)
                {
                    smallestCount = count;
                    smallestTeam = i;
                }
            }
            
            return smallestTeam;
        }
        
        public int GetPlayerTeam(int fishnetClientId)
        {
            return _playerTeamMap.TryGetValue(fishnetClientId, out int team) ? team : 0;
        }
    }
}