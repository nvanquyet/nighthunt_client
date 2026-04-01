using System;
using System.Collections;
using UnityEngine;
using NightHunt.Data;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Utilities;

namespace NightHunt.Gameplay.Character.Combat.Weapons
{
    /// <summary>
    /// Weapon model component. Attach ONE instance to every weapon model prefab.
    ///
    /// A single Fire() handles both modes:
    ///   • Hitscan  — instant raycast for damage + spawns a visual-only bullet trail (useHitscan=true).
    ///   • Projectile — spawns an authoritative bullet that applies damage on collision.
    ///
    /// VFX split:
    ///   • Muzzle flash  → managed here (weapon side).
    ///   • Bullet trail / impact/explosion VFX → managed by ProjectileBase children on the projectile prefab.
    ///
    /// Replaces PrWeapon for fire-origin and IK:
    ///   • Assign firePoint to the muzzle tip child transform.
    ///   • Assign leftHandIKTarget to the "ArmIK" child (or any IK anchor) instead of relying on PrWeapon.
    ///
    /// WeaponSystem calls Fire() after ammo is deducted; this component does NOT read ItemDatabase.
    /// </summary>
    public class WeaponBase : MonoBehaviour
    {
        // ── Ballistic ─────────────────────────────────────────────────────────
        [Header("Ballistic")]
        [Tooltip("Whether this weapon fires an instant raycast or a spawned projectile.")]
        [SerializeField] protected BallisticType ballisticType = BallisticType.Hitscan;

        [Tooltip("Layers that bullets/projectiles can hit.")]
        [SerializeField] protected LayerMask hitLayers = ~0;

        [Tooltip("Damage multiplier applied on a confirmed headshot.")]
        [SerializeField] protected float damageHeadMultiplier = 2f;

        // ── Projectile (BallisticType = Projectile only) ──────────────────────
        [Header("Projectile")]
        [Tooltip("ProjectileComponent prefab retrieved from ProjectilePool on each shot.")]
        [SerializeField] protected GameObject projectilePrefab;

        [Tooltip("Travel speed in world-units/second.")]
        [SerializeField] protected float projectileSpeed = 50f;

        [Tooltip("Max travel distance before auto-despawn.")]
        [SerializeField] protected float maxRange = 150f;

        [Tooltip("Downward gravity scale applied during flight (0 = no drop).")]
        [SerializeField] protected float gravityScale = 0f;

        // ── Fire Mode ─────────────────────────────────────────────────────────
        [Header("Fire Mode")]
        [Tooltip("Default fire mode on first equip / no saved PlayerPrefs value.")]
        [SerializeField] protected FireMode defaultFireMode = FireMode.Auto;

        [Tooltip("Allow the player to toggle between Auto and Single via HUD.")]
        [SerializeField] protected bool allowFireModeToggle = true;

        // ── Reload ────────────────────────────────────────────────────────────
        [Header("Reload")]
        [Tooltip("Allow reload while the magazine still has ammo (tactical / speed-reload).")]
        [SerializeField] protected bool canTacticalReload = true;

        // ── VFX ───────────────────────────────────────────────────────────────
        [Header("VFX")]
        [Tooltip("Child GameObject with the muzzle-flash particle. Inactive by default.")]
        [SerializeField] protected GameObject muzzleFlashChild;

        [Tooltip("Seconds before the muzzle flash is disabled.")]
        [SerializeField] protected float muzzleFlashDuration = 0.08f;

        // ── Fire Origin ───────────────────────────────────────────────────────
        [Header("Fire Origin")]
        [Tooltip("Muzzle-tip transform. Leave null to use the weapon root.")]
        [SerializeField] protected Transform firePoint;

        // ── IK ────────────────────────────────────────────────────────────────
        [Header("IK")]
        [Tooltip("Left-hand IK anchor (replaces PrWeapon ArmIK). Assign the ArmIK child or any suitable bone. Leave null for no left-hand IK.")]
        [SerializeField] private Transform leftHandIKTarget;

