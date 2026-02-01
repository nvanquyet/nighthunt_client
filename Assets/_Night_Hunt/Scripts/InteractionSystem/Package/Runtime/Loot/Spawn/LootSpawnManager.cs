using UnityEngine;
using System.Collections.Generic;
using FishNet;
using NightHunt.InteractionSystem.Loot.Spawn;

namespace NightHunt.InteractionSystem.Loot.Spawn
{
    /// <summary>
    /// Manages multiple loot spawn points in the scene.
    /// Can spawn all points at once or manage them individually.
    /// </summary>
    public class LootSpawnManager : LootManagerBase<LootSpawnPoint>
    {
        protected override void Start()
        {
            base.Start();
            // Note: Each LootSpawnPoint handles its own spawning based on its config
        }

        /// <summary>
        /// Find all LootSpawnPoints in the scene.
        /// </summary>
        protected override void FindAllSpawnPoints()
        {
            activeSpawnPoints.Clear();
            LootSpawnPoint[] foundPoints = FindObjectsOfType<LootSpawnPoint>();
            activeSpawnPoints.AddRange(foundPoints);
            Debug.Log($"[LootSpawnManager] Found {activeSpawnPoints.Count} spawn points");
        }

        /// <summary>
        /// Spawn loot at all spawn points (server only).
        /// </summary>
        public void SpawnAll()
        {
            if (!InstanceFinder.IsServer)
            {
                Debug.LogWarning("[LootSpawnManager] SpawnAll can only be called on server!");
                return;
            }

            foreach (var spawnPoint in activeSpawnPoints)
            {
                if (spawnPoint != null)
                {
                    spawnPoint.TriggerSpawn();
                }
            }
        }

        /// <summary>
        /// Spawn loot at a specific spawn point by index.
        /// </summary>
        public void SpawnAt(int index)
        {
            if (index >= 0 && index < activeSpawnPoints.Count)
            {
                activeSpawnPoints[index].TriggerSpawn();
            }
        }

        /// <summary>
        /// Spawn loot at a specific spawn point.
        /// </summary>
        public void SpawnAt(LootSpawnPoint spawnPoint)
        {
            if (spawnPoint != null && activeSpawnPoints.Contains(spawnPoint))
            {
                spawnPoint.TriggerSpawn();
            }
        }

    }
}
