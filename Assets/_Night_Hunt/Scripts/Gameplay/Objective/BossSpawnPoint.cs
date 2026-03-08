using UnityEngine;
using FishNet.Object;

namespace NightHunt.Gameplay.Objective
{
    /// <summary>
    /// Boss spawn point
    /// </summary>
    public class BossSpawnPoint : MonoBehaviour
    {
        [Header("Boss Settings")]
        [SerializeField] private GameObject bossPrefab;
        [SerializeField] private bool hasSpawned = false;

        /// <summary>
        /// Spawn boss at this point
        /// </summary>
        public void SpawnBoss()
        {
            if (hasSpawned || bossPrefab == null) return;

            GameObject boss = Instantiate(bossPrefab, transform.position, transform.rotation);
            hasSpawned = true;

            // Spawn on network if needed
            var networkObject = boss.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                // Would need NetworkManager reference to spawn
            }
        }
    }
}
