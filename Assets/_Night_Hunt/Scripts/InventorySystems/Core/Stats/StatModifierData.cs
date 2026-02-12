using NightHunt.Inventory.Core.Enums;

namespace NightHunt.Inventory.Stats
{
    /// <summary>
    /// Data structure for stat modifiers with targeting info.
    /// Matches existing ItemDefinition.StatModifierDefinition structure.
    /// </summary>
    [System.Serializable]
    public struct StatModifierData
    {
        /// <summary>What does this modifier target? (Character or Weapon)</summary>
        public StatModifierTarget Target;
        
        /// <summary>Character stat type (if Target == Character)</summary>
        public CharacterStatType CharacterStat;
        
        /// <summary>Weapon stat type (if Target == Weapon)</summary>
        public WeaponStatType WeaponStat;
        
        /// <summary>Calculation type (Flat or Percentage)</summary>
        public ModifierCalculationType CalculationType;
        
        /// <summary>Modifier value</summary>
        public float Value;
        
        /// <summary>Display name for UI</summary>
        public string DisplayName;
        
        #region Factory Methods
        
        /// <summary>
        /// Create a character stat modifier.
        /// </summary>
        public static StatModifierData CreateCharacterModifier(
            CharacterStatType stat, 
            ModifierCalculationType calcType, 
            float value,
            string displayName = "")
        {
            return new StatModifierData
            {
                Target = StatModifierTarget.Character,
                CharacterStat = stat,
                CalculationType = calcType,
                Value = value,
                DisplayName = displayName
            };
        }
        
        /// <summary>
        /// Create a weapon stat modifier.
        /// </summary>
        public static StatModifierData CreateWeaponModifier(
            WeaponStatType stat, 
            ModifierCalculationType calcType, 
            float value,
            string displayName = "")
        {
            return new StatModifierData
            {
                Target = StatModifierTarget.Weapon,
                WeaponStat = stat,
                CalculationType = calcType,
                Value = value,
                DisplayName = displayName
            };
        }
        
        #endregion
        
        #region Helper Methods
        
        /// <summary>
        /// Calculate final modifier value based on calculation type.
        /// </summary>
        public float GetFinalValue(float baseValue)
        {
            switch (CalculationType)
            {
                case ModifierCalculationType.Flat:
                    return Value;
                    
                case ModifierCalculationType.Percentage:
                    return baseValue * Value; // Value should be 0.15 for 15%
                    
                default:
                    return Value;
            }
        }
        
        /// <summary>
        /// Get display string for UI.
        /// </summary>
        public string GetDisplayString()
        {
            if (!string.IsNullOrEmpty(DisplayName))
                return DisplayName;
            
            string sign = Value >= 0 ? "+" : "";
            string suffix = CalculationType == ModifierCalculationType.Percentage ? "%" : "";
            float displayValue = CalculationType == ModifierCalculationType.Percentage ? Value * 100f : Value;
            
            if (Target == StatModifierTarget.Character)
            {
                return $"{sign}{displayValue}{suffix} {CharacterStat}";
            }
            else
            {
                return $"{sign}{displayValue}{suffix} {WeaponStat}";
            }
        }
        
        public override string ToString()
        {
            return GetDisplayString();
        }
        
        #endregion
    }
}