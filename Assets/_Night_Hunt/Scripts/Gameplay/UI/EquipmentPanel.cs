using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using NightHunt.Gameplay.Inventory;
using NightHunt.Gameplay.UI.Config;

namespace NightHunt.Gameplay.UI
{
    /// <summary>
    /// Equipment panel - displayed on the right in Equipment Mode
    /// Shows weapon slots, equipment slots, and trash slot
    /// Supports config-based spawning or manual assignment
    /// </summary>
    public class EquipmentPanel : MonoBehaviour
    {
        [Header("Weapon Slots")]
        [SerializeField] private Transform weaponSlotsContainer;
        [SerializeField] private WeaponSlotsConfig weaponSlotsConfig; // Config for weapon slots (REQUIRED - use prefab)

        [Header("Quick Slots")]
        [SerializeField] private Transform quickSlotsContainer;
        [SerializeField] private Transform quickSlotContainerHUD;
        [SerializeField] private QuickSlotsConfig quickSlotsConfig; // Config for quick slots (REQUIRED - use prefab)

        [Header("Equipment Slots")]
        [SerializeField] private Transform equipmentSlotsContainer;
        [SerializeField] private EquipmentPartsConfig equipmentPartsConfig; // Config for equipment parts (REQUIRED - use prefab)

        [Header("Panel Root")]
        [SerializeField] private GameObject panelRoot; // Panel UI GameObject (separate from script GameObject)

        private InventoryPanel inventoryPanel;
        private InventoryService inventorySystem;
        private List<ItemCell> spawnedWeaponSlots = new List<ItemCell>(); // Track spawned weapon slots
        private List<ItemCell> spawnedQuickSlots = new List<ItemCell>(); // Track spawned quick slots
        private List<ItemCell> spawnedEquipmentSlots = new List<ItemCell>(); // Track spawned equipment slots

        /// <summary>
        /// Initialize equipment panel
        /// </summary>
        public void Initialize(InventoryPanel panel, InventoryService inventory)
        {
            inventoryPanel = panel;
            inventorySystem = inventory;

            SetupWeaponSlots();
            SetupQuickSlots();
            SetupEquipmentSlots();

            // Hide initially
            Hide();
        }

        /// <summary>
        /// Setup weapon slots - spawn from config/prefab (REQUIRED)
        /// </summary>
        private void SetupWeaponSlots()
        {
            if (weaponSlotsContainer == null)
            {
                Debug.LogError("[EquipmentPanel] weaponSlotsContainer is null!");
                return;
            }

            GameObject prefab = null;
            int slotCount = 2; // Default 2 weapon slots

            // Get prefab from config or direct reference
            if (weaponSlotsConfig != null && weaponSlotsConfig.slotPrefab != null)
            {
                prefab = weaponSlotsConfig.slotPrefab;
                slotCount = weaponSlotsConfig.slotCount;
            }
            else
            {
                Debug.LogError("[EquipmentPanel] Weapon slot prefab is required! Assign WeaponSlotsConfig or weaponSlotPrefab.");
                return;
            }

            // Clear existing spawned slots
            foreach (var slot in spawnedWeaponSlots)
            {
                if (slot != null)
                {
                    Destroy(slot.gameObject);
                }
            }
            spawnedWeaponSlots.Clear();

            // Spawn weapon slots
            for (int i = 0; i < slotCount; i++)
            {
                GameObject slotObj = Instantiate(prefab, weaponSlotsContainer);
                slotObj.SetActive(true);

                ItemCell slotUI = slotObj.GetComponent<ItemCell>();
                if (slotUI == null)
                {
                    slotUI = slotObj.AddComponent<ItemCell>();
                }

                // Create empty slot for weapon
                InventorySlot emptySlot = new InventorySlot();
                slotUI.Initialize(emptySlot, inventoryPanel, ItemCellLocation.Weapon, i);
                spawnedWeaponSlots.Add(slotUI);
            }

            Debug.Log($"[EquipmentPanel] Spawned {spawnedWeaponSlots.Count} weapon slots from config/prefab");
        }

