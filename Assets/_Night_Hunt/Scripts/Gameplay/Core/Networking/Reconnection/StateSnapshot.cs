using UnityEngine;
using NightHunt.Gameplay.Character;
using NightHunt.Networking;

namespace NightHunt.Gameplay.Core.Networking.Reconnection
{
    /// <summary>
    /// Creates and restores game state snapshots
    /// </summary>
    public static class StateSnapshot
    {
        /// <summary>
        /// Create snapshot of current game state
        /// </summary>
        public static ReconnectionState CreateSnapshot()
        {
            var state = new ReconnectionState();

            // Get local player
            var localPlayer = FindLocalPlayer();
            if (localPlayer != null)
            {
                state.PlayerPosition = localPlayer.transform.position;
                state.PlayerRotation = localPlayer.transform.rotation;

                // Get character stats
                var stats = localPlayer.GetComponent<CharacterStats>();
                if (stats != null)
                {
                    state.CurrentHP = stats.GetHP();
                    state.CurrentStamina = stats.GetStamina();
                }

                // Get team ID
                state.TeamId = localPlayer.TeamId;
            }

            // Get match phase (if available)
            var phaseManager = Object.FindFirstObjectByType<Match.MatchPhaseManager>();
            if (phaseManager != null)
            {
                // Store phase as int (would need phase enum conversion)
                state.CurrentPhase = 0; // TODO: Convert phase to int
            }

            // TODO: Get score, inventory, etc.

            return state;
        }

        /// <summary>
        /// Restore snapshot to game state
        /// </summary>
        public static void RestoreSnapshot(ReconnectionState state)
        {
            if (state == null)
            {
                Debug.LogWarning("[StateSnapshot] Cannot restore null state");
                return;
            }

            var localPlayer = FindLocalPlayer();
            if (localPlayer != null)
            {
                // Restore position and rotation
                localPlayer.transform.position = state.PlayerPosition;
                localPlayer.transform.rotation = state.PlayerRotation;

                // Restore character stats
                var stats = localPlayer.GetComponent<CharacterStats>();
                if (stats != null)
                {
                    stats.SetHP(state.CurrentHP);
                    stats.SetStamina(state.CurrentStamina);
                }
            }

            // TODO: Restore inventory, score, phase, etc.
        }

        /// <summary>
        /// Find local player
        /// </summary>
        private static NetworkPlayer FindLocalPlayer()
        {
            var allPlayers = Object.FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
            foreach (var player in allPlayers)
            {
                if (player.IsLocalPlayer)
                {
                    return player;
                }
            }
            return null;
        }
    }
}

