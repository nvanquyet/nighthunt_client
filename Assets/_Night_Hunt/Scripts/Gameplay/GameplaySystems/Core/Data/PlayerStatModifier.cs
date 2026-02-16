using GameplaySystems.Stat;
using UnityEngine;

namespace GameplaySystems.Core.Data
{
    /// <summary>
    /// Player stat modifier from equipment/weapons
    /// Applied when item is equipped
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