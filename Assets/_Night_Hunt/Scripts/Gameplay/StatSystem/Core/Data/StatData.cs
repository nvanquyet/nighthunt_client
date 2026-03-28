using NightHunt.Gameplay.StatSystem.Core.Types;

namespace NightHunt.Gameplay.StatSystem.Core.Data
{
    /// <summary>
    /// Network-synced stat data structure
    /// 
    /// RESPONSIBILITIES:
    /// - Represents a single stat's data for network synchronization
    /// - Used in SyncList for automatic network synchronization
    /// - Keeps only essential data for network efficiency
    /// 
    /// NETWORK ARCHITECTURE:
    /// - Server calculates and updates CurrentValue
    /// - Client receives final values via SyncList
    /// </summary>
    [System.Serializable]
    public struct StatData
    {
        /// <summary>
        /// Type of stat this data represents
        /// </summary>
        public PlayerStatType Type;
        
        /// <summary>
        /// Base value without any modifiers
        /// Set from StatConfig defaults
        /// Only modified when base changes (e.g., level up)
        /// </summary>
        public float BaseValue;
        
        /// <summary>
        /// Current calculated value including all modifiers
        /// This is the actual value used in gameplay
        /// Recalculated when modifiers change
        /// </summary>
        public float CurrentValue;
        
        /// <summary>
        /// Minimum allowed value (for clamping)
        /// From StatConfig
        /// </summary>
        public float MinValue;
        
        /// <summary>
        /// Maximum allowed value (for clamping)
        /// From StatConfig
        /// </summary>
        public float MaxValue;
        
        /// <summary>
        /// Creates a StatData with specified values
        /// </summary>
        public static StatData Create(PlayerStatType type, float baseValue, float min = 0f, float max = float.MaxValue)
        {
            return new StatData
            {
                Type = type,
                BaseValue = baseValue,
                CurrentValue = baseValue,
                MinValue = min,
                MaxValue = max
            };
        }
        
        public override bool Equals(object obj)
        {
            if (!(obj is StatData))
                return false;
            
            var other = (StatData)obj;
            // FISHNET SYNC LIST BUG FIX: Must compare all fields to trigger SyncList update
            return Type == other.Type &&
                   UnityEngine.Mathf.Approximately(BaseValue, other.BaseValue) &&
                   UnityEngine.Mathf.Approximately(CurrentValue, other.CurrentValue) &&
                   UnityEngine.Mathf.Approximately(MinValue, other.MinValue) &&
                   UnityEngine.Mathf.Approximately(MaxValue, other.MaxValue);
        }
        
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + Type.GetHashCode();
                hash = hash * 23 + BaseValue.GetHashCode();
                hash = hash * 23 + CurrentValue.GetHashCode();
                hash = hash * 23 + MinValue.GetHashCode();
                hash = hash * 23 + MaxValue.GetHashCode();
                return hash;
            }
        }
        
        public static bool operator ==(StatData a, StatData b)
        {
            return a.Equals(b);
        }
        
        public static bool operator !=(StatData a, StatData b)
        {
            return !(a == b);
        }
    }
}
