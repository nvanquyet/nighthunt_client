using System.Collections.Generic;
using System.Linq;
using NightHunt.Networking;

namespace NightHunt.Gameplay.Camera.Spectator
{
    /// <summary>
    /// Filter players by team for spectating
    /// </summary>
    public static class TeamSpectatorFilter
    {
        /// <summary>
        /// Get players from same team as local player
        /// </summary>
        public static List<NetworkPlayer> GetTeammates(NetworkPlayer localPlayer)
        {
            var teammates = new List<NetworkPlayer>();

            if (localPlayer == null) return teammates;

            int localTeamId = localPlayer.TeamId;
            var allPlayers = UnityEngine.Object.FindObjectsByType<NetworkPlayer>(UnityEngine.FindObjectsSortMode.None);

            foreach (var player in allPlayers)
            {
                if (player != null && player != localPlayer && player.TeamId == localTeamId)
                {
                    // TODO: Check if player is alive
                    teammates.Add(player);
                }
            }

            return teammates;
        }

        /// <summary>
        /// Get all players (for free spectating)
        /// </summary>
        public static List<NetworkPlayer> GetAllPlayers(NetworkPlayer localPlayer)
        {
            var players = new List<NetworkPlayer>();

            var allPlayers = UnityEngine.Object.FindObjectsByType<NetworkPlayer>(UnityEngine.FindObjectsSortMode.None);

            foreach (var player in allPlayers)
            {
                if (player != null && player != localPlayer)
                {
                    // TODO: Check if player is alive
                    players.Add(player);
                }
            }

            return players;
        }

        /// <summary>
        /// Filter players by team ID
        /// </summary>
        public static List<NetworkPlayer> FilterByTeam(int teamId, NetworkPlayer excludePlayer = null)
        {
            var players = new List<NetworkPlayer>();

            var allPlayers = UnityEngine.Object.FindObjectsByType<NetworkPlayer>(UnityEngine.FindObjectsSortMode.None);

            foreach (var player in allPlayers)
            {
                if (player != null && player != excludePlayer && player.TeamId == teamId)
                {
                    // TODO: Check if player is alive
                    players.Add(player);
                }
            }

            return players;
        }
    }
}

