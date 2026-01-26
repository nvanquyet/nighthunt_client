using System.Collections.Generic;
using UnityEngine;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Core.Interfaces;
using NightHunt.InteractionSystem.Items.Data;

namespace NightHunt.InteractionSystem.Items.Attachments
{
    /// <summary>
    /// Calculates final stats from base equipment and attachments.
    /// </summary>
    public class StatCalculator : MonoBehaviour
    {
        private Dictionary<StatType, float> finalStats = new Dictionary<StatType, float>();

        /// <summary>
        /// Recalculate stats from equipment and attachments.
        /// </summary>
        public void RecalculateStats(EquipmentDataBase equipment, Dictionary<Core.Interfaces.AttachmentSlotType, AttachmentData> attachments)
        {
            finalStats.Clear();

            // Start with base stat modifiers from equipment
            if (equipment != null && equipment.BaseStatModifiers != null)
            {
                foreach (var modifier in equipment.BaseStatModifiers)
                {
                    ApplyModifier(modifier);
                }
            }

            // Apply modifiers from all attachments
            if (attachments != null)
            {
                foreach (var attachment in attachments.Values)
                {
                    if (attachment != null && attachment.StatModifiers != null)
                    {
                        foreach (var modifier in attachment.StatModifiers)
                        {
                            ApplyModifier(modifier);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Apply a stat modifier.
        /// </summary>
        private void ApplyModifier(StatModifier modifier)
        {
            float baseValue = GetBaseValue(modifier.statType);
            float newValue = modifier.ApplyTo(baseValue);
            finalStats[modifier.statType] = newValue;
        }

        /// <summary>
        /// Get base value for a stat type (from equipment data).
        /// </summary>
        private float GetBaseValue(StatType statType)
        {
            if (finalStats.ContainsKey(statType))
                return finalStats[statType];

            // Default base values
            return 0f;
        }

        /// <summary>
        /// Get final calculated stat value.
        /// </summary>
        public float GetFinalStat(StatType statType)
        {
            if (finalStats.ContainsKey(statType))
                return finalStats[statType];

            return GetBaseValue(statType);
        }

        /// <summary>
        /// Get all final stats.
        /// </summary>
        public Dictionary<StatType, float> GetAllFinalStats()
        {
            return new Dictionary<StatType, float>(finalStats);
        }
    }
}
