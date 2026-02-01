using System.Collections.Generic;
using UnityEngine;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Events;

namespace NightHunt.InteractionSystem.Core.Abstractions
{
    /// <summary>
    /// Base class for inventory components.
    /// </summary>
    public abstract class InventoryComponentBase : MonoBehaviour
    {
        [Header("Inventory Settings")]
        [SerializeField] protected float maxWeight = 20f;
        [SerializeField] protected int maxSlots = 12;

        protected float currentWeight = 0f;
        protected List<ItemInstance> items = new List<ItemInstance>();

        protected virtual void Awake()
        {
            // Base implementation - can be overridden
        }

        // Properties
        public float MaxWeight => maxWeight;
        public int MaxSlots => maxSlots;
        public float CurrentWeight => currentWeight;
        public float WeightPercentage => maxWeight > 0 ? currentWeight / maxWeight : 0f;
        public int ItemCount => items.Count;
        public IReadOnlyList<ItemInstance> Items => items;

        /// <summary>
        /// Set maximum weight capacity.
        /// NOTE: Gameplay systems should NOT call this directly. Capacity is owned by CharacterStats
        /// and synchronized via InventoryService to keep a single source of truth.
        /// </summary>
        public void SetMaxWeight(float newMaxWeight)
        {
            if (newMaxWeight < 0f)
                newMaxWeight = 0f;
            
            maxWeight = newMaxWeight;
            UpdateWeight();
        }

        /// <summary>
        /// Add an item to the inventory.
        /// </summary>
        public abstract bool AddItem(ItemInstance item);

        /// <summary>
        /// Remove an item from the inventory.
        /// </summary>
        public abstract bool RemoveItem(string itemId, int quantity = 1);

        /// <summary>
        /// Check if an item can be added.
        /// </summary>
        public abstract bool CanAddItem(ItemInstance item);

        /// <summary>
        /// Find an item by ID.
        /// </summary>
        public ItemInstance? FindItem(string itemId)
        {
            foreach (var item in items)
            {
                if (item.itemDataId == itemId)
                    return item;
            }
            return null;
        }

        /// <summary>
        /// Get the total quantity of an item.
        /// </summary>
        public int GetItemQuantity(string itemId)
        {
            int total = 0;
            foreach (var item in items)
            {
                if (item.itemDataId == itemId)
                    total += item.quantity;
            }
            return total;
        }

        /// <summary>
        /// Check if the inventory has space for an item.
        /// </summary>
        protected bool HasSpace(ItemInstance item, ItemDataBase itemData)
        {
            // Check weight
            float itemWeight = itemData.GetTotalWeight(item.quantity);
            if (currentWeight + itemWeight > maxWeight)
                return false;

            // Check slots (will be overridden by grid/list implementations)
            if (items.Count >= maxSlots)
            {
                // Check if item can stack
                if (itemData.IsStackable)
                {
                    var existing = FindItem(item.itemDataId);
                    if (existing.HasValue)
                        return true; // Can stack
                }
                return false;
            }

            return true;
        }

        /// <summary>
        /// Update the current weight and invoke events.
        /// </summary>
        protected void UpdateWeight()
        {
            // Weight calculation will be done by implementations
            // This method should be called after weight changes
            
            // Invoke weight changed event
            InventoryEvents.InvokeWeightChanged(currentWeight, maxWeight);
            
            // Update slot count
            int usedSlots = items.Count;
            InventoryEvents.InvokeSlotCountChanged(usedSlots, maxSlots);
        }

        /// <summary>
        /// Clear all items from inventory.
        /// </summary>
        public virtual void Clear()
        {
            items.Clear();
            currentWeight = 0f;
        }
    }
}
