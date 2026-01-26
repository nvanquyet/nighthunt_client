using UnityEngine;
using NightHunt.InteractionSystem.Core.Structs;

namespace NightHunt.InteractionSystem.Core.Abstractions
{
    /// <summary>
    /// Base class for all item data (ScriptableObject).
    /// </summary>
    public abstract class ItemDataBase : ScriptableObject
    {
        [Header("Basic Info")]
        [SerializeField] protected string itemId;
        [SerializeField] protected string displayName;
        [SerializeField] protected string description;
        [SerializeField] protected Sprite icon;

        [Header("Item Properties")]
        [SerializeField] protected float weight = 1f;
        [SerializeField] protected int maxStack = 1;
        [SerializeField] protected bool isConsumable = false;
        [SerializeField] protected bool isStackable = false;
        [SerializeField] protected ItemCategory category = ItemCategory.Misc;

        [Header("Durability")]
        [SerializeField] protected bool hasDurability = false;
        [SerializeField] protected float maxDurability = 100f;

        // Properties
        public string ItemId => itemId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public float Weight => weight;
        public int MaxStack => maxStack;
        public bool IsConsumable => isConsumable;
        public bool IsStackable => isStackable;
        public ItemCategory Category => category;
        public bool HasDurability => hasDurability;
        public float MaxDurability => maxDurability;

        protected virtual void OnEnable()
        {
            // Base implementation - can be overridden
        }

        /// <summary>
        /// Create an item instance from this data.
        /// </summary>
        public virtual ItemInstance CreateInstance(int quantity = 1)
        {
            float durability = hasDurability ? maxDurability : -1f;
            return new ItemInstance(itemId, quantity, durability);
        }

        /// <summary>
        /// Check if this item can stack with another item.
        /// </summary>
        public virtual bool CanStackWith(ItemInstance other)
        {
            if (!isStackable)
                return false;

            return other.itemDataId == itemId;
        }

        /// <summary>
        /// Get the total weight for a quantity of this item.
        /// </summary>
        public virtual float GetTotalWeight(int quantity)
        {
            return weight * quantity;
        }
    }

    /// <summary>
    /// Categories of items.
    /// </summary>
    public enum ItemCategory
    {
        Weapon,
        Armor,
        Helmet,
        Backpack,
        Ammo,
        Consumable,
        Attachment,
        Misc
    }
}
