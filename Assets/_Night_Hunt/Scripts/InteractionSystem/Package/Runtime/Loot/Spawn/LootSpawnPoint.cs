using UnityEngine;
using FishNet.Object;
using System.Collections.Generic;
using NightHunt.InteractionSystem.Loot.Definitions;
using NightHunt.InteractionSystem.Items.Runtime;

namespace NightHunt.InteractionSystem.Loot.Spawn
{
    /// <summary>
    /// Loot spawn point that spawns items based on a LootTable ScriptableObject.
    /// Only manages spawn timing and mode - config is in LootTable.
    /// </summary>
    public class LootSpawnPoint : NetworkBehaviour
    {
        [Header("Loot Table")]
        [Tooltip("Reference to LootTable ScriptableObject (contains all spawn config)")]
        [SerializeField] private LootTable lootTable;

        [Header("Prefab")]
        [Tooltip("Generic NetworkLootItem prefab to spawn (will be initialized with loot data)")]
        [SerializeField] private NetworkLootItem lootItemPrefab;

        [Header("Spawn Settings")]
        [Tooltip("Radius around spawn point to randomly place items")]
        [SerializeField] private float spawnRadius = 1f;

        [Header("Spawn Timing")]
        [SerializeField] private SpawnMode spawnMode = SpawnMode.Once;
        [SerializeField] private float respawnInterval = 300f; // 5 minutes default
        [SerializeField] private float initialDelay = 0f;

        [Header("Visual")]
        [SerializeField] private GameObject spawnEffectPrefab;
        [SerializeField] private bool showGizmos = true;

        private float lastSpawnTime = 0f;
        private bool hasSpawned = false;
        private List<NetworkLootItem> spawnedItems = new List<NetworkLootItem>();

        public enum SpawnMode
        {
            Once,           // Spawn once when scene starts
            Interval,       // Spawn every N seconds
            OnDemand        // Spawn manually via code
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

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
            if (lootItemPrefab == null)
            {
                Debug.LogError("[LootSpawnPoint] lootItemPrefab is not assigned. Please set a NetworkLootItem prefab.");
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
            var generatedItems = lootTable.GenerateLoot();
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
            if (spawnEffectPrefab != null)
            {
                GameObject effect = Instantiate(spawnEffectPrefab, transform.position, Quaternion.identity);
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
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            Vector3 spawnPosition = transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);

            // Spawn generic NetworkLootItem prefab and inject definition at runtime (server)
            NetworkLootItem lootItem = Instantiate(lootItemPrefab, spawnPosition, Quaternion.identity);
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

        private void OnDrawGizmosSelected()
        {
            if (!showGizmos)
                return;

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, spawnRadius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(transform.position, 0.2f);
        }
    }
}
