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

        private InventoryGrid inventoryGrid;
        private InventorySlot[] quickSlots;
        private float currentWeight = 0f;
        private InventoryConfigData config;
        private InventorySync inventorySync;

        private CharacterStats characterStats;

        private void Awake()
        {
            characterStats = GetComponent<CharacterStats>();
            inventorySync = GetComponent<InventorySync>();
            if (inventorySync == null)
            {
                inventorySync = gameObject.AddComponent<InventorySync>();
            }

            // Load config
            config = GameConfigLoader.Instance?.GetInventoryConfig();
            if (config != null)
            {
                backpackSlots = config.BackpackSlots;
                baseWeightCapacity = config.BaseWeightCapacity;
                quickSlotCount = config.QuickSlotCount;
            }

            // Initialize grid (default 4x3 grid for 12 slots)
            int gridWidth = 4;
            int gridHeight = Mathf.CeilToInt(backpackSlots / (float)gridWidth);
            inventoryGrid = new InventoryGrid(gridWidth, gridHeight);
            quickSlots = new InventorySlot[quickSlotCount];
            
            for (int i = 0; i < quickSlotCount; i++)
            {
                quickSlots[i] = new InventorySlot();
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

            // Check weight using WeightCalculator
            float itemWeight = itemConfig.Weight * quantity;
            if (!WeightCalculator.CanAddItem(currentWeight, itemWeight, baseWeightCapacity))
            {
                Debug.LogWarning($"[InventorySystem] Not enough weight capacity!");
                return false;
            }

            // Check if item is stackable and find existing slot
            if (itemConfig.IsConsumable && itemConfig.MaxStack > 1)
            {
                var existingSlot = FindSlotWithItem(itemId);
                if (existingSlot != null && existingSlot.Quantity < itemConfig.MaxStack)
                {
                    // Add to existing stack
                    int canAdd = itemConfig.MaxStack - existingSlot.Quantity;
                    int toAdd = Mathf.Min(quantity, canAdd);
                    existingSlot.AddQuantity(toAdd);
                    currentWeight += itemConfig.Weight * toAdd;
                    UpdateWeight();
                    SyncInventory();
                    return true;
                }
            }

            // Find empty slot
            if (!inventoryGrid.FindEmptySlot(out int x, out int y))
            {
                Debug.LogWarning($"[InventorySystem] Inventory full!");
                return false;
            }

            // Add new item to grid
            var newSlot = new InventorySlot();
            newSlot.SetItem(itemConfig, quantity);
            inventoryGrid.PlaceItem(x, y, newSlot);
            currentWeight += itemWeight;

            UpdateWeight();
            SyncInventory();
            return true;
        }

        /// <summary>
        /// Remove item from inventory
        /// </summary>
        public bool RemoveItem(string itemId, int quantity = 1)
        {
            var slot = FindSlotWithItem(itemId);
            if (slot == null || slot.IsEmpty) return false;

            var itemConfig = slot.Item;
            int toRemove = Mathf.Min(quantity, slot.Quantity);
            
            if (slot.RemoveQuantity(toRemove))
            {
                currentWeight -= itemConfig.Weight * toRemove;
                UpdateWeight();
                SyncInventory();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Find slot containing item
        /// </summary>
        private InventorySlot FindSlotWithItem(string itemId)
        {
            // Check grid
            for (int x = 0; x < inventoryGrid.Width; x++)
            {
                for (int y = 0; y < inventoryGrid.Height; y++)
                {
                    var slot = inventoryGrid.GetSlot(x, y);
                    if (slot != null && !slot.IsEmpty && slot.Item.ItemId == itemId)
                    {
                        return slot;
                    }
                }
            }

            // Check quick slots
            foreach (var slot in quickSlots)
            {
                if (slot != null && !slot.IsEmpty && slot.Item.ItemId == itemId)
                {
                    return slot;
                }
            }

            return null;
        }

        /// <summary>
        /// Update weight in character stats
        /// </summary>
        private void UpdateWeight()
        {
            if (characterStats != null)
            {
                characterStats.SetWeight(currentWeight);
                
                // Update weight penalty in movement
                var movement = GetComponent<CharacterMovement>();
                if (movement != null)
                {
                    float penalty = 1f - WeightCalculator.GetWeightPenaltyMultiplier(currentWeight, baseWeightCapacity);
                    movement.SetWeightPenalty(penalty);
                }
            }
        }

        /// <summary>
        /// Sync inventory to network
        /// </summary>
        private void SyncInventory()
        {
            if (inventorySync != null)
            {
                var allSlots = inventoryGrid.GetAllItems();
                inventorySync.SyncInventory(allSlots);
            }
        }

        /// <summary>
        /// Apply inventory data from network
        /// </summary>
        public void ApplyInventoryData(List<InventorySlot> slots)
        {
            // TODO: Apply inventory data from network sync
            // This would restore inventory state after network sync
        }

        /// <summary>
        /// Use item from inventory
        /// </summary>
        public bool UseItem(string itemId)
        {
            var slot = FindSlotWithItem(itemId);
            if (slot == null || slot.IsEmpty) return false;

            var itemConfig = slot.Item;

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
        /// Get all items from grid
        /// </summary>
        public List<InventorySlot> GetItems() => inventoryGrid.GetAllItems();

        /// <summary>
        /// Get item count
        /// </summary>
        public int GetItemCount() => inventoryGrid.GetAllItems().Count;

        /// <summary>
        /// Get inventory grid
        /// </summary>
        public InventoryGrid GetGrid() => inventoryGrid;

        /// <summary>
        /// Get quick slots
        /// </summary>
        public InventorySlot[] GetQuickSlots() => quickSlots;
    }
}

