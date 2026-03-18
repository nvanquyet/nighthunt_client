using UnityEngine;
using NightHunt.Gameplay.StatSystem.Core.Types;

namespace NightHunt.Gameplay.StatSystem.Core.Data
{
    /// <summary>
    /// Player stat modifier from equipment/weapons
    /// 
    /// RESPONSIBILITIES:
    /// - Represents a modifier for player stats (used by equipment/weapons)
    /// - Applied when item is equipped
    /// - Used in EquipmentDefinition and WeaponDefinition
    /// 
    /// DESIGN:
    /// - Struct for performance (value type)
    /// - Separate from StatModifier (which is runtime modifier)
    /// - This is definition-time modifier (from ScriptableObject)
    /// 
    /// USAGE:
    /// Example: Heavy Vest
    /// - Armor: +50 (Flat)
    /// - MovementSpeed: -10% (Percentage)
    /// </summary>
    [System.Serializable]
    public struct PlayerStatModifier
    {
        [Tooltip("Player stat type to modify")]
        public PlayerStatType StatType;
        
        [Tooltip("Modifier value")]
        public float Value;
        
        [Tooltip("Modifier type")]
        public ModifierType ModifierType;
        
        [Tooltip("Description (for tooltip)")]
        public string Description;
    }
}
