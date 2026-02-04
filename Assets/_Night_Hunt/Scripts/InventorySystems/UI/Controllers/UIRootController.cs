using UnityEngine;
using NightHunt.Inventory.Input;
using NightHunt.Inventory.UI.Panels;
using NightHunt.Inventory.Domain.Equipment;
using NightHunt.Inventory.Domain.Weapon;
using NightHunt.Inventory.Domain.Attachment;
using NightHunt.Inventory.Domain.QuickSlot;
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
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        private InventoryInputHandler inventoryInputHandler;
        
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
            
            // Initialize UI state
            SetInitialUIState();
            
            // Try to setup managers from local player (if already spawned)
            FindAndSetupLocalPlayer();
        }
        
        void Start()
        {
            // Try again in Start in case player spawns after OnEnable
            FindAndSetupLocalPlayer();
        }
        
        void OnDisable()
        {
            if (inventoryInputHandler != null)
            {
                inventoryInputHandler.OnToggleInventory -= HandleInventoryToggle;
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
        /// Sets up all UI panels with managers from local player.
        /// Called when local player spawns.
        /// </summary>
        public void SetupForPlayer(NetworkPlayer localPlayer)
        {
            if (localPlayer == null)
            {
                Debug.LogError("[UIRootController] Cannot setup - localPlayer is null!");
                return;
            }
            
            // Inject EquipmentManager
            if (equipmentPanelUI != null)
            {
                var equipmentManager = localPlayer.GetComponent<EquipmentManager>();
                if (equipmentManager != null)
                {
                    equipmentPanelUI.SetEquipmentManager(equipmentManager);
                }
                else
                {
                    Debug.LogWarning("[UIRootController] EquipmentManager not found on player!");
                }
            }
            
            // Inject WeaponManager
            if (weaponPanelUI != null)
            {
                var weaponManager = localPlayer.GetComponent<WeaponManager>();
                if (weaponManager != null)
                {
                    weaponPanelUI.SetWeaponManager(weaponManager);
                }
                else
                {
                    Debug.LogWarning("[UIRootController] WeaponManager not found on player!");
                }
            }
            
            // Inject AttachmentManager
            if (attachmentPanelUI != null)
            {
                var attachmentManager = localPlayer.GetComponent<AttachmentManager>();
                if (attachmentManager != null)
                {
                    attachmentPanelUI.SetAttachmentManager(attachmentManager);
                }
                else
                {
                    Debug.LogWarning("[UIRootController] AttachmentManager not found on player!");
                }
            }
            
            // Inject QuickSlotManager
            if (quickSlotHUDController != null)
            {
                var quickSlotManager = localPlayer.GetComponent<QuickSlotManager>();
                if (quickSlotManager != null)
                {
                    quickSlotHUDController.SetQuickSlotManager(quickSlotManager);
                }
                else
                {
                    Debug.LogWarning("[UIRootController] QuickSlotManager not found on player!");
                }
            }
            
            if (enableDebugLogs)
                Debug.Log("[UIRootController] Setup complete for local player");
        }
        
        /// <summary>
        /// Helper method to find local player and setup UI.
        /// Can be called from Start() or when player spawns.
        /// </summary>
        public void FindAndSetupLocalPlayer()
        {
            NetworkPlayer[] players = FindObjectsOfType<NetworkPlayer>();
            foreach (var player in players)
            {
                if (player.IsLocalPlayer)
                {
                    SetupForPlayer(player);
                    break;
                }
            }
        }
        
        #endregion
    }
}
