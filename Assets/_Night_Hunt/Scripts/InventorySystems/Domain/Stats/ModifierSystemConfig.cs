using UnityEngine;
using NightHunt.Inventory.Core.Enums;
using System;

namespace NightHunt.Inventory.Domain.Stats
{
    /// <summary>
    /// Global configuration for modifier calculation system.
    /// This affects ALL items in the game.
    /// </summary>
    [CreateAssetMenu(fileName = "ModifierSystemConfig", menuName = "NightHunt/Inventory/Modifier System Config")]
    public class ModifierSystemConfig : ScriptableObject
    {
        [Header("Calculation Type (Global Setting)")] [Tooltip("This affects ALL items in the game")]
        public ModifierCalculationType CalculationType = ModifierCalculationType.FlatAddition;
    }
}