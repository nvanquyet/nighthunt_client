using System;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace NightHunt.Gameplay.Team
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
        
        // Server-only: tracks individual client IDs per team for addition / removal logic.
        private Dictionary<int, List<int>> _playersByTeam = new(); // TeamId → List<FishNet ClientId>

        // Network-synced: broadcasts team counts to ALL clients including late-joiners.
        // SyncDictionary carries current state in every spawn packet — no manual TargetRpc needed.
        private readonly SyncDictionary<int, int> _teamCounts = new SyncDictionary<int, int>();
        
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

            // Pre-initialize both player list and synced count for every slot so that
            // late-joining clients receive a 0 rather than a missing key.
            for (int i = 0; i < _teamColors.Length; i++)
            {
                _playersByTeam[i] = new List<int>();
                _teamCounts[i]    = 0;
            }

            Debug.Log("[TeamService] ✅ Initialized");
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            _teamCounts.OnChange += OnTeamCountsChanged;

            // Replay current snapshot so UI is correct even when joining mid-game.
            foreach (var kvp in _teamCounts)
                OnTeamCountChanged?.Invoke(kvp.Key, kvp.Value);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            _teamCounts.OnChange -= OnTeamCountsChanged;
        }
        
        // ===== NOTIFICATIONS FROM TeamAssignmentSystem =====
        
        [Server]
        public void OnPlayerAssignedToTeam(int fishnetClientId, int teamId)
        {
            if (!_playersByTeam.ContainsKey(teamId))
                _playersByTeam[teamId] = new List<int>();

            _playersByTeam[teamId].Add(fishnetClientId);

            Debug.Log($"[TeamService] Player {fishnetClientId} assigned to Team {teamId} (Count: {_playersByTeam[teamId].Count})");

            _teamCounts[teamId] = _playersByTeam[teamId].Count;
        }

        [Server]
        public void OnPlayerSwitchedTeam(int fishnetClientId, int oldTeam, int newTeam)
        {
            _playersByTeam[oldTeam]?.Remove(fishnetClientId);

            if (!_playersByTeam.ContainsKey(newTeam))
                _playersByTeam[newTeam] = new List<int>();

            _playersByTeam[newTeam].Add(fishnetClientId);

            Debug.Log($"[TeamService] Player {fishnetClientId} switched: {oldTeam} → {newTeam}");

            _teamCounts[oldTeam] = _playersByTeam[oldTeam].Count;
            _teamCounts[newTeam] = _playersByTeam[newTeam].Count;
        }

        [Server]
        public void OnPlayerRemovedFromTeam(int fishnetClientId, int teamId)
        {
            _playersByTeam[teamId]?.Remove(fishnetClientId);

            Debug.Log($"[TeamService] Player {fishnetClientId} removed from Team {teamId}");

            _teamCounts[teamId] = _playersByTeam.TryGetValue(teamId, out var list) ? list.Count : 0;
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
            // Reads from SyncDictionary so both server and clients return consistent values.
            return _teamCounts.TryGetValue(teamId, out var count) ? count : 0;
        }

        // ===== NETWORK SYNC =====

        /// <summary>
        /// Fires on all clients (including server) whenever _teamCounts changes.
        /// Forwards the update through the public OnTeamCountChanged event for UI.
        /// </summary>
        private void OnTeamCountsChanged(
            SyncDictionaryOperation op, int teamId, int count, bool asServer)
        {
            if (op == SyncDictionaryOperation.Add || op == SyncDictionaryOperation.Set)
                OnTeamCountChanged?.Invoke(teamId, count);
        }
    }
}