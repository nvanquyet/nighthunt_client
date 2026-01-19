using UnityEngine;
using NightHunt.Data;

namespace NightHunt.Gameplay.Inventory
{
    /// <summary>
    /// Individual inventory slot with item reference
    /// </summary>
    [System.Serializable]
    public class InventorySlot
    {
        public ItemConfigData Item { get; set; }
        public int Quantity { get; set; }
        public bool IsEmpty => Item == null || Quantity <= 0;

        public InventorySlot()
        {
            Item = null;
            Quantity = 0;
        }

        /// <summary>
        /// Set item in slot
        /// </summary>
        public void SetItem(ItemConfigData item, int quantity = 1)
        {
            Item = item;
            Quantity = quantity;
        }

        /// <summary>
        /// Clear slot
        /// </summary>
        public void Clear()
        {
            Item = null;
            Quantity = 0;
        }

        /// <summary>
        /// Add quantity
        /// </summary>
        public void AddQuantity(int amount)
        {
            Quantity += amount;
        }

        /// <summary>
        /// Remove quantity
        /// </summary>
        public bool RemoveQuantity(int amount)
        {
            if (Quantity < amount) return false;
            
            Quantity -= amount;
            if (Quantity <= 0)
            {
                Clear();
            }
            return true;
        }
    }
}

