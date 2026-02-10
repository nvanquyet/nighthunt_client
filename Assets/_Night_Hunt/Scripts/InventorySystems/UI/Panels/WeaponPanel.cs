using UnityEngine;
using UnityEngine.UI;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Config;
using NightHunt.Inventory.UI.Slots;
using NightHunt.Inventory.UI.Data;
using System.Collections.Generic;

namespace NightHunt.Inventory.UI.Panels
{
    /// <summary>
    /// Panel controller for weapon slots.
    /// Spawns WeaponSlotUI[], subscribes to WeaponEvents, manages layout.
    /// </summary>
    public class WeaponPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform slotContainer;
        [SerializeField] private WeaponSlotUI weaponSlotPrefab;
        [SerializeField] private InventoryUIDataProvider dataProvider;
        [SerializeField] public SlotLayoutConfig slotLayoutConfig;
        
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        // State
        private Dictionary<WeaponSlotType, WeaponSlotUI> slotUIs = new Dictionary<WeaponSlotType, WeaponSlotUI>();
        
        // === Public API ===
        
        /// <summary>
        /// Initialize panel with weapon slots from SlotLayoutConfig.
        /// </summary>
        public void Initialize()
        {
            if (slotContainer == null || weaponSlotPrefab == null)
            {
                LogError("Slot container or prefab not assigned!");
                return;
            }
            
            // Use SlotLayoutConfig to determine which slots to spawn
            if (slotLayoutConfig != null && slotLayoutConfig.WeaponSlots != null)
            {
                // Spawn slots based on config
                foreach (var slotDef in slotLayoutConfig.WeaponSlots)
                {
                    SpawnWeaponSlot(slotDef.SlotType);
                }
            }
            else
            {
                // Fallback: spawn all enum values if no config
                var slotTypes = System.Enum.GetValues(typeof(WeaponSlotType));
                foreach (WeaponSlotType slotType in slotTypes)
                {
                    SpawnWeaponSlot(slotType);
                }
            }
            
            // Refresh from data
            RefreshFromData();
            
            Log($"Initialized with {slotUIs.Count} weapon slots (from config: {slotLayoutConfig != null})");
        }
        
        /// <summary>
        /// Refresh all slots from weapon data.
        /// </summary>
        public void RefreshFromData()
        {
            if (dataProvider == null)
                return;
            
            foreach (var kvp in slotUIs)
            {
                kvp.Value.RefreshFromWeapons();
            }
        }
        
        /// <summary>
        /// Refresh specific slot.
        /// </summary>
        public void RefreshSlot(WeaponSlotType slotType)
        {
            if (slotUIs.ContainsKey(slotType))
            {
                slotUIs[slotType].RefreshFromWeapons();
            }
        }
        
        // === Slot Management ===
        
        private void SpawnWeaponSlot(WeaponSlotType slotType)
        {
            if (slotContainer == null || weaponSlotPrefab == null)
                return;
            
            var slotUI = Instantiate(weaponSlotPrefab, slotContainer);
            
            if (slotUI != null)
            {
                slotUI.SetSlotType(slotType);
                slotUIs[slotType] = slotUI;
                slotUI.gameObject.SetActive(true);
                
                // Always spawn - even if empty, slot should be visible
                Log($"Spawned weapon slot: {slotType}");
            }
        }
        
        // === Event Handlers ===
        
        private void OnWeaponEquipped(ItemInstance weapon, WeaponSlotType slotType)
        {
            RefreshSlot(slotType);
            Log($"Weapon equipped in {slotType}: {weapon.Definition.DisplayName}");
        }
        
        private void OnWeaponUnequipped(ItemInstance weapon, WeaponSlotType slotType)
        {
            RefreshSlot(slotType);
            Log($"Weapon unequipped from {slotType}");
        }
        
        private void OnActiveWeaponChanged(ItemInstance previous, ItemInstance newWeapon, WeaponSlotType slotType)
        {
            // Refresh all slots to update active indicator
            RefreshFromData();
            Log($"Active weapon changed to {slotType}");
        }
        
        // === Event Subscription ===
        
        void Start()
        {
            if (dataProvider == null)
                dataProvider = FindObjectOfType<InventoryUIDataProvider>();
            
            // Subscribe to events
            WeaponEvents.OnWeaponEquipped += OnWeaponEquipped;
            WeaponEvents.OnWeaponUnequipped += OnWeaponUnequipped;
            WeaponEvents.OnActiveWeaponChanged += OnActiveWeaponChanged;
            
            // Initialize
            Initialize();
        }
        
        void OnDestroy()
        {
            // Unsubscribe
            WeaponEvents.OnWeaponEquipped -= OnWeaponEquipped;
            WeaponEvents.OnWeaponUnequipped -= OnWeaponUnequipped;
            WeaponEvents.OnActiveWeaponChanged -= OnActiveWeaponChanged;
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
                Debug.Log($"[WeaponPanel] {message}");
        }
        
        void LogError(string message)
        {
            Debug.LogError($"[WeaponPanel] {message}");
        }
    }
}
