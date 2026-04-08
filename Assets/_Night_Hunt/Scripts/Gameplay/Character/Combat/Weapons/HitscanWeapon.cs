using UnityEngine;
using NightHunt.Data;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Utilities;

namespace NightHunt.Gameplay.Character.Combat.Weapons
{
    /// <summary>
    /// Concrete weapon component for hitscan weapons (rifles, SMGs, pistols, snipers).
    /// Also supports multiple pellets per shot, making it suitable for shotguns.
    ///
    /// BALLISTIC MODEL:
    ///   Each shot fires <see cref="pelletCount"/> raycasts. Damage is applied immediately on the
    ///   owner machine — no projectile physics involved.  A visual-only ProjectileComponent is
    ///   spawned from the pool for the bullet trail and impact VFX so remote clients see the
    ///   same effect via the network-broadcast projectile spawn.
    ///
    /// VFX:
    ///   All effects (muzzle flash, trail, impact) are owned by the projectile prefab's
    ///   ProjectileComponent. This class has no VFX logic.
    ///
    /// SHOTGUN SETUP:
    ///   Set pelletCount = 8 and pelletSpreadBonus = 3–5° for a typical shotgun.
    ///   The base spread from WeaponBase is applied first; each pellet adds pelletSpreadBonus
    ///   on top so the pattern fans out naturally.
    /// </summary>
    public sealed class HitscanWeapon : WeaponBase
    {
        [Header("Hitscan")]
        [Tooltip("Physics layers the raycast can register a hit on.\n" +
                 "IMPORTANT: Do NOT include the 'PlayerCharacter' layer in this mask — " +
                 "the shooter's own colliders are excluded in code via IsChildOf check, " +
                 "but excluding the layer in the mask is an additional safety net.")]
        [SerializeField] private LayerMask hitLayers = ~0;

        // Cached once in Start() — the player root that owns this weapon model.
        // Used to skip self-hits in FirePellet() without per-raycast allocations.
        private Transform _shooterRoot;

        [Tooltip("Damage multiplier applied when the ray hits a headshot collider.")]
        [SerializeField, Min(1f)] private float damageHeadMultiplier = 2f;

        public float DamageHeadMultiplier => damageHeadMultiplier;

        [Header("Shotgun / Multi-pellet")]
        [Tooltip("Number of pellets fired per shot. 1 = standard bullet (rifle / pistol / SMG). " +
                 "≥2 = shotgun spread pattern.")]
        [SerializeField, Min(1)] private int pelletCount = 1;

        [Tooltip("Extra spread half-angle added per pellet for multi-pellet shots (degrees). " +
                 "Ignored when pelletCount = 1. Stacks ON TOP of the base radial spread from WeaponBase.")]
        [SerializeField, Min(0f)] private float pelletSpreadBonus = 3f;

        // ── WeaponBase.Fire implementation ────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            // Cache the player root that owns this weapon model so FirePellet() can exclude
            // the shooter's own colliders from the raycast result without any allocation.
            // This weapon is a child of the character's WeaponR bone, so transform.root = player root.
            _shooterRoot = transform.root;
        }

        public override void Fire(Vector3 origin, Vector3 direction,
                                  WeaponConfigData config, int shooterNetObjId)
        {
            // Ensure _shooterRoot is set even if Awake ran before reparenting.
            if (_shooterRoot == null) _shooterRoot = transform.root;

            RecordShot();   // grows accumulated spread before applying it

            Vector3 endpointSum = Vector3.zero;

            for (int i = 0; i < pelletCount; i++)
            {
                // Base radial spread (accumulated from continuous fire).
                Vector3 pelletDir = ApplyRadialSpread(direction);

                // For multi-pellet weapons also add per-pellet bonus spread so the
                // full pattern stretches wider than the base cone.
                if (pelletCount > 1 && pelletSpreadBonus > 0.001f)
                    pelletDir = AddExtraSpread(pelletDir, pelletSpreadBonus);

                Vector3 endpoint = FirePellet(origin, pelletDir, config, shooterNetObjId);
                endpointSum += endpoint;

                // Spawn visual bullet trail from pool — damage already applied above.
                SpawnVisualBullet(origin, pelletDir, config, endpoint);
            }

            // Report the average endpoint for the aim-trail VFX.
            RaiseFireResult(origin, endpointSum / pelletCount);
        }

