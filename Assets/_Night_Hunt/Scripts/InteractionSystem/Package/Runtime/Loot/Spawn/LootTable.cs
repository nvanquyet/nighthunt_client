using UnityEngine;
using System.Collections.Generic;
using NightHunt.InteractionSystem.Loot.Definitions;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Core.Structs;

namespace NightHunt.InteractionSystem.Loot.Spawn
{
    /// <summary>
    /// Rarity levels for loot items.
    /// </summary>
    public enum LootRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    /// <summary>
    /// Entry in a loot table defining what can spawn.
    /// </summary>
    [System.Serializable]
    public class LootTableEntry
    {
        [Header("Item")]
        [Tooltip("The loot definition (contains item data and world config)")]
        public LootItemDefinition loot;

        [Header("Spawn Probability")]
        [Range(0f, 100f)]
        [Tooltip("Weight for random selection (0-100). Higher = more likely to spawn.")]
        public float weight = 10f;

        [Header("Quantity")]
        [Tooltip("Minimum quantity when this item spawns")]
        public int minQuantity = 1;

        [Tooltip("Maximum quantity when this item spawns")]
        public int maxQuantity = 1;

        [Header("Spawn Rules")]
        [Tooltip("Can this item spawn multiple times in the same spawn cycle?")]
        public bool canSpawnMultiple = false;

        [Header("Rarity")]
        [Tooltip("Rarity level of this item (for visual/UI purposes)")]
        public LootRarity rarity = LootRarity.Common;
    }

    /// <summary>
    /// ScriptableObject that defines a loot table with weighted entries.
    /// Can be reused by multiple LootSpawnPoints or LootContainers.
    /// </summary>
    [CreateAssetMenu(fileName = "LootTable", menuName = "NightHunt/Interaction System/Loot Table", order = 1)]
    public class LootTable : ScriptableObject
    {
        [Header("Loot Entries")]
        [Tooltip("List of items that can spawn from this table")]
        public LootTableEntry[] entries = new LootTableEntry[0];

        [Header("Spawn Count")]
        [Tooltip("Minimum number of items to spawn per cycle")]
        public int minItemsPerSpawn = 1;

        [Tooltip("Maximum number of items to spawn per cycle")]
        public int maxItemsPerSpawn = 3;

        /// <summary>
        /// Generate a list of items based on the loot table (weighted random).
        /// </summary>
        /// <returns>List of ItemInstance with random quantities</returns>
        public List<ItemInstance> GenerateLoot()
        {
            return GenerateLoot(0, 0);
        }

        /// <summary>
        /// Generate a list of items based on the loot table (weighted random) with override min/max items.
        /// </summary>
        /// <param name="overrideMinItems">Override minItemsPerSpawn (0 = use default)</param>
        /// <param name="overrideMaxItems">Override maxItemsPerSpawn (0 = use default)</param>
        /// <returns>List of ItemInstance with random quantities</returns>
        public List<ItemInstance> GenerateLoot(int overrideMinItems, int overrideMaxItems)
        {
            List<ItemInstance> generatedItems = new List<ItemInstance>();

            if (entries == null || entries.Length == 0)
            {
                Debug.LogWarning($"[LootTable] {name} has no entries!");
                return generatedItems;
            }

            // Calculate total weight
            float totalWeight = 0f;
            foreach (var entry in entries)
            {
                if (entry.loot != null && entry.loot.ItemData != null)
                    totalWeight += entry.weight;
            }

            if (totalWeight <= 0f)
            {
                Debug.LogWarning($"[LootTable] {name} total weight is 0!");
                return generatedItems;
            }

            // Determine how many items to spawn (use override if provided)
            int minItems = overrideMinItems > 0 ? overrideMinItems : minItemsPerSpawn;
            int maxItems = overrideMaxItems > 0 ? overrideMaxItems : maxItemsPerSpawn;
            int itemsToSpawn = Random.Range(minItems, maxItems + 1);
            HashSet<LootTableEntry> usedEntries = new HashSet<LootTableEntry>();

            for (int i = 0; i < itemsToSpawn; i++)
            {
                // Select random entry based on weight
                float randomValue = Random.Range(0f, totalWeight);
                float currentWeight = 0f;
                LootTableEntry selectedEntry = null;

                foreach (var entry in entries)
                {
                    if (entry.loot == null || entry.loot.ItemData == null)
                        continue;

                    // Skip if already used and can't spawn multiple
                    if (!entry.canSpawnMultiple && usedEntries.Contains(entry))
                        continue;

                    currentWeight += entry.weight;
                    if (randomValue <= currentWeight)
                    {
                        selectedEntry = entry;
                        break;
                    }
                }

                if (selectedEntry != null && selectedEntry.loot != null && selectedEntry.loot.ItemData != null)
                {
                    int quantity = Random.Range(selectedEntry.minQuantity, selectedEntry.maxQuantity + 1);
                    ItemInstance itemInstance = new ItemInstance
                    {
                        itemDataId = selectedEntry.loot.ItemData.ItemId,
                        quantity = quantity
                    };
                    generatedItems.Add(itemInstance);

                    if (!selectedEntry.canSpawnMultiple)
                    {
                        usedEntries.Add(selectedEntry);
                    }
                }
            }

            return generatedItems;
        }

        /// <summary>
        /// Get total weight of all valid entries.
        /// </summary>
        public float GetTotalWeight()
        {
            float total = 0f;
            foreach (var entry in entries)
            {
                if (entry.loot != null && entry.loot.ItemData != null)
                    total += entry.weight;
            }
            return total;
        }

        /// <summary>
        /// Get number of valid entries.
        /// </summary>
        public int GetValidEntryCount()
        {
            int count = 0;
            foreach (var entry in entries)
            {
                if (entry.loot != null && entry.loot.ItemData != null)
                    count++;
            }
            return count;
        }
    }
}
