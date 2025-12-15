using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Networking;
using System.Collections.Generic;
using FishNet;

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
        [SerializeField] private Color[] teamColors = new Color[] { Color.blue, Color.red, Color.green, Color.yellow };

        // Synchronized team data
        // Note: Dictionary cannot be directly synced, will use manual sync or SyncList
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
        }

        /// <summary>
        /// Server: Assign player to team
        /// </summary>
        [Server]
        public int AssignPlayerToTeam(NetworkPlayer player)
        {
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
            playerTeams[playerId] = teamId;
            teams[teamId].PlayerCount++;
            teams[teamId].PlayerIds.Add(playerId);

            // Set team on player
            player.SetTeamId(teamId);

            return teamId;
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

