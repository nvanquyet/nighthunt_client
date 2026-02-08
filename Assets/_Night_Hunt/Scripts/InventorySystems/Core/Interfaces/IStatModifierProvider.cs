using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Gameplay.Character.Stats;
using System.Collections.Generic;

namespace NightHunt.Inventory.Core.Interfaces
{
    /// <summary>
    /// Interface for items that provide stat modifiers.
    /// Example: Helmet with flashlight → provides VisionRadius modifier to Character
    ///          Weapon with grip → provides Recoil modifier to Weapon
    /// </summary>
    public interface IStatModifierProvider
    {
        /// <summary>
        /// Get all stat modifiers this item provides.
        /// Returns tuple: (Target, StatType, CalcType, Value)
        /// </summary>
        List<StatModifierData> GetStatModifiers();
        
        /// <summary>
        /// Get unique source ID for tracking modifiers.
        /// Format: "Equip:{instanceId}" or "Attach:{instanceId}"
        /// </summary>
        string GetModifierSourceId();
    }
    
    /// <summary>
    /// Data structure for stat modifiers with targeting info.
    /// </summary>
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
    }
}