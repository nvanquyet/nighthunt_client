using System;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;

namespace NightHunt.Inventory.Core.Events
{
    /// <summary>
    /// Events for weapon stats system.
    /// </summary>
    public static class WeaponStatsEvents
    {
        public static event Action<ItemInstance, WeaponStatType, ModifierCalculationType, float, string> OnAddModifier;
        public static event Action<ItemInstance, string> OnRemoveModifier; // weapon, sourceId
        
        public static void InvokeAddModifier(ItemInstance weapon, WeaponStatType stat, ModifierCalculationType type, float value, string sourceId) 
            => OnAddModifier?.Invoke(weapon, stat, type, value, sourceId);
        public static void InvokeRemoveModifier(ItemInstance weapon, string sourceId) => OnRemoveModifier?.Invoke(weapon, sourceId);
    }
}