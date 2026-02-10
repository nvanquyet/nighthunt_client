using UnityEngine;
using NightHunt.Data;
using FishNet.Object;

namespace NightHunt.Gameplay.Character.Combat.Weapons
{
    /// <summary>
    /// Local projectile spawner
    /// Spawns projectiles locally on client, syncs via network
    /// </summary>
    public class ProjectileSpawner : NetworkBehaviour
    {
        [Header("Projectile Settings")]
        [SerializeField] private GameObject projectilePrefab;

        //private LocalSpawnManager localSpawnManager;
        private uint nextProjectileId = 1;

        private void Awake()
        {
            //localSpawnManager = LocalSpawnManager.Instance;
        }

        /// <summary>
        /// Spawn projectile locally
        /// </summary>
        public void SpawnLocal(Vector3 position, Vector3 direction, WeaponConfigData weaponConfig)
        {
            if (projectilePrefab == null)
            {
                Debug.LogError("[ProjectileSpawner] Projectile prefab is null!");
                return;
            }

            // Spawn visual projectile locally
            GameObject projectile = Instantiate(projectilePrefab, position, Quaternion.LookRotation(direction));
            
            // Setup projectile component
            var projectileComponent = projectile.GetComponent<ProjectileComponent>();
            if (projectileComponent != null)
            {
                projectileComponent.Initialize(weaponConfig, direction, false);
            }

            // Register with local spawn manager
            // if (localSpawnManager != null)
            // {
            //     localSpawnManager.SpawnLocal(projectile, position, Quaternion.LookRotation(direction), nextProjectileId++);
            // }

            // Send to server for validation and broadcast
            if (IsOwner)
            {
                SendProjectileToServer(position, direction, weaponConfig);
            }
        }

        /// <summary>
        /// Send projectile data to server
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        private void SendProjectileToServer(Vector3 position, Vector3 direction, WeaponConfigData weaponConfig)
        {
            // Validate projectile
            // Broadcast to all clients
            BroadcastProjectileToClients(position, direction, weaponConfig);
        }

        /// <summary>
        /// Broadcast projectile to all clients
        /// </summary>
        [ObserversRpc]
        private void BroadcastProjectileToClients(Vector3 position, Vector3 direction, WeaponConfigData weaponConfig)
        {
            // Spawn projectile on other clients
            if (!IsOwner && projectilePrefab != null)
            {
                GameObject projectile = Instantiate(projectilePrefab, position, Quaternion.LookRotation(direction));
                var projectileComponent = projectile.GetComponent<ProjectileComponent>();
                if (projectileComponent != null)
                {
                    projectileComponent.Initialize(weaponConfig, direction, false);
                }
            }
        }
    }
}

