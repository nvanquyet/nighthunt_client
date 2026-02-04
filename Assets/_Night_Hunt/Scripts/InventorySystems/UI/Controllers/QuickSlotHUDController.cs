using UnityEngine;
using System.Collections.Generic;
using NightHunt.Inventory.Domain.QuickSlot;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.UI.Buttons;

namespace NightHunt.Inventory.UI.Controllers
{
    /// <summary>
    /// Controller for HUD quick slot buttons.
    /// Spawns quick slot buttons from QuickSlotConfig at runtime.
    /// </summary>
    public class QuickSlotHUDController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Transform quickSlotContainer;
        [SerializeField] private GameObject quickSlotButtonPrefab;
        
        [Header("Configuration")]
        [SerializeField] private QuickSlotConfig config;
        
        [Header("References")]
        [SerializeField] private QuickSlotManager quickSlotManager;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        // Runtime-only fields (not shown in Inspector)
        private List<QuickSlotHUDButton> buttons;
        
        #region Lifecycle
        
        void Awake()
        {
            buttons = new List<QuickSlotHUDButton>();
        }
        
        /// <summary>
        /// Sets the QuickSlotManager reference (from local player).
        /// Called by parent controller or player setup.
        /// </summary>
        public void SetQuickSlotManager(QuickSlotManager manager)
        {
            quickSlotManager = manager;
            
            if (enableDebugLogs)
                Debug.Log("[QuickSlotHUDController] QuickSlotManager injected");
            
            // Initialize buttons if config is ready
            if (config != null && quickSlotContainer != null && quickSlotButtonPrefab != null)
            {
                InitializeButtons();
            }
        }
        
        void Start()
        {
            // Only initialize if manager is already set
            if (quickSlotManager != null)
            {
                InitializeButtons();
            }
        }
        
        void OnEnable()
        {
            // Subscribe to quick slot changes to update UI
            QuickSlotEvents.OnQuickSlotChanged += HandleQuickSlotChanged;
        }
        
        void OnDisable()
        {
            QuickSlotEvents.OnQuickSlotChanged -= HandleQuickSlotChanged;
        }
        
        #endregion
        
        #region Initialization
        
        private void InitializeButtons()
        {
            if (config == null)
            {
                Debug.LogError("[QuickSlotHUDController] QuickSlotConfig not assigned!");
                return;
            }
            
            if (quickSlotContainer == null)
            {
                Debug.LogError("[QuickSlotHUDController] QuickSlotContainer not assigned!");
                return;
            }
            
            if (quickSlotButtonPrefab == null)
            {
                Debug.LogError("[QuickSlotHUDController] QuickSlotButtonPrefab not assigned!");
                return;
            }
            
            if (quickSlotManager == null)
            {
                Debug.LogError("[QuickSlotHUDController] QuickSlotManager not assigned! Call SetQuickSlotManager first.");
                return;
            }
            
            // Clear existing buttons
            foreach (var btn in buttons)
            {
                if (btn != null)
                    Destroy(btn.gameObject);
            }
            buttons.Clear();
            
            // Spawn buttons from config
            for (int i = 0; i < config.SlotCount; i++)
            {
                var buttonObj = Instantiate(quickSlotButtonPrefab, quickSlotContainer);
                var button = buttonObj.GetComponent<QuickSlotHUDButton>();
                
                if (button != null)
                {
                    // Get display key from config if available
                    string displayKey = $"Ctrl+{i + 1}";
                    if (config.Bindings != null && i < config.Bindings.Length)
                    {
                        displayKey = config.Bindings[i].DisplayKey;
                    }
                    
                    button.Initialize(i, quickSlotManager, displayKey);
                    buttons.Add(button);
                    
                    if (enableDebugLogs)
                        Debug.Log($"[QuickSlotHUDController] Spawned quick slot button {i} with key {displayKey}");
                }
                else
                {
                    Debug.LogError($"[QuickSlotHUDController] Button prefab doesn't have QuickSlotHUDButton component!");
                    Destroy(buttonObj);
                }
            }
        }
        
        #endregion
        
        #region Event Handlers
        
        private void HandleQuickSlotChanged(int slotIndex, ItemInstance item)
        {
            // Update button UI if slot index is valid
            if (slotIndex >= 0 && slotIndex < buttons.Count && buttons[slotIndex] != null)
            {
                buttons[slotIndex].RefreshUI(item);
            }
        }
        
        #endregion
    }
}
