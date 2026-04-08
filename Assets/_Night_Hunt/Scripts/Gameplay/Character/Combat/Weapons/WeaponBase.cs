using System;
using System.Collections;
using UnityEngine;
using NightHunt.Data;
using NightHunt.GameplaySystems.Core.Data;

namespace NightHunt.Gameplay.Character.Combat.Weapons
{
    /// <summary>
    /// Abstract weapon model component — attach ONE instance to every weapon model prefab.
    ///
    /// RESPONSIBILITIES:
    ///   • Exposes the fire origin (muzzle tip) and left-hand IK anchor.
    ///   • Tracks the radial spread state: accumulated spread grows per shot and
    ///     recovers over time. Spread is applied in a circle (both horizontal AND
    ///     vertical axes) — NOT a continuous upward drift. Each shot lands at a
    ///     random position inside the current spread disc.
    ///   • Declares fire mode defaults (Auto / Single) and tactical-reload support.
    ///   • Raises OnFireResult so WeaponSystem can update VFX subscribers.
    ///
    /// SUBCLASSES:
    ///   HitscanWeapon   — instant raycasts; one or more pellets per shot (shotgun support).
    ///   ProjectileWeapon — spawns a physics projectile from the pool per shot.
    ///
    /// VFX (muzzle flash, trail, impact) are owned by the projectile prefab's
    /// ProjectileComponent so that remote clients see the same effects via the
    /// network-broadcast projectile spawn, not a separate RPC.
    /// </summary>
    public abstract class WeaponBase : MonoBehaviour
    {
        // ── Fire Origin ────────────────────────────────────────────────────────
        [Header("Fire Origin & IK")]
        [Tooltip("Muzzle-tip transform. Leave null to use the weapon root.")]
        [SerializeField] protected Transform firePoint;

        [Tooltip("Left-hand IK anchor (ArmIK child or equivalent). " +
                 "Read by WeaponModelController after each weapon swap.")]
        [SerializeField] private Transform leftHandIKTarget;

        // ── Spread ────────────────────────────────────────────────────────────
        [Header("Spread (Radial)")]
        [Tooltip("Starting spread cone half-angle in degrees. " +
                 "At rest the spread equals this value.")]
        [SerializeField, Min(0f)] protected float spreadBase = 1f;

        [Tooltip("Spread added per shot when firing in quick succession (degrees / shot). " +
                 "Penalises full-auto spray.")]
        [SerializeField, Min(0f)] protected float spreadPenaltyPerShot = 0.4f;

        [Tooltip("Spread radius recovered per second after the trigger is released (degrees / s). " +
                 "Lower = weapon stays inaccurate longer after a burst.")]
        [SerializeField, Min(0f)] protected float spreadRecoveryRate = 2f;

        [Tooltip("Hard cap on the accumulated spread radius (degrees). " +
                 "0 = auto-set to spreadBase × 4 at runtime.")]
        [SerializeField, Min(0f)] protected float spreadMax = 0f;

        // ── Projectile prefab (shared reference for both subtypes) ─────────────
        [Header("Projectile")]
        [Tooltip("ProjectileComponent prefab. " +
                 "HitscanWeapon: assign Projectile_Hitscan_Template — visual-only trail, no physics damage. " +
                 "ProjectileWeapon: assign Projectile_Physics_Template — physics body with authoritative damage. " +
                 "Generate both via NightHunt/Tools/Build Template Prefabs. " +
                 "Add ParticleSystem to [MuzzleFlash] and [DetonationVFX] children, TrailRenderer to [MainVisual].")]
        [SerializeField] protected GameObject projectilePrefab;

        [Tooltip("Maximum travel distance (world units) before the bullet / raycast stops.")]
        [SerializeField, Min(1f)] protected float maxRange = 150f;

        [Tooltip("Speed of spawned projectiles in units per second.")]
        [SerializeField, Min(1f)] protected float projectileSpeed = 50f;

        [Tooltip("Gravity multiplier applied to the projectile each frame. " +
                 "0 = no gravity (hitscan visual / flat trajectory). " +
                 "1 = standard 9.81 m/s². Set on ProjectileWeapon prefabs only.")]
        [SerializeField, Min(0f)] protected float gravityScale = 0f;

        // ── Fire Mode ─────────────────────────────────────────────────────────
        [Header("Fire Mode")]
        [Tooltip("Default fire mode when no PlayerPrefs value has been saved yet.")]
        [SerializeField] protected FireMode defaultFireMode = FireMode.Auto;

