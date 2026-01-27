using System;
using UnityEngine;
using NightHunt.Data;

namespace NightHunt.Gameplay.Inventory
{
    /// <summary>
    /// Lightweight wrapper around an inventory item for gameplay/UI.
    /// This is a replacement for the old InventorySlot type, backed by ItemConfigData.
    /// Actual authoritative data lives in the InteractionSystem package (ItemInstance).
    /// </summary>
    [Serializable]
    public class InventoryItem
    {
        public string ItemId;
        public ItemConfigData Config;
        public int Quantity;

        public InventoryItem() { }

        public InventoryItem(string itemId, int quantity)
        {
            ItemId = itemId;
            Quantity = quantity;
            Config = GameConfigLoader.Instance?.GetItemConfig(itemId);
        }
    }

    [Serializable]
    public class InventorySlot
    {
        [SerializeField] private InventoryItem item;

        public InventoryItem Item => item;
        public int Quantity => item != null ? item.Quantity : 0;
        public bool IsEmpty => item == null || item.Quantity <= 0;

        public void SetItem(ItemConfigData config, int quantity)
        {
            if (config == null || quantity <= 0)
            {
                item = null;
                return;
            }

            item = new InventoryItem(config.ItemId, quantity)
            {
                Config = config
            };
        }

        public void SetItem(InventoryItem newItem, int quantity)
        {
            if (newItem == null || quantity <= 0)
        {
                item = null;
                return;
        }

            newItem.Quantity = quantity;
            item = newItem;
        }

        public void AddQuantity(int amount)
        {
            if (item == null) return;
            item.Quantity += amount;
            if (item.Quantity <= 0)
            {
                item = null;
            }
        }

        /// <summary>
        /// Remove quantity; returns true if slot became empty.
        /// </summary>
        public bool RemoveQuantity(int amount)
        {
            if (item == null) return false;
            item.Quantity -= amount;
            if (item.Quantity <= 0)
            {
                item = null;
            return true;
            }
            return false;
        }
    }
}

