using UnityEngine;
using NightHunt.Networking;
using NightHunt.Gameplay.Vision;

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

            // Apply other penalties
            // TODO: Apply RP penalty, etc.
        }

        /// <summary>
        /// Reveal camper position
        /// </summary>
        private static void RevealPosition(NetworkPlayer player, float radius)
        {
            // Create reveal effect for enemies
            // Use ComponentFinder to search in hierarchy (including children)
            var visionSystem = NightHunt.InteractionSystem.Utilities.ComponentFinder.FindComponentInHierarchy<Vision.VisionSystem>(player.gameObject, includeInactive: false);
            if (visionSystem != null)
            {
                // Increase vision radius temporarily (makes player visible)
                // This would be handled by vision system
            }

            // TODO: Show reveal indicator on minimap for enemies
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

