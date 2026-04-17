using UnityEngine;
using System.Collections.Generic;
using NightHunt.Networking;
using NightHunt.Networking.Player;

namespace NightHunt.Gameplay.AntiCamping
{
    /// <summary>
    /// Camping detection - tracks position for >90s stationary
    /// </summary>
    public class CampingDetector
    {
        private readonly Dictionary<NetworkPlayer, CampingData> playerPositions = new Dictionary<NetworkPlayer, CampingData>();
        private readonly float campingThreshold = 90f; // seconds
        private readonly float positionThreshold = 2f; // meters

        /// <summary>
        /// Camping data for a player
        /// </summary>
        private class CampingData
        {
            public Vector3 LastPosition;
            public float StationaryTime;
            public bool IsCamping;
        }

        /// <summary>
        /// Update camping detection
        /// </summary>
        public void Update(NetworkPlayer player, Vector3 currentPosition)
        {
            if (!playerPositions.ContainsKey(player))
            {
                playerPositions[player] = new CampingData
                {
                    LastPosition = currentPosition,
                    StationaryTime = 0f,
                    IsCamping = false
                };
                return;
            }

            var data = playerPositions[player];
            float distance = Vector3.Distance(data.LastPosition, currentPosition);

            if (distance < positionThreshold)
            {
                // Player is stationary
                data.StationaryTime += Time.deltaTime;
                
                if (data.StationaryTime >= campingThreshold && !data.IsCamping)
                {
                    data.IsCamping = true;
                    OnPlayerCampingDetected(player);
                }
            }
            else
            {
                // Player moved
                data.StationaryTime = 0f;
                data.IsCamping = false;
            }

            data.LastPosition = currentPosition;
        }

        /// <summary>
        /// Check if player is camping
        /// </summary>
        public bool IsPlayerCamping(NetworkPlayer player)
        {
            return playerPositions.ContainsKey(player) && playerPositions[player].IsCamping;
        }

        /// <summary>
        /// Handle player camping detected
        /// </summary>
        private void OnPlayerCampingDetected(NetworkPlayer player)
        {
            Debug.Log($"[CampingDetector] Player {player.DisplayName} is camping!");
            // Notify anti-camping system
        }

        /// <summary>
        /// Reset camping data for player
        /// </summary>
        public void ResetPlayer(NetworkPlayer player)
        {
            playerPositions.Remove(player);
        }
    }
}

