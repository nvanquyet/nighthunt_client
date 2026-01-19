using UnityEngine;
using NightHunt.Data;

namespace NightHunt.Gameplay.Character.Combat.Weapons
{
    /// <summary>
    /// Base weapon class
    /// </summary>
    public abstract class WeaponBase : MonoBehaviour
    {
        [Header("Weapon Settings")]
        [SerializeField] protected WeaponConfigData weaponConfig;
        [SerializeField] protected Transform firePoint;

        protected int currentAmmo;
        protected int reserveAmmo;
        protected bool isReloading;

        public WeaponConfigData WeaponConfig => weaponConfig;
        public int CurrentAmmo => currentAmmo;
        public int ReserveAmmo => reserveAmmo;
        public bool IsReloading => isReloading;

        protected virtual void Awake()
        {
            if (firePoint == null)
            {
                firePoint = transform;
            }
        }

        /// <summary>
        /// Initialize weapon with config
        /// </summary>
        public virtual void Initialize(WeaponConfigData config)
        {
            weaponConfig = config;
            currentAmmo = config.MagazineSize;
            reserveAmmo = config.ReserveAmmo;
        }

        /// <summary>
        /// Fire weapon
        /// </summary>
        public abstract void Fire(Vector3 direction);

        /// <summary>
        /// Reload weapon
        /// </summary>
        public abstract void Reload();

        /// <summary>
        /// Check if can fire
        /// </summary>
        public abstract bool CanFire();
    }
}

