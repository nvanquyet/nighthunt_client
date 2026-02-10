using System.Linq;
using UnityEngine;
using NightHunt.Networking;
using NightHunt.Inventory.Core.Utilities;
using NightHunt.Inventory.Systems;
using NightHunt.Networking.Player;

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

        [Header("Debug")] [SerializeField] private bool enableDebugLogs = false;

        private NetworkPlayer localPlayer;
        private NetworkPlayer currentSpectatedPlayer;
        private bool isSpectating = false;

        public event System.Action<NetworkPlayer> OnCurrentPlayerChanged;

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
                InventoryLogger.LogWarning("SpectateManager", "Attempted to set non-local player", enableDebugLogs);
                return;
            }

            localPlayer = player;

            if (!isSpectating)
            {
                OnCurrentPlayerChanged?.Invoke(localPlayer);
            }

            InventoryLogger.Log("SpectateManager", $"Local player set: {player.DisplayName}", enableDebugLogs);
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

            InventoryLogger.Log("SpectateManager", $"Started spectating {player.DisplayName}", enableDebugLogs);
        }

        public void StopSpectating()
        {
            if (!isSpectating) return;

            isSpectating = false;
            var previousSpectated = currentSpectatedPlayer;
            currentSpectatedPlayer = null;

            OnCurrentPlayerChanged?.Invoke(localPlayer);

            InventoryLogger.Log("SpectateManager", $"Stopped spectating {previousSpectated?.DisplayName}",
                enableDebugLogs);
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
                Debug.LogWarning("[SpectateManager] PlayerPublicRegistry not available");
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

        public PlayerInventoryController GetCurrentPlayerInventory()
        {
            var currentPlayer = GetCurrentPlayer();
            if (currentPlayer == null)
                return null;

            return currentPlayer.GetComponent<PlayerInventoryController>();
        }

        public InventorySystem GetCurrentInventory()
        {
            var inventoryController = GetCurrentPlayerInventory();
            return inventoryController?.Inventory;
        }

        public EquipmentSystem GetCurrentEquipment()
        {
            var inventoryController = GetCurrentPlayerInventory();
            return inventoryController?.Equipment;
        }

        public WeaponSystem GetCurrentWeapons()
        {
            var inventoryController = GetCurrentPlayerInventory();
            return inventoryController?.Weapons;
        }

        public QuickSlotSystem GetCurrentQuickSlots()
        {
            var inventoryController = GetCurrentPlayerInventory();
            return inventoryController?.QuickSlots;
        }

        public AttachmentSystem GetCurrentAttachments()
        {
            var inventoryController = GetCurrentPlayerInventory();
            return inventoryController?.Attachments;
        }
    }
}