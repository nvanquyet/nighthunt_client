using UnityEngine;
using System.Collections.Generic;
using NightHunt.Networking;
using NightHunt.Gameplay.Vision;

namespace NightHunt.Gameplay.PredatorPrey
{
    /// <summary>
    /// Radar ping cho trailing team (Prey)
    /// </summary>
    public class RadarPingSystem : MonoBehaviour
    {
        [Header("Radar Ping Settings")]
        [SerializeField] private float pingInterval = 5f;
        [SerializeField] private float pingRadius = 100f;
        [SerializeField] private float pingDuration = 2f;

        private PredatorPreySystem predatorPreySystem;
        private float lastPingTime;

        private void Awake()
        {
            predatorPreySystem = FindFirstObjectByType<PredatorPreySystem>();
        }

        private void Update()
        {
            if (predatorPreySystem == null) return;

            if (Time.time - lastPingTime >= pingInterval)
            {
                SendRadarPings();
                lastPingTime = Time.time;
            }
        }

        /// <summary>
        /// Send radar pings for prey team
        /// </summary>
        private void SendRadarPings()
        {
            int leadingTeam = predatorPreySystem.GetLeadingTeamId();
            if (leadingTeam < 0) return;

            // Get trailing team (prey)
            var allPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
            var preyPlayers = new List<NetworkPlayer>();
            var predatorPlayers = new List<NetworkPlayer>();

            foreach (var player in allPlayers)
            {
                if (player.TeamId == leadingTeam)
                {
                    predatorPlayers.Add(player);
                }
                else
                {
                    preyPlayers.Add(player);
                }
            }

            // Send radar ping to prey players showing predator positions
            foreach (var preyPlayer in preyPlayers)
            {
                foreach (var predatorPlayer in predatorPlayers)
                {
                    float distance = Vector3.Distance(preyPlayer.transform.position, predatorPlayer.transform.position);
                    if (distance <= pingRadius)
                    {
                        PingPosition(preyPlayer, predatorPlayer.transform.position);
                    }
                }
            }
        }

        /// <summary>
        /// Ping position on radar
        /// </summary>
        private void PingPosition(NetworkPlayer player, Vector3 position)
        {
            // TODO: Show radar ping on UI/minimap
            // This would be handled by UI system
            Debug.Log($"[RadarPingSystem] Ping at {position} for player {player.PlayerName}");
        }
    }
}

