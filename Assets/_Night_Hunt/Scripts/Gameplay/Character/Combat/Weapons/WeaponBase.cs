using System;
using System.Collections;
using UnityEngine;
using NightHunt.Data;
using NightHunt.GameplaySystems.Core.Data;

namespace NightHunt.Gameplay.Character.Combat.Weapons
{
    /// <summary>
    /// Base weapon model component. Attach one instance to every weapon model prefab.
    /// It owns fire origin, IK anchors, fire mode defaults, projectile prefab data, and
    /// accumulated radial spread.
    /// </summary>
    public abstract class WeaponBase : MonoBehaviour
    {
        public readonly struct WeaponFireResult
        {
            public readonly Vector3 Origin;
            public readonly Vector3 Endpoint;
            public readonly bool HitAnIHittable;
            public readonly Vector3 HitNormal;

            public WeaponFireResult(Vector3 origin, Vector3 endpoint, bool hitAnIHittable, Vector3 hitNormal)
            {
                Origin = origin;
                Endpoint = endpoint;
                HitAnIHittable = hitAnIHittable;
                HitNormal = hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized : Vector3.up;
            }
        }

        [Header("Fire Origin & IK")]
        [Tooltip("Muzzle-tip transform. Leave null to use the weapon root.")]
        [SerializeField] protected Transform firePoint;

        [Tooltip("Left-hand IK anchor read by WeaponModelController after each weapon swap.")]
        [SerializeField] private Transform leftHandIKTarget;

        [Header("Local Offset Override")]
        [Tooltip("Local position offset relative to the hand bone. If zero, uses prefab root position.")]
        [SerializeField] private Vector3 baseLocalPosition = Vector3.zero;

        [Tooltip("Local rotation offset relative to the hand bone. (e.g. 90, 0, 0)")]
        [SerializeField] private Vector3 baseLocalRotation = new Vector3(90f, 0f, 0f);

        [Header("Spread (Radial)")]
        [Tooltip("Starting spread cone half-angle in degrees. At rest the spread equals this value.")]
        [SerializeField, Min(0f)] protected float spreadBase = 1f;

        [Tooltip("Spread added per shot when firing in quick succession, in degrees per shot.")]
        [SerializeField, Min(0f)] protected float spreadPenaltyPerShot = 0.4f;

        [Tooltip("Spread radius recovered per second after the trigger is released, in degrees per second.")]
        [SerializeField, Min(0f)] protected float spreadRecoveryRate = 2f;

        [Tooltip("Hard cap on accumulated spread radius. 0 = auto-set to spreadBase x 4 at runtime.")]
        [SerializeField, Min(0f)] protected float spreadMax = 0f;

        [Header("Projectile")]
        [Tooltip("ProjectileComponent prefab. Hitscan weapons use it for visual trails; projectile weapons use it for physical travel.")]
        [SerializeField] protected GameObject projectilePrefab;

        [Tooltip("Maximum travel distance in world units.")]
        [SerializeField, Min(1f)] protected float maxRange = 150f;

        [Tooltip("Speed of spawned projectiles in units per second.")]
        [SerializeField, Min(1f)] protected float projectileSpeed = 50f;

        [Tooltip("Gravity multiplier applied to physical projectiles. 0 = no gravity, 1 = standard gravity.")]
        [SerializeField, Min(0f)] protected float gravityScale = 0f;

        [Header("Fire Mode")]
        [Tooltip("Default fire mode when no PlayerPrefs value has been saved yet.")]
        [SerializeField] protected FireMode defaultFireMode = FireMode.Auto;

        [Tooltip("Allow the player to toggle between Auto and Single on the HUD.")]
        [SerializeField] protected bool allowFireModeToggle = true;

        [Tooltip("Allow reload while the magazine still has rounds.")]
        [SerializeField] protected bool canTacticalReload = true;

        private float _currentSpread;
        private float _resolvedSpreadMax;

        public FireMode DefaultFireMode => defaultFireMode;
        public bool AllowFireModeToggle => allowFireModeToggle;
        public bool CanTacticalReload => canTacticalReload;
        public float MaxRange => maxRange;
        public float ProjectileSpeed => projectileSpeed;
        public float GravityScale => gravityScale;
        public float SpreadBase => spreadBase;
        public float SpreadPenaltyPerShot => spreadPenaltyPerShot;
        public float SpreadRecoveryRate => spreadRecoveryRate;
        public float CurrentSpread => _currentSpread;
        public GameObject ProjectilePrefab => projectilePrefab;
        public Transform FirePoint => firePoint != null ? firePoint : transform;
        public Transform LeftHandIKTarget => leftHandIKTarget;
        public Vector3 BaseLocalPosition => baseLocalPosition;
        public Vector3 BaseLocalRotation => baseLocalRotation;

        /// <summary>
        /// Raised after each shot with the shot origin and resolved endpoint.
        /// Hitscan endpoint is the hit point or max-range point. Projectile endpoint is an estimate.
        /// </summary>
        public event Action<Vector3, Vector3> OnFireResult;
        public event Action<WeaponFireResult> OnFireResultDetailed;

        protected virtual void Awake()
        {
            _resolvedSpreadMax = spreadMax > 0f ? spreadMax : spreadBase * 4f;
            _currentSpread = spreadBase;
        }

