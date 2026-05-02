using UnityEngine;
using NightHunt.Data;

namespace NightHunt.Gameplay.Character.Combat.Weapons
{
    /// <summary>
    /// Concrete weapon component for physical-projectile weapons (rocket launchers, grenade launchers).
    ///
    /// BALLISTIC MODEL:
    ///   Each shot spawns ONE physics-based ProjectileComponent from the pool.
    ///   The projectile moves under gravity (configurable) and applies damage on collision.
    ///   Client projectiles are visual-only in production. Server-side WeaponSystem resolves
    ///   projectile travel and damage.
    ///
    /// VFX:
    ///   All effects (muzzle flash, trail, detonation / explosion) are owned by the
    ///   ProjectileComponent prefab. This class has no VFX logic.
    ///
    /// SETUP:
    ///   1. Attach this component to the weapon model prefab.
    ///   2. Assign a ProjectileComponent prefab that has a Rigidbody-free
    ///      (or kinematic) setup — the ProjectileComponent moves it via Transform.
    ///   3. Tune maxRange, spreadBase, spreadPenaltyPerShot on this component.
    ///   4. Set the projectile prefab's gravityScale for the ballistic arc.
    /// </summary>
    public sealed class ProjectileWeapon : WeaponBase
    {
        // ── WeaponBase.Fire implementation ────────────────────────────────────
        public override void Fire(Vector3 origin, Vector3 direction,
                                  WeaponConfigData config, int shooterNetObjId)
        {
            RecordShot();   // accumulate spread before applying

            Vector3 fireDir = ApplyRadialSpread(direction);

            var pool = ProjectilePool.Instance;
            if (pool == null)
                Debug.LogWarning("[ProjectileWeapon] ProjectilePool not found in scene. Instantiating projectile directly.", this);

            if (projectilePrefab == null)
            {
                Debug.LogWarning("[ProjectileWeapon] projectilePrefab not assigned.", this);
                return;
            }

            var proj = pool != null
                ? pool.Get(projectilePrefab, origin, Quaternion.LookRotation(fireDir))
                : Instantiate(projectilePrefab, origin, Quaternion.LookRotation(fireDir)).GetComponent<ProjectileComponent>();
            if (proj == null)
            {
                Debug.LogWarning($"[SHOOT.PLAYER] ProjectileWeapon.Fire — pool.Get() returned null. " +
                                 $"Increase ProjectilePool capacity for '{projectilePrefab.name}'.");
                return;
            }

            // Prevent the bullet from immediately triggering on the shooter's own colliders.
            var shooterRoot = transform.root;
            proj.SetIgnoredRoot(shooterRoot);
            var ownerCols   = shooterRoot.GetComponentsInChildren<Collider>();
            var projCols    = proj.GetComponentsInChildren<Collider>(true);
            foreach (var oc in ownerCols)
                foreach (var pc in projCols)
                    Physics.IgnoreCollision(oc, pc, true);

            // Initialize projectile. In production mode the server owns damage; client
            // projectiles are visual-only unless config explicitly allows damage.
            proj.Initialize(config, fireDir, useHitscan: false, hitscanEndpoint: null);
            if (config.ApplyDamage)
                proj.SetOwnerData(shooterNetObjId, config.WeaponId);

            Debug.Log($"[SHOOT.PLAYER] ProjectileWeapon.Fire — origin={origin:F1}  dir={fireDir:F2}  " +
                      $"proj='{proj.gameObject.name}'  shooterNetObjId={shooterNetObjId}  " +
                      $"speed={config.ProjectileSpeed}  maxRange={config.MaxRange}");

            // Estimated endpoint for the aim-trail VFX on the firing client.
            RaiseFireResult(origin, origin + fireDir * maxRange);
        }
    }
}
