using System.Collections.Generic;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Data;

namespace NightHunt.GameplaySystems.Core.Configs
{
    /// <summary>
    /// Spawn mode for loot tables
    /// </summary>
    public enum SpawnTableMode
    {
        RandomOnly,        // Chỉ random từ pool
        FixedOnly,         // Chỉ danh sách cố định
        FixedPlusRandom   // Fixed + thêm random
    }

    /// <summary>
    /// Result from SpawnTable.Roll() - contains item definition and quantity
    /// </summary>
    [System.Serializable]
    public struct SpawnResult
    {
        public ItemDefinition ItemDef;
        public int Quantity;

        public SpawnResult(ItemDefinition itemDef, int quantity)
        {
            ItemDef = itemDef;
            Quantity = quantity;
        }
    }

    /// <summary>
    /// Configuration for spawning items in the world
    /// Supports 3 modes: RandomOnly, FixedOnly, FixedPlusRandom
    /// Used by SpawnPoints, Containers, and other loot sources
    /// </summary>
    [CreateAssetMenu(fileName = "SpawnTable", menuName = "GameplaySystems/Config/Spawn Table")]
    public class SpawnTable : ScriptableObject
    {
        [Header("Spawn Mode")]
        [Tooltip("RandomOnly: Chỉ random từ pool\nFixedOnly: Chỉ danh sách cố định\nFixedPlusRandom: Fixed + thêm random")]
        public SpawnTableMode Mode = SpawnTableMode.FixedPlusRandom;

        [Header("Fixed Items (Guaranteed)")]
        [Tooltip("Items luôn spawn (không random). Dùng cho điểm có đồ hiếm cố định.")]
        public List<LootItemEntry> FixedEntries = new List<LootItemEntry>();

        [Header("Random Pool")]
        [Tooltip("Items random từ pool này")]
        public List<LootItemEntry> RandomEntries = new List<LootItemEntry>();

        [Header("Random Settings")]
        [Tooltip("Số lượng item random tối thiểu (khi Mode = RandomOnly hoặc FixedPlusRandom)")]
        [Min(0)]
        public int MinRandomCount = 0;

        [Tooltip("Số lượng item random tối đa")]
        [Min(0)]
        public int MaxRandomCount = 2;

        [Header("Roll Settings")]
        [Tooltip("Container: roll khi mở lần đầu. SpawnPoint: roll ngay khi spawn")]
        public bool RollOnOpen = true;

        [Tooltip("Container: spawn ra world thay vì giữ trong storage")]
        public bool DropToWorldOnOpen = false;

        [Header("Total Items")]
        [Tooltip("Số lượng item tối thiểu tổng thể")]
        [Min(0)]
        public int MinTotalItems = 1;

        [Tooltip("Số lượng item tối đa tổng thể")]
        [Min(0)]
        public int MaxTotalItems = 3;

        #region Roll Logic

        /// <summary>
        /// Roll items from this spawn table
        /// Returns list of SpawnResult (ItemDefinition + quantity)
        /// Does NOT spawn prefabs - caller is responsible for spawning
        /// </summary>
        /// <param name="seed">Optional seed for deterministic rolls</param>
        /// <returns>List of items to spawn</returns>
        public List<SpawnResult> Roll(int? seed = null)
        {
            if (seed.HasValue)
            {
                Random.InitState(seed.Value);
            }

            var results = new List<SpawnResult>();

            // 1. Fixed entries (guaranteed)
            if (Mode == SpawnTableMode.FixedOnly || Mode == SpawnTableMode.FixedPlusRandom)
            {
                foreach (var entry in FixedEntries)
                {
                    if (entry.Item == null) continue;

                    int qty = Random.Range(entry.MinQuantity, entry.MaxQuantity + 1);
                    if (qty > 0)
                    {
                        results.Add(new SpawnResult(entry.Item, qty));
                    }
                }
            }

            // 2. Random entries
            if (Mode == SpawnTableMode.RandomOnly || Mode == SpawnTableMode.FixedPlusRandom)
            {
                int randomCount = Random.Range(MinRandomCount, MaxRandomCount + 1);

                for (int i = 0; i < randomCount; i++)
                {
                    var entry = RollRandomEntry(RandomEntries);
                    if (entry.Item == null) continue;

                    int qty = Random.Range(entry.MinQuantity, entry.MaxQuantity + 1);
                    if (qty > 0)
                    {
                        results.Add(new SpawnResult(entry.Item, qty));
                    }
                }
            }

            // 3. Clamp total items (optional)
            if (results.Count > MaxTotalItems)
            {
                results = results.GetRange(0, MaxTotalItems);
            }
            else if (results.Count < MinTotalItems)
            {
                // Try to add more random items if below minimum
                int needed = MinTotalItems - results.Count;
                for (int i = 0; i < needed && RandomEntries.Count > 0; i++)
                {
                    var entry = RollRandomEntry(RandomEntries);
                    if (entry.Item == null) continue;

                    int qty = Random.Range(entry.MinQuantity, entry.MaxQuantity + 1);
                    if (qty > 0)
                    {
                        results.Add(new SpawnResult(entry.Item, qty));
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Roll a random entry from pool using weighted chance
        /// </summary>
        private LootItemEntry RollRandomEntry(List<LootItemEntry> pool)
        {
            if (pool == null || pool.Count == 0)
                return default;

            // Calculate total weight
            float totalWeight = 0f;
            foreach (var e in pool)
            {
                totalWeight += e.Chance;
            }

            if (totalWeight <= 0f)
            {
                // All entries have 0 chance, return random
                return pool[Random.Range(0, pool.Count)];
            }

            // Roll weighted
            float roll = Random.value * totalWeight;
            float current = 0f;

            foreach (var entry in pool)
            {
                current += entry.Chance;
                if (roll <= current)
                {
                    return entry;
                }
            }

            // Fallback to last entry
            return pool[pool.Count - 1];
        }

        #endregion

        #region Validation

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Ensure min <= max
            if (MinRandomCount > MaxRandomCount)
                MaxRandomCount = MinRandomCount;

            if (MinTotalItems > MaxTotalItems)
                MaxTotalItems = MinTotalItems;

            // Validate entries
            if (FixedEntries != null)
            {
                foreach (var entry in FixedEntries)
                {
                    if (entry.Item == null)
                        Debug.LogWarning($"[SpawnTable] Fixed entry has null Item in {name}");
                    
                    if (entry.MinQuantity > entry.MaxQuantity)
                        entry.MaxQuantity = entry.MinQuantity;
                }
            }

            if (RandomEntries != null)
            {
                foreach (var entry in RandomEntries)
                {
                    if (entry.Item == null)
                        Debug.LogWarning($"[SpawnTable] Random entry has null Item in {name}");
                    
                    if (entry.MinQuantity > entry.MaxQuantity)
                        entry.MaxQuantity = entry.MinQuantity;
                }
            }
        }
#endif

        #endregion
    }

    /// <summary>
    /// Entry in spawn table - defines one item that can spawn
    /// </summary>
    [System.Serializable]
    public class LootItemEntry
    {
        [Tooltip("Item definition to spawn")]
        public ItemDefinition Item;

        [Tooltip("Minimum quantity")]
        [Min(1)]
        public int MinQuantity = 1;

        [Tooltip("Maximum quantity")]
        [Min(1)]
        public int MaxQuantity = 1;

        [Tooltip("Chance/weight for random rolls (0-1). Higher = more likely")]
        [Range(0f, 1f)]
        public float Chance = 1f;
    }
}
