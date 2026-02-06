using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Utilities;
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

        [Header("References")]
        [SerializeField] private Networking.QuickSlotNetworkSync networkSync;

        [Header("Debug")] [SerializeField] private bool enableDebugLogs = false;

        private ItemInstance[] quickSlots;

        #region Lifecycle

        void Awake()
        {
            if (config == null)
            {
                InventoryLogger.LogError("QuickSlotManager", "Config not assigned!");
                return;
            }

            quickSlots = new ItemInstance[config.SlotCount];

            InventoryLogger.Log("QuickSlotManager", $"Initialized with {config.SlotCount} slots", enableDebugLogs);
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
                InventoryLogger.LogWarning("QuickSlotManager", $"Invalid slot index: {slotIndex}", enableDebugLogs);
                return false;
            }

            // Validate item type
            if (item.Definition.ItemType != ItemType.Consumable &&
                item.Definition.ItemType != ItemType.Throwable)
            {
                InventoryLogger.LogWarning("QuickSlotManager", "Only consumables/throwables allowed in quick slots", enableDebugLogs);
                return false;
            }

            quickSlots[slotIndex] = item;

            // Fire event for UI update
            QuickSlotEvents.InvokeQuickSlotChanged(slotIndex, item);

            InventoryLogger.Log("QuickSlotManager", $"Added {item.Definition.ItemId} to slot {slotIndex}", enableDebugLogs);

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

            // Fire event for UI update
            QuickSlotEvents.InvokeQuickSlotChanged(slotIndex, null);

            if (item != null)
            {
                InventoryLogger.Log("QuickSlotManager", $"Removed {item.Definition.ItemId} from slot {slotIndex}", enableDebugLogs);
            }

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
                QuickSlotEvents.InvokeQuickSlotChanged(slotIndex, null);
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
                QuickSlotEvents.InvokeQuickSlotChanged(i, null);
            }

            InventoryLogger.Log("QuickSlotManager", "Cleared all quick slots", enableDebugLogs);
        }

        #endregion
    }
}