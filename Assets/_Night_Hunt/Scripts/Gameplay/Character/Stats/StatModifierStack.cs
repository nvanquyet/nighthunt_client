using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NightHunt.Gameplay.Character.Stats
{
    /// <summary>
    /// Stack management for modifiers
    /// </summary>
    public class StatModifierStack
    {
        private readonly Dictionary<string, List<StatModifier>> modifiersByStat = new Dictionary<string, List<StatModifier>>();

        /// <summary>
        /// Add modifier to stack
        /// </summary>
        public void AddModifier(StatModifier modifier)
        {
            if (!modifiersByStat.ContainsKey(modifier.StatName))
            {
                modifiersByStat[modifier.StatName] = new List<StatModifier>();
            }

            modifiersByStat[modifier.StatName].Add(modifier);
        }

        /// <summary>
        /// Remove modifier from stack
        /// </summary>
        public bool RemoveModifier(string statName, string sourceId)
        {
            if (!modifiersByStat.ContainsKey(statName)) return false;

            var modifiers = modifiersByStat[statName];
            var modifier = modifiers.FirstOrDefault(m => m.SourceId == sourceId);
            if (modifier != null)
            {
                modifiers.Remove(modifier);
                if (modifiers.Count == 0)
                {
                    modifiersByStat.Remove(statName);
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// Get all modifiers for a stat
        /// </summary>
        public List<StatModifier> GetModifiers(string statName)
        {
            return modifiersByStat.ContainsKey(statName) 
                ? new List<StatModifier>(modifiersByStat[statName]) 
                : new List<StatModifier>();
        }

        /// <summary>
        /// Calculate final stat value
        /// Formula: Base × Multipliers + Additions
        /// </summary>
        public float CalculateFinalValue(string statName, float baseValue)
        {
            if (!modifiersByStat.ContainsKey(statName))
            {
                return baseValue;
            }

            float result = baseValue;
            var modifiers = modifiersByStat[statName];

            // Apply multipliers first
            foreach (var modifier in modifiers.Where(m => m.Type == ModifierType.Multiply))
            {
                result *= modifier.Value;
            }

            // Then apply additions
            foreach (var modifier in modifiers.Where(m => m.Type == ModifierType.Add))
            {
                result += modifier.Value;
            }

            return result;
        }

        /// <summary>
        /// Clear all modifiers
        /// </summary>
        public void Clear()
        {
            modifiersByStat.Clear();
        }

        /// <summary>
        /// Clear modifiers for specific stat
        /// </summary>
        public void ClearStat(string statName)
        {
            modifiersByStat.Remove(statName);
        }
    }
}

