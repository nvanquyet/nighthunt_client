using System.Collections.Generic;
using UnityEngine;
using NightHunt.Core;
using NightHunt.Data;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Diagnostics;
using NightHunt.Utilities;

namespace NightHunt.Gameplay.Character.Combat.Weapons
{
    /// <summary>
    /// Concrete weapon component for hitscan weapons (rifles, SMGs, pistols, snipers).
    /// Also supports multiple pellets per shot, making it suitable for shotguns.
    ///
    /// BALLISTIC MODEL:
    ///   Each shot fires <see cref="pelletCount"/> raycasts for local visuals only.
    ///   Server-side WeaponSystem resolves final damage. A visual-only ProjectileComponent is
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
        private readonly RaycastHit[] _raycastHits = new RaycastHit[32];

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

        public int PelletCount => pelletCount;
        public float PelletSpreadBonus => pelletSpreadBonus;

        // ── WeaponBase.Fire implementation ────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            // Cache the player root that owns this weapon model so FirePellet() can exclude
            // the shooter's own colliders from the raycast result without any allocation.
            // This weapon is a child of the character's WeaponR bone, so transform.root = player root.
            _shooterRoot = transform.root;
            // Self-heal: if hitLayers is still "Everything" (~0) or empty (0), apply the
            // canonical full hitscan mask so bullets stop at correct geometry layers only.
            if (hitLayers.value == -1 || hitLayers.value == 0)
                hitLayers = NightHuntLayers.MaskHitscanFull;
        }

        public override void Fire(Vector3 origin, Vector3 direction,
                                  WeaponConfigData config, int shooterNetObjId)
        {
            if (_shooterRoot == null) _shooterRoot = transform.root;

            RecordShot();

            float effectiveRange = Mathf.Max(1f, config.MaxRange > 0f ? config.MaxRange : maxRange);
            Vector3 endpointSum = Vector3.zero;
            Vector3 hitNormalSum = Vector3.zero;
            int hitNormalCount = 0;
            bool hitHittableSum = false;

            for (int i = 0; i < pelletCount; i++)
            {
                Vector3 pelletDir = ApplyRadialSpread(direction);

                if (pelletCount > 1 && pelletSpreadBonus > 0.001f)
                    pelletDir = AddExtraSpread(pelletDir, pelletSpreadBonus);

                (Vector3 endpoint, bool hitHittable, Vector3 hitNormal, bool hitAnything) = FirePellet(origin, pelletDir, config, shooterNetObjId, effectiveRange);
                endpointSum += endpoint;
                hitHittableSum |= hitHittable;
                if (hitAnything)
                {
                    hitNormalSum += hitNormal;
                    hitNormalCount++;
                }

                SpawnVisualBullet(origin, pelletDir, config, endpoint, hitHittable, hitNormal);
            }

            Vector3 resultNormal = hitNormalCount > 0 ? hitNormalSum : -direction.normalized;
            RaiseFireResult(origin, endpointSum / pelletCount, hitHittableSum, resultNormal);
        }

        // ── Private helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Fires one raycast pellet. Applies damage to the first hittable in range.
        /// Returns the endpoint (hit point or max-range terminus), hit type, and hit normal.
        /// </summary>
        private (Vector3 endpoint, bool hitHittable, Vector3 hitNormal, bool hitAnything) FirePellet(Vector3 origin, Vector3 direction,
                                                                                                     WeaponConfigData config, int shooterNetObjId, float range)
        {
            if (!TryGetFirstNonSelfHit(origin, direction, range, out RaycastHit rayHit))
            {
                if (pelletCount == 1 &&
                    config != null &&
                    config.HasProjectileTargetPoint &&
                    IsUsableRegisteredEndpoint(origin, config.ProjectileTargetPoint, range))
                {
                    PhaseTestLog.Log(
                        PhaseTestLogCategory.Weapon,
                        "ClientHitscanRegisteredEndpoint",
                        $"weapon={config.WeaponId} origin={origin:F2} endpoint={config.ProjectileTargetPoint:F2} range={range:F1} speed={config.ProjectileSpeed:F1}",
                        this);
                    return (config.ProjectileTargetPoint, true, -direction.normalized, true);
                }

                return (origin + direction * range, false, -direction.normalized, false);
            }

            Vector3 endpoint = rayHit.point;
            Vector3 hitNormal = rayHit.normal.sqrMagnitude > 0.0001f ? rayHit.normal.normalized : -direction.normalized;
            PhaseTestLog.Log(
                PhaseTestLogCategory.Weapon,
                "ClientHitscanHit",
                $"weapon={config?.WeaponId ?? "null"} origin={origin:F2} endpoint={endpoint:F2} dist={rayHit.distance:F2} collider={rayHit.collider?.name ?? "null"} layer={(rayHit.collider != null ? PhaseTestLog.DescribeLayer(rayHit.collider.gameObject) : "null")} applyDamage={config?.ApplyDamage ?? false}",
                this);

            var hitbox = ComponentResolver.Find<PlayerHitboxMarker>(rayHit.collider)
                                          .OnSelf().InParent()
                                          .Resolve();
            var hittable = rayHit.collider.GetComponentInParent<IHittable>();
            bool hitHittable = hitbox != null || hittable != null;

            if (!config.ApplyDamage)
                return (endpoint, hitHittable, hitNormal, true);

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
                return (endpoint, true, hitNormal, true);
            }

            // Fallback: any other hittable (boss, deployable, objective …).
            hittable?.RequestDamage(new DamageInfo
            {
                Damage                 = config.DamageBody,
                IsHeadshot             = false,
                HitPoint               = endpoint,
                HitNormal              = rayHit.normal,
                ShooterNetworkObjectId = shooterNetObjId,
                WeaponId               = config.WeaponId ?? string.Empty,
            });

            return (endpoint, hitHittable, hitNormal, true);
        }

        private bool TryGetFirstNonSelfHit(Vector3 origin, Vector3 direction, float range, out RaycastHit hit)
        {
            hit = default;

            int count = Physics.RaycastNonAlloc(origin, direction, _raycastHits, range, hitLayers, QueryTriggerInteraction.Ignore);
            if (count <= 0)
                return false;

            System.Array.Sort(_raycastHits, 0, count, RaycastHitDistanceComparer.Instance);
            for (int i = 0; i < count; i++)
            {
                if (_raycastHits[i].collider == null || IsSelfHit(_raycastHits[i].collider))
                    continue;

                hit = _raycastHits[i];
                return true;
            }

            return false;
        }

        private static bool IsUsableRegisteredEndpoint(Vector3 origin, Vector3 endpoint, float range)
        {
            if (!IsFinite(endpoint))
                return false;

            float distance = Vector3.Distance(origin, endpoint);
            return distance > 0.05f && distance <= Mathf.Max(1f, range) + 1f;
        }

        private static bool IsFinite(Vector3 value)
            => !float.IsNaN(value.x)
            && !float.IsNaN(value.y)
            && !float.IsNaN(value.z)
            && !float.IsInfinity(value.x)
            && !float.IsInfinity(value.y)
            && !float.IsInfinity(value.z);

        private bool IsSelfHit(Collider collider)
        {
            return _shooterRoot != null &&
                   collider != null &&
                   (collider.transform == _shooterRoot || collider.transform.IsChildOf(_shooterRoot));
        }

        private sealed class RaycastHitDistanceComparer : IComparer<RaycastHit>
        {
            public static readonly RaycastHitDistanceComparer Instance = new();

            public int Compare(RaycastHit x, RaycastHit y)
            {
                return x.distance.CompareTo(y.distance);
            }
        }

        /// <summary>
        /// Spawns a visual-only ProjectileComponent from the pool so the trail and impact VFX play.
        /// Sets useHitscan = true so the component skips damage on collision.
        /// <paramref name="hitAnIHittable"/> tells the visual bullet which VFX to play at the endpoint.
        /// No-ops gracefully when the prefab or pool is missing.
        /// </summary>
        private void SpawnVisualBullet(Vector3 origin, Vector3 direction,
                                       WeaponConfigData config, Vector3 endpoint,
                                       bool hitAnIHittable = false, Vector3 hitNormal = default)
        {
            if (projectilePrefab == null)
            {
                WarnProjectile($"[SHOOT.PLAYER] HitscanWeapon.SpawnVisualBullet — projectilePrefab is NULL on '{name}'. " +
                                 $"Assign Projectile_Hitscan_Template prefab to the weapon model component. " +
                                 $"Owner will NOT see bullet visual.");
                return;
            }

            var pool = ProjectilePool.Instance;
            if (pool == null)
                WarnProjectile("[SHOOT.PLAYER] HitscanWeapon.SpawnVisualBullet — ProjectilePool not found in scene. Instantiating one-shot visual.", this);

            var proj = pool != null
                ? pool.Get(projectilePrefab, origin, Quaternion.LookRotation(direction))
                : Instantiate(projectilePrefab, origin, Quaternion.LookRotation(direction)).GetComponent<ProjectileComponent>();
            if (proj == null) return;

            proj.SetIgnoredRoot(_shooterRoot);
            proj.SetHitscanHitType(hitAnIHittable, hitNormal); // blood vs impact VFX at endpoint

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

            LogProjectile($"[SHOOT.PLAYER] HitscanWeapon.SpawnVisualBullet — origin={origin:F1}  " +
                      $"endpoint={endpoint:F1}  dir={direction:F2}  proj='{proj.gameObject.name}'  hitHittable={hitAnIHittable}");
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

        private static bool ProjectileDebugEnabled()
        {
            var cfg = NightHuntDebugConfig.Instance;
            return cfg != null && cfg.EnableProjectileDebugLogs;
        }

        private static void LogProjectile(string message)
        {
            if (ProjectileDebugEnabled())
                Debug.Log(message);
        }

        private static void WarnProjectile(string message, UnityEngine.Object context = null)
        {
            if (context != null)
                Debug.LogWarning(message, context);
            else
                Debug.LogWarning(message);
        }
    }
}