        // ── Private helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Fires one raycast pellet. Applies damage to the first hittable in range.
        /// Returns the endpoint (hit point or max-range terminus).
        /// </summary>
        private Vector3 FirePellet(Vector3 origin, Vector3 direction,
                                   WeaponConfigData config, int shooterNetObjId)
        {
            bool hit = Physics.Raycast(origin, direction, out RaycastHit rayHit, maxRange,
                                       hitLayers, QueryTriggerInteraction.Ignore);

            if (!hit)
                return origin + direction * maxRange;

            // ── Self-hit guard ────────────────────────────────────────────────
            // The muzzle point is OUTSIDE the character mesh but the raycast can still
            // register on the shooter's own colliders when _shooterRoot is not null.
            // Skip any hit that is part of the shooter's own hierarchy.
            if (_shooterRoot != null &&
                (rayHit.collider.transform == _shooterRoot ||
                 rayHit.collider.transform.IsChildOf(_shooterRoot)))
            {
                // Continue the ray past the self-hit (extend origin to just beyond the hit
                // and fire again so we don't silently miss legitimate targets behind the body).
                Vector3 newOrigin = rayHit.point + direction * 0.05f;
                float   remaining = maxRange - rayHit.distance;
                if (remaining > 0.1f)
                {
                    bool hit2 = Physics.Raycast(newOrigin, direction, out RaycastHit rayHit2,
                                                remaining, hitLayers, QueryTriggerInteraction.Ignore);
                    if (hit2 &&
                        rayHit2.collider.transform != _shooterRoot &&
                        !rayHit2.collider.transform.IsChildOf(_shooterRoot))
                    {
                        // Treat hit2 as the real hit.
                        rayHit   = rayHit2;
                        hit      = true;
                    }
                    else
                    {
                        return origin + direction * maxRange;
                    }
                }
                else
                {
                    return origin + direction * maxRange;
                }
            }

            Vector3 endpoint = rayHit.point;

            // Try player hitbox first.
            var hitbox = ComponentResolver.Find<PlayerHitboxMarker>(rayHit.collider)
                                          .OnSelf().InParent()
                                          .Resolve();

            if (hitbox != null && hitbox.HealthSystem != null)
            {
                float dmg = config.DamageBody *
                            (hitbox.IsHeadshot ? damageHeadMultiplier : 1f);

                hitbox.HealthSystem.RequestDamage(new DamageInfo
                {
                    Damage                 = dmg,
                    IsHeadshot             = hitbox.IsHeadshot,
                    HitPoint               = endpoint,
                    HitNormal              = rayHit.normal,
                    ShooterNetworkObjectId = shooterNetObjId,
                    WeaponId               = config.WeaponId ?? string.Empty,
                });
                return endpoint;
            }

            // Fallback: any other hittable (boss, deployable, objective …).
            var hittable = rayHit.collider.GetComponentInParent<IHittable>();
            hittable?.RequestDamage(new DamageInfo
            {
                Damage                 = config.DamageBody,
                IsHeadshot             = false,
                HitPoint               = endpoint,
                HitNormal              = rayHit.normal,
                ShooterNetworkObjectId = shooterNetObjId,
                WeaponId               = config.WeaponId ?? string.Empty,
            });

            return endpoint;
        }

        /// <summary>
        /// Spawns a visual-only ProjectileComponent from the pool so the trail and impact VFX play.
        /// Sets useHitscan = true so the component skips damage on collision.
        /// No-ops gracefully when the prefab or pool is missing.
        /// </summary>
        private void SpawnVisualBullet(Vector3 origin, Vector3 direction,
                                       WeaponConfigData config, Vector3 endpoint)
        {
            if (projectilePrefab == null)
            {
                Debug.LogWarning($"[SHOOT.PLAYER] HitscanWeapon.SpawnVisualBullet — projectilePrefab is NULL on '{name}'. " +
                                 $"Assign Projectile_Hitscan_Template prefab to the weapon model component. " +
                                 $"Owner will NOT see bullet visual.");
                return;
            }

            var pool = ProjectilePool.Instance;
            if (pool == null)
            {
                Debug.LogWarning("[SHOOT.PLAYER] HitscanWeapon.SpawnVisualBullet — ProjectilePool not found in scene.", this);
                return;
            }

            var proj = pool.Get(projectilePrefab, origin, Quaternion.LookRotation(direction));
            if (proj == null) return;

            // Ignore collisions between the hitscan trail projectile and the owner so the
            // muzzle-flash child trigger doesn't immediately detonate on the shooter's own body.
            if (_shooterRoot != null)
            {
                var ownerCols = _shooterRoot.GetComponentsInChildren<Collider>();
                var projCols  = proj.GetComponentsInChildren<Collider>(true);
                foreach (var oc in ownerCols)
                    foreach (var pc in projCols)
                        Physics.IgnoreCollision(oc, pc, true);
            }

            proj.Initialize(config, direction, useHitscan: true, hitscanEndpoint: endpoint);

            Debug.Log($"[SHOOT.PLAYER] HitscanWeapon.SpawnVisualBullet — origin={origin:F1}  " +
                      $"endpoint={endpoint:F1}  dir={direction:F2}  proj='{proj.gameObject.name}'");
        }

        /// <summary>
        /// Applies an additional random radial spread on top of the given direction.
        /// Used to give each pellet its own independent offset inside the shotgun cone.
        /// </summary>
        private static Vector3 AddExtraSpread(Vector3 direction, float halfAngleDeg)
        {
            float radius  = Mathf.Tan(halfAngleDeg * Mathf.Deg2Rad);
            Vector2 disc  = Random.insideUnitCircle * radius;
            Vector3 right = Vector3.Cross(direction, Vector3.up);
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            right = right.normalized;
            Vector3 up = Vector3.Cross(right, direction).normalized;
            return (direction + right * disc.x + up * disc.y).normalized;
        }
    }
}
