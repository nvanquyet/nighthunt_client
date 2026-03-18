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
        
        /// <summary>
        /// Check equality for sync comparisons
        /// </summary>
        public override bool Equals(object obj)
        {
            if (!(obj is StatData))
                return false;
            
            var other = (StatData)obj;
            return Type == other.Type;
        }
        
        public override int GetHashCode()
        {
            return (int)Type;
        }
        
        public static bool operator ==(StatData a, StatData b)
        {
            return a.Type == b.Type;
        }
        
        public static bool operator !=(StatData a, StatData b)
        {
            return !(a == b);
        }
    }
}
