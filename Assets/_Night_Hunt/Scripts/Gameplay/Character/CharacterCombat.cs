using UnityEngine;
using System.Collections;
using NightHunt.Gameplay.Core;
using NightHunt.Networking;
using NightHunt.InteractionSystem.Utilities;

namespace NightHunt.Gameplay.Character
{
    /// <summary>
    /// Handles character combat: weapons, shooting, damage, reloading
    /// Supports both Hitscan and Projectile weapons
    /// </summary>
    public class CharacterCombat : MonoBehaviour
    {
        [Header("Combat Settings")]
        [SerializeField] private Transform firePoint;
        [SerializeField] private LayerMask hitLayers = -1;
        [SerializeField] private GameObject projectilePrefab; // For projectile weapons

        [Header("Visual Effects")]
        [SerializeField] private GameObject muzzleFlashPrefab;
        [SerializeField] private GameObject hitEffectPrefab;

        // TODO: Replace with WeaponConfig ScriptableObject when implemented
        private object currentWeapon; // Was: WeaponConfigData
        private CharacterStats characterStats;
        private CharacterPredictedMovement _characterPredictedMovement;

        // Weapon state
        private int currentAmmo;
        private int reserveAmmo;
        private bool isReloading;
        private bool isAttacking;
        private float lastFireTime;
        private Vector3 aimDirection;

        // Recoil
        private float currentSpread;

        private void Awake()
        {
            // Use ComponentFinder to find components in hierarchy (supports child objects)
            characterStats = gameObject.FindInHierarchy<CharacterStats>();
            _characterPredictedMovement = gameObject.FindInHierarchy<CharacterPredictedMovement>();

            if (firePoint == null)
            {
                firePoint = transform;
            }
            
            // Register in ComponentRegistry (event-based, no FindObject after this)
            RegisterInComponentRegistry();
        }

        /// <summary>
        /// Register this component in ComponentRegistry
        /// </summary>
        private void RegisterInComponentRegistry()
        {
            NetworkPlayer networkPlayer = gameObject.FindInHierarchy<NetworkPlayer>();
            if (networkPlayer != null)
            {
                ComponentRegistry.RegisterCharacterCombat(networkPlayer, this);
            }
        }

        private void OnDestroy()
        {
            NetworkPlayer networkPlayer = gameObject.FindInHierarchy<NetworkPlayer>();
            if (networkPlayer != null)
            {
                ComponentRegistry.UnregisterCharacterCombat(networkPlayer, this);
            }
        }

        private void Start()
        {
            // Default weapon (can be changed later)
            EquipWeapon("PISTOL_9MM");
        }

        /// <summary>
        /// Equip a weapon by ID
        /// TODO: Implement WeaponConfig ScriptableObject system to replace GameConfigLoader
        /// </summary>
        public void EquipWeapon(string weaponId)
        {
            // TODO: Load weapon config from ScriptableObject registry
            // For now, weapon equipping is disabled until WeaponConfig system is implemented
            Debug.LogWarning($"[CharacterCombat] EquipWeapon({weaponId}) - Weapon system needs WeaponConfig ScriptableObject implementation");
            currentWeapon = null;
            return;
            
            /* OLD CODE - REMOVED (GameConfigLoader dependency)
            currentWeapon = GameConfigLoader.Instance?.GetWeaponConfig(weaponId);
            if (currentWeapon == null)
            {
                Debug.LogWarning($"[CharacterCombat] Weapon not found: {weaponId}");
                return;
            }

            currentAmmo = currentWeapon.MagazineSize;
            reserveAmmo = currentWeapon.ReserveAmmo;
            isReloading = false;
            currentSpread = currentWeapon.SpreadBase;
            Debug.Log($"[CharacterCombat] Equipped: {currentWeapon.DisplayName}");
            */
        }

        /// <summary>
        /// Set aim direction
        /// </summary>
        public void SetAimDirection(Vector3 direction)
        {
            aimDirection = direction.normalized;
        }

        /// <summary>
        /// Set attacking state
        /// </summary>
        public void SetAttacking(bool attacking)
        {
            isAttacking = attacking;
        }

        /// <summary>
        /// Set reloading state
        /// </summary>
        public void SetReloading(bool reloading)
        {
            if (reloading && !isReloading && CanReload())
            {
                StartCoroutine(ReloadCoroutine());
            }
        }

