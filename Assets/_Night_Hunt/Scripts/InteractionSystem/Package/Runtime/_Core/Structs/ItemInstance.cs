using System;
using UnityEngine;

namespace NightHunt.InteractionSystem.Core.Structs
{
    /// <summary>
    /// Runtime instance of an item in inventory or container.
    /// </summary>
    [Serializable]
    public struct ItemInstance
    {
        /// <summary>
        /// Unique ID of this item instance.
        /// </summary>
        public string instanceId;

        /// <summary>
        /// Item data ID (references ItemDataBase).
        /// </summary>
        public string itemDataId;

        /// <summary>
        /// Quantity of this item.
        /// </summary>
        public int quantity;

        /// <summary>
        /// Current durability (0-100, -1 for non-durable items).
        /// </summary>
        public float durability;

        /// <summary>
        /// Custom data for this instance (JSON string).
        /// </summary>
        public string customData;

        /// <summary>
        /// Create a new item instance.
        /// </summary>
        public ItemInstance(string itemDataId, int quantity = 1, float durability = -1f)
        {
            this.instanceId = Guid.NewGuid().ToString();
            this.itemDataId = itemDataId;
            this.quantity = quantity;
            this.durability = durability;
            this.customData = string.Empty;
        }

        /// <summary>
        /// Check if this instance is valid.
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(instanceId) && !string.IsNullOrEmpty(itemDataId) && quantity > 0;
        }

        /// <summary>
        /// Check if this item is stackable.
        /// </summary>
        public bool IsStackable()
        {
            // Will be checked against ItemDataBase.MaxStack
            return true;
        }

        /// <summary>
        /// Create a copy of this instance with modified quantity.
        /// </summary>
        public ItemInstance WithQuantity(int newQuantity)
        {
            var copy = this;
            copy.quantity = newQuantity;
            return copy;
        }

        /// <summary>
        /// Create a copy of this instance with modified durability.
        /// </summary>
        public ItemInstance WithDurability(float newDurability)
        {
            var copy = this;
            copy.durability = newDurability;
            return copy;
        }
    }
}
