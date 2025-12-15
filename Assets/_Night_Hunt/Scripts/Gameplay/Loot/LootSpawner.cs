using UnityEngine;
using System.Collections.Generic;
using NightHunt.Data;
using FishNet.Object;
using NightHunt.Gameplay.Loot;

namespace NightHunt.Gameplay.Loot
{
    /// <summary>
    /// Spawns loot items on the map
    /// Server-authoritative spawning
    /// Works with both host and dedicated server
    /// </summary>
    public class LootSpawner : NetworkBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] private List<LootSpawnPoint> spawnPoints = new List<LootSpawnPoint>();
        [SerializeField] private float spawnInterval = 30f;
        [SerializeField] private int maxLootPerSpawn = 3;

        [Header("Loot Configuration")]
        [SerializeField] private LootTier[] lootTiers;

        private float lastSpawnTime;
        private List<LootItem> activeLoot = new List<LootItem>();

        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Initial spawn
            SpawnLoot();
        }

        private void Update()
        {
            if (!IsServer) return;

            // Periodic spawning
            if (Time.time - lastSpawnTime >= spawnInterval)
            {
                SpawnLoot();
                lastSpawnTime = Time.time;
            }
        }

        /// <summary>
        /// Server: Spawn loot items
        /// </summary>
        [Server]
        private void SpawnLoot()
        {
            // Clear old loot
            CleanupDespawnedLoot();

            // Spawn new loot
            int spawnCount = Random.Range(1, maxLootPerSpawn + 1);
            
            for (int i = 0; i < spawnCount; i++)
            {
                if (spawnPoints.Count == 0) break;

                LootSpawnPoint spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Count)];
                SpawnLootAtPoint(spawnPoint);
            }
        }

        /// <summary>
        /// Server: Spawn loot at specific point
        /// </summary>
        [Server]
        private void SpawnLootAtPoint(LootSpawnPoint spawnPoint)
        {
            // Select loot tier based on phase
            LootTier tier = SelectLootTier();
            if (tier == null || tier.items.Count == 0) return;

            // Select random item from tier
            string itemId = tier.items[Random.Range(0, tier.items.Count)];

            // Spawn loot item
            GameObject lootPrefab = GetLootPrefab(itemId);
            if (lootPrefab == null) return;

            Vector3 spawnPos = spawnPoint.GetSpawnPosition();
            GameObject lootObj = Instantiate(lootPrefab, spawnPos, Quaternion.identity);
            
            LootItem lootItem = lootObj.GetComponent<LootItem>();
            if (lootItem == null)
            {
                lootItem = lootObj.AddComponent<LootItem>();
            }

            lootItem.Initialize(itemId, tier.rarity);
            
            // Spawn on network
            Spawn(lootObj);

            activeLoot.Add(lootItem);
        }

        /// <summary>
        /// Select loot tier based on current phase
        /// </summary>
        private LootTier SelectLootTier()
        {
            // Simple selection - can be improved with phase-based logic
            if (lootTiers == null || lootTiers.Length == 0) return null;
            return lootTiers[Random.Range(0, lootTiers.Length)];
        }

        /// <summary>
        /// Get loot prefab by item ID
        /// </summary>
        private GameObject GetLootPrefab(string itemId)
        {
            // This would load from Resources or use a prefab reference
            // For now, return a basic prefab
            GameObject prefab = new GameObject("LootItem");
            prefab.AddComponent<LootItem>();
            return prefab;
        }

        /// <summary>
        /// Cleanup despawned loot
        /// </summary>
        private void CleanupDespawnedLoot()
        {
            activeLoot.RemoveAll(loot => loot == null || !loot.IsSpawned);
        }

        /// <summary>
        /// Add spawn point
        /// </summary>
        public void AddSpawnPoint(LootSpawnPoint point)
        {
            if (!spawnPoints.Contains(point))
            {
                spawnPoints.Add(point);
            }
        }
    }

    /// <summary>
    /// Loot spawn point
    /// </summary>
    public class LootSpawnPoint : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] private float spawnRadius = 2f;
        [SerializeField] private bool canSpawn = true;

        public Vector3 GetSpawnPosition()
        {
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            return transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);
        }

        public bool CanSpawn() => canSpawn;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
        }
    }

    /// <summary>
    /// Loot tier configuration
    /// </summary>
    [System.Serializable]
    public class LootTier
    {
        public string tierName;
        public string rarity; // Common, Rare, Epic, Legendary
        public List<string> items = new List<string>();
        public float spawnWeight = 1f;
    }
}

