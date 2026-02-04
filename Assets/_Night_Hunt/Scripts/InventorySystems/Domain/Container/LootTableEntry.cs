using System;
using NightHunt.Inventory.Core.Data;
using UnityEngine;

namespace NightHunt.Inventory.Domain.Container
{
    /// <summary>
    /// Defines a random loot table entry.
    /// </summary>
    [Serializable]
    public class LootTableEntry
    {
        public ItemDefinition Item;
        
        [Range(0f, 1f)]
        [Tooltip("Chance to spawn (0-1)")]
        public float SpawnChance = 0.5f;
        
        public int MinQuantity = 1;
        public int MaxQuantity = 1;
        
        [Range(0f, 100f)]
        public float MinDurability = 50f;
        
        [Range(0f, 100f)]
        public float MaxDurability = 100f;
    }
}