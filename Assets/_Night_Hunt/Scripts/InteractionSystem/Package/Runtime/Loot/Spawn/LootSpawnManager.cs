using UnityEngine;
using System.Collections.Generic;
using NightHunt.InteractionSystem.Loot.Spawn;

namespace NightHunt.InteractionSystem.Loot.Spawn
{
    /// <summary>
    /// Manages multiple loot spawn points in the scene.
    /// Can spawn all points at once or manage them individually.
    /// </summary>
    public class LootSpawnManager : MonoBehaviour
    {
        [Header("Spawn Management")]
        [SerializeField] private bool autoFindSpawnPoints = true;
        [SerializeField] private LootSpawnPoint[] spawnPoints = new LootSpawnPoint[0];

        [Header("Global Spawn Settings")]
        [SerializeField] private bool spawnOnStart = true;
        [SerializeField] private float globalSpawnDelay = 0f;

        private List<LootSpawnPoint> activeSpawnPoints = new List<LootSpawnPoint>();

        private void Start()
        {
            if (autoFindSpawnPoints)
            {
                FindAllSpawnPoints();
            }
            else
            {
                activeSpawnPoints.AddRange(spawnPoints);
            }

            if (spawnOnStart)
            {
                Invoke(nameof(SpawnAll), globalSpawnDelay);
            }
        }

        /// <summary>
        /// Find all LootSpawnPoints in the scene.
        /// </summary>
        private void FindAllSpawnPoints()
        {
            activeSpawnPoints.Clear();
            LootSpawnPoint[] foundPoints = FindObjectsOfType<LootSpawnPoint>();
            activeSpawnPoints.AddRange(foundPoints);
            Debug.Log($"[LootSpawnManager] Found {activeSpawnPoints.Count} spawn points");
        }

        /// <summary>
        /// Spawn loot at all spawn points.
        /// </summary>
        public void SpawnAll()
        {
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

        /// <summary>
        /// Register a spawn point manually.
        /// </summary>
        public void RegisterSpawnPoint(LootSpawnPoint spawnPoint)
        {
            if (spawnPoint != null && !activeSpawnPoints.Contains(spawnPoint))
            {
                activeSpawnPoints.Add(spawnPoint);
            }
        }

        /// <summary>
        /// Unregister a spawn point.
        /// </summary>
        public void UnregisterSpawnPoint(LootSpawnPoint spawnPoint)
        {
            activeSpawnPoints.Remove(spawnPoint);
        }

        /// <summary>
        /// Get all active spawn points.
        /// </summary>
        public List<LootSpawnPoint> GetActiveSpawnPoints()
        {
            return new List<LootSpawnPoint>(activeSpawnPoints);
        }
    }
}