        private void Update()
        {
            if (currentWeapon == null) return;

            // Handle firing
            if (isAttacking && CanFire())
            {
                Fire();
            }

            // Update spread (recovery)
            // TODO: Access weapon config when WeaponConfig ScriptableObject is implemented
            if (Time.time - lastFireTime > 0.1f)
            {
                // currentSpread = Mathf.Lerp(currentSpread, currentWeapon.SpreadBase, Time.deltaTime * 5f);
                currentSpread = Mathf.Lerp(currentSpread, 0.1f, Time.deltaTime * 5f); // Placeholder
            }
        }

        private bool CanFire()
        {
            if (currentWeapon == null) return false;
            if (isReloading) return false;
            if (currentAmmo <= 0) return false;

            float timeSinceLastFire = Time.time - lastFireTime;
            // TODO: Access weapon config when WeaponConfig ScriptableObject is implemented
            // float fireInterval = 1f / currentWeapon.FireRate;
            float fireInterval = 0.1f; // Placeholder
            return timeSinceLastFire >= fireInterval;
        }

        private bool CanReload()
        {
            if (currentWeapon == null) return false;
            if (isReloading) return false;
            // TODO: Access weapon config when WeaponConfig ScriptableObject is implemented
            // if (currentAmmo >= currentWeapon.MagazineSize) return false;
            if (currentAmmo >= 30) return false; // Placeholder magazine size
            if (reserveAmmo <= 0) return false;
            return true;
        }

        private void Fire()
        {
            if (currentWeapon == null) return;

            lastFireTime = Time.time;
            currentAmmo--;

            // Calculate spread
            float spread = currentSpread;
            if (_characterPredictedMovement != null && _characterPredictedMovement.GetCurrentMoveSpeed() > 0.1f)
            {
                // TODO: Access weapon config when WeaponConfig ScriptableObject is implemented
                // spread *= currentWeapon.SpreadMoveMul;
                spread *= 1.5f; // Placeholder
            }

            // Calculate fire direction with spread
            Vector3 fireDirection = ApplySpread(aimDirection, spread);

            // TODO: Access weapon config when WeaponConfig ScriptableObject is implemented
            // Fire based on weapon type
            // if (currentWeapon.BallisticType == "Hitscan")
            // {
                FireHitscan(fireDirection); // Default to hitscan for now
            // }
            // else if (currentWeapon.BallisticType == "Projectile")
            // {
            //     FireProjectile(fireDirection);
            // }

            // Update spread
            currentSpread = Mathf.Min(currentSpread + 0.1f, 1f);

            // Visual effects
            if (muzzleFlashPrefab != null && firePoint != null)
            {
                Instantiate(muzzleFlashPrefab, firePoint.position, firePoint.rotation);
            }
        }

        private Vector3 ApplySpread(Vector3 direction, float spread)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float distance = Random.Range(0f, spread);
            Vector3 spreadOffset = new Vector3(
                Mathf.Cos(angle) * distance,
                0f,
                Mathf.Sin(angle) * distance
            );
            return (direction + spreadOffset).normalized;
        }

        private void FireHitscan(Vector3 direction)
        {
            RaycastHit hit;
            Vector3 startPos = firePoint != null ? firePoint.position : transform.position;
            // TODO: Access weapon config when WeaponConfig ScriptableObject is implemented
            // float maxRange = currentWeapon.MaxRange;
            float maxRange = 100f; // Placeholder
            Vector3 endPos = startPos + direction * maxRange;

            if (Physics.Raycast(startPos, direction, out hit, maxRange, hitLayers))
            {
                // Hit something
                endPos = hit.point;

                // Check if hit a character - use ComponentFinder to search in hierarchy (including children)
                var hitCharacter = NightHunt.InteractionSystem.Utilities.ComponentFinder.FindComponentInHierarchy<CharacterStats>(hit.collider.gameObject, includeInactive: false);
                if (hitCharacter != null)
                {
                    // TODO: Access weapon config when WeaponConfig ScriptableObject is implemented
                    // Calculate damage
                    // float damage = currentWeapon.DamageBody;
                    float damage = 10f; // Placeholder
                    
                    // Check for headshot (simplified - check if hit upper body)
                    bool isHeadshot = hit.collider.CompareTag("Head");
                    if (isHeadshot)
                    {
                        // damage *= currentWeapon.DamageHeadMul;
                        damage *= 2f; // Placeholder headshot multiplier
                    }

                    // Apply damage
                    hitCharacter.TakeDamage(damage);
                }

                // Hit effect
                if (hitEffectPrefab != null)
                {
                    Instantiate(hitEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));
                }
            }

