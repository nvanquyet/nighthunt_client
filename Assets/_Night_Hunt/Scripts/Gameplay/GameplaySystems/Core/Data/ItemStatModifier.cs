using GameplaySystems.Stat;

namespace GameplaySystems.Core.Data
{
    /// <summary>
    /// Item stat modifier
    /// Used by attachments to modify parent item stats
    /// 
    /// Example: Red Dot Scope on AK-47
    /// - Accuracy: +10 (Flat)
    /// - AimSpeed: +20% (Percentage)
    /// - Recoil: -5% (Percentage)
    /// </summary>
    [System.Serializable]
    public struct ItemStatModifier
    {
        /// <summary>
        /// Item stat type to modify
        /// e.g., Damage, Accuracy, Recoil
        /// </summary>
        public ItemStatType StatType;
        
        /// <summary>
        /// Modifier value
        /// Flat: Direct value (e.g., +10 damage)
        /// Percentage: Percentage value (e.g., +20 = +20%)
        /// </summary>
        public float Value;
        
        /// <summary>
        /// Type of modifier
        /// </summary>
        public ModifierType ModifierType;
        
        /// <summary>
        /// Description for tooltip
        /// </summary>
        public string Description;
        
        #region Factory Methods
        
        /// <summary>
        /// Create flat modifier
        /// </summary>
        public static ItemStatModifier CreateFlat(ItemStatType statType, float value, string description = "")
        {
            return new ItemStatModifier
            {
                StatType = statType,
                Value = value,
                ModifierType = ModifierType.Flat,
                Description = description
            };
        }
        
        /// <summary>
        /// Create percentage modifier
        /// </summary>
        public static ItemStatModifier CreatePercentage(ItemStatType statType, float value, string description = "")
        {
            return new ItemStatModifier
            {
                StatType = statType,
                Value = value,
                ModifierType = ModifierType.Percentage,
                Description = description
            };
        }
        
        #endregion
        
        #region Debug
        
        public override string ToString()
        {
            string typeStr = ModifierType == ModifierType.Percentage ? "%" : "";
            string sign = Value >= 0 ? "+" : "";
            return $"{StatType} {sign}{Value}{typeStr} ({ModifierType})";
        }
        
        #endregion
    }
}