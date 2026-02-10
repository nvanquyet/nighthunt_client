using System;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;

namespace NightHunt.Networking
{
    /// <summary>
    /// TeamService - Data provider cho UI
    /// KHÔNG tham gia logic assignment
    /// Chỉ nhận notifications từ TeamAssignmentSystem
    /// </summary>
    public class TeamService : NetworkBehaviour
    {
        public static TeamService Instance { get; private set; }
        
        [Header("Team Display Data")]
        [SerializeField] private Color[] _teamColors = new Color[]
        {
            new Color(0.2f, 0.4f, 1f),    // Team 0: Blue
            new Color(1f, 0.2f, 0.2f),    // Team 1: Red
        };
        
        [SerializeField] private string[] _teamNames = new string[]
        {
            "Blue Team",
            "Red Team"
        };
        
        // Data tracking (cho UI queries)
        private Dictionary<int, List<int>> _playersByTeam = new(); // TeamId → List<FishNet ClientId>
        
        // Events
        public event Action<int, int> OnTeamCountChanged; // TeamId, new count
        
        // ===== LIFECYCLE =====
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
        }
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Initialize team lists
            for (int i = 0; i < _teamColors.Length; i++)
            {
                _playersByTeam[i] = new List<int>();
            }
            
            Debug.Log("[TeamService] ✅ Initialized");
        }
        
        // ===== NOTIFICATIONS FROM TeamAssignmentSystem =====
        
        [Server]
        public void OnPlayerAssignedToTeam(int fishnetClientId, int teamId)
        {
            if (!_playersByTeam.ContainsKey(teamId))
                _playersByTeam[teamId] = new List<int>();
            
            _playersByTeam[teamId].Add(fishnetClientId);
            
            Debug.Log($"[TeamService] Player {fishnetClientId} assigned to Team {teamId} (Count: {_playersByTeam[teamId].Count})");
            
            // Sync to clients
            RpcUpdateTeamCount(teamId, _playersByTeam[teamId].Count);
        }
        
        [Server]
        public void OnPlayerSwitchedTeam(int fishnetClientId, int oldTeam, int newTeam)
        {
            _playersByTeam[oldTeam]?.Remove(fishnetClientId);
            
            if (!_playersByTeam.ContainsKey(newTeam))
                _playersByTeam[newTeam] = new List<int>();
            
            _playersByTeam[newTeam].Add(fishnetClientId);
            
            Debug.Log($"[TeamService] Player {fishnetClientId} switched: {oldTeam} → {newTeam}");
            
            // Sync counts
            RpcUpdateTeamCount(oldTeam, _playersByTeam[oldTeam].Count);
            RpcUpdateTeamCount(newTeam, _playersByTeam[newTeam].Count);
        }
        
        [Server]
        public void OnPlayerRemovedFromTeam(int fishnetClientId, int teamId)
        {
            _playersByTeam[teamId]?.Remove(fishnetClientId);
            
            Debug.Log($"[TeamService] Player {fishnetClientId} removed from Team {teamId}");
            
            RpcUpdateTeamCount(teamId, _playersByTeam[teamId].Count);
        }
        
        // ===== UI QUERIES =====
        
        public Color GetTeamColor(int teamId)
        {
            if (teamId >= 0 && teamId < _teamColors.Length)
                return _teamColors[teamId];
            return Color.white;
        }
        
        public string GetTeamName(int teamId)
        {
            if (teamId >= 0 && teamId < _teamNames.Length)
                return _teamNames[teamId];
            return $"Team {teamId}";
        }
        
        public int GetTeamPlayerCount(int teamId)
        {
            return _playersByTeam.TryGetValue(teamId, out var list) ? list.Count : 0;
        }
        
        // ===== NETWORK SYNC =====
        
        [ObserversRpc]
        private void RpcUpdateTeamCount(int teamId, int count)
        {
            OnTeamCountChanged?.Invoke(teamId, count);
        }
    }
}