        /// <summary>
        /// Setup quick slots - spawn from config (REQUIRED)
        /// </summary>
        private void SetupQuickSlots()
        {
            if (quickSlotsConfig == null)
            {
                Debug.LogError("[EquipmentPanel] QuickSlotsConfig is REQUIRED! Please assign a QuickSlotsConfig asset.");
                return;
            }

            SpawnQuickSlotsFromConfig();
        }

        /// <summary>
        /// Spawn quick slots from config
        /// </summary>
        private void SpawnQuickSlotsFromConfig()
        {
            if (quickSlotsConfig == null || quickSlotsConfig.slotPrefab == null)
            {
                Debug.LogError("[EquipmentPanel] QuickSlotsConfig or slotPrefab is null!");
                return;
            }

            if (quickSlotsContainer == null)
            {
                Debug.LogError("[EquipmentPanel] quickSlotsContainer is null!");
                return;
            }

            // Clear existing spawned slots
            foreach (var slot in spawnedQuickSlots)
            {
                if (slot != null)
                {
                    Destroy(slot.gameObject);
                }
            }
            spawnedQuickSlots.Clear();

            // Spawn slots based on config
            for (int i = 0; i < quickSlotsConfig.slotCount; i++)
            {
                GameObject slotObj = Instantiate(quickSlotsConfig.slotPrefab, quickSlotsContainer);
                slotObj.SetActive(true);

                ItemCell slotUI = slotObj.GetComponent<ItemCell>();
                if (slotUI == null)
                {
                    slotUI = slotObj.AddComponent<ItemCell>();
                }

                // Create empty slot for quick slot
                InventorySlot emptySlot = new InventorySlot();
                slotUI.Initialize(emptySlot, inventoryPanel, ItemCellLocation.QuickSlot, i);
                spawnedQuickSlots.Add(slotUI);
            }

            Debug.Log($"[EquipmentPanel] Spawned {spawnedQuickSlots.Count} quick slots from config");
        }

        /// <summary>
        /// Update quick slots count at runtime (if allowed by config)
        /// </summary>
        public void UpdateQuickSlotsCount(int newCount)
        {
            if (quickSlotsConfig == null)
            {
                Debug.LogWarning("[EquipmentPanel] Cannot update quick slots count: QuickSlotsConfig is null");
                return;
            }

            if (!quickSlotsConfig.canChangeInRuntime)
            {
                Debug.LogWarning("[EquipmentPanel] Cannot update quick slots count: Runtime changes not allowed");
                return;
            }

            if (newCount < quickSlotsConfig.minSlotCount || newCount > quickSlotsConfig.maxSlotCount)
            {
                Debug.LogWarning($"[EquipmentPanel] Quick slots count {newCount} is out of range [{quickSlotsConfig.minSlotCount}, {quickSlotsConfig.maxSlotCount}]");
                return;
            }

            quickSlotsConfig.slotCount = newCount;
            SpawnQuickSlotsFromConfig();
            Debug.Log($"[EquipmentPanel] Updated quick slots count to {newCount}");
        }

        /// <summary>
        /// Setup equipment slots - spawn from config (REQUIRED)
        /// </summary>
        private void SetupEquipmentSlots()
        {
            if (equipmentPartsConfig == null)
            {
                Debug.LogError("[EquipmentPanel] EquipmentPartsConfig is REQUIRED! Please assign an EquipmentPartsConfig asset.");
                return;
            }

            SpawnEquipmentSlotsFromConfig();
        }

