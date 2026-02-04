using UnityEngine;
using NightHunt.Inventory.Core.Events;
using System.Collections;

namespace NightHunt.Inventory.Domain.Container
{
    /// <summary>
    /// Spawns boss loot containers when bosses are defeated.
    /// Handles automatic despawning after configured delay.
    /// </summary>
    public class BossLootSpawner : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private GameObject containerPrefab;
        
        [Header("Visual Effects")]
        [SerializeField] private GameObject spawnVFX;
        [SerializeField] private GameObject despawnWarningVFX;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        
        #region Lifecycle
        
        void OnEnable()
        {
            BossLootEvents.OnBossDefeated += SpawnLoot;
        }
        
        void OnDisable()
        {
            BossLootEvents.OnBossDefeated -= SpawnLoot;
        }
        
        #endregion
        
        #region Loot Spawning
        
        private void SpawnLoot(Vector3 bossPosition, object lootConfigObj)
        {
            if (containerPrefab == null)
            {
                Debug.LogError("[BossLootSpawner] Container prefab not assigned!");
                return;
            }
            
            var lootConfig = lootConfigObj as ContainerConfig;
            if (lootConfig == null)
            {
                Debug.LogError("[BossLootSpawner] Invalid loot config!");
                return;
            }
            
            // Spawn container at boss position
            var containerObj = Instantiate(containerPrefab, bossPosition, Quaternion.identity);
            var containerComponent = containerObj.GetComponent<LootContainer>();
            
            if (containerComponent == null)
            {
                Debug.LogError("[BossLootSpawner] Container component not found on prefab!");
                Destroy(containerObj);
                return;
            }
            
            // Initialize with loot table
            containerComponent.Initialize(lootConfig);
            
            // Spawn VFX
            if (spawnVFX != null)
            {
                Instantiate(spawnVFX, bossPosition, Quaternion.identity);
            }
            
            // Start despawn timer
            StartCoroutine(DespawnAfterDelay(containerObj, lootConfig));
            
            if (enableDebugLogs)
                Debug.Log($"[BossLootSpawner] Spawned boss loot at {bossPosition}");
        }
        
        #endregion
        
        #region Despawn System
        
        private IEnumerator DespawnAfterDelay(GameObject container, ContainerConfig config)
        {
            // Wait for main delay
            float remainingTime = config.DespawnDelay;
            yield return new WaitForSeconds(remainingTime - config.DespawnWarning);
            
            // Show warning
            if (despawnWarningVFX != null)
            {
                var warningVFX = Instantiate(despawnWarningVFX, container.transform.position, Quaternion.identity);
                warningVFX.transform.SetParent(container.transform);
            }
            
            if (enableDebugLogs)
                Debug.Log($"[BossLootSpawner] Loot will despawn in {config.DespawnWarning}s");
            
            // Wait for warning period
            yield return new WaitForSeconds(config.DespawnWarning);
            
            // Despawn
            if (enableDebugLogs)
                Debug.Log("[BossLootSpawner] Loot despawned");
            
            Destroy(container);
        }
        
        #endregion
    }
}