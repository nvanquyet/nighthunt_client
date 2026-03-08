using UnityEngine;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.Systems.Weapon
{
    /// <summary>
    /// Handles ALL weapon visual effects for the local player's character.
    ///
    /// Attach to the same GameObject as WeaponSystem (or any child with access to it).
    ///
    /// Architecture:
    ///   • Subscribes to <see cref="IWeaponSystem.OnShotFired"/>.
    ///   • On each shot: reads <see cref="WeaponDefinition.MuzzleFlashPrefab"/>,
    ///     <see cref="WeaponDefinition.ProjectilePrefab"/>, <see cref="WeaponDefinition.BulletTrailPrefab"/>,
    ///     <see cref="WeaponDefinition.HitEffectPrefab"/> from the active weapon's definition.
    ///   • Spawns / plays them from <see cref="_muzzlePoint"/> — no centralized prefab refs on Character.
    ///
    /// Inspector setup:
    ///   • _weaponSystemSource – MonoBehaviour implementing IWeaponSystem (same GO as WeaponSystem)
    ///   • _muzzlePoint        – Transform at the barrel tip (child of weapon model)
    ///   • _muzzleFlashPool    – optional pool parent; prefabs are instantiated here if supplied
    /// </summary>
    public class WeaponVFXController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("MonoBehaviour that implements IWeaponSystem (e.g. the WeaponSystem component).")]
        [SerializeField] private MonoBehaviour _weaponSystemSource;

        [Tooltip("Transform at weapon barrel tip — origin for muzzle flash and projectile direction.")]
        [SerializeField] private Transform _muzzlePoint;

        [Tooltip("Parent transform used to keep instantiated VFX organised. If null, spawns at scene root.")]
        [SerializeField] private Transform _vfxPoolParent;

        // ── Runtime ──────────────────────────────────────────────────────────
        private IWeaponSystem _weaponSystem;

        // ── Unity Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            _weaponSystem = _weaponSystemSource as IWeaponSystem;
            if (_weaponSystem == null)
                Debug.LogWarning("[WeaponVFXController] _weaponSystemSource does not implement IWeaponSystem.");
        }

        private void OnEnable()
        {
            if (_weaponSystem != null)
                _weaponSystem.OnShotFired += HandleShotFired;
        }

        private void OnDisable()
        {
            if (_weaponSystem != null)
                _weaponSystem.OnShotFired -= HandleShotFired;
        }

        // ── Event Handler ─────────────────────────────────────────────────────

        private void HandleShotFired(WeaponSlotType slot, Vector3 aimDirection)
        {
            // Fetch weapon definition for this slot
            var inst = _weaponSystem.GetWeapon(slot);
            if (inst == null) return;

            var def = ItemDatabase.GetDefinition(inst.DefinitionID) as WeaponDefinition;
            if (def == null) return;

            Vector3 origin = _muzzlePoint != null ? _muzzlePoint.position : transform.position;
            Quaternion rot = aimDirection.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(aimDirection)
                : (_muzzlePoint != null ? _muzzlePoint.rotation : transform.rotation);

            // 1. Muzzle flash ──────────────────────────────────────────────────
            if (def.MuzzleFlashPrefab != null)
            {
                var flash = Spawn(def.MuzzleFlashPrefab, origin, rot);
                // Auto-destroy after 0.15 s if no Particle System manages its lifetime
                if (flash.GetComponentInChildren<ParticleSystem>() == null)
                    Destroy(flash, 0.15f);
            }

            // 2. Projectile (Projectile-type weapons only) ─────────────────────
            if (def.BallisticType == BallisticType.Projectile && def.ProjectilePrefab != null)
            {
                var proj = Spawn(def.ProjectilePrefab, origin, rot);
                // Give the projectile its direction if it has a Rigidbody
                var rb = proj.GetComponent<Rigidbody>();
                if (rb != null) rb.linearVelocity = aimDirection.normalized * rb.linearVelocity.magnitude;
            }

            // 3. Bullet trail (Hitscan) ────────────────────────────────────────
            if (def.BallisticType == BallisticType.Hitscan && def.BulletTrailPrefab != null)
            {
                // Trail: spawn at muzzle, trail renderer points toward aim direction.
                // Actual trail animation is managed by the prefab itself.
                var trail = Spawn(def.BulletTrailPrefab, origin, rot);
                Destroy(trail, 1f); // safety cleanup
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            return Instantiate(prefab, position, rotation, _vfxPoolParent);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Override the muzzle point at runtime (e.g. after weapon model swap).
        /// </summary>
        public void SetMuzzlePoint(Transform muzzle) => _muzzlePoint = muzzle;
    }
}
