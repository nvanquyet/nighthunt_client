using System.Collections;
using UnityEngine;
using NightHunt.Data;

namespace NightHunt.Gameplay.Character.Combat.Weapons
{
    /// <summary>
    /// Weapon bắn đạn (bullet/projectile).
    ///
    /// Prefab weapon cần có:
    ///   FirePoint        — empty child ở đầu nòng.
    ///   MuzzleFlashChild — child particle, inactive mặc định; bật/tắt khi Fire().
    ///
    /// Projectile prefab được gán trên ProjectileSpawner.projectilePrefab.
    /// Prefab chứa ProjectileComponent (kế thừa ProjectileBase), VFX là child objects.
    /// </summary>
    public class ProjectileWeapon : WeaponBase
    {
        [Header("Projectile Settings")]
        [SerializeField] private bool       useHitscanForLogic = false;
        [SerializeField] private LayerMask  hitLayers          = -1;

        [Header("Weapon VFX Children")]
        [Tooltip("Child particle trên weapon prefab — bật khi Fire, tắt tự động.")]
        [SerializeField] private GameObject muzzleFlashChild;
        [Tooltip("Giây muzzle flash tắt sau khi bắn.")]
        [SerializeField] private float      muzzleFlashDuration = 0.08f;

        private float            _lastFireTime;
        private ProjectileSpawner _projectileSpawner;

        // -----------------------------------------------------------------
        protected override void Awake()
        {
            base.Awake();
            _projectileSpawner = GetComponent<ProjectileSpawner>();
            if (_projectileSpawner == null)
                _projectileSpawner = gameObject.AddComponent<ProjectileSpawner>();

            // Đảm bảo muzzle flash tắt lúc đầu
            if (muzzleFlashChild != null)
                muzzleFlashChild.SetActive(false);
        }

        // -----------------------------------------------------------------
        public override void Fire(Vector3 direction)
        {
            if (!CanFire()) return;

            _lastFireTime = Time.time;
            currentAmmo--;

            Vector3 startPos = firePoint != null ? firePoint.position : transform.position;

            // Muzzle flash — bật rồi tắt sau muzzleFlashDuration
            PlayMuzzleFlash();

            // Spawn projectile:
            // ProjectileSpawner xử lý cả local instance + network broadcast.
            // Owner sẽ thấy bản local; các client khác nhận bản broadcast.
            _projectileSpawner.SpawnLocal(startPos, direction, weaponConfig);

            // Hitscan: raycast ngay lập tức để tính damage server-side
            if (useHitscanForLogic)
                ProcessHitscanLogic(startPos, direction);
        }

        // -----------------------------------------------------------------
        // Muzzle Flash
        // -----------------------------------------------------------------
        private void PlayMuzzleFlash()
        {
            if (muzzleFlashChild == null) return;
            muzzleFlashChild.SetActive(true);

            // Restart particle nếu có
            foreach (var ps in muzzleFlashChild.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(true);
            }

            StartCoroutine(DisableMuzzleFlashAfter(muzzleFlashDuration));
        }

        private IEnumerator DisableMuzzleFlashAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (muzzleFlashChild != null)
                muzzleFlashChild.SetActive(false);
        }

        // -----------------------------------------------------------------
        // Hitscan
        // -----------------------------------------------------------------
        private void ProcessHitscanLogic(Vector3 startPos, Vector3 direction)
        {
            if (Physics.Raycast(startPos, direction, out RaycastHit hit, weaponConfig.MaxRange, hitLayers))
                ProcessHit(hit);
        }

        private void ProcessHit(RaycastHit hit)
        {
            // TODO: gửi ServerRpc để server apply damage
            // var hitStat = hit.collider.GetComponent<IPlayerStats>();
            // if (hitStat != null)
            //     RequestDamageServerRpc(hitStat.OwnerId, weaponConfig.DamageBody, hit.collider.CompareTag("Head"));
        }

        // -----------------------------------------------------------------
        public override void Reload()
        {
            if (isReloading || currentAmmo >= weaponConfig.MagazineSize || reserveAmmo <= 0)
                return;
            isReloading = true;
        }

        public override bool CanFire()
        {
            if (weaponConfig == null) return false;
            if (isReloading)         return false;
            if (currentAmmo <= 0)    return false;

            float timeSinceLastFire = Time.time - _lastFireTime;
            float fireInterval      = 1f / weaponConfig.FireRate;
            return timeSinceLastFire >= fireInterval;
        }
    }
}

