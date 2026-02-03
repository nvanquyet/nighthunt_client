using System;
using UnityEngine;

namespace NightHunt.Inventory.Core
{
    /// <summary>
    /// Configuration for a stat modifier applied by an item.
    /// </summary>
    [Serializable]
    public class StatModifierConfig
    {
        [Header("Target Stat")]
        public CharacterStatType CharacterStat; // If modifies character
        public WeaponStatType WeaponStat;       // If modifies weapon
        
        [Header("Modifier Settings")]
        public ModifierType Type;               // Flat or Percentage
        public float Value;
        
        public string GetSourceId(string itemInstanceId)
        {
            return $"Attach:{itemInstanceId}";
        }
    }
}
