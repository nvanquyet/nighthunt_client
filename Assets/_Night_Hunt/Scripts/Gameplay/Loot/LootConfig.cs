using System;
using UnityEngine;

namespace NightHunt.Gameplay.Loot
{
    /// <summary>
    /// Loot configuration
    /// </summary>
    [Serializable]
    public class LootConfig
    {
        public string LootId;
        public string ItemId;
        public float SpawnChance;
        public LootRarity Rarity;
        public string[] ActivePhases; // Phases when this loot can spawn
    }

    /// <summary>
    /// Loot rarity
    /// </summary>
    public enum LootRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }
}

