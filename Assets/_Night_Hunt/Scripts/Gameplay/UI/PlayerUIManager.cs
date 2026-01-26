using UnityEngine;
using NightHunt.Networking;
using NightHunt.Gameplay.Inventory;

namespace NightHunt.Gameplay.UI
{
    /// <summary>
    /// Manages UI lifecycle for each player
    /// Only enables UI for the local player (owner)
    /// </summary>
    public class PlayerUIManager : MonoBehaviour
    {
        private static PlayerUIManager _instance;
        public static PlayerUIManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("PlayerUIManager");
                    _instance = go.AddComponent<PlayerUIManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("UI References")]
        [SerializeField] private PlayerHUD playerHUD;
        [SerializeField] private InventoryPanel inventoryPanel;

        private NetworkPlayer localPlayer;
        private InventorySystem inventorySystem;
        private bool isInitialized = false;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            FindLocalPlayer();
        }

        private void Update()
        {
            // Continuously check for local player if not found
            if (localPlayer == null || !localPlayer.IsLocalPlayer)
            {
                FindLocalPlayer();
            }
        }

        /// <summary>
        /// Find local player and initialize UI
        /// </summary>
        private void FindLocalPlayer()
        {
            if (isInitialized && localPlayer != null && localPlayer.IsLocalPlayer)
                return;

            NetworkPlayer[] players = FindObjectsOfType<NetworkPlayer>();
            foreach (var player in players)
            {
                if (player.IsLocalPlayer)
                {
                    localPlayer = player;
                    inventorySystem = player.GetComponent<InventorySystem>();
                    
                    InitializeUI();
                    break;
                }
            }
        }

        /// <summary>
        /// Initialize UI components for local player
        /// </summary>
        private void InitializeUI()
        {
            if (localPlayer == null || !localPlayer.IsLocalPlayer)
            {
                Debug.LogWarning("[PlayerUIManager] Cannot initialize UI: Not local player!");
                return;
            }

            // Create or find PlayerHUD
            if (playerHUD == null)
            {
                playerHUD = FindFirstObjectByType<PlayerHUD>();
                if (playerHUD == null)
                {
                    Debug.LogWarning("[PlayerUIManager] PlayerHUD not found. Please create it in the scene.");
                }
            }

            // Create or find InventoryPanel
            if (inventoryPanel == null)
            {
                inventoryPanel = FindFirstObjectByType<InventoryPanel>();
                if (inventoryPanel == null)
                {
                    Debug.LogWarning("[PlayerUIManager] InventoryPanel not found. Please create it in the scene.");
                }
            }

            // Initialize components
            if (inventoryPanel != null)
            {
                inventoryPanel.Initialize(localPlayer, inventorySystem);
            }

            if (playerHUD != null)
            {
                playerHUD.Initialize(localPlayer, inventorySystem, inventoryPanel);
            }

            isInitialized = true;
            Debug.Log("[PlayerUIManager] UI initialized for local player");
        }

        /// <summary>
        /// Get local player reference
        /// </summary>
        public NetworkPlayer GetLocalPlayer() => localPlayer;

        /// <summary>
        /// Get inventory system reference
        /// </summary>
        public InventorySystem GetInventorySystem() => inventorySystem;

        /// <summary>
        /// Get PlayerHUD reference
        /// </summary>
        public PlayerHUD GetPlayerHUD() => playerHUD;

        /// <summary>
        /// Get InventoryPanel reference
        /// </summary>
        public InventoryPanel GetInventoryPanel() => inventoryPanel;

        /// <summary>
        /// Check if UI is initialized for local player
        /// </summary>
        public bool IsInitialized() => isInitialized && localPlayer != null && localPlayer.IsLocalPlayer;
    }
}
