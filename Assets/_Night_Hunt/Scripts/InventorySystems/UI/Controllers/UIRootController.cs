using UnityEngine;
using NightHunt.Inventory.Input;
using NightHunt.Inventory.UI.Panels;
using NightHunt.Inventory.Domain.Equipment;
using NightHunt.Inventory.Domain.Weapon;
using NightHunt.Inventory.Domain.Attachment;
using NightHunt.Inventory.Domain.QuickSlot;
using NightHunt.Inventory.Core;
using NightHunt.Inventory.Core.Utilities;
using NightHunt.Networking;

namespace NightHunt.Inventory.UI.Controllers
{
    /// <summary>
    /// Centralized controller for showing/hiding HUD vs Inventory UI.
    /// Subscribes to InventoryInputHandler.OnToggleInventory to manage UI visibility.
    /// Also handles injection of managers from local player to UI panels.
    /// </summary>
    public class UIRootController : MonoBehaviour
    {
        [Header("UI Root References")]
        [SerializeField] private GameObject playerHUDRoot;
        [SerializeField] private GameObject inventoryUIRoot;
        
        [Header("Panel References (for manager injection)")]
        [SerializeField] private EquipmentPanelUI equipmentPanelUI;
        [SerializeField] private WeaponPanelUI weaponPanelUI;
        [SerializeField] private AttachmentPanelUI attachmentPanelUI;
        [SerializeField] private QuickSlotHUDController quickSlotHUDController;
        [SerializeField] private QuickSlotPanelUI quickSlotPanelUI;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        private InventoryInputHandler inventoryInputHandler;
        private NetworkPlayer currentDisplayedPlayer;
        
        #region Lifecycle
        
        void Awake()
        {
            // Find InventoryInputHandler from InputManager
            if (Gameplay.Input.Core.InputManager.Instance != null)
            {
                inventoryInputHandler = Gameplay.Input.Core.InputManager.Instance.InventoryHandler;
            }
            
            if (inventoryInputHandler == null)
            {
                Debug.LogWarning("[UIRootController] InventoryInputHandler not found! UI toggle may not work.");
            }
        }
        
        void OnEnable()
        {
            if (inventoryInputHandler != null)
            {
                inventoryInputHandler.OnToggleInventory += HandleInventoryToggle;
            }
            
            // Subscribe to spectate changes
            if (SpectateManager.Instance != null)
            {
                SpectateManager.Instance.OnCurrentPlayerChanged += OnCurrentPlayerChanged;
            }
            
            // Initialize UI state
            SetInitialUIState();
            
            // Try to setup managers from current player (if already spawned)
            FindAndSetupCurrentPlayer();
        }
        
        void Start()
        {
            // Try again in Start in case player spawns after OnEnable
            FindAndSetupCurrentPlayer();
        }
        
        void OnDisable()
        {
            if (inventoryInputHandler != null)
            {
                inventoryInputHandler.OnToggleInventory -= HandleInventoryToggle;
            }
            
            if (SpectateManager.Instance != null)
            {
                SpectateManager.Instance.OnCurrentPlayerChanged -= OnCurrentPlayerChanged;
            }
        }
        
        #endregion
        
        #region Event Handlers
        
        private void HandleInventoryToggle()
        {
            bool isInventoryOpen = inventoryInputHandler != null && inventoryInputHandler.IsInventoryOpen();
            
            if (isInventoryOpen)
            {
                // Show inventory, hide HUD
                ShowInventoryUI();
            }
            else
            {
                // Show HUD, hide inventory
                ShowPlayerHUD();
            }
        }
        
