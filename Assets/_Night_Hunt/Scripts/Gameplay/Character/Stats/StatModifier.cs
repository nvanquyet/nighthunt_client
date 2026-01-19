using System;

namespace NightHunt.Gameplay.Character.Stats
{
    /// <summary>
    /// Generic stat modifier
    /// </summary>
    public class StatModifier
    {
        public string StatName { get; set; }
        public ModifierType Type { get; set; }
        public float Value { get; set; }
        public string SourceId { get; set; } // ID of the source (item, status effect, etc.)

        public StatModifier(string statName, ModifierType type, float value, string sourceId)
        {
            StatName = statName;
            Type = type;
            Value = value;
            SourceId = sourceId;
        }

        /// <summary>
        /// Apply modifier to base value
        /// </summary>
        public float Apply(float baseValue)
        {
            return Type == ModifierType.Multiply 
                ? baseValue * Value 
                : baseValue + Value;
        }

        /// <summary>
        /// Remove modifier from value
        /// </summary>
        public float Remove(float modifiedValue)
        {
            return Type == ModifierType.Multiply
                ? modifiedValue / Value
                : modifiedValue - Value;
        }
    }

    /// <summary>
    /// Modifier type
    /// </summary>
    public enum ModifierType
    {
        Multiply,
        Add
    }
}

