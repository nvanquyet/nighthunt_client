using UnityEngine;
using UnityEngine.UI;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Config;
using NightHunt.Inventory.UI.Slots;
using NightHunt.Inventory.UI.Data;
using System.Collections.Generic;

namespace NightHunt.Inventory.UI.Panels
{
    /// <summary>
    /// Panel controller for quick slots.
    /// Spawns QuickSlotUI[], subscribes to QuickSlotEvents, manages layout.
    /// </summary>
    public class QuickSlotPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform slotContainer;
        [SerializeField] private QuickSlotUI quickSlotPrefab;
        [SerializeField] private InventoryUIDataProvider dataProvider;
        [SerializeField] private SlotLayoutConfig slotLayoutConfig;
        
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        // State
        private List<QuickSlotUI> slotUIs = new List<QuickSlotUI>();
        private int slotCount = 0;
        
        // === Public API ===
        
        /// <summary>
        /// Initialize panel with quick slot count from SlotLayoutConfig.
        /// </summary>
        public void Initialize(int slotCount = 0)
        {
            // Use config if slotCount not provided or use config value
            if (slotCount <= 0 && slotLayoutConfig != null)
            {
                slotCount = slotLayoutConfig.QuickSlotCount;
            }
            
            // Fallback to default if still 0
            if (slotCount <= 0)
                slotCount = 4; // Default 4 quick slots
            
            this.slotCount = slotCount;
            
            // Spawn ALL slots (including empty ones) - UI always shows all slots
            SpawnSlots();
            
            // Refresh from data
            RefreshFromData();
            
            Log($"Initialized with {slotCount} quick slots (from config: {slotLayoutConfig != null})");
        }
        
        /// <summary>
        /// Refresh all slots from quick slot data.
        /// </summary>
        public void RefreshFromData()
        {
            if (dataProvider == null)
                return;
            
            for (int i = 0; i < slotUIs.Count && i < slotCount; i++)
            {
                slotUIs[i].RefreshFromQuickSlots();
            }
        }
        
        /// <summary>
        /// Refresh specific slot.
        /// </summary>
        public void RefreshSlot(int quickSlotIndex)
        {
            if (quickSlotIndex >= 0 && quickSlotIndex < slotUIs.Count)
            {
                slotUIs[quickSlotIndex].RefreshFromQuickSlots();
            }
        }
        
        // === Slot Management ===
        
        private void SpawnSlots()
        {
            // Clear existing slots
            ClearSlots();
            
            if (slotContainer == null || quickSlotPrefab == null)
            {
                LogError("Slot container or prefab not assigned!");
                return;
            }
            
            // Spawn slots
            for (int i = 0; i < slotCount; i++)
            {
                var slotUI = Instantiate(quickSlotPrefab, slotContainer);
                
                if (slotUI != null)
                {
                    slotUI.SetQuickSlotIndex(i);
                    slotUIs.Add(slotUI);
                    slotUI.gameObject.SetActive(true);
                }
            }
            
            Log($"Spawned {slotUIs.Count} quick slots");
        }
        
        private void ClearSlots()
        {
            foreach (var slot in slotUIs)
            {
                if (slot != null)
                {
                    Destroy(slot.gameObject);
                }
            }
            
            slotUIs.Clear();
        }
        
        // === Event Handlers ===
        
        private void OnQuickSlotAssigned(ItemInstance item, int quickSlotIndex)
        {
            RefreshSlot(quickSlotIndex);
            Log($"Quick slot assigned: {item.Definition.DisplayName} to slot {quickSlotIndex}");
        }
        
        private void OnQuickSlotCleared(int quickSlotIndex)
        {
            RefreshSlot(quickSlotIndex);
            Log($"Quick slot cleared: {quickSlotIndex}");
        }
        
        private void OnQuickSlotUpdated(ItemInstance item, int quickSlotIndex)
        {
            RefreshSlot(quickSlotIndex);
            Log($"Quick slot updated: slot {quickSlotIndex}");
        }
        
        // === Event Subscription ===
        
        void Start()
        {
            if (dataProvider == null)
                dataProvider = FindObjectOfType<InventoryUIDataProvider>();
            
            // Subscribe to events
            QuickSlotEvents.OnQuickSlotAssigned += OnQuickSlotAssigned;
            QuickSlotEvents.OnQuickSlotCleared += OnQuickSlotCleared;
            QuickSlotEvents.OnQuickSlotUpdated += OnQuickSlotUpdated;
            
            // Initialize - use config or data provider
            if (slotLayoutConfig != null)
            {
                Initialize(slotLayoutConfig.QuickSlotCount);
            }
            else if (dataProvider != null)
            {
                int count = dataProvider.GetQuickSlotCount();
                if (count > 0)
                {
                    Initialize(count);
                }
                else
                {
                    Initialize(4); // Default
                }
            }
            else
            {
                Initialize(4); // Default fallback
            }
        }
        
        void OnDestroy()
        {
            // Unsubscribe
            QuickSlotEvents.OnQuickSlotAssigned -= OnQuickSlotAssigned;
            QuickSlotEvents.OnQuickSlotCleared -= OnQuickSlotCleared;
            QuickSlotEvents.OnQuickSlotUpdated -= OnQuickSlotUpdated;
        }
        
        // === Lifecycle ===
        
        void Awake()
        {
            if (slotContainer == null)
                slotContainer = transform;
        
        }
        
        // === Debug ===
        
        void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[QuickSlotPanel] {message}");
        }
        
        void LogError(string message)
        {
            Debug.LogError($"[QuickSlotPanel] {message}");
        }
    }
}
