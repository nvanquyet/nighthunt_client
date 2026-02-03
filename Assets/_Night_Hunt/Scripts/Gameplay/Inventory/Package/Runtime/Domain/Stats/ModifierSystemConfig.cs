using UnityEngine;
using NightHunt.Inventory.Core;

namespace NightHunt.Inventory.Domain
{
    /// <summary>
    /// Global configuration for modifier calculation system.
    /// Choose ONE calculation type that affects ALL items in the game.
    /// </summary>
    [CreateAssetMenu(fileName = "ModifierConfig", menuName = "Inventory/ModifierSystemConfig")]
    public class ModifierSystemConfig : ScriptableObject
    {
        [Header("Calculation Type (Global Setting)")]
        [Tooltip("This affects ALL items in the game")]
        public ModifierCalculationType CalculationType = ModifierCalculationType.FlatAddition;
    }
}
