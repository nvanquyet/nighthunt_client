using UnityEngine;
using UnityEngine.UI;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Config;
using NightHunt.Inventory.UI.Slots;
using NightHunt.Inventory.UI.Data;
using NightHunt.Inventory.UI;
using System.Collections.Generic;

namespace NightHunt.Inventory.UI.Panels
{
    /// <summary>
    /// Panel controller for equipment slots.
    /// Spawns EquipmentSlotUI[], subscribes to EquipmentEvents, manages layout.
    /// </summary>
    public class EquipmentPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform slotContainer;
        [SerializeField] private EquipmentSlotUI equipmentSlotPrefab;
        [SerializeField] private InventoryUIDataProvider dataProvider;
        [SerializeField] private MainInventoryUIManager uiManager;
        [SerializeField] public SlotLayoutConfig slotLayoutConfig;
        
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        // State
        private Dictionary<EquipmentSlotType, EquipmentSlotUI> slotUIs = new Dictionary<EquipmentSlotType, EquipmentSlotUI>();
        
        // === Public API ===
        
        /// <summary>
        /// Initialize panel with equipment slots from SlotLayoutConfig.
        /// </summary>
        public void Initialize()
        {
            if (slotContainer == null || equipmentSlotPrefab == null)
            {
                LogError("Slot container or prefab not assigned!");
                return;
            }
            
            // Use SlotLayoutConfig to determine which slots to spawn
            if (slotLayoutConfig != null && slotLayoutConfig.EquipmentSlots != null)
            {
                // Spawn slots based on config
                foreach (var slotDef in slotLayoutConfig.EquipmentSlots)
                {
                    SpawnEquipmentSlot(slotDef.SlotType);
                }
            }
            else
            {
                // Fallback: spawn all enum values if no config
                var slotTypes = System.Enum.GetValues(typeof(EquipmentSlotType));
                foreach (EquipmentSlotType slotType in slotTypes)
                {
                    SpawnEquipmentSlot(slotType);
                }
            }
            
            // Refresh from data
            RefreshFromData();
            
            Log($"Initialized with {slotUIs.Count} equipment slots (from config: {slotLayoutConfig != null})");
        }
        
        /// <summary>
        /// Refresh all slots from equipment data.
        /// </summary>
        public void RefreshFromData()
        {
            if (dataProvider == null)
                return;
            
            foreach (var kvp in slotUIs)
            {
                kvp.Value.RefreshFromEquipment();
            }
        }
        
        /// <summary>
        /// Refresh specific slot.
        /// </summary>
        public void RefreshSlot(EquipmentSlotType slotType)
        {
            if (slotUIs.ContainsKey(slotType))
            {
                slotUIs[slotType].RefreshFromEquipment();
            }
        }
        
        // === Slot Management ===
        
        private void SpawnEquipmentSlot(EquipmentSlotType slotType)
        {
            if (slotContainer == null || equipmentSlotPrefab == null)
                return;
            
            var slotUI = Instantiate(equipmentSlotPrefab, slotContainer);
            
            if (slotUI != null)
            {
                slotUI.SetSlotType(slotType);
                
                // Slot will get slotLayoutConfig from this panel when needed
                slotUIs[slotType] = slotUI;
                
                // Subscribe to slot events
                slotUI.OnSlotHovered += OnSlotHovered;
                
                slotUI.gameObject.SetActive(true);
                
                // Always spawn - even if empty, slot should be visible
                Log($"Spawned equipment slot: {slotType}");
            }
        }
        
        // === Event Handlers ===
        
        private void OnSlotHovered(ItemSlotUI slot)
        {
            if (slot is EquipmentSlotUI equipmentSlot)
            {
                var item = equipmentSlot.GetItem();
                if (uiManager != null && item != null)
                {
                    uiManager.HoverEquippedItem(item);
                }
            }
        }
        
        private void OnEquipmentItemEquipped(ItemInstance item, EquipmentSlotType slotType)
        {
            RefreshSlot(slotType);
            Log($"Item equipped in {slotType}: {item.Definition.DisplayName}");
        }
        
        private void OnEquipmentItemUnequipped(ItemInstance item, EquipmentSlotType slotType)
        {
            RefreshSlot(slotType);
            Log($"Item unequipped from {slotType}");
        }
        
        private void OnEquipmentSwapped(ItemInstance oldItem, ItemInstance newItem, EquipmentSlotType slotType)
        {
            RefreshSlot(slotType);
            Log($"Equipment swapped in {slotType}");
        }
        
        // === Event Subscription ===
        
        void Start()
        {
            if (dataProvider == null)
                dataProvider = FindObjectOfType<InventoryUIDataProvider>();
            
            if (uiManager == null)
                uiManager = FindObjectOfType<MainInventoryUIManager>();
            
            // Subscribe to events
            EquipmentEvents.OnItemEquipped += OnEquipmentItemEquipped;
            EquipmentEvents.OnItemUnequipped += OnEquipmentItemUnequipped;
            EquipmentEvents.OnEquipmentSwapped += OnEquipmentSwapped;
            
            // Initialize
            Initialize();
        }
        
        void OnDestroy()
        {
            // Unsubscribe
            EquipmentEvents.OnItemEquipped -= OnEquipmentItemEquipped;
            EquipmentEvents.OnItemUnequipped -= OnEquipmentItemUnequipped;
            EquipmentEvents.OnEquipmentSwapped -= OnEquipmentSwapped;
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
                Debug.Log($"[EquipmentPanel] {message}");
        }
        
        void LogError(string message)
        {
            Debug.LogError($"[EquipmentPanel] {message}");
        }
    }
}
