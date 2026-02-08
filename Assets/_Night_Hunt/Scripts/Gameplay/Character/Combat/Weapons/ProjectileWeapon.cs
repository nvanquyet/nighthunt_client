using UnityEngine;
using NightHunt.Data;
using NightHunt.Gameplay.Core.Networking;
using NightHunt.Gameplay.Core.Events;
using NightHunt.Networking;
using FishNet.Object;
using NightHunt.Inventory.Stats;

namespace NightHunt.Gameplay.Character.Combat.Weapons
{
    /// <summary>
    /// Projectile weapon với local spawn
    /// Visual bullets always spawn, collision logic uses hitscan or collider based on config
    /// </summary>
    public class ProjectileWeapon : WeaponBase
    {
        [Header("Projectile Settings")]
        [SerializeField] private GameObject visualProjectilePrefab; // Always spawn for visual
        [SerializeField] private bool useHitscanForLogic = false; // If true, use hitscan; if false, use collider
        [SerializeField] private LayerMask hitLayers = -1;

        private float lastFireTime;
        private ProjectileSpawner projectileSpawner;

        protected override void Awake()
        {
            base.Awake();
            projectileSpawner = GetComponent<ProjectileSpawner>();
            if (projectileSpawner == null)
            {
                projectileSpawner = gameObject.AddComponent<ProjectileSpawner>();
            }
        }

        public override void Fire(Vector3 direction)
        {
            if (!CanFire()) return;

            lastFireTime = Time.time;
            currentAmmo--;

            Vector3 startPos = firePoint != null ? firePoint.position : transform.position;

            // Always spawn visual projectile locally
            if (visualProjectilePrefab != null)
            {
                SpawnVisualProjectile(startPos, direction);
            }

            // Send to server for validation and broadcast
            var networkPlayer = GetComponentInParent<NetworkPlayer>();
            if (networkPlayer != null && networkPlayer.IsOwner)
            {
                SendProjectileToServer(startPos, direction);
            }

            // Process collision logic based on config
            if (useHitscanForLogic)
            {
                ProcessHitscanLogic(startPos, direction);
            }
            // Otherwise, collision is handled by projectile collider
        }

        /// <summary>
        /// Spawn visual projectile locally
        /// </summary>
        private void SpawnVisualProjectile(Vector3 position, Vector3 direction)
        {
            if (projectileSpawner != null)
            {
                projectileSpawner.SpawnLocal(position, direction, weaponConfig);
            }
            else if (visualProjectilePrefab != null)
            {
                GameObject projectile = Instantiate(visualProjectilePrefab, position, Quaternion.LookRotation(direction));
                var projectileComponent = projectile.GetComponent<ProjectileComponent>();
                if (projectileComponent != null)
                {
                    projectileComponent.Initialize(weaponConfig, direction, useHitscanForLogic);
                }
            }
        }

        /// <summary>
        /// Send projectile data to server
        /// </summary>
        private void SendProjectileToServer(Vector3 position, Vector3 direction)
        {
            // TODO: Implement RPC to send projectile data to server
            // Server will validate and broadcast to other clients
        }

        /// <summary>
        /// Process hitscan logic (if config says use hitscan)
        /// </summary>
        private void ProcessHitscanLogic(Vector3 startPos, Vector3 direction)
        {
            RaycastHit hit;
            if (Physics.Raycast(startPos, direction, out hit, weaponConfig.MaxRange, hitLayers))
            {
                ProcessHit(hit);
            }
        }

        /// <summary>
        /// Process hit
        /// </summary>
        private void ProcessHit(RaycastHit hit)
        {
            var hitCharacter = hit.collider.GetComponent<CharacterStats>();
            if (hitCharacter != null)
            {
                float damage = weaponConfig.DamageBody;
                bool isHeadshot = hit.collider.CompareTag("Head");
                if (isHeadshot)
                {
                    damage *= weaponConfig.DamageHeadMul;
                }

                // TODO: Send RPC to server for damage application
                hitCharacter.TakeDamage(damage);
            }
        }

        public override void Reload()
        {
            if (isReloading || currentAmmo >= weaponConfig.MagazineSize || reserveAmmo <= 0)
                return;

            isReloading = true;
        }

        public override bool CanFire()
        {
            if (weaponConfig == null) return false;
            if (isReloading) return false;
            if (currentAmmo <= 0) return false;

            float timeSinceLastFire = Time.time - lastFireTime;
            float fireInterval = 1f / weaponConfig.FireRate;
            return timeSinceLastFire >= fireInterval;
        }
    }
}

