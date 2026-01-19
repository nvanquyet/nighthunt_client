using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Networking;
using System.Collections.Generic;

namespace NightHunt.Gameplay.Team
{
    /// <summary>
    /// Manages teams: assignment, colors, team-based logic
    /// </summary>
    public class TeamSystem : NetworkBehaviour
    {
        [Header("Team Settings")]
        [SerializeField] private int maxTeams = 2;
        [SerializeField] private int playersPerTeam = 5;

        [Header("Team Colors")]
        [SerializeField] private Color[] teamColors = new Color[] 
        { 
            Color.blue, 
            Color.red, 
            Color.green, 
            Color.yellow 
        };

        // Team data storage
        private Dictionary<int, TeamData> teams = new Dictionary<int, TeamData>();
        private Dictionary<uint, int> playerTeams = new Dictionary<uint, int>();

        public override void OnStartServer()
        {
            base.OnStartServer();
            InitializeTeams();
        }

        /// <summary>
        /// Server: Initialize teams
        /// </summary>
        [Server]
        private void InitializeTeams()
        {
            teams.Clear();
            for (int i = 0; i < maxTeams; i++)
            {
                teams[i] = new TeamData
                {
                    TeamId = i,
                    TeamColor = teamColors[i % teamColors.Length],
                    PlayerCount = 0
                };
            }

            Debug.Log($"[TeamSystem] Initialized {maxTeams} teams");
        }

        /// <summary>
        /// Server: Assign player to team
        /// Auto-balances teams by assigning to team with least players
        /// </summary>
        [Server]
        public int AssignPlayerToTeam(NetworkPlayer player)
        {
            if (player == null || !player.IsSpawned)
            {
                Debug.LogError("[TeamSystem] Cannot assign invalid player to team!");
                return 0;
            }

            // Find team with least players
            int teamId = 0;
            int minPlayers = int.MaxValue;

            foreach (var kvp in teams)
            {
                if (kvp.Value.PlayerCount < minPlayers && kvp.Value.PlayerCount < playersPerTeam)
                {
                    minPlayers = kvp.Value.PlayerCount;
                    teamId = kvp.Key;
                }
            }

            // Assign player
            uint playerId = (uint)player.ObjectId;
            
            // Remove from old team if already assigned
            if (playerTeams.ContainsKey(playerId))
            {
                int oldTeam = playerTeams[playerId];
                if (teams.ContainsKey(oldTeam))
                {
                    teams[oldTeam].PlayerCount--;
                    teams[oldTeam].PlayerIds.Remove(playerId);
                }
            }

            // Add to new team
            playerTeams[playerId] = teamId;
            teams[teamId].PlayerCount++;
            teams[teamId].PlayerIds.Add(playerId);

            // Set team on player (this syncs to all clients)
            player.SetTeamId(teamId);

            Debug.Log($"[TeamSystem] Assigned player {player.PlayerName} to team {teamId} ({teams[teamId].PlayerCount}/{playersPerTeam} players)");

            return teamId;
        }

        /// <summary>
        /// Server: Remove player from team
        /// </summary>
        [Server]
        public void RemovePlayerFromTeam(NetworkPlayer player)
        {
            if (player == null) return;

            uint playerId = (uint)player.ObjectId;
            
            if (playerTeams.ContainsKey(playerId))
            {
                int teamId = playerTeams[playerId];
                
                if (teams.ContainsKey(teamId))
                {
                    teams[teamId].PlayerCount--;
                    teams[teamId].PlayerIds.Remove(playerId);
                    Debug.Log($"[TeamSystem] Removed player {player.PlayerName} from team {teamId}");
                }
                
                playerTeams.Remove(playerId);
            }
        }

        /// <summary>
        /// Get team color
        /// </summary>
        public Color GetTeamColor(int teamId)
        {
            if (teams.ContainsKey(teamId))
            {
                return teams[teamId].TeamColor;
            }
            return Color.white;
        }

        /// <summary>
        /// Get player team
        /// </summary>
        public int GetPlayerTeam(uint playerId)
        {
            if (playerTeams.ContainsKey(playerId))
            {
                return playerTeams[playerId];
            }
            return -1;
        }

        /// <summary>
        /// Check if players are on same team
        /// </summary>
        public bool AreSameTeam(uint playerId1, uint playerId2)
        {
            int team1 = GetPlayerTeam(playerId1);
            int team2 = GetPlayerTeam(playerId2);
            return team1 >= 0 && team1 == team2;
        }

        /// <summary>
        /// Check if players are on same team (NetworkPlayer overload)
        /// </summary>
        public bool AreSameTeam(NetworkPlayer player1, NetworkPlayer player2)
        {
            if (player1 == null || player2 == null) return false;
            return player1.TeamId == player2.TeamId;
        }

        /// <summary>
        /// Get team data
        /// </summary>
        public TeamData GetTeamData(int teamId)
        {
            if (teams.ContainsKey(teamId))
            {
                return teams[teamId];
            }
            return null;
        }

        /// <summary>
        /// Server: Add score to team
        /// </summary>
        [Server]
        public void AddTeamScore(int teamId, int score)
        {
            if (teams.ContainsKey(teamId))
            {
                teams[teamId].Score += score;
                Debug.Log($"[TeamSystem] Team {teamId} scored {score} points. Total: {teams[teamId].Score}");
                
                // Broadcast score update to all clients
                RpcUpdateTeamScore(teamId, teams[teamId].Score);
            }
        }

        /// <summary>
        /// Client: Update team score display
        /// </summary>
        [ObserversRpc]
        private void RpcUpdateTeamScore(int teamId, int newScore)
        {
            // Update UI, scoreboard, etc.
            Debug.Log($"[Client] Team {teamId} score updated: {newScore}");
        }

        /// <summary>
        /// Get all teams
        /// </summary>
        public Dictionary<int, TeamData> GetAllTeams()
        {
            return teams;
        }

        /// <summary>
        /// Get team player count
        /// </summary>
        public int GetTeamPlayerCount(int teamId)
        {
            if (teams.ContainsKey(teamId))
            {
                return teams[teamId].PlayerCount;
            }
            return 0;
        }

        /// <summary>
        /// Get winning team
        /// </summary>
        public int GetWinningTeam()
        {
            int winningTeam = -1;
            int highestScore = int.MinValue;

            foreach (var kvp in teams)
            {
                if (kvp.Value.Score > highestScore)
                {
                    highestScore = kvp.Value.Score;
                    winningTeam = kvp.Key;
                }
            }

            return winningTeam;
        }
    }

    /// <summary>
    /// Team data structure
    /// </summary>
    [System.Serializable]
    public class TeamData
    {
        public int TeamId;
        public Color TeamColor;
        public int PlayerCount;
        public List<uint> PlayerIds = new List<uint>();
        public int Score = 0;
    }
}