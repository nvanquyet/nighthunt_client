using UnityEngine;
using FishNet.Object;
using System.Collections.Generic;
using NightHunt.InteractionSystem.Loot.Definitions;
using NightHunt.InteractionSystem.Items.Runtime;

namespace NightHunt.InteractionSystem.Loot.Spawn
{
    /// <summary>
    /// Loot spawn point that spawns items based on a LootTable ScriptableObject.
    /// Each point has its own LootTable and spawn settings.
    /// </summary>
    public class LootSpawnPoint : NetworkBehaviour
    {
        [Header("Spawn Config")]
        [Tooltip("Configuration for prefab, radius, and visual effects (fixed settings)")]
        [SerializeField] private LootSpawnConfig config;

        [Header("Loot Table")]
        [Tooltip("LootTable to generate items from (per-point configuration)")]
        [SerializeField] private LootTable lootTable;

        [Header("Spawn Timing")]
        [Tooltip("When should items spawn?")]
        [SerializeField] private SpawnMode spawnMode = SpawnMode.Once;
        
        [Tooltip("Respawn interval in seconds (for Interval mode)")]
        [SerializeField] private float respawnInterval = 300f;
        
        [Tooltip("Initial delay before first spawn")]
        [SerializeField] private float initialDelay = 0f;

        [Header("Loot Generation Override")]
        [Tooltip("Override LootTable's minItemsPerSpawn? (0 = use LootTable default)")]
        [SerializeField] private int overrideMinItems = 0;

        [Tooltip("Override LootTable's maxItemsPerSpawn? (0 = use LootTable default)")]
        [SerializeField] private int overrideMaxItems = 0;

        [Header("Visual")]
        [SerializeField] private bool showGizmos = true;

        private float lastSpawnTime = 0f;
        private bool hasSpawned = false;
        private List<NetworkLootItem> spawnedItems = new List<NetworkLootItem>();

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (config == null)
            {
                Debug.LogError("[LootSpawnPoint] Spawn config is not assigned!");
                return;
            }

            if (spawnMode == SpawnMode.Once || spawnMode == SpawnMode.Interval)
            {
                Invoke(nameof(SpawnLoot), initialDelay);
            }
        }

        private void Update()
        {
            if (!IsServer)
                return;

            if (spawnMode == SpawnMode.Interval && hasSpawned)
            {
                // Check if all spawned items have been picked up
                bool allPickedUp = true;
                foreach (var item in spawnedItems)
                {
                    if (item != null && item.IsSpawned)
                    {
                        allPickedUp = false;
                        break;
                    }
                }

                // If all picked up and enough time has passed, respawn
                if (allPickedUp && Time.time - lastSpawnTime >= respawnInterval)
                {
                    SpawnLoot();
                }
            }
        }

        /// <summary>
        /// Spawn loot based on loot table.
        /// </summary>
        [Server]
        public void SpawnLoot()
        {
            if (config == null)
            {
                Debug.LogError("[LootSpawnPoint] Spawn config is not assigned!");
                return;
            }

            if (config.LootItemPrefab == null)
            {
                Debug.LogError("[LootSpawnPoint] Loot item prefab is not assigned in config!");
                return;
            }

            if (lootTable == null)
            {
                Debug.LogWarning("[LootSpawnPoint] Loot table is not assigned!");
                return;
            }

            // Clear old spawned items list
            spawnedItems.Clear();

            // Generate loot from LootTable (weighted random)
            var generatedItems = lootTable.GenerateLoot(
                overrideMinItems > 0 ? overrideMinItems : 0,
                overrideMaxItems > 0 ? overrideMaxItems : 0
            );
            if (generatedItems == null || generatedItems.Count == 0)
            {
                Debug.LogWarning("[LootSpawnPoint] No items generated from loot table!");
                return;
            }

            // Spawn each generated item
            foreach (var itemInstance in generatedItems)
            {
                // Find the loot definition for this item
                LootItemDefinition lootDef = FindLootDefinitionForItem(itemInstance.itemDataId);
                if (lootDef == null)
                {
                    Debug.LogWarning($"[LootSpawnPoint] Could not find LootItemDefinition for item {itemInstance.itemDataId}");
                    continue;
                }

                SpawnItem(lootDef, itemInstance.quantity);
            }

            lastSpawnTime = Time.time;
            hasSpawned = true;

            // Spawn visual effect
            if (config != null && config.SpawnEffectPrefab != null)
            {
                GameObject effect = Instantiate(config.SpawnEffectPrefab, transform.position, Quaternion.identity);
                NetworkObject effectNO = effect.GetComponent<NetworkObject>();
                if (effectNO != null)
                {
                    Spawn(effectNO);
                }
            }
        }

        /// <summary>
        /// Find LootItemDefinition for a given item ID.
        /// </summary>
        private LootItemDefinition FindLootDefinitionForItem(string itemId)
        {
            if (lootTable == null || lootTable.entries == null)
                return null;

            foreach (var entry in lootTable.entries)
            {
                if (entry.loot != null && entry.loot.ItemData != null && entry.loot.ItemData.ItemId == itemId)
                {
                    return entry.loot;
                }
            }

            return null;
        }

        /// <summary>
        /// Spawn a single item with loot definition.
        /// </summary>
        [Server]
        private void SpawnItem(LootItemDefinition lootDef, int quantity)
        {
            // Calculate spawn position (random within radius)
            float radius = config != null ? config.SpawnRadius : 1f;
            Vector2 randomCircle = Random.insideUnitCircle * radius;
            Vector3 spawnPosition = transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);

            // Spawn generic NetworkLootItem prefab and inject definition at runtime (server)
            if (config == null || config.LootItemPrefab == null)
            {
                Debug.LogError("[LootSpawnPoint] Config or LootItemPrefab is not assigned!");
                return;
            }
            NetworkLootItem lootItem = Instantiate(config.LootItemPrefab, spawnPosition, Quaternion.identity);
            NetworkObject lootNO = lootItem.GetComponent<NetworkObject>();
            if (lootNO == null)
            {
                Debug.LogError("[LootSpawnPoint] lootItemPrefab must have a NetworkObject component.");
                Destroy(lootItem.gameObject);
                return;
            }

            Spawn(lootNO);

            lootItem.ServerInitialize(lootDef, quantity);
            spawnedItems.Add(lootItem);
        }

        /// <summary>
        /// Manually trigger spawn (for OnDemand mode).
        /// </summary>
        [Server]
        public void TriggerSpawn()
        {
            SpawnLoot();
        }

        /// <summary>
        /// Get the spawn config (for centralized spawning).
        /// </summary>
        public LootSpawnConfig GetConfig()
        {
            return config;
        }

        private void OnDrawGizmosSelected()
        {
            if (!showGizmos)
                return;

            float radius = config != null ? config.SpawnRadius : 1f;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, radius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(transform.position, 0.2f);
        }
    }
}
