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
                var movement = GetComponent<IMovementController>();
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

        /// <summary>
        /// Move item within grid
        /// </summary>
        public bool MoveItem(int fromX, int fromY, int toX, int toY)
        {
            if (!inventoryGrid.IsValidPosition(fromX, fromY) || !inventoryGrid.IsValidPosition(toX, toY))
                return false;

            var fromSlot = inventoryGrid.GetSlot(fromX, fromY);
            var toSlot = inventoryGrid.GetSlot(toX, toY);

            if (fromSlot == null || fromSlot.IsEmpty)
                return false;

            // If target is empty, just move
            if (toSlot == null || toSlot.IsEmpty)
            {
                inventoryGrid.RemoveItem(fromX, fromY);
                inventoryGrid.PlaceItem(toX, toY, fromSlot);
                SyncInventory();
                return true;
            }

            // If target has item, try to swap
            if (toSlot.Item.ItemId == fromSlot.Item.ItemId && toSlot.Item.IsConsumable && toSlot.Item.MaxStack > 1)
            {
                // Try to stack
                int canAdd = toSlot.Item.MaxStack - toSlot.Quantity;
                if (canAdd > 0)
                {
                    int toAdd = Mathf.Min(fromSlot.Quantity, canAdd);
                    toSlot.AddQuantity(toAdd);
                    fromSlot.RemoveQuantity(toAdd);
                    if (fromSlot.IsEmpty)
                    {
                        inventoryGrid.RemoveItem(fromX, fromY);
                    }
                    SyncInventory();
                    return true;
                }
            }

            // Swap items
            var tempSlot = fromSlot;
            inventoryGrid.RemoveItem(fromX, fromY);
            inventoryGrid.PlaceItem(fromX, fromY, toSlot);
            inventoryGrid.RemoveItem(toX, toY);
            inventoryGrid.PlaceItem(toX, toY, tempSlot);
            SyncInventory();
            return true;
        }

        /// <summary>
        /// Assign item to quick slot
        /// </summary>
        public bool AssignQuickSlot(int slotIndex, string itemId)
        {
            if (slotIndex < 0 || slotIndex >= quickSlots.Length)
                return false;

            // Find item in inventory
            var slot = FindSlotWithItem(itemId);
            if (slot == null || slot.IsEmpty)
                return false;

            // If quick slot already has this item, do nothing
            if (quickSlots[slotIndex].Item != null && quickSlots[slotIndex].Item.ItemId == itemId)
                return true;

            // Assign item to quick slot
            var newSlot = new InventorySlot();
            newSlot.SetItem(slot.Item, 1); // Assign 1 item to quick slot

            // Remove from inventory if not already in quick slot
            bool isInQuickSlot = false;
            foreach (var qs in quickSlots)
            {
                if (qs != null && !qs.IsEmpty && qs.Item.ItemId == itemId)
                {
                    isInQuickSlot = true;
                    break;
                }
            }

            if (!isInQuickSlot)
            {
                slot.RemoveQuantity(1);
                if (slot.IsEmpty)
                {
                    // Find and remove from grid
                    for (int x = 0; x < inventoryGrid.Width; x++)
                    {
                        for (int y = 0; y < inventoryGrid.Height; y++)
                        {
                            var gridSlot = inventoryGrid.GetSlot(x, y);
                            if (gridSlot == slot)
                            {
                                inventoryGrid.RemoveItem(x, y);
                                break;
                            }
                        }
                    }
                }
            }

            quickSlots[slotIndex] = newSlot;
            SyncInventory();
            return true;
        }

        /// <summary>
        /// Equip weapon to weapon slot
        /// </summary>
        public bool EquipWeapon(int slotIndex, string weaponId)
        {
            if (slotIndex < 0 || slotIndex >= 2) // Only 2 weapon slots
                return false;

            // Find weapon in inventory
            var slot = FindSlotWithItem(weaponId);
            if (slot == null || slot.IsEmpty)
                return false;

            // TODO: Check if item is actually a weapon
            // For now, just remove from inventory
            // In a full implementation, this would:
            // 1. Unequip current weapon (if any) and return to inventory
            // 2. Equip new weapon
            // 3. Update combat system

            RemoveItem(weaponId, 1);
            SyncInventory();
            return true;
        }

        /// <summary>
        /// Drop item from inventory
        /// </summary>
        public bool DropItem(string itemId, int quantity)
        {
            var slot = FindSlotWithItem(itemId);
            if (slot == null || slot.IsEmpty)
                return false;

            var itemConfig = slot.Item;
            int toDrop = Mathf.Min(quantity, slot.Quantity);

            // Remove from inventory
            if (slot.RemoveQuantity(toDrop))
            {
                currentWeight -= itemConfig.Weight * toDrop;
                UpdateWeight();

                // TODO: Spawn item in world at player position
                // This would require integration with item spawn system

                SyncInventory();
                return true;
            }

            return false;
        }
    }
}

