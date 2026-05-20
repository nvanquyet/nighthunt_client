using NightHunt.Gameplay.StatSystem.Core.Types;
using UnityEngine;

namespace NightHunt.Gameplay.StatSystem.Core.Data
{
    /// <summary>
    /// Represents a stat modification from items, effects, or buffs
    /// 
    /// RESPONSIBILITIES:
    /// - Stores modifier data (source, type, value, priority)
    /// - Provides factory methods for creating modifiers
    /// - Not network-synced directly - stored locally on server
    /// - Only final calculated values are synced via StatData
    /// 
    /// NETWORK ARCHITECTURE:
    /// - Server-only: Modifiers stored locally on server
    /// - Client receives final calculated values via StatData
    /// </summary>
    [System.Serializable]
    public struct StatModifier
    {
        /// <summary>
        /// Unique identifier for the source of this modifier
        /// Usually: ItemInstanceID, BuffID, EffectID, or "System"
        /// Used to remove specific modifiers when item unequipped/buff expires
        /// </summary>
        public string SourceID;
        
        /// <summary>
        /// Type of modification (Flat, Percentage, Override)
        /// </summary>
        public ModifierType Type;
        
        /// <summary>
        /// Modification value
        /// - For Flat: direct addition (e.g., 10 = +10)
        /// - For Percentage: percentage value (e.g., 10 = +10%, not 0.1)
        /// - For Override: replaces base value entirely
        /// </summary>
        public float Value;
        
        /// <summary>
        /// Priority for calculation order (lower = applied first)
        /// Default priorities:
        /// - -1000: Override modifiers (always first)
        /// - 0: Item modifiers
        /// - 100: Buff modifiers
        /// - -100: Debuff modifiers
        /// </summary>
        public int Priority;
        
        /// <summary>
        /// Optional: Description for debugging and tooltips
        /// Example: "AK-47 Movement Speed Penalty"
        /// </summary>
        public string Description;
        
        #region Factory Methods
        
        /// <summary>
        /// Creates a flat modifier
        /// Example: +10 Armor from Heavy Vest
        /// </summary>
        public static StatModifier CreateFlat(string sourceID, float value, int priority = 0, string description = "")
        {
            return new StatModifier
            {
                SourceID = sourceID,
                Type = ModifierType.Flat,
                Value = value,
                Priority = priority,
                Description = description
            };
        }
        
        /// <summary>
        /// Creates a percentage modifier
        /// Example: -10% Movement Speed from Heavy Armor
        /// </summary>
        public static StatModifier CreatePercentage(string sourceID, float percentValue, int priority = 0, string description = "")
        {
            return new StatModifier
            {
                SourceID = sourceID,
                Type = ModifierType.Percentage,
                Value = NormalizePercentValue(percentValue),
                Priority = priority,
                Description = description
            };
        }

        /// <summary>
        /// Accepts both authoring styles used in existing assets:
        /// 15 = 15%, 0.15 = 15%. Values of exactly +/-1 stay as +/-1%.
        /// </summary>
        public static float NormalizePercentValue(float value)
        {
            return Mathf.Abs(value) > 0f && Mathf.Abs(value) < 1f
                ? value * 100f
                : value;
        }
        
        /// <summary>
        /// Creates an override modifier
        /// Example: Set Movement Speed to 0 (stunned)
        /// </summary>
        public static StatModifier CreateOverride(string sourceID, float value, string description = "")
        {
            return new StatModifier
            {
                SourceID = sourceID,
                Type = ModifierType.Override,
                Value = value,
                Priority = -1000, // Always highest priority
                Description = description
            };
        }
        
        #endregion
        
        #region Equality
        
        public override bool Equals(object obj)
        {
            if (!(obj is StatModifier))
                return false;
            
            var other = (StatModifier)obj;
            return SourceID == other.SourceID && Type == other.Type;
        }
        
        public override int GetHashCode()
        {
            return (SourceID != null ? SourceID.GetHashCode() : 0) ^ (int)Type;
        }
        
        #endregion
        
        #region String Representation
        
        public override string ToString()
        {
            string sign = Value >= 0 ? "+" : "";
            string valueStr = Type == ModifierType.Percentage 
                ? $"{sign}{Value}%" 
                : $"{sign}{Value}";
            
            string desc = !string.IsNullOrEmpty(Description) ? $" ({Description})" : "";
            return $"[{Type}] {valueStr} from {SourceID}{desc}";
        }
        
        #endregion
    }
}
