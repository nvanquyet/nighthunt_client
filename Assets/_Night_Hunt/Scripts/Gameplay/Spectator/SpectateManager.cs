using System.Linq;
using NightHunt.Core;
using UnityEngine;
using NightHunt.Networking;
using NightHunt.Networking.Player;

namespace NightHunt.Gameplay.Spectator
{
    /// <summary>
    /// Manages spectate mode and current player tracking for UI systems.
    /// UI components use this to determine which player's data to display.
    /// Follows Singleton pattern for global access.
    /// </summary>
    public class SpectateManager : SingletonPersistent<SpectateManager>
    {

        [Header("Debug")] [SerializeField] private bool enableDebugLogs = false;

        private NetworkPlayer localPlayer;
        private NetworkPlayer currentSpectatedPlayer;
        private bool isSpectating = false;

        public event System.Action<NetworkPlayer> OnCurrentPlayerChanged;

        /// <summary>
        /// Fired once when the local player is registered for the first time.
        /// FogTeamVisibilityBinder (and any other component that needs the local
        /// player's team) subscribes here and refreshes its state on this callback.
        /// </summary>
        public event System.Action<NetworkPlayer> OnLocalPlayerSet;



        public NetworkPlayer GetCurrentPlayer()
        {
            if (isSpectating && currentSpectatedPlayer != null)
                return currentSpectatedPlayer;
            return localPlayer;
        }

        public bool IsSpectating() => isSpectating && currentSpectatedPlayer != null;

        public bool IsCurrentPlayerLocal()
        {
            if (!isSpectating)
                return true;

            return currentSpectatedPlayer != null && currentSpectatedPlayer.IsLocalPlayer;
        }

        public NetworkPlayer GetLocalPlayer() => localPlayer;

        public void SetLocalPlayer(NetworkPlayer player)
        {
            if (player == null || !player.IsLocalPlayer)
            {
                LogWarning("Attempted to set non-local player");
                return;
            }

            localPlayer = player;

            // Notify fog and any other late-init systems that need the local team.
            OnLocalPlayerSet?.Invoke(localPlayer);

            if (!isSpectating)
            {
                OnCurrentPlayerChanged?.Invoke(localPlayer);
            }

            Log($"Local player set: {player.DisplayName}");
        }

        public void StartSpectating(NetworkPlayer player)
        {
            if (player == null)
            {
                StopSpectating();
                return;
            }

            if (player.IsLocalPlayer)
            {
                StopSpectating();
                return;
            }

            currentSpectatedPlayer = player;
            isSpectating = true;
            OnCurrentPlayerChanged?.Invoke(player);

            Log($"Started spectating {player.DisplayName}");
        }

        public void StopSpectating()
        {
            if (!isSpectating) return;

            isSpectating = false;
            var previousSpectated = currentSpectatedPlayer;
            currentSpectatedPlayer = null;

            OnCurrentPlayerChanged?.Invoke(localPlayer);

            Log($"Stopped spectating {previousSpectated?.DisplayName}");
        }

        /// <summary>
        /// ✅ OPTIMIZED: Use PlayerRegistry instead of FindObjectsOfType
        /// </summary>
        public void SwitchSpectatedPlayer(bool next = true)
        {
            if (localPlayer == null) return;

            // ✅ Get from Registry (O(1) access to list)
            if (PlayerPublicRegistry.Instance == null)
            {
                LogWarning("PlayerPublicRegistry not available");
                return;
            }

            // Get all players except local player
            var allPlayers = PlayerPublicRegistry.Instance.GetAllPlayers();
            // Filter out local player
            allPlayers = System.Array.FindAll(allPlayers, p => p != localPlayer);
            if (allPlayers.Length == 0)
            {
                StopSpectating();
                return;
            }

            int currentIndex = -1;
            if (isSpectating && currentSpectatedPlayer != null)
            {
                for (int i = 0; i < allPlayers.Length; i++)
                {
                    if (allPlayers[i] == currentSpectatedPlayer)
                    {
                        currentIndex = i;
                        break;
                    }
                }
            }

            // Next/previous
            int direction = next ? 1 : -1;
            int newIndex = (currentIndex + direction + allPlayers.Length) % allPlayers.Length;

            StartSpectating(allPlayers[newIndex]);
        }
        
        private void Log(string message)
        {
            if (enableDebugLogs)
                UnityEngine.Debug.Log($"[SpectateManager] {message}");
        }
        
        private void LogWarning(string message)
        {
            if (enableDebugLogs) UnityEngine.Debug.LogWarning($"[SpectateManager] {message}");
        }
    }
}