using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using System.Collections.Generic;
using System.Linq;

namespace NightHunt.Inventory.Domain.QuickSlot
{
    /// <summary>
    /// Manages quick slot items (Ctrl+1/2/3/4).
    /// </summary>
    public class QuickSlotManager : MonoBehaviour
    {
        [Header("Configuration")] [SerializeField]
        private QuickSlotConfig config;

        [Header("Debug")] [SerializeField] private bool enableDebugLogs = false;

        private ItemInstance[] quickSlots;

        #region Lifecycle

        void Awake()
        {
            if (config == null)
            {
                Debug.LogError("[QuickSlotManager] Config not assigned!");
                return;
            }

            quickSlots = new ItemInstance[config.SlotCount];

            if (enableDebugLogs)
                Debug.Log($"[QuickSlotManager] Initialized with {config.SlotCount} slots");
        }

        #endregion

        #region Public API

        /// <summary>
        /// Tries to add an item to a quick slot.
        /// </summary>
        public bool TryAddItem(ItemInstance item, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= quickSlots.Length)
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"[QuickSlotManager] Invalid slot index: {slotIndex}");
                return false;
            }

            // Validate item type
            if (item.Definition.ItemType != ItemType.Consumable &&
                item.Definition.ItemType != ItemType.Throwable)
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[QuickSlotManager] Only consumables/throwables allowed in quick slots");
                return false;
            }

            quickSlots[slotIndex] = item;

            if (enableDebugLogs)
                Debug.Log($"[QuickSlotManager] Added {item.Definition.ItemId} to slot {slotIndex}");

            return true;
        }

        /// <summary>
        /// Removes an item from a quick slot.
        /// </summary>
        public ItemInstance RemoveItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= quickSlots.Length)
                return null;

            var item = quickSlots[slotIndex];
            quickSlots[slotIndex] = null;

            if (enableDebugLogs && item != null)
                Debug.Log($"[QuickSlotManager] Removed {item.Definition.ItemId} from slot {slotIndex}");

            return item;
        }

        /// <summary>
        /// Gets item in a specific slot.
        /// </summary>
        public ItemInstance GetItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= quickSlots.Length)
                return null;

            return quickSlots[slotIndex];
        }

        /// <summary>
        /// Gets all items in quick slots.
        /// </summary>
        public List<ItemInstance> GetAllItems()
        {
            return quickSlots.Where(item => item != null).ToList();
        }

        /// <summary>
        /// Clears a specific slot.
        /// </summary>
        public void ClearSlot(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < quickSlots.Length)
            {
                quickSlots[slotIndex] = null;
            }
        }

        /// <summary>
        /// Clears all slots.
        /// </summary>
        public void ClearAll()
        {
            for (int i = 0; i < quickSlots.Length; i++)
            {
                quickSlots[i] = null;
            }

            if (enableDebugLogs)
                Debug.Log("[QuickSlotManager] Cleared all quick slots");
        }

        #endregion
    }
}