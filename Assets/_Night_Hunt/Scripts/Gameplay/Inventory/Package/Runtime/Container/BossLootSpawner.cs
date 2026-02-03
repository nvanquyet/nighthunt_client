using System.Collections;
using UnityEngine;
using NightHunt.Inventory.Container;

namespace NightHunt.Inventory.Container
{
    /// <summary>
    /// Spawns boss loot container when boss is defeated.
    /// </summary>
    public class BossLootSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject containerPrefab;
        
        void OnEnable()
        {
            // TODO: Subscribe to BossLootEvents.OnBossDefeated when available
            // BossLootEvents.OnBossDefeated += SpawnLoot;
        }
        
        void OnDisable()
        {
            // TODO: Unsubscribe
            // BossLootEvents.OnBossDefeated -= SpawnLoot;
        }
        
        void SpawnLoot(Vector3 bossPosition, ContainerConfig lootTable)
        {
            // Spawn container at boss position
            var container = Instantiate(containerPrefab, bossPosition, Quaternion.identity);
            var containerComponent = container.GetComponent<Container>();
            
            // Initialize with loot table
            containerComponent.Initialize(lootTable);
            
            // Start despawn timer
            StartCoroutine(DespawnAfterDelay(container, lootTable.DespawnDelay));
        }
        
        private IEnumerator DespawnAfterDelay(GameObject container, float delay)
        {
            yield return new WaitForSeconds(delay);
            
            // TODO: Show warning visual before despawn (3s countdown)
            yield return new WaitForSeconds(3f);
            
            Destroy(container);
        }
    }
}
