using System.Collections.Generic;
using UnityEngine;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Events;

namespace NightHunt.InteractionSystem.Inventory
{
    /// <summary>
    /// List-based inventory component (simple slot-based).
    /// </summary>
    public class ListInventoryComponent : InventoryComponentBase
    {
        private Dictionary<string, ItemDataBase> itemDataCache = new Dictionary<string, ItemDataBase>();

        /// <summary>
        /// Add an item to the inventory.
        /// </summary>
        public override bool AddItem(ItemInstance item)
        {
            if (!CanAddItem(item))
                return false;

            ItemDataBase itemData = GetItemData(item.itemDataId);
            if (itemData == null)
                return false;

            // Try to stack with existing item
            if (itemData.IsStackable)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    var existing = items[i];
                    if (existing.itemDataId == item.itemDataId)
                    {
                        int canAdd = itemData.MaxStack - existing.quantity;
                        if (canAdd > 0)
                        {
                            int toAdd = Mathf.Min(item.quantity, canAdd);
                            items[i] = existing.WithQuantity(existing.quantity + toAdd);

                            if (toAdd < item.quantity)
                            {
                                // Still have items left, create new slot
                                var remaining = item.WithQuantity(item.quantity - toAdd);
                                items.Add(remaining);
                                currentWeight += itemData.GetTotalWeight(remaining.quantity);
                            }
                            else
                            {
                                currentWeight += itemData.GetTotalWeight(toAdd);
                            }

                            UpdateWeight();
                            InventoryEvents.InvokeItemQuantityChanged(items[i], items[i].quantity);
                            return true;
                        }
                    }
                }
            }

            // Add new item
            items.Add(item);
            currentWeight += itemData.GetTotalWeight(item.quantity);
            UpdateWeight();
            InventoryEvents.InvokeItemAdded(item);
            return true;
        }

        /// <summary>
        /// Remove an item from the inventory.
        /// </summary>
        public override bool RemoveItem(string itemId, int quantity = 1)
        {
            ItemDataBase itemData = GetItemData(itemId);
            if (itemData == null)
                return false;

            int remaining = quantity;

            for (int i = items.Count - 1; i >= 0; i--)
            {
                var item = items[i];
                if (item.itemDataId == itemId)
                {
                    int toRemove = Mathf.Min(remaining, item.quantity);
                    int newQuantity = item.quantity - toRemove;
                    remaining -= toRemove;

                    if (newQuantity <= 0)
                    {
                        items.RemoveAt(i);
                        InventoryEvents.InvokeItemRemoved(item, toRemove);
                    }
                    else
                    {
                        var updatedItem = item.WithQuantity(newQuantity);
                        items[i] = updatedItem;
                        InventoryEvents.InvokeItemQuantityChanged(updatedItem, newQuantity);
                    }

                    currentWeight -= itemData.GetTotalWeight(toRemove);

                    if (remaining <= 0)
                    {
                        UpdateWeight();
                        return true;
                    }
                }
            }

            UpdateWeight();
            return remaining < quantity; // Return true if at least some items were removed
        }

        /// <summary>
        /// Check if an item can be added.
        /// </summary>
        public override bool CanAddItem(ItemInstance item)
        {
            ItemDataBase itemData = GetItemData(item.itemDataId);
            if (itemData == null)
                return false;

            return HasSpace(item, itemData);
        }

        /// <summary>
        /// Get item data (with caching).
        /// </summary>
        private ItemDataBase GetItemData(string itemId)
        {
            if (itemDataCache.ContainsKey(itemId))
                return itemDataCache[itemId];

            // Load from resources or database
            // TODO: Implement proper item data loading
            return null;
        }
    }
}
