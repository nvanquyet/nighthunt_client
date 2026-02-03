using System;
using System.Collections.Generic;
using NightHunt.Inventory.Core;

namespace NightHunt.Inventory.Events
{
    /// <summary>
    /// Events for stats system.
    /// </summary>
    public static class StatsEvents
    {
        public static event Action<Dictionary<CharacterStatType, float>> OnStatsChanged;
        
        public static void FireStatsChanged(Dictionary<CharacterStatType, float> stats) => OnStatsChanged?.Invoke(stats);
    }
}
