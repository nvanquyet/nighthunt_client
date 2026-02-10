using UnityEngine;
using FishNet.Object;
using NightHunt.Inventory.UI;

namespace NightHunt.Inventory.UI.Management
{
    /// <summary>
    /// Centralized UI manager for inventory system.
    /// Manages show/hide of entire MainInventoryUI root GameObject,
    /// handles Tab key input, and auto-detects server to deactivate UI.
    /// </summary>
    public class InventoryUIManager : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Root GameObject of MainInventoryUI to show/hide")]
        [SerializeField] private GameObject mainInventoryUIRoot;
        [Tooltip("MainInventoryUIManager component")]
        [SerializeField] private MainInventoryUIManager mainInventoryManager;
        
        [Header("Settings")]
        [Tooltip("Key to toggle inventory UI")]
        [SerializeField] private KeyCode toggleKey = KeyCode.Tab;
        [Tooltip("Start with inventory closed")]
        [SerializeField] private bool startClosed = true;
        [Tooltip("Disable UI on server automatically")]
        [SerializeField] private bool disableOnServer = true;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        // State
        private bool isOpen = false;
        
        // === Lifecycle ===
        
        void Start()
        {
            // Auto-find references if not assigned
            if (mainInventoryUIRoot == null)
            {
                // Try to find MainInventoryUI root
                var manager = FindObjectOfType<MainInventoryUIManager>();
                if (manager != null)
                {
                    mainInventoryUIRoot = manager.gameObject;
                    mainInventoryManager = manager;
                }
            }
            
            if (mainInventoryUIRoot == null)
            {
                LogError("MainInventoryUIRoot not assigned and could not be found!");
                enabled = false;
                return;
            }
            
            if (mainInventoryManager == null)
            {
                mainInventoryManager = mainInventoryUIRoot.GetComponent<MainInventoryUIManager>();
                if (mainInventoryManager == null)
                {
                    LogError("MainInventoryUIManager component not found on root!");
                    enabled = false;
                    return;
                }
            }
            
            // Check if running on server
            if (IsServer() && disableOnServer)
            {
                Log("Running on server - deactivating UI");
                mainInventoryUIRoot.SetActive(false);
                enabled = false; // Disable this component
                return;
            }
            
            // Set initial state
            if (startClosed)
            {
                CloseInventory();
            }
            else
            {
                OpenInventory();
            }
        }
        
        void Update()
        {
            // Handle toggle key
            if (Input.GetKeyDown(toggleKey))
            {
                ToggleInventory();
            }
        }
        
        // === Public API ===
        
        /// <summary>
        /// Toggle inventory UI open/closed.
        /// </summary>
        public void ToggleInventory()
        {
            if (isOpen)
                CloseInventory();
            else
                OpenInventory();
        }
        
        /// <summary>
        /// Open inventory UI.
        /// </summary>
        public void OpenInventory()
        {
            if (isOpen)
                return;
            
            if (mainInventoryUIRoot == null || mainInventoryManager == null)
                return;
            
            isOpen = true;
            mainInventoryUIRoot.SetActive(true);
            mainInventoryManager.OpenInventory();
            
            Log("Inventory UI opened");
        }
        
        /// <summary>
        /// Close inventory UI.
        /// </summary>
        public void CloseInventory()
        {
            if (!isOpen)
                return;
            
            if (mainInventoryUIRoot == null || mainInventoryManager == null)
                return;
            
            isOpen = false;
            mainInventoryManager.CloseInventory();
            mainInventoryUIRoot.SetActive(false);
            
            Log("Inventory UI closed");
        }
        
        /// <summary>
        /// Check if inventory UI is open.
        /// </summary>
        public bool IsOpen() => isOpen;
        
        // === Utility ===
        
        /// <summary>
        /// Check if running on server.
        /// </summary>
        private bool IsServer()
        {
            // Check if FishNet NetworkManager exists and is server
            var networkManager = FishNet.InstanceFinder.NetworkManager;
            if (networkManager != null)
            {
                return networkManager.IsServer;
            }
            
            // Fallback: Check if any NetworkObject in scene is server-only
            // This is a simple check - in practice, you might want more robust detection
            return false;
        }
        
        // === Debug ===
        
        void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[InventoryUIManager] {message}");
        }
        
        void LogError(string message)
        {
            Debug.LogError($"[InventoryUIManager] {message}");
        }
    }
}