        // ── Public Properties ─────────────────────────────────────────────────
        public BallisticType BallisticType        => ballisticType;
        public LayerMask     HitLayers            => hitLayers;
        public float         DamageHeadMultiplier => damageHeadMultiplier;
        public GameObject    ProjectilePrefab     => projectilePrefab;
        public float         ProjectileSpeed      => projectileSpeed;
        public float         MaxRange             => maxRange;
        public float         GravityScale         => gravityScale;
        public FireMode      DefaultFireMode      => defaultFireMode;
        public bool          AllowFireModeToggle  => allowFireModeToggle;
        public bool          CanTacticalReload    => canTacticalReload;
        public Transform     FirePoint            => firePoint != null ? firePoint : transform;
        /// <summary>Left-hand IK anchor. Read by WeaponModelController after each weapon swap.</summary>
        public Transform     LeftHandIKTarget     => leftHandIKTarget;

        // ── Event ─────────────────────────────────────────────────────────────
        /// <summary>
        /// Raised after each Fire() with the shot origin and endpoint.
        /// Hitscan: endpoint = hit point or origin + dir * maxRange.
        /// Projectile: endpoint = origin + dir * maxRange (estimated).
        /// WeaponSystem subscribes and re-raises as OnHitscanResult for VFX.
        /// </summary>
        public event Action<Vector3, Vector3> OnFireResult;

        // ── Unity Lifecycle ───────────────────────────────────────────────────
        protected virtual void Awake()
        {
            if (muzzleFlashChild != null)
                muzzleFlashChild.SetActive(false);
        }

        // ── Fire ──────────────────────────────────────────────────────────────
        /// <summary>
        /// Process one shot. Handles both Hitscan and Projectile based on ballisticType.
        ///
        /// Hitscan:   raycast → damage → spawn visual-only bullet trail from pool.
        /// Projectile: spawn authoritative bullet from pool → bullet handles damage on collision.
        ///
        /// In both cases muzzle flash plays, spread is applied, and OnFireResult is raised for VFX.
        /// </summary>
        public virtual void Fire(Vector3 origin, Vector3 direction, WeaponConfigData config, int shooterNetObjId)
        {
            PlayMuzzleFlash();

            // Apply bullet spread based on SpreadBase (half-angle in degrees from WeaponConfigData).
            Vector3 spreadDir = ApplySpread(direction, config.SpreadBase);
            if (config.SpreadBase > 0.001f)
                Debug.Log($"[WeaponBase][Spread] SpreadBase={config.SpreadBase:F3}° — original={direction}, spread={spreadDir}");

            if (ballisticType == BallisticType.Hitscan)
                FireHitscan(origin, spreadDir, config, shooterNetObjId);
            else
                FireProjectile(origin, spreadDir, config, shooterNetObjId);
        }

        /// <summary>
        /// Randomly offsets <paramref name="direction"/> inside a cone of half-angle
        /// <paramref name="halfAngleDeg"/> degrees. Returns the original direction unchanged if
        /// halfAngleDeg &lt;= 0.
        /// </summary>
        private static Vector3 ApplySpread(Vector3 direction, float halfAngleDeg)
        {
            if (halfAngleDeg <= 0f) return direction;

            // Build orthonormal basis perpendicular to the direction so we can
            // offset in both axes without a gimbal-lock risk.
            float spreadRadius = Mathf.Tan(halfAngleDeg * Mathf.Deg2Rad);
            Vector2 offset     = UnityEngine.Random.insideUnitCircle * spreadRadius;

            Vector3 right = Vector3.Cross(direction, Vector3.up);
            if (right.sqrMagnitude < 0.001f) right = Vector3.right; // handle straight-up/down shots
            right = right.normalized;
            Vector3 up = Vector3.Cross(right, direction).normalized;

            return (direction + right * offset.x + up * offset.y).normalized;
        }

