using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightHunt.Gameplay.Character.Stats
{
    /// <summary>
    /// ScriptableObject config for character base stats.
    /// Can be created per character class/type or use a default one.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterStatsConfig", menuName = "NightHunt/Character/CharacterStatsConfig")]
    public class CharacterStatsConfig : ScriptableObject
    {
        [System.Serializable]
        public class StatEntry
        {
            public CharacterStatType statType;
            public float baseValue;
        }

        [Header("Base Stats")]
        [SerializeField] private List<StatEntry> baseStats = new List<StatEntry>();

        /// <summary>
        /// Get base value for a stat type. Returns 0 if not found.
        /// </summary>
        public float GetBaseValue(CharacterStatType statType)
        {
            var entry = baseStats.Find(e => e.statType == statType);
            return entry != null ? entry.baseValue : 0f;
        }

        /// <summary>
        /// Set base value for a stat type (creates entry if not exists).
        /// </summary>
        public void SetBaseValue(CharacterStatType statType, float value)
        {
            var entry = baseStats.Find(e => e.statType == statType);
            if (entry != null)
            {
                entry.baseValue = value;
            }
            else
            {
                baseStats.Add(new StatEntry { statType = statType, baseValue = value });
            }
        }

        /// <summary>
        /// Get all stat entries.
        /// </summary>
        public IReadOnlyList<StatEntry> GetAllStats() => baseStats;

        /// <summary>
        /// Create default stats config (for backward compatibility with old system).
        /// </summary>
        public static CharacterStatsConfig CreateDefault()
        {
            var config = ScriptableObject.CreateInstance<CharacterStatsConfig>();
            config.SetBaseValue(CharacterStatType.MaxHP, 100f);
            config.SetBaseValue(CharacterStatType.Stamina, 100f);
            config.SetBaseValue(CharacterStatType.MoveSpeed, 1f);
            config.SetBaseValue(CharacterStatType.WeightCapacity, 20f);
            config.SetBaseValue(CharacterStatType.VisionRadius, 12f);
            config.SetBaseValue(CharacterStatType.NoiseLevel, 1f);
            return config;
        }
    }
}
