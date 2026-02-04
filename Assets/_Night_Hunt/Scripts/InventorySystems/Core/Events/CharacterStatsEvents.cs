using System;
using NightHunt.Inventory.Core.Enums;

namespace NightHunt.Inventory.Core.Events
{
    /// <summary>
    /// Events for character stats system.
    /// </summary>
    public static class CharacterStatsEvents
    {
        public static event Action<CharacterStatType, ModifierCalculationType, float, string> OnAddModifier; // stat, type, value, sourceId
        public static event Action<string> OnRemoveModifier; // sourceId
        public static event Action OnStatsChanged;
        
        public static void InvokeAddModifier(CharacterStatType stat, ModifierCalculationType type, float value, string sourceId) 
            => OnAddModifier?.Invoke(stat, type, value, sourceId);
        public static void InvokeRemoveModifier(string sourceId) => OnRemoveModifier?.Invoke(sourceId);
        public static void InvokeStatsChanged() => OnStatsChanged?.Invoke();
    }
}