        private void OnCurrentPlayerChanged(NetworkPlayer player)
        {
            SetupForPlayer(player);
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Shows player HUD and hides inventory UI.
        /// </summary>
        public void ShowPlayerHUD()
        {
            if (playerHUDRoot != null)
            {
                playerHUDRoot.SetActive(true);
            }
            
            if (inventoryUIRoot != null)
            {
                inventoryUIRoot.SetActive(false);
            }
            
            if (enableDebugLogs)
                Debug.Log("[UIRootController] Showing Player HUD");
        }
        
        /// <summary>
        /// Shows inventory UI and hides player HUD.
        /// </summary>
        public void ShowInventoryUI()
        {
            if (playerHUDRoot != null)
            {
                playerHUDRoot.SetActive(false);
            }
            
            if (inventoryUIRoot != null)
            {
                inventoryUIRoot.SetActive(true);
            }
            
            if (enableDebugLogs)
                Debug.Log("[UIRootController] Showing Inventory UI");
        }
        
        #endregion
        
        #region Helper Methods
        
        private void SetInitialUIState()
        {
            // Default: show HUD, hide inventory
            bool isInventoryOpen = inventoryInputHandler != null && inventoryInputHandler.IsInventoryOpen();
            
            if (isInventoryOpen)
            {
                ShowInventoryUI();
            }
            else
            {
                ShowPlayerHUD();
            }
        }
        
        #endregion
        
        #region Manager Injection
        
        /// <summary>
        /// Sets up all UI panels with managers from current player (local or spectated).
        /// Called when player spawns or spectate changes.
        /// </summary>
        public void SetupForPlayer(NetworkPlayer player)
        {
            // Use SpectateManager to get current player (local or spectated)
            var currentPlayer = SpectateManager.Instance?.GetCurrentPlayer() ?? player;
            
            if (currentPlayer == null)
            {
                InventoryLogger.LogError("UIRootController", "Cannot setup - currentPlayer is null!");
                return;
            }
            
            currentDisplayedPlayer = currentPlayer;
            
            // Inject EquipmentManager
            if (equipmentPanelUI != null)
            {
                var equipmentManager = currentPlayer.GetComponent<EquipmentManager>();
                if (equipmentManager != null)
                {
                    equipmentPanelUI.SetEquipmentManager(equipmentManager);
                }
                else
                {
                    InventoryLogger.LogWarning("UIRootController", "EquipmentManager not found on player!", enableDebugLogs);
                }
            }
            
            // Inject WeaponManager
            if (weaponPanelUI != null)
            {
                var weaponManager = currentPlayer.GetComponent<WeaponManager>();
                if (weaponManager != null)
                {
                    weaponPanelUI.SetWeaponManager(weaponManager);
                }
                else
                {
                    InventoryLogger.LogWarning("UIRootController", "WeaponManager not found on player!", enableDebugLogs);
                }
            }
            
            // Inject AttachmentManager
            if (attachmentPanelUI != null)
            {
                var attachmentManager = currentPlayer.GetComponent<AttachmentManager>();
                if (attachmentManager != null)
                {
                    attachmentPanelUI.SetAttachmentManager(attachmentManager);
                }
                else
                {
                    InventoryLogger.LogWarning("UIRootController", "AttachmentManager not found on player!", enableDebugLogs);
                }
            }
            
            // Inject QuickSlotManager to HUD controller
            if (quickSlotHUDController != null)
            {
                var quickSlotManager = currentPlayer.GetComponent<QuickSlotManager>();
                if (quickSlotManager != null)
                {
                    quickSlotHUDController.SetQuickSlotManager(quickSlotManager);
                }
                else
                {
                    InventoryLogger.LogWarning("UIRootController", "QuickSlotManager not found on player!", enableDebugLogs);
                }
            }
            
            // Inject QuickSlotManager to Inventory Panel
            if (quickSlotPanelUI != null)
            {
                var quickSlotManager = currentPlayer.GetComponent<QuickSlotManager>();
                if (quickSlotManager != null)
                {
                    quickSlotPanelUI.SetQuickSlotManager(quickSlotManager);
                }
                else
                {
                    InventoryLogger.LogWarning("UIRootController", "QuickSlotManager not found on player!", enableDebugLogs);
                }
            }
            
            InventoryLogger.Log("UIRootController", $"Setup complete for player: {currentPlayer.PlayerName}", enableDebugLogs);
        }
        
        /// <summary>
        /// Helper method to find local player, set it in SpectateManager, and setup UI for current player.
        /// Can be called from Start() or when player spawns.
        /// </summary>
        public void FindAndSetupCurrentPlayer()
        {
            // First, set local player in SpectateManager
            NetworkPlayer[] players = FindObjectsOfType<NetworkPlayer>();
            foreach (var player in players)
            {
                if (player.IsLocalPlayer)
                {
                    SpectateManager.Instance?.SetLocalPlayer(player);
                    break;
                }
            }
            
            // Then setup UI for current player (local or spectated)
            var currentPlayer = SpectateManager.Instance?.GetCurrentPlayer();
            if (currentPlayer != null)
            {
                SetupForPlayer(currentPlayer);
            }
        }
        
        #endregion
    }
}
