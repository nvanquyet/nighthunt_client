using System;
using UnityEngine;
using NightHunt.InteractionSystem.Core.Abstractions;

namespace NightHunt.Gameplay.Inventory
{
    /// <summary>
    /// Lightweight wrapper around an inventory item for gameplay/UI.
    /// Uses ItemDataBase directly from ItemDataRegistry (no conversion needed).
    /// Actual authoritative data lives in the InteractionSystem package (ItemInstance).
    /// </summary>
    [Serializable]
    public class InventoryItem
    {
        public string ItemId;
        public string InstanceId;
        public ItemDataBase ItemData; // Direct reference to ItemDataBase from ItemDataRegistry
        public int Quantity;

        public InventoryItem() { }

        public InventoryItem(string itemId, int quantity, string instanceId = null)
        {
            ItemId = itemId;
            Quantity = quantity;
            InstanceId = instanceId;
            // Load ItemDataBase from ItemDataRegistry
            var registry = ItemDataRegistry.Load();
            if (registry != null)
            {
                ItemData = registry.GetById(itemId);
            }
        }
    }

    [Serializable]
    public class InventorySlot
    {
        [SerializeField] private InventoryItem item;

        public InventoryItem Item => item;
        public int Quantity => item != null ? item.Quantity : 0;
        public bool IsEmpty => item == null || item.Quantity <= 0;

        /// <summary>
        /// Set item using ItemDataBase directly (no conversion needed)
        /// </summary>
        public void SetItem(ItemDataBase itemData, int quantity, string instanceId = null)
        {
            if (itemData == null || quantity <= 0)
            {
                item = null;
                return;
            }

            item = new InventoryItem(itemData.ItemId, quantity, instanceId)
            {
                ItemData = itemData
            };
        }

        /// <summary>
        /// Set item using itemId (loads from ItemDataRegistry)
        /// </summary>
        public void SetItem(string itemId, int quantity)
        {
            if (string.IsNullOrEmpty(itemId) || quantity <= 0)
            {
                item = null;
                return;
            }

            var registry = ItemDataRegistry.Load();
            if (registry != null)
            {
                var itemData = registry.GetById(itemId);
                if (itemData != null)
                {
                    item = new InventoryItem(itemId, quantity)
                    {
                        ItemData = itemData
                    };
                }
                else
                {
                    Debug.LogWarning($"[InventorySlot] Item '{itemId}' not found in ItemDataRegistry");
                    item = null;
                }
            }
            else
            {
                Debug.LogWarning("[InventorySlot] ItemDataRegistry is null - cannot load item");
                item = null;
            }
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

