using System.Collections.Generic;
using NightHunt.Data;
using UnityEngine;

namespace NightHunt.Gameplay.Boss
{
    /// <summary>
    /// Weighted random loot roller used by <see cref="BossChest"/>.
    ///
    /// Supports fixed drops (always included) and weighted random drops.
    /// Input: a list of <see cref="BossDropEntryData"/> from config JSON.
    /// Output: a list of (ItemId, Quantity) pairs to materialise as ItemInstances.
    /// </summary>
    public static class ItemDropTable
    {
        /// <summary>Roll loot from the given drop table entries.</summary>
        public static List<DroppedItem> Roll(IList<BossDropEntryData> table)
        {
            var results = new List<DroppedItem>();
            if (table == null || table.Count == 0) return results;

            // ── Fixed drops (always included) ──────────────────────────────
            var weightedPool = new List<BossDropEntryData>();
            float totalWeight = 0f;

            foreach (var entry in table)
            {
                if (entry.IsFixed)
                {
                    int qty = Random.Range(entry.MinQuantity, entry.MaxQuantity + 1);
                    results.Add(new DroppedItem { ItemId = entry.ItemId, Quantity = qty });
                }
                else
                {
                    weightedPool.Add(entry);
                    totalWeight += entry.Weight;
                }
            }

            // ── One weighted pick from the random pool ──────────────────────
            if (weightedPool.Count > 0 && totalWeight > 0f)
            {
                float roll = Random.Range(0f, totalWeight);
                float cumulative = 0f;

                foreach (var entry in weightedPool)
                {
                    cumulative += entry.Weight;
                    if (roll <= cumulative)
                    {
                        int qty = Random.Range(entry.MinQuantity, entry.MaxQuantity + 1);
                        results.Add(new DroppedItem { ItemId = entry.ItemId, Quantity = qty });
                        break;
                    }
                }
            }

            return results;
        }
    }

    /// <summary>A single resolved drop item.</summary>
    public struct DroppedItem
    {
        public string ItemId;
        public int    Quantity;
    }
}
