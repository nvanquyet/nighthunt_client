using UnityEngine;
using System.Collections.Generic;
using FishNet;
using NightHunt.InteractionSystem.Loot;

namespace NightHunt.InteractionSystem.Loot.Spawn
{
    /// <summary>
    /// Manages multiple loot containers in the scene.
    /// Can pre-generate all containers at once or manage them individually.
    /// Similar to LootSpawnManager but for containers.
    /// </summary>
    public class LootContainerManager : LootManagerBase<LootContainerPoint>
    {
        protected override void Start()
        {
            base.Start();
            // Note: Each NetworkLootContainer handles its own pre-generation based on its config
        }

        /// <summary>
        /// Find all LootContainerPoints in the scene.
        /// </summary>
        protected override void FindAllSpawnPoints()
        {
            activeSpawnPoints.Clear();
            LootContainerPoint[] foundPoints = FindObjectsOfType<LootContainerPoint>();
            activeSpawnPoints.AddRange(foundPoints);
            Debug.Log($"[LootContainerManager] Found {activeSpawnPoints.Count} container spawn points");
        }
    }
}
