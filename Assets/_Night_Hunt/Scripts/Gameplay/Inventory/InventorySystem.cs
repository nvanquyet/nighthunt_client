using System.Collections.Generic;
using UnityEngine;
using NightHunt.Data;
using NightHunt.Gameplay.Character;

namespace NightHunt.Gameplay.Inventory
{
    /// <summary>
    /// Manages player inventory: items, weapons, weight system
    /// Grid-based inventory with weight capacity
    /// </summary>
    public class InventorySystem : MonoBehaviour
    {
        [Header("Inventory Settings")]
        [SerializeField] private int backpackSlots = 12;
        [SerializeField] private float baseWeightCapacity = 20f;
        [SerializeField] private int quickSlotCount = 3;

        private List<InventoryItem> items = new List<InventoryItem>();
        private InventoryItem[] quickSlots;
        private float currentWeight = 0f;
        private InventoryConfigData config;

        private CharacterStats characterStats;

        private void Awake()
        {
            characterStats = GetComponent<CharacterStats>();
            quickSlots = new InventoryItem[quickSlotCount];

            // Load config
            config = GameConfigLoader.Instance?.GetInventoryConfig();
            if (config != null)
            {
                backpackSlots = config.BackpackSlots;
                baseWeightCapacity = config.BaseWeightCapacity;
                quickSlotCount = config.QuickSlotCount;
            }
        }

        /// <summary>
        /// Add item to inventory
        /// </summary>
        public bool AddItem(string itemId, int quantity = 1)
        {
            var itemConfig = GameConfigLoader.Instance?.GetItemConfig(itemId);
            if (itemConfig == null)
            {
                Debug.LogWarning($"[InventorySystem] Item config not found: {itemId}");
                return false;
            }

            // Check weight
            float itemWeight = itemConfig.Weight * quantity;
            if (currentWeight + itemWeight > baseWeightCapacity)
            {
                Debug.LogWarning($"[InventorySystem] Not enough weight capacity!");
                return false;
            }

            // Check if item is stackable
            if (itemConfig.IsConsumable && itemConfig.MaxStack > 1)
            {
                var existingItem = items.Find(i => i.ItemId == itemId && i.Quantity < itemConfig.MaxStack);
                if (existingItem != null)
                {
                    // Add to existing stack
                    int canAdd = itemConfig.MaxStack - existingItem.Quantity;
                    int toAdd = Mathf.Min(quantity, canAdd);
                    existingItem.Quantity += toAdd;
                    currentWeight += itemConfig.Weight * toAdd;
                    return true;
                }
            }

            // Check if have space
            if (items.Count >= backpackSlots)
            {
                Debug.LogWarning($"[InventorySystem] Inventory full!");
                return false;
            }

            // Add new item
            var newItem = new InventoryItem
            {
                ItemId = itemId,
                Config = itemConfig,
                Quantity = quantity
            };

            items.Add(newItem);
            currentWeight += itemWeight;

            // Update character stats
            if (characterStats != null)
            {
                characterStats.SetWeight(currentWeight);
            }

            return true;
        }

        /// <summary>
        /// Remove item from inventory
        /// </summary>
        public bool RemoveItem(string itemId, int quantity = 1)
        {
            var item = items.Find(i => i.ItemId == itemId);
            if (item == null) return false;

            var itemConfig = item.Config;
            int toRemove = Mathf.Min(quantity, item.Quantity);
            item.Quantity -= toRemove;

            currentWeight -= itemConfig.Weight * toRemove;

            if (item.Quantity <= 0)
            {
                items.Remove(item);
            }

            // Update character stats
            if (characterStats != null)
            {
                characterStats.SetWeight(currentWeight);
            }

            return true;
        }

        /// <summary>
        /// Use item from inventory
        /// </summary>
        public bool UseItem(string itemId)
        {
            var item = items.Find(i => i.ItemId == itemId);
            if (item == null || item.Quantity <= 0) return false;

            var itemConfig = item.Config;

            // Use item based on type
            bool used = false;
            switch (itemConfig.EffectType)
            {
                case "HealHP":
                    if (characterStats != null)
                    {
                        characterStats.Heal(itemConfig.EffectValue);
                        used = true;
                    }
                    break;
                case "HealStaminaOverTime":
                    // Would need a coroutine for over-time effects
                    used = true;
                    break;
                case "SpeedBuff":
                    if (characterStats != null)
                    {
                        characterStats.ApplyStatusEffect("STATUS_SPEED", itemConfig.EffectDuration);
                        used = true;
                    }
                    break;
                // Add more effect types as needed
            }

            if (used && itemConfig.IsConsumable)
            {
                RemoveItem(itemId, 1);
            }

            return used;
        }

        /// <summary>
        /// Get current weight
        /// </summary>
        public float GetCurrentWeight() => currentWeight;

        /// <summary>
        /// Get weight capacity
        /// </summary>
        public float GetWeightCapacity() => baseWeightCapacity;

        /// <summary>
        /// Get weight percentage (0-1)
        /// </summary>
        public float GetWeightPercentage() => baseWeightCapacity > 0 ? currentWeight / baseWeightCapacity : 0f;

        /// <summary>
        /// Get all items
        /// </summary>
        public List<InventoryItem> GetItems() => items;

        /// <summary>
        /// Get item count
        /// </summary>
        public int GetItemCount() => items.Count;
    }

    /// <summary>
    /// Inventory item data
    /// </summary>
    [System.Serializable]
    public class InventoryItem
    {
        public string ItemId;
        public ItemConfigData Config;
        public int Quantity = 1;
    }
}

