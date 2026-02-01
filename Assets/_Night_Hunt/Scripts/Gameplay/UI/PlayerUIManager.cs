using UnityEngine;
using NightHunt.Networking;
using NightHunt.Gameplay.Inventory;
using NightHunt.Gameplay.Core;
using NightHunt.InteractionSystem.Utilities;

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
                    Debug.Log("[PlayerUIManager] ===== Instance getter called but _instance is NULL! Creating new instance... =====");
                    GameObject go = new GameObject("PlayerUIManager");
                    _instance = go.AddComponent<PlayerUIManager>();
                    DontDestroyOnLoad(go);
                    Debug.Log($"[PlayerUIManager] Instance created via getter: {go.name}, Instance: {_instance != null}");
                }
                return _instance;
            }
        }

        [Header("UI References")]
        [SerializeField] private PlayerHUD playerHUD;
        [SerializeField] private InventoryPanel inventoryPanel;

        private NetworkPlayer localPlayer;
        private InventoryService inventorySystem;
        private bool isInitialized = false;

        private void Awake()
        {
            Debug.Log($"[PlayerUIManager] ===== Awake() called =====");
            Debug.Log($"[PlayerUIManager] GameObject: {gameObject.name}, Active: {gameObject.activeSelf}, Scene: {gameObject.scene.name}");
            Debug.Log($"[PlayerUIManager] Current _instance: {_instance != null}, IsThisInstance: {_instance == this}");
            
            if (_instance == null)
            {
                _instance = this;
                // Only apply DontDestroyOnLoad if not in a regular scene (to allow scene-based setup)
                // If in a regular scene, keep it in scene (user can setup references in Inspector)
                if (gameObject.scene.name != "DontDestroyOnLoad")
                {
                    Debug.Log($"[PlayerUIManager] Instance set to this (scene-based): {gameObject.name}");
                    Debug.Log($"[PlayerUIManager] NOTE: PlayerUIManager is in scene '{gameObject.scene.name}'. This allows Inspector setup.");
                    Debug.Log($"[PlayerUIManager] Assign PlayerHUD and InventoryPanel references in Inspector if needed.");
                }
                else
                {
                    Debug.Log($"[PlayerUIManager] Instance set to this (DontDestroyOnLoad): {gameObject.name}");
                }
            }
            else if (_instance != this)
            {
                // Check which instance should be kept
                bool existingIsDontDestroy = _instance.gameObject.scene.name == "DontDestroyOnLoad";
                bool thisIsDontDestroy = gameObject.scene.name == "DontDestroyOnLoad";
                
                Debug.LogWarning($"[PlayerUIManager] ===== Duplicate instance detected! =====");
                Debug.LogWarning($"[PlayerUIManager] Existing: {_instance.gameObject.name} (Scene: {_instance.gameObject.scene.name}, IsDontDestroy: {existingIsDontDestroy})");
                Debug.LogWarning($"[PlayerUIManager] New: {gameObject.name} (Scene: {gameObject.scene.name}, IsDontDestroy: {thisIsDontDestroy})");
                
                // Prefer scene instance over DontDestroyOnLoad (for Inspector setup)
                if (!existingIsDontDestroy && thisIsDontDestroy)
                {
                    Debug.LogWarning($"[PlayerUIManager] Preferring scene instance over DontDestroyOnLoad (for Inspector setup)");
                    Destroy(_instance.gameObject);
                    _instance = this;
                }
                // If existing is DontDestroyOnLoad and this is scene, keep existing
                else if (existingIsDontDestroy && !thisIsDontDestroy)
                {
                    Debug.LogWarning($"[PlayerUIManager] Keeping DontDestroyOnLoad instance, removing PlayerUIManager component from scene GameObject: {gameObject.name}");
                    Debug.LogWarning($"[PlayerUIManager] If you want to use scene instance, remove PlayerUIManager from DontDestroyOnLoad first.");
                    Destroy(this); // Destroy this component, not the GameObject
                    return;
                }
                // Both in same scene type, keep first one
                else
                {
                    Debug.LogWarning($"[PlayerUIManager] Both instances in same scene type, keeping first one, removing component from: {gameObject.name}");
                    Destroy(this); // Destroy this component, not the GameObject
                    return;
                }
            }
            
            // Subscribe to event EARLY in Awake() to catch any early spawns
            // This ensures we don't miss the OnLocalPlayerReady event
            Debug.Log("[PlayerUIManager] Subscribing to NetworkPlayer.OnLocalPlayerReady...");
            NetworkPlayer.OnLocalPlayerReady += OnLocalPlayerReady;
            Debug.Log("[PlayerUIManager] Successfully subscribed to NetworkPlayer.OnLocalPlayerReady in Awake()");
        }

        private void Start()
        {
            Debug.Log("[PlayerUIManager] ===== Start() called =====");
            Debug.Log($"[PlayerUIManager] GameObject: {gameObject.name}, Active: {gameObject.activeSelf}, Scene: {gameObject.scene.name}");
            Debug.Log($"[PlayerUIManager] _instance: {_instance != null}, IsThisInstance: {_instance == this}");
            
            // Event subscription already done in Awake() to catch early spawns
            // Cannot check listener count from outside NetworkPlayer class
            Debug.Log("[PlayerUIManager] Event subscription done in Awake() - ready to receive OnLocalPlayerReady events");
            
            // Get UI references from registry (no FindObject, instant access)
            // UI components register themselves on Awake, so they should be available
            // But if they're not registered yet, we'll get them when local player is ready
            RefreshUIReferences();
            
            // Only disable UI if we have references (don't disable null references)
            // UI will be enabled when local player is ready
            if (playerHUD != null || inventoryPanel != null)
            {
                DisableUI();
            }
            else
            {
                Debug.Log("[PlayerUIManager] UI not registered yet. Will enable when local player is ready.");
            }
        }

        /// <summary>
        /// Ensure PlayerUIManager instance exists early (before NetworkPlayer spawns)
        /// This static method is called automatically by Unity before scene loads
        /// NOTE: If PlayerUIManager exists in scene, it will be used instead of creating a new one
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureInstanceExistsEarly()
        {
            Debug.Log("[PlayerUIManager] ===== RuntimeInitializeOnLoadMethod (BeforeSceneLoad) CALLED =====");
            Debug.Log($"[PlayerUIManager] Current _instance: {_instance != null}");
            
            // Don't create instance here - let scene instance handle it
            // This method is just to ensure we're ready, but we'll use scene instance if available
            Debug.Log("[PlayerUIManager] RuntimeInitializeOnLoadMethod - will use scene instance if available, or create singleton if not");
        }

        /// <summary>
        /// Refresh UI references from registry (called when needed)
        /// Priority: Inspector assignment > UIRegistry > Find in scene
        /// </summary>
        private void RefreshUIReferences()
        {
            // Use Inspector-assigned references first (if user set them up)
            bool hudFromInspector = playerHUD != null;
            bool panelFromInspector = inventoryPanel != null;
            
            // If not assigned in Inspector, get from registry
            if (playerHUD == null)
            {
                playerHUD = UIRegistry.GetPlayerHUD();
            }
            
            if (inventoryPanel == null)
            {
                inventoryPanel = UIRegistry.GetInventoryPanel();
            }
            
            Debug.Log($"[PlayerUIManager] UI references - PlayerHUD: {playerHUD != null} (Inspector: {hudFromInspector}), InventoryPanel: {inventoryPanel != null} (Inspector: {panelFromInspector})");
            
            // If UI not found, try to find in scene (fallback for debugging)
            if (playerHUD == null)
            {
                PlayerHUD foundHUD = FindFirstObjectByType<PlayerHUD>();
                if (foundHUD != null)
                {
                    Debug.LogWarning($"[PlayerUIManager] PlayerHUD found in scene but NOT registered! GameObject: {foundHUD.name}, Active: {foundHUD.gameObject.activeSelf}, ActiveInHierarchy: {foundHUD.gameObject.activeInHierarchy}");
                    Debug.LogWarning("[PlayerUIManager] PlayerHUD.Awake() may not have been called. Check if GameObject is active and has PlayerHUD component.");
                }
                else
                {
                    Debug.LogError("[PlayerUIManager] PlayerHUD NOT FOUND in scene! Please create PlayerHUD GameObject in Canvas.");
                }
            }
            
            if (inventoryPanel == null)
            {
                InventoryPanel foundPanel = FindFirstObjectByType<InventoryPanel>();
                if (foundPanel != null)
                {
                    Debug.LogWarning($"[PlayerUIManager] InventoryPanel found in scene but NOT registered! GameObject: {foundPanel.name}, Active: {foundPanel.gameObject.activeSelf}, ActiveInHierarchy: {foundPanel.gameObject.activeInHierarchy}");
                    Debug.LogWarning("[PlayerUIManager] InventoryPanel.Awake() may not have been called. Check if GameObject is active and has InventoryPanel component.");
                }
                else
                {
                    Debug.LogError("[PlayerUIManager] InventoryPanel NOT FOUND in scene! Please create InventoryPanel GameObject in Canvas.");
                }
            }
        }

        private void OnApplicationQuit()
        {
            // Gracefully handle application quit - this is expected behavior
            if (_instance == this)
            {
                Debug.Log("[PlayerUIManager] Application quitting - cleaning up singleton instance");
                Cleanup();
            }
        }

        private void OnDestroy()
        {
            // Check if application is quitting - if so, this is expected and not an error
            bool isQuitting = UnityEngine.Application.isPlaying == false;
            
            if (isQuitting)
            {
                // Application is quitting - cleanup gracefully without warnings
                if (_instance == this)
                {
                    Cleanup();
                }
                return;
            }
            
            Debug.LogWarning($"[PlayerUIManager] ===== OnDestroy() called (during runtime) =====");
            Debug.LogWarning($"[PlayerUIManager] GameObject: {gameObject.name}, Scene: {gameObject.scene.name}");
            Debug.LogWarning($"[PlayerUIManager] IsInstance: {_instance == this}, _instance: {_instance != null}");
            
            if (_instance == this)
            {
                Debug.LogError($"[PlayerUIManager] CRITICAL: The singleton instance is being destroyed during runtime! This should not happen!");
                Debug.LogError($"[PlayerUIManager] GameObject: {gameObject.name}, Scene: {gameObject.scene.name}");
                Debug.LogError($"[PlayerUIManager] Stack trace: {System.Environment.StackTrace}");
                
                Cleanup();
            }
            else
            {
                Debug.Log($"[PlayerUIManager] OnDestroy called on non-instance (duplicate), skipping cleanup");
            }
        }

        /// <summary>
        /// Cleanup method - unsubscribes from events and clears instance
        /// </summary>
        private void Cleanup()
        {
            // Unsubscribe from events
            NetworkPlayer.OnLocalPlayerReady -= OnLocalPlayerReady;
            Debug.Log("[PlayerUIManager] Unsubscribed from NetworkPlayer.OnLocalPlayerReady");
            
            // Clear instance if this is being destroyed
            if (_instance == this)
            {
                _instance = null;
                Debug.Log("[PlayerUIManager] Instance cleared");
            }
        }

        /// <summary>
        /// Called when local player is ready (from NetworkPlayer event)
        /// Handles both early spawn and late join scenarios
        /// </summary>
        private void OnLocalPlayerReady(NetworkPlayer player)
        {
            if (player == null)
            {
                Debug.LogWarning("[PlayerUIManager] OnLocalPlayerReady called with null player!");
                return;
            }

            if (!player.IsOwner)
            {
                Debug.LogWarning($"[PlayerUIManager] OnLocalPlayerReady called with non-owner player: {player.name}");
                return;
            }

            if (!player.IsSpawned)
            {
                Debug.LogWarning($"[PlayerUIManager] OnLocalPlayerReady called with non-spawned player: {player.name}");
                return;
            }

            // Don't re-initialize if already initialized with same player
            if (isInitialized && localPlayer == player)
            {
                Debug.Log($"[PlayerUIManager] Already initialized with this player: {player.name}. Skipping re-initialization.");
                return;
            }

            Debug.Log($"[PlayerUIManager] ===== Local player ready event received ===== " +
                     $"Player: {player.name}, IsOwner: {player.IsOwner}, IsSpawned: {player.IsSpawned}, " +
                      $"UI Ready: {UIRegistry.IsUIReady()}, PlayerHUD: {UIRegistry.GetPlayerHUD() != null}, " +
                      $"InventoryPanel: {UIRegistry.GetInventoryPanel() != null}");
               
            localPlayer = player;
            
            // Use ComponentRegistry first (event-based, no FindObject)
            inventorySystem = ComponentRegistry.GetInventoryService(player);
            
            // Fallback to ComponentFinder if not in registry (searches in hierarchy including children)
            if (inventorySystem == null)
            {
                Debug.LogError($"[PlayerUIManager] InventoryService not found in ComponentRegistry, trying ComponentFinder...");
                inventorySystem = NightHunt.InteractionSystem.Utilities.ComponentFinder.FindComponentInHierarchy<InventoryService>(player.gameObject, includeInactive: false);
            }
            
            if (inventorySystem == null)
            {
                Debug.LogError($"[PlayerUIManager] InventoryService not found on player: {player.name}");
                Debug.LogError($"[PlayerUIManager] Searched in: ComponentRegistry, ComponentFinder (hierarchy including children)");
                Debug.LogError($"[PlayerUIManager] Please ensure InventoryService component exists on player or child object and is registered in ComponentRegistry!");
                
                // List all components on player for debugging
                Component[] allComponents = player.GetComponents<Component>();
                Debug.Log($"[PlayerUIManager] Components on {player.name}:");
                foreach (var comp in allComponents)
                {
                    Debug.Log($"  - {comp.GetType().Name}");
                }
            }
            else
            {
                Debug.Log($"[PlayerUIManager] InventoryService found: {inventorySystem.GetType().Name}");
            }
            
            InitializeUI();
        }

        /// <summary>
        /// Initialize UI components for local player
        /// </summary>
        private void InitializeUI()
        {
            if (localPlayer == null || !localPlayer.IsLocalPlayer)
            {
                Debug.LogWarning("[PlayerUIManager] Cannot initialize UI: Not local player!");
                DisableUI(); // Ensure UI is disabled for non-owner
                return;
            }

            Debug.Log($"[PlayerUIManager] InitializeUI() called for local player: {localPlayer.name}");

            // Refresh UI references from registry (no FindObject, instant access)
            // UI should be registered by now (they register on Awake)
            RefreshUIReferences();

            // Validate UI references
            if (playerHUD == null)
            {
                Debug.LogError("[PlayerUIManager] PlayerHUD not registered! Make sure PlayerHUD exists in scene and has Awake() called.");
                return; // Cannot continue without HUD
            }
            else
            {
                Debug.Log($"[PlayerUIManager] Using PlayerHUD: {playerHUD.name}, active={playerHUD.gameObject.activeSelf}");
            }

            if (inventoryPanel == null)
            {
                Debug.LogError("[PlayerUIManager] InventoryPanel not registered! Make sure InventoryPanel exists in scene and has Awake() called.");
                // Continue anyway, HUD is more important
            }
            else
            {
                Debug.Log($"[PlayerUIManager] Using InventoryPanel: {inventoryPanel.name}, active={inventoryPanel.gameObject.activeSelf}");
            }

            // Initialize components (only for owner)
            if (inventoryPanel != null)
            {
                Debug.Log($"[PlayerUIManager] Initializing InventoryPanel with localPlayer={localPlayer.name}, IsLocalPlayer={localPlayer.IsLocalPlayer}");
                inventoryPanel.Initialize(localPlayer, inventorySystem);
            }
            else
            {
                Debug.LogWarning("[PlayerUIManager] inventoryPanel is null! Cannot initialize inventory panel.");
            }

            if (playerHUD != null)
            {
                Debug.Log($"[PlayerUIManager] Initializing PlayerHUD with localPlayer={localPlayer.name}");
                playerHUD.Initialize(localPlayer, inventorySystem, inventoryPanel);
            }
            else
            {
                Debug.LogError("[PlayerUIManager] playerHUD is null! Cannot initialize HUD.");
                return; // Cannot continue without HUD
            }

            // Enable UI for owner
            EnableUI();

            isInitialized = true;
            Debug.Log($"[PlayerUIManager] UI initialized and enabled for local player: {localPlayer.name}");
        }

        /// <summary>
        /// Enable UI for local player (owner)
        /// </summary>
        private void EnableUI()
        {
            Debug.Log($"[PlayerUIManager] EnableUI() called - playerHUD={playerHUD != null}, inventoryPanel={inventoryPanel != null}");
            
            // Refresh UI references before enabling (in case they weren't found earlier)
            RefreshUIReferences();
            
            if (playerHUD != null)
            {
                // Ensure parent is active first
                if (playerHUD.transform.parent != null && !playerHUD.transform.parent.gameObject.activeSelf)
                {
                    Debug.LogWarning($"[PlayerUIManager] PlayerHUD parent is inactive! Activating parent: {playerHUD.transform.parent.name}");
                    playerHUD.transform.parent.gameObject.SetActive(true);
                }
                
                playerHUD.gameObject.SetActive(true);
                Debug.Log($"[PlayerUIManager] PlayerHUD enabled: activeSelf={playerHUD.gameObject.activeSelf}, activeInHierarchy={playerHUD.gameObject.activeInHierarchy}");
                
                // Verify it's actually visible
                if (!playerHUD.gameObject.activeInHierarchy)
                {
                    Debug.LogError($"[PlayerUIManager] PlayerHUD is still not active in hierarchy! Parent: {playerHUD.transform.parent?.name ?? "None"}, Root: {playerHUD.transform.root.name}");
                }
            }
            else
            {
                Debug.LogError("[PlayerUIManager] Cannot enable PlayerHUD - reference is null! Trying to find in scene...");
                // Try to find and register again
                PlayerHUD foundHUD = FindFirstObjectByType<PlayerHUD>();
                if (foundHUD != null)
                {
                    Debug.LogWarning($"[PlayerUIManager] Found PlayerHUD in scene: {foundHUD.name}, registering and enabling...");
                    UIRegistry.RegisterPlayerHUD(foundHUD);
                    playerHUD = foundHUD;
                    foundHUD.gameObject.SetActive(true);
                }
                else
                {
                    Debug.LogError("[PlayerUIManager] PlayerHUD NOT FOUND in scene! Please create PlayerHUD GameObject in Canvas.");
                }
            }
            
            // InventoryPanel will be enabled/disabled by ToggleInventory()
            // But ensure it's initialized and ready
            if (inventoryPanel != null)
            {
                inventoryPanel.gameObject.SetActive(true); // Enable the GameObject
                // panelRoot will be controlled by ToggleInventory()
                Debug.Log($"[PlayerUIManager] InventoryPanel GameObject enabled: {inventoryPanel.gameObject.activeSelf}");
            }
        }

        /// <summary>
        /// Disable UI for non-owner or when no local player
        /// </summary>
        private void DisableUI()
        {
            Debug.Log($"[PlayerUIManager] DisableUI() called - playerHUD={playerHUD != null}, inventoryPanel={inventoryPanel != null}");
            
            if (playerHUD != null)
            {
                playerHUD.gameObject.SetActive(false);
                Debug.Log("[PlayerUIManager] PlayerHUD disabled");
            }
            
            if (inventoryPanel != null)
            {
                inventoryPanel.ForceDisable();
                inventoryPanel.gameObject.SetActive(false);
                Debug.Log("[PlayerUIManager] InventoryPanel disabled");
            }
        }

        /// <summary>
        /// Get local player reference
        /// </summary>
        public NetworkPlayer GetLocalPlayer() => localPlayer;

        /// <summary>
        /// Get inventory system reference
        /// </summary>
            public InventoryService GetInventorySystem() => inventorySystem;

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
