using System;
using NightHunt.Inventory.Core.Enums;

namespace NightHunt.Gameplay.Character.Stats
{
    /// <summary>
    /// Data structure for character stat modifiers.
    /// Used internally by CharacterStats to track modifiers by source.
    /// </summary>
    [Serializable]
    public struct StatModifier
    {
        /// <summary>Which character stat this modifies</summary>
        public CharacterStatType StatType;
        
        /// <summary>Calculation type (Flat or Percentage)</summary>
        public ModifierCalculationType CalculationType;
        
        /// <summary>Modifier value</summary>
        public float Value;
        
        /// <summary>Source ID for tracking (e.g., "Equip:instanceId")</summary>
        public string SourceId;
        
        public override string ToString()
        {
            string sign = Value >= 0 ? "+" : "";
            string valueStr = CalculationType == ModifierCalculationType.Percentage 
                ? $"{sign}{Value * 100:F0}%" 
                : $"{sign}{Value:F1}";
            
            return $"{StatType}: {valueStr} (from {SourceId})";
        }
    }
}