        /// <summary>
        /// Spawn equipment slots from config
        /// </summary>
        private void SpawnEquipmentSlotsFromConfig()
        {
            if (equipmentPartsConfig == null)
            {
                Debug.LogError("[EquipmentPanel] EquipmentPartsConfig is null!");
                return;
            }

            if (equipmentSlotsContainer == null)
            {
                Debug.LogError("[EquipmentPanel] equipmentSlotsContainer is null!");
                return;
            }

            // Clear existing spawned slots
            foreach (var slot in spawnedEquipmentSlots)
            {
                if (slot != null)
                {
                    Destroy(slot.gameObject);
                }
            }
            spawnedEquipmentSlots.Clear();

            // Spawn slots based on config
            foreach (var partData in equipmentPartsConfig.parts)
            {
                GameObject slotObj = Instantiate(equipmentPartsConfig.slotPrefab, equipmentSlotsContainer);
                slotObj.SetActive(true);

                ItemCell slotUI = slotObj.GetComponent<ItemCell>();
                if (slotUI == null)
                {
                    slotUI = slotObj.AddComponent<ItemCell>();
                }

                // Create empty slot for equipment
                InventorySlot emptySlot = new InventorySlot();
                slotUI.Initialize(emptySlot, inventoryPanel, ItemCellLocation.Equipment, -1, partData.slotType);
                spawnedEquipmentSlots.Add(slotUI);
            }

            Debug.Log($"[EquipmentPanel] Spawned {spawnedEquipmentSlots.Count} equipment slots from config");
        }


        /// <summary>
        /// Get all equipment slots
        /// </summary>
        public ItemCell[] GetEquipmentSlots()
        {
            return spawnedEquipmentSlots.ToArray();
        }

        /// <summary>
        /// Get all weapon slots
        /// </summary>
        public ItemCell[] GetWeaponSlots()
        {
            return spawnedWeaponSlots.ToArray();
        }

        /// <summary>
        /// Show equipment panel
        /// </summary>
        public void Show()
        {
            Debug.Log($"[EquipmentPanel] ===== Show() called =====");
            
            // Show panel UI GameObject (script GameObject remains active)
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
                Debug.Log($"[EquipmentPanel] panelRoot.SetActive(true) called");
            }
            else
            {
                Debug.LogWarning($"[EquipmentPanel] panelRoot is null! Cannot show panel UI.");
            }
        }

        /// <summary>
        /// Hide equipment panel
        /// </summary>
        public void Hide()
        {
            Debug.Log($"[EquipmentPanel] ===== Hide() called =====");
            
            // Hide panel UI GameObject (script GameObject remains active)
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
                Debug.Log($"[EquipmentPanel] panelRoot.SetActive(false) called");
            }
            else
            {
                Debug.LogWarning($"[EquipmentPanel] panelRoot is null! Cannot hide panel UI.");
            }
        }

        /// <summary>
        /// Refresh equipment slots display
        /// </summary>
        public void RefreshSlots()
        {
            // TODO: Update weapon slots from equipped weapons
            // TODO: Update equipment slots from equipped items
        }

        /// <summary>
        /// Get weapon slot by index
        /// </summary>
        public ItemCell GetWeaponSlot(int index)
        {
            if (spawnedWeaponSlots != null && index >= 0 && index < spawnedWeaponSlots.Count)
            {
                return spawnedWeaponSlots[index];
            }
            return null;
        }

        /// <summary>
        /// Get equipment slot by type
        /// </summary>
        public ItemCell GetEquipmentSlot(EquipmentSlotType type)
        {
            foreach (var slot in spawnedEquipmentSlots)
            {
                if (slot != null && slot.GetEquipmentSlotType() == type)
                {
                    return slot;
                }
            }
            return null;
        }

        /// <summary>
        /// Get quick slot by index
        /// </summary>
        public ItemCell GetQuickSlot(int index)
        {
            if (spawnedQuickSlots != null && index >= 0 && index < spawnedQuickSlots.Count)
            {
                return spawnedQuickSlots[index];
            }
            return null;
        }

        /// <summary>
        /// Refresh quick slots display
        /// </summary>
        public void RefreshQuickSlots()
        {
            // TODO: Update quick slots from inventory system
            if (inventorySystem == null) return;

            for (int i = 0; i < spawnedQuickSlots.Count; i++)
            {
                if (spawnedQuickSlots[i] != null)
                {
                    // TODO: Get assigned item from inventory system and update slot
                    // var assignedSlot = inventorySystem.GetQuickSlot(i);
                    // spawnedQuickSlots[i].AssignItem(assignedSlot);
                }
            }
        }
    }
}