        protected virtual void Update()
        {
            if (_currentSpread <= spreadBase)
                return;

            _currentSpread -= spreadRecoveryRate * Time.deltaTime;
            if (_currentSpread < spreadBase)
                _currentSpread = spreadBase;
        }

        /// <summary>
        /// Processes one shot. Subclasses apply spread, perform raycast or projectile spawn,
        /// and call RaiseFireResult when done.
        /// </summary>
        public abstract void Fire(Vector3 origin, Vector3 direction, WeaponConfigData config, int shooterNetObjId);

        /// <summary>
        /// Returns a direction randomly offset inside the current radial spread cone.
        /// The offset is sampled on the plane perpendicular to the fire direction, so vertical
        /// hit variance is preserved for head/body targeting without creating a constant upward drift.
        /// </summary>
        protected Vector3 ApplyRadialSpread(Vector3 direction)
        {
            if (_currentSpread <= 0.001f)
                return direction.normalized;

            Vector3 forward = direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
            float radius = Mathf.Tan(_currentSpread * Mathf.Deg2Rad);
            Vector2 disc = UnityEngine.Random.insideUnitCircle * radius;

            Vector3 right = Vector3.Cross(forward, Vector3.up);
            if (right.sqrMagnitude < 0.001f)
                right = Vector3.right;

            right.Normalize();
            Vector3 up = Vector3.Cross(right, forward).normalized;
            return (forward + right * disc.x + up * disc.y).normalized;
        }

        /// <summary>
        /// Records one fired shot and grows accumulated spread.
        /// </summary>
        protected void RecordShot()
        {
            _currentSpread = Mathf.Min(_resolvedSpreadMax, _currentSpread + spreadPenaltyPerShot);
        }

        /// <summary>
        /// Resets spread immediately, for example on weapon unequip or holster.
        /// </summary>
        public void ResetSpread() => _currentSpread = spreadBase;

        protected void RaiseFireResult(Vector3 origin, Vector3 endpoint)
            => RaiseFireResult(origin, endpoint, false, Vector3.up);

        protected void RaiseFireResult(Vector3 origin, Vector3 endpoint, bool hitAnIHittable, Vector3 hitNormal)
        {
            OnFireResult?.Invoke(origin, endpoint);
            OnFireResultDetailed?.Invoke(new WeaponFireResult(origin, endpoint, hitAnIHittable, hitNormal));
        }
    }

    public static class BallisticTrajectory
    {
        public static bool TrySolveLaunchVelocity(
            Vector3 from,
            Vector3 to,
            float speed,
            float gravityScale,
            bool preferHighArc,
            out Vector3 velocity)
        {
            velocity = Vector3.zero;

            if (!IsFinite(from) || !IsFinite(to))
                return false;

            speed = Mathf.Max(0f, speed);
            float gravity = Mathf.Max(0f, gravityScale) * Mathf.Abs(Physics.gravity.y);
            Vector3 delta = to - from;
            Vector3 horizontal = Vector3.ProjectOnPlane(delta, Vector3.up);
            float horizontalDistance = horizontal.magnitude;

            if (speed <= 0.001f)
                return false;

            if (gravity <= 0.001f || horizontalDistance <= 0.01f)
            {
                if (delta.sqrMagnitude <= 0.0001f)
                    return false;

                velocity = delta.normalized * speed;
                return true;
            }

            float speedSq = speed * speed;
            float y = delta.y;
            float root = speedSq * speedSq
                       - gravity * (gravity * horizontalDistance * horizontalDistance + 2f * y * speedSq);
            if (root < 0f)
                return false;

            float sqrtRoot = Mathf.Sqrt(root);
            float tanTheta = (speedSq + (preferHighArc ? sqrtRoot : -sqrtRoot)) / (gravity * horizontalDistance);
            float cosTheta = 1f / Mathf.Sqrt(1f + tanTheta * tanTheta);
            float sinTheta = tanTheta * cosTheta;

            Vector3 horizontalDir = horizontal / horizontalDistance;
            velocity = horizontalDir * (speed * cosTheta) + Vector3.up * (speed * sinTheta);
            return IsFinite(velocity) && velocity.sqrMagnitude > 0.0001f;
        }

        public static Vector3 ResolveLaunchDirection(Vector3 origin, Vector3 requestedDirection, WeaponConfigData config)
        {
            Vector3 fallback = requestedDirection.sqrMagnitude > 0.0001f
                ? requestedDirection.normalized
                : Vector3.forward;

            if (config == null || !config.HasProjectileTargetPoint || config.GravityScale <= 0f)
                return fallback;

            return TrySolveLaunchVelocity(
                origin,
                config.ProjectileTargetPoint,
                Mathf.Max(1f, config.ProjectileSpeed),
                config.GravityScale,
                config.PreferHighArc,
                out Vector3 velocity)
                ? velocity.normalized
                : fallback;
        }

        private static bool IsFinite(Vector3 value)
            => !float.IsNaN(value.x)
            && !float.IsNaN(value.y)
            && !float.IsNaN(value.z)
            && !float.IsInfinity(value.x)
            && !float.IsInfinity(value.y)
            && !float.IsInfinity(value.z);
    }
}