            // Debug line
            Debug.DrawLine(startPos, endPos, Color.red, 0.1f);
        }

        private void FireProjectile(Vector3 direction)
        {
            if (projectilePrefab == null)
            {
                Debug.LogWarning("[CharacterCombat] Projectile prefab not set!");
                return;
            }

            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
            GameObject projectile = Instantiate(projectilePrefab, spawnPos, Quaternion.LookRotation(direction));
            
            // Setup projectile (would need a Projectile component)
            var projectileComponent = projectile.GetComponent<Projectile>();
            if (projectileComponent != null)
            {
                projectileComponent.Initialize(currentWeapon, direction);
            }
        }

        private IEnumerator ReloadCoroutine()
        {
            isReloading = true;
            // TODO: Access weapon config when WeaponConfig ScriptableObject is implemented
            // yield return new WaitForSeconds(currentWeapon.ReloadTime);
            yield return new WaitForSeconds(2f); // Placeholder reload time

            // Reload logic
            // TODO: Access weapon config when WeaponConfig ScriptableObject is implemented
            // int ammoNeeded = currentWeapon.MagazineSize - currentAmmo;
            int ammoNeeded = 30 - currentAmmo; // Placeholder magazine size
            int ammoToReload = Mathf.Min(ammoNeeded, reserveAmmo);

            currentAmmo += ammoToReload;
            reserveAmmo -= ammoToReload;

            isReloading = false;
            Debug.Log($"[CharacterCombat] Reloaded. Ammo: {currentAmmo}/{reserveAmmo}");
        }
        // Public getters
        // TODO: Replace return type with WeaponConfig ScriptableObject when implemented
        public object GetCurrentWeapon() => currentWeapon; // Was: WeaponConfigData
        public int GetCurrentAmmo() => currentAmmo;
        public int GetReserveAmmo() => reserveAmmo;
        public bool IsReloading() => isReloading;
    }

    /// <summary>
    /// Projectile component for projectile weapons
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        // TODO: Replace with WeaponConfig ScriptableObject when implemented
        private object weaponConfig; // Was: WeaponConfigData
        private Vector3 direction;
        private float speed;
        private float lifetime;

        // TODO: Replace parameter type with WeaponConfig ScriptableObject when implemented
        public void Initialize(object config, Vector3 dir) // Was: WeaponConfigData config
        {
            weaponConfig = config;
            direction = dir;
            // TODO: Access weapon config properties when WeaponConfig ScriptableObject is implemented
            // speed = config.ProjectileSpeed;
            // lifetime = config.MaxRange / speed;
            speed = 10f; // Placeholder
            lifetime = 5f; // Placeholder
        }

        private void Update()
        {
            // Move projectile
            Vector3 movement = direction * speed * Time.deltaTime;
            // TODO: Access weapon config when WeaponConfig ScriptableObject is implemented
            // movement.y -= weaponConfig.GravityScale * 9.81f * Time.deltaTime;
            movement.y -= 1f * 9.81f * Time.deltaTime; // Placeholder gravity
            transform.position += movement;

            // Update direction based on gravity
            direction = movement.normalized;

            // Lifetime
            lifetime -= Time.deltaTime;
            if (lifetime <= 0f)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Handle hit - use ComponentFinder to search in hierarchy (including children)
            var character = NightHunt.InteractionSystem.Utilities.ComponentFinder.FindComponentInHierarchy<CharacterStats>(other.gameObject, includeInactive: false);
            if (character != null)
            {
                // TODO: Get damage from WeaponConfig ScriptableObject when implemented
                // character.TakeDamage(weaponConfig.DamageBody);
                character.TakeDamage(10f); // Placeholder damage
            }

            Destroy(gameObject);
        }
    }
}