        [Tooltip("Allow the player to toggle between Auto and Single on the HUD.")]
        [SerializeField] protected bool allowFireModeToggle = true;

        [Tooltip("Allow reload while the magazine still has rounds (tactical / speed-reload).")]
        [SerializeField] protected bool canTacticalReload = true;

        // ── Runtime spread state ───────────────────────────────────────────────
        private float _currentSpread;   // degrees; accumulated, never below spreadBase
        private float _resolvedSpreadMax;

        // ── Properties ────────────────────────────────────────────────────────
        public FireMode DefaultFireMode      => defaultFireMode;
        public bool     AllowFireModeToggle  => allowFireModeToggle;
        public bool     CanTacticalReload    => canTacticalReload;
        public float    MaxRange             => maxRange;
        public float    ProjectileSpeed      => projectileSpeed;
        public float    GravityScale         => gravityScale;
        public float    CurrentSpread        => _currentSpread;
        public GameObject ProjectilePrefab   => projectilePrefab;
        public Transform  FirePoint          => firePoint != null ? firePoint : transform;
        public Transform  LeftHandIKTarget   => leftHandIKTarget;

        // ── Event ─────────────────────────────────────────────────────────────
        /// <summary>
        /// Raised after each shot with the shot origin and the resolved endpoint.
        /// Hitscan: endpoint = hit point or origin + direction × maxRange.
        /// Projectile: endpoint = estimated origin + direction × maxRange.
        /// WeaponSystem subscribes and re-raises as OnHitscanResult for the aim trail.
        /// </summary>
        public event Action<Vector3, Vector3> OnFireResult;

        // ── Unity Lifecycle ───────────────────────────────────────────────────
        protected virtual void Awake()
        {
            _resolvedSpreadMax = spreadMax > 0f ? spreadMax : spreadBase * 4f;
            _currentSpread = spreadBase;
        }

        protected virtual void Update()
        {
            // Spread recovers over time when not firing.
            if (_currentSpread > spreadBase)
            {
                _currentSpread -= spreadRecoveryRate * Time.deltaTime;
                if (_currentSpread < spreadBase) _currentSpread = spreadBase;
            }
        }

        // ── Abstract fire ─────────────────────────────────────────────────────

        /// <summary>
        /// Process one shot. Called by WeaponSystem after ammo is deducted.
        /// Subclasses apply spread, perform raycast or projectile spawn, and call
        /// RaiseFireResult when done.
        /// </summary>
        public abstract void Fire(Vector3 origin, Vector3 direction,
                                  WeaponConfigData config, int shooterNetObjId);

        // ── Spread helpers ─────────────────────────────────────────────────────

        /// <summary>
        /// Returns a direction randomly offset inside a circle of radius <see cref="_currentSpread"/>
        /// in degrees. Both horizontal and vertical axes are randomised equally — the offset is a
        /// 2-D point sampled uniformly from a disc, never a straight upward drift.
        /// </summary>
        protected Vector3 ApplyRadialSpread(Vector3 direction)
        {
            if (_currentSpread <= 0.001f) return direction;

            float radius = Mathf.Tan(_currentSpread * Mathf.Deg2Rad);
            Vector2 disc = UnityEngine.Random.insideUnitCircle * radius;

            // Build an orthonormal basis perpendicular to the fire direction.
            Vector3 right = Vector3.Cross(direction, Vector3.up);
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            right = right.normalized;
            Vector3 up = Vector3.Cross(right, direction).normalized;

            return (direction + right * disc.x + up * disc.y).normalized;
        }

        /// <summary>
        /// Records that one shot was fired — grows the accumulated spread by
        /// <see cref="spreadPenaltyPerShot"/>, clamped to <see cref="_resolvedSpreadMax"/>.
        /// Call this at the start of Fire() before spawning damage / projectile.
        /// </summary>
        protected void RecordShot()
        {
            _currentSpread = Mathf.Min(_resolvedSpreadMax,
                                       _currentSpread + spreadPenaltyPerShot);
        }

        /// <summary>
        /// Resets spread immediately to <see cref="spreadBase"/> (e.g. on weapon unequip / holster).
        /// </summary>
        public void ResetSpread() => _currentSpread = spreadBase;

        // ── Result broadcast ──────────────────────────────────────────────────

        /// <summary>Raises OnFireResult so WeaponSystem can update the aim-trail VFX.</summary>
        protected void RaiseFireResult(Vector3 origin, Vector3 endpoint)
            => OnFireResult?.Invoke(origin, endpoint);
    }
}

