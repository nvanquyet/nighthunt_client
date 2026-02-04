using System;
using UnityEngine;

namespace NightHunt.Inventory.Core.Events
{
    /// <summary>
    /// Events for boss loot system.
    /// </summary>
    public static class BossLootEvents
    {
        public static event Action<Vector3, object> OnBossDefeated; // position, ContainerConfig
        
        public static void InvokeBossDefeated(Vector3 position, object lootConfig) => OnBossDefeated?.Invoke(position, lootConfig);
    }
}