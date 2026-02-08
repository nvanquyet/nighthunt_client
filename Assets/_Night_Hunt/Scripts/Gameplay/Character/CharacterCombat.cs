using UnityEngine;
using NightHunt.Data;
using System.Collections;
using NightHunt.Inventory.Stats;

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

        private WeaponConfigData currentWeapon;
        private CharacterStats characterStats;
        private IMovementController _characterPredictedMovement;

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
            characterStats = GetComponent<CharacterStats>();
            _characterPredictedMovement = GetComponent<IMovementController>();

            if (firePoint == null)
            {
                firePoint = transform;
            }
        }

        private void Start()
        {
            // Default weapon (can be changed later)
            EquipWeapon("PISTOL_9MM");
        }

        /// <summary>
        /// Equip a weapon by ID
        /// </summary>
        public void EquipWeapon(string weaponId)
        {
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
            if (Time.time - lastFireTime > 0.1f)
            {
                currentSpread = Mathf.Lerp(currentSpread, currentWeapon.SpreadBase, Time.deltaTime * 5f);
            }
        }

        private bool CanFire()
        {
            if (currentWeapon == null) return false;
            if (isReloading) return false;
            if (currentAmmo <= 0) return false;

            float timeSinceLastFire = Time.time - lastFireTime;
            float fireInterval = 1f / currentWeapon.FireRate;
            return timeSinceLastFire >= fireInterval;
        }

        private bool CanReload()
        {
            if (currentWeapon == null) return false;
            if (isReloading) return false;
            if (currentAmmo >= currentWeapon.MagazineSize) return false;
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
                spread *= currentWeapon.SpreadMoveMul;
            }

            // Calculate fire direction with spread
            Vector3 fireDirection = ApplySpread(aimDirection, spread);

            // Fire based on weapon type
            if (currentWeapon.BallisticType == "Hitscan")
            {
                FireHitscan(fireDirection);
            }
            else if (currentWeapon.BallisticType == "Projectile")
            {
                FireProjectile(fireDirection);
            }

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
            Vector3 endPos = startPos + direction * currentWeapon.MaxRange;

            if (Physics.Raycast(startPos, direction, out hit, currentWeapon.MaxRange, hitLayers))
            {
                // Hit something
                endPos = hit.point;

                // Check if hit a character
                var hitCharacter = hit.collider.GetComponent<CharacterStats>();
                if (hitCharacter != null)
                {
                    // Calculate damage
                    float damage = currentWeapon.DamageBody;
                    
                    // Check for headshot (simplified - check if hit upper body)
                    bool isHeadshot = hit.collider.CompareTag("Head");
                    if (isHeadshot)
                    {
                        damage *= currentWeapon.DamageHeadMul;
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
            yield return new WaitForSeconds(currentWeapon.ReloadTime);

            // Reload logic
            int ammoNeeded = currentWeapon.MagazineSize - currentAmmo;
            int ammoToReload = Mathf.Min(ammoNeeded, reserveAmmo);

            currentAmmo += ammoToReload;
            reserveAmmo -= ammoToReload;

            isReloading = false;
            Debug.Log($"[CharacterCombat] Reloaded. Ammo: {currentAmmo}/{reserveAmmo}");
        }
        // Public getters
        public WeaponConfigData GetCurrentWeapon() => currentWeapon;
        public int GetCurrentAmmo() => currentAmmo;
        public int GetReserveAmmo() => reserveAmmo;
        public bool IsReloading() => isReloading;
    }

    /// <summary>
    /// Projectile component for projectile weapons
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        private WeaponConfigData weaponConfig;
        private Vector3 direction;
        private float speed;
        private float lifetime;

        public void Initialize(WeaponConfigData config, Vector3 dir)
        {
            weaponConfig = config;
            direction = dir;
            speed = config.ProjectileSpeed;
            lifetime = config.MaxRange / speed;
        }

        private void Update()
        {
            // Move projectile
            Vector3 movement = direction * speed * Time.deltaTime;
            movement.y -= weaponConfig.GravityScale * 9.81f * Time.deltaTime;
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
            // Handle hit
            var character = other.GetComponent<CharacterStats>();
            if (character != null)
            {
                character.TakeDamage(weaponConfig.DamageBody);
            }

            Destroy(gameObject);
        }
    }
}


