using UnityEngine;
using System.Collections.Generic;
using NightHunt.Gameplay.Inventory;

namespace NightHunt.Gameplay.UI
{
    /// <summary>
    /// Equipment panel - displayed on the right in Equipment Mode
    /// Shows weapon slots, equipment slots, and trash slot
    /// </summary>
    public class EquipmentPanel : MonoBehaviour
    {
        [Header("Weapon Slots")]
        [SerializeField] private Transform weaponSlotsContainer;
        [SerializeField] private WeaponSlotUI[] weaponSlots = new WeaponSlotUI[2]; // Assign manually in inspector

        [Header("Quick Slots")]
        [SerializeField] private QuickSlotUI[] quickSlots = new QuickSlotUI[4]; // Assign manually in inspector (placed below weapon slots)

        [Header("Equipment Slots")]
        [SerializeField] private EquipmentSlotUI[] equipmentSlots; // Assign manually in inspector (Backpack, Armor, Helmet, Vest)

        [Header("Trash Slot")]
        [SerializeField] private TrashSlotUI trashSlot;

        private InventoryPanel inventoryPanel;
        private InventorySystem inventorySystem;

        /// <summary>
        /// Initialize equipment panel
        /// </summary>
        public void Initialize(InventoryPanel panel, InventorySystem inventory)
        {
            inventoryPanel = panel;
            inventorySystem = inventory;

            SetupWeaponSlots();
            SetupQuickSlots();
            SetupEquipmentSlots();
            SetupTrashSlot();

            // Hide initially
            Hide();
        }

        /// <summary>
        /// Setup weapon slots - initialize manually assigned slots
        /// </summary>
        private void SetupWeaponSlots()
        {
            // Initialize manually assigned weapon slots
            for (int i = 0; i < weaponSlots.Length; i++)
            {
                if (weaponSlots[i] != null)
                {
                    weaponSlots[i].Initialize(i, null, inventoryPanel);
                }
            }
        }

        /// <summary>
        /// Setup quick slots - initialize manually assigned slots
        /// </summary>
        private void SetupQuickSlots()
        {
            // Initialize manually assigned quick slots
            for (int i = 0; i < quickSlots.Length; i++)
            {
                if (quickSlots[i] != null)
                {
                    quickSlots[i].Initialize(i, null, inventoryPanel);
                }
            }
        }

        /// <summary>
        /// Setup equipment slots - initialize manually assigned slots
        /// </summary>
        private void SetupEquipmentSlots()
        {
            // Initialize manually assigned equipment slots
            if (equipmentSlots == null)
                return;

            // Equipment slot types in order (should match the order in inspector)
            EquipmentSlotType[] slotTypes = {
                EquipmentSlotType.Backpack,
                EquipmentSlotType.Armor,
                EquipmentSlotType.Helmet,
                EquipmentSlotType.Vest
            };

            for (int i = 0; i < equipmentSlots.Length && i < slotTypes.Length; i++)
            {
                if (equipmentSlots[i] != null)
                {
                    equipmentSlots[i].Initialize(slotTypes[i], this, inventoryPanel);
                }
            }
        }

        /// <summary>
        /// Setup trash slot
        /// </summary>
        private void SetupTrashSlot()
        {
            if (trashSlot != null && inventoryPanel != null)
            {
                trashSlot.Initialize(inventoryPanel);
            }
        }

        /// <summary>
        /// Show equipment panel
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Hide equipment panel
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
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
        public WeaponSlotUI GetWeaponSlot(int index)
        {
            if (index >= 0 && index < weaponSlots.Length)
                return weaponSlots[index];
            return null;
        }

        /// <summary>
        /// Get equipment slot by type
        /// </summary>
        public EquipmentSlotUI GetEquipmentSlot(EquipmentSlotType type)
        {
            foreach (var slot in equipmentSlots)
            {
                if (slot != null && slot.GetSlotType() == type)
                {
                    return slot;
                }
            }
            return null;
        }

        /// <summary>
        /// Get quick slot by index
        /// </summary>
        public QuickSlotUI GetQuickSlot(int index)
        {
            if (index >= 0 && index < quickSlots.Length)
                return quickSlots[index];
            return null;
        }

        /// <summary>
        /// Refresh quick slots display
        /// </summary>
        public void RefreshQuickSlots()
        {
            // TODO: Update quick slots from inventory system
            if (inventorySystem == null) return;

            for (int i = 0; i < quickSlots.Length; i++)
            {
                if (quickSlots[i] != null)
                {
                    // TODO: Get assigned item from inventory system and update slot
                    // var assignedSlot = inventorySystem.GetQuickSlot(i);
                    // quickSlots[i].AssignItem(assignedSlot);
                }
            }
        }
    }
}
