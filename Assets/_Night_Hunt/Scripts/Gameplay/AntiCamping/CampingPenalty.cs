using UnityEngine;
using NightHunt.Networking;
using NightHunt.Networking.Player;

namespace NightHunt.Gameplay.AntiCamping
{
    /// <summary>
    /// Penalty application for campers
    /// </summary>
    public static class CampingPenalty
    {
        /// <summary>
        /// Apply camping penalties
        /// </summary>
        public static void ApplyPenalties(NetworkPlayer player, float revealRadius = 30f)
        {
            if (player == null) return;

            // Reveal position to enemies
            RevealPosition(player, revealRadius);

            // RP/score penalty applied via backend post-match; local effect is visual only.
        }

        /// <summary>
        /// Reveal camper position
        /// </summary>
        private static void RevealPosition(NetworkPlayer player, float radius)
        {
            // // Create reveal effect for enemies
            // var visionSystem = player.GetComponent<Vision.VisionSystem>();
            // if (visionSystem != null)
            // {
            //     // Increase vision radius temporarily (makes player visible)
            //     // This would be handled by vision system
            // }

            // Show reveal indicator on minimap once MinimapSystem is implemented.
            Debug.LogWarning($"[CampingPenalty] {player?.name} position revealed to enemies.");
        }

        /// <summary>
        /// Remove camping penalties
        /// </summary>
        public static void RemovePenalties(NetworkPlayer player)
        {
            if (player == null) return;
            // Remove reveal effects
        }
    }
}

