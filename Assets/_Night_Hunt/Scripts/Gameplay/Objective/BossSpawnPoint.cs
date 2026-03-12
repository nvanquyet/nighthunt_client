using UnityEngine;
using FishNet.Object;
using NightHunt.Utilities;

namespace NightHunt.Gameplay.Objective
{
    /// <summary>
    /// Boss spawn point
    /// </summary>
    public class BossSpawnPoint : MonoBehaviour
    {
        [Header("Boss Settings")] [SerializeField]
        private GameObject bossPrefab;

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
            var networkObject = ComponentResolver.Find<NetworkObject>(boss)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] NetworkObject not found")
                .Resolve();
            if (networkObject != null)
            {
                // Would need NetworkManager reference to spawn
            }
        }
    }
}