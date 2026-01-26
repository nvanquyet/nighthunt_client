using System;
using UnityEngine;

namespace NightHunt.InteractionSystem.Core.Structs
{
    /// <summary>
    /// Stat modifier for equipment attachments.
    /// </summary>
    [Serializable]
    public struct StatModifier
    {
        /// <summary>
        /// Type of stat being modified.
        /// </summary>
        public StatType statType;

        /// <summary>
        /// Type of modifier (Additive, Multiplicative, Override).
        /// </summary>
        public ModifierType modifierType;

        /// <summary>
        /// Value of the modifier.
        /// </summary>
        public float value;

        /// <summary>
        /// Source of this modifier (for debugging).
        /// </summary>
        public string source;

        /// <summary>
        /// Create a new stat modifier.
        /// </summary>
        public StatModifier(StatType statType, ModifierType modifierType, float value, string source = "")
        {
            this.statType = statType;
            this.modifierType = modifierType;
            this.value = value;
            this.source = source;
        }

        /// <summary>
        /// Apply this modifier to a base value.
        /// </summary>
        public float ApplyTo(float baseValue)
        {
            switch (modifierType)
            {
                case ModifierType.Additive:
                    return baseValue + value;
                case ModifierType.Multiplicative:
                    return baseValue * value;
                case ModifierType.Override:
                    return value;
                default:
                    return baseValue;
            }
        }
    }

    /// <summary>
    /// Types of stats that can be modified.
    /// </summary>
    public enum StatType
    {
        // Weapon stats
        Damage,
        Accuracy,
        Recoil,
        FireRate,
        Range,
        ReloadSpeed,

        // Armor stats
        DamageReduction,
        MovementSpeed,
        Weight,
        ArmorValue,

        // Helmet stats
        VisionRange,
        HeadshotProtection,
        DetectionRange,

        // Universal stats
        Durability,
        RepairCost,
        Stealth,
        Visibility
    }

    /// <summary>
    /// Type of modifier calculation.
    /// </summary>
    public enum ModifierType
    {
        /// <summary>
        /// Add value to base: result = base + value
        /// </summary>
        Additive,

        /// <summary>
        /// Multiply base by value: result = base * value
        /// </summary>
        Multiplicative,

        /// <summary>
        /// Override base with value: result = value
        /// </summary>
        Override
    }
}
