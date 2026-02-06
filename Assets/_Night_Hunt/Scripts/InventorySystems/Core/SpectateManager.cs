using UnityEngine;
using NightHunt.Networking;
using NightHunt.Inventory.Core.Utilities;

namespace NightHunt.Inventory.Core
{
    /// <summary>
    /// Manages spectate mode and current player tracking for UI systems.
    /// UI components use this to determine which player's data to display.
    /// Follows Singleton pattern for global access.
    /// </summary>
    public class SpectateManager : MonoBehaviour
    {
        public static SpectateManager Instance { get; private set; }
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        private NetworkPlayer localPlayer;
        private NetworkPlayer currentSpectatedPlayer;
        private bool isSpectating = false;
        
        /// <summary>
        /// Event fired when the current player (for UI display) changes.
        /// Fired when: local player spawns, spectate starts/stops, spectated player changes.
        /// </summary>
        public event System.Action<NetworkPlayer> OnCurrentPlayerChanged;
        
        #region Lifecycle
        
        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Gets the current player that UI should display data from.
        /// Returns localPlayer if not spectating, otherwise returns spectatedPlayer.
        /// </summary>
        public NetworkPlayer GetCurrentPlayer()
        {
            if (isSpectating && currentSpectatedPlayer != null)
                return currentSpectatedPlayer;
            return localPlayer;
        }
        
        /// <summary>
        /// Checks if currently spectating another player.
        /// </summary>
        public bool IsSpectating() => isSpectating && currentSpectatedPlayer != null;
        
        /// <summary>
        /// Checks if current player is the local player (can interact/drag-drop).
        /// </summary>
        public bool IsCurrentPlayerLocal()
        {
            if (!isSpectating)
                return true; // Not spectating = local player
            
            return currentSpectatedPlayer != null && currentSpectatedPlayer.IsLocalPlayer;
        }
        
        /// <summary>
        /// Gets the local player reference.
        /// </summary>
        public NetworkPlayer GetLocalPlayer() => localPlayer;
        
        /// <summary>
        /// Sets the local player (called when local player spawns).
        /// </summary>
        public void SetLocalPlayer(NetworkPlayer player)
        {
            if (player == null || !player.IsLocalPlayer)
            {
                InventoryLogger.LogWarning("SpectateManager", "Attempted to set non-local player as local", enableDebugLogs);
                return;
            }
            
            localPlayer = player;
            
            // If not spectating, current player is local player
            if (!isSpectating)
            {
                OnCurrentPlayerChanged?.Invoke(localPlayer);
            }
            
            InventoryLogger.Log("SpectateManager", $"Local player set: {player.PlayerName}", enableDebugLogs);
        }
        
        /// <summary>
        /// Starts spectating a player.
        /// </summary>
        public void StartSpectating(NetworkPlayer player)
        {
            if (player == null)
            {
                StopSpectating();
                return;
            }
            
            // If spectating local player, just stop spectating
            if (player.IsLocalPlayer)
            {
                StopSpectating();
                return;
            }
            
            currentSpectatedPlayer = player;
            isSpectating = true;
            OnCurrentPlayerChanged?.Invoke(player);
            
            InventoryLogger.Log("SpectateManager", $"Started spectating {player.PlayerName}", enableDebugLogs);
        }
        
        /// <summary>
        /// Stops spectating and returns to local player view.
        /// </summary>
        public void StopSpectating()
        {
            if (!isSpectating) return;
            
            isSpectating = false;
            var previousSpectated = currentSpectatedPlayer;
            currentSpectatedPlayer = null;
            
            OnCurrentPlayerChanged?.Invoke(localPlayer);
            
            InventoryLogger.Log("SpectateManager", $"Stopped spectating {previousSpectated?.PlayerName}", enableDebugLogs);
        }
        
        /// <summary>
        /// Switches to spectate next/previous player.
        /// </summary>
        public void SwitchSpectatedPlayer(bool next = true)
        {
            if (localPlayer == null) return;
            
            NetworkPlayer[] allPlayers = FindObjectsOfType<NetworkPlayer>();
            if (allPlayers.Length <= 1) return;
            
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
            
            // Find next/previous player (skip local player)
            int direction = next ? 1 : -1;
            int attempts = 0;
            int newIndex = currentIndex;
            
            do
            {
                newIndex = (newIndex + direction + allPlayers.Length) % allPlayers.Length;
                attempts++;
                
                if (allPlayers[newIndex] != localPlayer && allPlayers[newIndex] != currentSpectatedPlayer)
                {
                    StartSpectating(allPlayers[newIndex]);
                    return;
                }
            } while (attempts < allPlayers.Length);
        }
        
        #endregion
    }
}
