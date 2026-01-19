using UnityEngine;
using System.Collections.Generic;
using NightHunt.Networking;
using NightHunt.Gameplay.Vision;

namespace NightHunt.Gameplay.PredatorPrey
{
    /// <summary>
    /// Direction reveal cho leading team (Predators)
    /// </summary>
    public class RevealSystem : MonoBehaviour
    {
        [Header("Reveal Settings")]
        [SerializeField] private float revealUpdateInterval = 2f;
        [SerializeField] private float revealRadius = 50f;

        private PredatorPreySystem predatorPreySystem;
        private float lastUpdateTime;

        private void Awake()
        {
            predatorPreySystem = FindFirstObjectByType<PredatorPreySystem>();
        }

        private void Update()
        {
            if (predatorPreySystem == null) return;

            if (Time.time - lastUpdateTime >= revealUpdateInterval)
            {
                UpdateReveals();
                lastUpdateTime = Time.time;
            }
        }

        /// <summary>
        /// Update reveals for predator team
        /// </summary>
        private void UpdateReveals()
        {
            int leadingTeam = predatorPreySystem.GetLeadingTeamId();
            if (leadingTeam < 0) return;

            // Reveal direction of leading team players to all other teams
            var allPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
            var predatorPlayers = new List<NetworkPlayer>();
            var preyPlayers = new List<NetworkPlayer>();

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

            // Reveal predator positions to prey players
            foreach (var preyPlayer in preyPlayers)
            {
                foreach (var predatorPlayer in predatorPlayers)
                {
                    float distance = Vector3.Distance(preyPlayer.transform.position, predatorPlayer.transform.position);
                    if (distance <= revealRadius)
                    {
                        RevealDirection(preyPlayer, predatorPlayer);
                    }
                }
            }
        }

        /// <summary>
        /// Reveal direction to player
        /// </summary>
        private void RevealDirection(NetworkPlayer revealTo, NetworkPlayer revealTarget)
        {
            // Calculate direction from revealTo to revealTarget
            Vector3 direction = (revealTarget.transform.position - revealTo.transform.position).normalized;
            
            // TODO: Show direction indicator on UI
            // This would be handled by UI system
        }
    }
}