        private void FireHitscan(Vector3 origin, Vector3 direction, WeaponConfigData config, int shooterNetObjId)
        {
            bool didHit = Physics.Raycast(origin, direction, out RaycastHit hit, maxRange,
                hitLayers, QueryTriggerInteraction.Ignore);

            Vector3 endpoint = didHit ? hit.point : origin + direction * maxRange;

            if (didHit)
            {
                var hitbox = ComponentResolver.Find<PlayerHitboxMarker>(hit.collider)
                                            .OnSelf()
                                            .InChildren()
                                            .Resolve();
                if (hitbox != null && hitbox.HealthSystem != null)
                {
                    float dmg = config.DamageBody * (hitbox.IsHeadshot ? damageHeadMultiplier : 1f);
                    hitbox.HealthSystem.RequestDamage(new DamageInfo
                    {
                        Damage                 = dmg,
                        IsHeadshot             = hitbox.IsHeadshot,
                        HitPoint               = hit.point,
                        HitNormal              = hit.normal,
                        ShooterNetworkObjectId = shooterNetObjId,
                        WeaponId               = config.WeaponId ?? string.Empty,
                    });
                }
                else
                {
                    // Non-player hittable (boss, deployable, objective, etc.)
                    var hittable = hit.collider.GetComponentInParent<IHittable>();
                    hittable?.RequestDamage(new DamageInfo
                    {
                        Damage                 = config.DamageBody,
                        IsHeadshot             = false,
                        HitPoint               = hit.point,
                        HitNormal              = hit.normal,
                        ShooterNetworkObjectId = shooterNetObjId,
                        WeaponId               = config.WeaponId ?? string.Empty,
                    });
                }
            }

            // Visual-only bullet trail (no damage). useHitscan=true → ProjectileComponent skips damage on collision.
            SpawnVisualBullet(origin, direction, config, visualOnly: true);

            RaiseFireResult(origin, endpoint);
        }

        private void FireProjectile(Vector3 origin, Vector3 direction, WeaponConfigData config, int shooterNetObjId)
        {
            var proj = SpawnVisualBullet(origin, direction, config, visualOnly: false);
            // Mark as authoritative so ProjectileComponent sends damage RPCs on collision.
            proj?.SetOwnerData(shooterNetObjId, config.WeaponId);

            RaiseFireResult(origin, origin + direction * maxRange);
        }

        /// <summary>
        /// Spawn a bullet from the pool.
        /// <paramref name="visualOnly"/> = true → useHitscan flag set, projectile won't apply damage.
        /// Returns null if projectilePrefab is not assigned or pool is missing.
        /// </summary>
        private ProjectileComponent SpawnVisualBullet(Vector3 origin, Vector3 direction, WeaponConfigData config, bool visualOnly)
        {
            if (projectilePrefab == null) return null;
            var pool = ProjectilePool.Instance;
            if (pool == null)
            {
                Debug.LogWarning("[WeaponBase] ProjectilePool.Instance is null — add ProjectilePool to the scene.");
                return null;
            }
            var proj = pool.Get(projectilePrefab, origin, Quaternion.LookRotation(direction));
            proj?.Initialize(config, direction, useHitscan: visualOnly);
            return proj;
        }

        // ── Shared Helpers ────────────────────────────────────────────────────
        /// <summary>Blink the muzzle flash child particle for muzzleFlashDuration seconds.</summary>
        public void PlayMuzzleFlash()
        {
            if (muzzleFlashChild == null) return;
            muzzleFlashChild.SetActive(true);
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

        /// <summary>Raise OnFireResult so WeaponSystem can forward it to VFX subscribers.</summary>
        protected void RaiseFireResult(Vector3 origin, Vector3 endpoint)
            => OnFireResult?.Invoke(origin, endpoint);
    }
}

