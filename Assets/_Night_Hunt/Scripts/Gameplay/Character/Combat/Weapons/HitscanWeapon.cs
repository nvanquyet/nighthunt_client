using UnityEngine;
using NightHunt.Data;
using NightHunt.Gameplay.Core.Events;
using NightHunt.Gameplay.Core.Utils;
namespace NightHunt.Gameplay.Character.Combat.Weapons
{
    /// <summary>
    /// Hitscan weapon implementation
    /// </summary>
    public class HitscanWeapon : WeaponBase
    {
        [Header("Hitscan Settings")]
        [SerializeField] private LayerMask hitLayers = -1;
        [SerializeField] private GameObject hitEffectPrefab;

        private float lastFireTime;

        public override void Fire(Vector3 direction)
        {
            if (!CanFire()) return;

            lastFireTime = Time.time;
            currentAmmo--;

            // Perform hitscan raycast
            Vector3 startPos = firePoint != null ? firePoint.position : transform.position;
            RaycastHit hit;

            if (Physics.Raycast(startPos, direction, out hit, weaponConfig.MaxRange, hitLayers))
            {
                // Hit something
                ProcessHit(hit);
            }

            // Spawn visual effect (always spawn for visual feedback)
            SpawnVisualEffect(direction, hit.point);
        }

        /// <summary>
        /// Process hit
        /// </summary>
        private void ProcessHit(RaycastHit hit)
        {
            // Check if hit a character
            // var hitCharacter = hit.collider.GetComponent<PlayerStats>();
            // if (hitCharacter != null)
            // {
            //     // Calculate damage
            //     float damage = weaponConfig.DamageBody;
            //     
            //     // Check for headshot
            //     bool isHeadshot = hit.collider.CompareTag("Head");
            //     if (isHeadshot)
            //     {
            //         damage *= weaponConfig.DamageHeadMul;
            //     }
            //
            //     // Apply damage (server authority)
            //     // Send RPC to server for damage application once server-authoritative combat is added.
            //     hitCharacter.TakeDamage(damage);
            //
            //     // Publish damage event
            //     var damageEvent = new DamageEffectEvent
            //     {
            //         Damage = damage,
            //         HitPoint = hit.point,
            //         HitDirection = hit.normal,
            //         NetworkId = hitCharacter.GetInstanceID()
            //     };
            //     GameplayEventBus.Instance?.Publish(damageEvent);
            // }

            // Spawn hit effect
            if (hitEffectPrefab != null)
            {
                SpawnUtils.SpawnPrefab(hitEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));
            }
        }

        /// <summary>
        /// Spawn visual effect (local spawn)
        /// </summary>
        private void SpawnVisualEffect(Vector3 direction, Vector3 hitPoint)
        {
            // Spawn muzzle flash
            // Spawn bullet trail
            // These are visual only, spawned locally
        }

        public override void Reload()
        {
            if (isReloading || currentAmmo >= weaponConfig.MagazineSize || reserveAmmo <= 0)
                return;

            isReloading = true;
            // Reload logic would be handled by coroutine or async
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

