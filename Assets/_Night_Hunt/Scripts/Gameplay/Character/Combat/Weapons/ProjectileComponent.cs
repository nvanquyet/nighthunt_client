using System.Collections;
using UnityEngine;
using NightHunt.Core;
using NightHunt.Data;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Utilities;

namespace NightHunt.Gameplay.Character.Combat.Weapons
{
    /// <summary>
    /// Component for moving projectiles (bullets, grenades, smoke…).
    /// Inherits ProjectileBase; all VFX config is set on the prefab.
    ///
    /// Lifecycle:
    ///   OnEnable: reset state for pool reuse.
    ///   Initialize: set direction/speed, play muzzle flash, start fuse if needed.
    ///   Update: move and check range.
    ///   OnTrigger: detonate if isImpact is true.
    ///   Detonate: hide MainVisual, activate DetonationVFX, wait lifetimeAfterImpact, deactivate.
    ///
    /// For hitscan visual mode (useHitscan = true):
    ///   Damage was already applied by raycast, but the visual still starts at the
    ///   muzzle/fire point and travels toward hitscanEndpoint before playing impact VFX.
    /// </summary>
    public class ProjectileComponent : ProjectileBase
    {
        // Runtime state.
        private WeaponConfigData _config;
        private Vector3          _direction;
        private Vector3          _velocity;       // accumulated velocity for ballistic arc
        private float            _speed;
        private float            _maxRange;
        private float            _visionRangeClamp;   // >0: despawn when XZ distance from spawn exceeds this
        private Vector3          _spawnPosition;      // world position at Initialize() for vision-range check
        private bool             _useHitscan;
        private Vector3          _hitscanTargetPos;       // world position to detonate at for hitscan visuals
        private float            _hitscanTargetDistance;  // distance from spawn to _hitscanTargetPos
        private float            _distanceTraveled;
        private bool             _hasDetonated;
        private Coroutine        _fuseRoutine;
        private Coroutine        _despawnRoutine;
        private Rigidbody        _rigidbody;
        private Collider[]       _colliders;
        private Transform        _ignoredRoot;
        private float            _nextMovementLogTime;
        private readonly RaycastHit[] _sweepHits = new RaycastHit[16];

        [Tooltip("Seconds to keep the bullet trail visible after a hitscan visual reaches the endpoint before hiding it.")]
        [SerializeField] private float _hitscanTrailLingerDuration = 0.12f;

        [Tooltip("Minimum travel speed for visual-only hitscan bullets. Keeps rifle/SMG trails responsive even when the weapon projectile speed is tuned for physical projectiles.")]
        [SerializeField] private float _minHitscanVisualSpeed = 180f;

        [Tooltip("Fallback radius used by swept collision checks when no enabled collider radius can be resolved.")]
        [SerializeField] private float _sweepRadius = 0.08f;

        [Tooltip("When projectile debug logs are enabled, movement logs are emitted at this interval.")]
        [SerializeField] private float _movementDebugLogInterval = 0.25f;

        // Damage identity set via SetOwnerData() by the spawner on the owner machine.
        private float  _damage;
        private bool   _isOwnerShot;   // Only owner may send damage RPCs to server.
        private int    _shooterNetObjId = -1;
        private string _weaponId;

        // Pool reset
        private void Awake()
        {
            EnsurePhysicsSetup();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsurePhysicsSetup();
            // Self-heal layer: always ensure bullets land on the correct physics layer
            // regardless of the serialised prefab value (layer table renames break prefabs).
            gameObject.layer = LayerMask.NameToLayer(NightHuntLayers.Projectile);
            _hasDetonated          = false;
            _distanceTraveled      = 0f;
            _velocity              = Vector3.zero;
            _hitscanTargetPos      = Vector3.zero;
            _hitscanTargetDistance = 0f;
            _fuseRoutine           = null;
            _despawnRoutine        = null;
            _isOwnerShot           = false;
            _shooterNetObjId       = -1;
            _visionRangeClamp      = 0f;
            _spawnPosition         = Vector3.zero;
            _ignoredRoot           = null;
            _nextMovementLogTime   = 0f;
        }

        public void SetIgnoredRoot(Transform root)
        {
            _ignoredRoot = root != null ? root.root : null;
        }

        // Called on every spawn or reuse from pool.
        // hitscanEndpoint: if not null, the projectile flies from the current spawn point
        //                  to this world point (visual only; damage was already raycast).
        public void Initialize(WeaponConfigData config, Vector3 dir, bool useHitscan,
                               Vector3? hitscanEndpoint = null)
        {
            _config     = config;
            _direction  = dir.normalized;
            _speed      = useHitscan ? Mathf.Max(config.ProjectileSpeed, _minHitscanVisualSpeed) : config.ProjectileSpeed;
            // Ballistic projectiles still obey the visible-circle clamp. Hitscan
            // visuals already receive an authoritative endpoint, so clamping them
            // by spawn-to-vision distance can despawn the trail before impact when
            // the muzzle is offset from the player's root near walls.
            _maxRange   = config.MaxRange;
            _visionRangeClamp = useHitscan ? 0f : config.VisionRangeClamp;
            _spawnPosition    = transform.position;
            _useHitscan = useHitscan;
            _damage     = config.DamageBody;
            _weaponId   = config.WeaponId;
            _velocity   = _direction * _speed;
            _nextMovementLogTime = Time.time;

            LogProjectile($"[PROJ.INIT] Initialize - go='{gameObject.name}'  pos={transform.position:F1}  " +
                      $"dir={_direction:F2}  speed={_speed}  maxRange={_maxRange}  " +
                      $"useHitscan={useHitscan}  isOwnerShot={_isOwnerShot}  dmg={_damage}  " +
                      $"endpoint={hitscanEndpoint?.ToString("F1") ?? "null"}");

            // Muzzle flash plays on the projectile (spawned at the muzzle point).
            PlayMuzzleFlash();

            PlayMainVisual();

            if (useHitscan && hitscanEndpoint.HasValue)
            {
                Vector3 origin = transform.position;
                _hitscanTargetPos      = SanitizeHitscanEndpoint(origin, _direction, hitscanEndpoint.Value, _maxRange);
                _hitscanTargetDistance = Vector3.Distance(origin, _hitscanTargetPos);
                _maxRange              = Mathf.Max(_maxRange, _hitscanTargetDistance + 0.25f);
                LogProjectile($"[PROJ_VFX] Hitscan visual flight go='{gameObject.name}' origin={origin:F2} endpoint={_hitscanTargetPos:F2} dist={_hitscanTargetDistance:F2} speed={_speed:F1}");
            }

            if (!isImpact && fuseTime > 0f)
                _fuseRoutine = StartCoroutine(FuseRoutine());
        }

        /// <summary>
        /// Call after Initialize on the owner machine so that only the owner instance
        /// can send damage RPCs to the server (prevents duplicate damage from all clients).
        /// </summary>
        public void SetOwnerData(int shooterNetworkObjectId, string weaponId = null)
        {
            _isOwnerShot     = true;
            _shooterNetObjId = shooterNetworkObjectId;
            if (!string.IsNullOrEmpty(weaponId))
                _weaponId = weaponId;

            LogProjectile($"[PROJ.INIT] SetOwnerData - go='{gameObject.name}'  shooterNetObjId={shooterNetworkObjectId}  " +
                      $"weaponId='{_weaponId}'");
        }

        [Tooltip("Minimum travel distance before collision is registered. Prevents the projectile " +
                 "from self-detonating on the shooter's own colliders at the muzzle.")]
        [SerializeField] private float _selfIgnoreDistance = 0.4f;

        // -----------------------------------------------------------------
        // Movement
        // -----------------------------------------------------------------
        private void Update()
        {
            if (_hasDetonated || _config == null) return;

            float dt = Time.deltaTime;

            // Accumulate gravity into velocity (matches server ServerProjectileFlight).
            // GravityScale > 0 creates a downward-curving ballistic arc.
            if (_config.GravityScale > 0f)
                _velocity += Vector3.down * (_config.GravityScale * 9.81f * dt);

            Vector3 move = _velocity * dt;

            Vector3 startPosition = transform.position;
            if (!_useHitscan && TryGetSweptImpact(startPosition, move, out RaycastHit sweepHit))
            {
                MoveProjectile(sweepHit.point, move);
                _distanceTraveled += Vector3.Distance(startPosition, transform.position);
                HandleImpact(sweepHit.collider, sweepHit.point, sweepHit.normal);
                return;
            }

            MoveProjectile(startPosition + move, move);
            _distanceTraveled += move.magnitude;

            LogMovementTick();

            // Vision-range exit: XZ distance from spawn exceeds the player's visible circle.
            if (_visionRangeClamp > 0f)
            {
                Vector3 flat = transform.position - _spawnPosition;
                flat.y = 0f;
                if (flat.sqrMagnitude >= _visionRangeClamp * _visionRangeClamp)
                {
                    Despawn($"vision-range-exit distXZ={flat.magnitude:F2} clamp={_visionRangeClamp:F2}");
                    return;
                }
            }

            // Hitscan: reached target endpoint, snap to exact impact position and detonate.
            if (_useHitscan && _hitscanTargetDistance > 0f && _distanceTraveled >= _hitscanTargetDistance)
            {
                transform.position = _hitscanTargetPos;
                LogProjectile($"[PROJ.MOVE] Hitscan endpoint reached go='{gameObject.name}' pos={transform.position:F2} traveled={_distanceTraveled:F2}/{_hitscanTargetDistance:F2}");
                Detonate();
                return;
            }

            // Exceeded max range; despawn.
            if (_distanceTraveled >= _maxRange)
                Despawn($"max-range traveled={_distanceTraveled:F2} max={_maxRange:F2}");
        }

        // Collision
        private void OnTriggerEnter(Collider other)
        {
            if (_hasDetonated) return;

            // Hitscan bullets are visual-only and are already given an authoritative endpoint.
            // Let the movement step detonate at that endpoint instead of reacting to local
            // owner/remote-player colliders near the muzzle.
            if (_useHitscan)
                return;

            // Grace distance only applies to ignored owner colliders. Real geometry in front
            // of the muzzle should still stop the projectile immediately.
            if (_distanceTraveled < _selfIgnoreDistance && IsIgnoredCollider(other)) return;

            HandleImpact(other, transform.position, -_direction);
        }

        private void HandleImpact(Collider other, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (_hasDetonated || other == null)
                return;

            if (IsIgnoredCollider(other))
                return;

            if (IsFinite(hitPoint))
                transform.position = hitPoint;

            Vector3 normal = hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized : -_direction;

            var otherRoot = other.transform.root;
            LogProjectile($"[PROJ.HIT] Impact - proj='{gameObject.name}'  pos={transform.position:F1}  " +
                      $"collider='{other.name}'  root='{otherRoot.name}'  " +
                      $"isOwnerShot={_isOwnerShot}  useHitscan={_useHitscan}  dmg={_damage}");

            // For true ballistic projectiles: ONLY the instance that called SetOwnerData()
            // (i.e. the weapon-owner's local copy) may send damage RPCs.
            // Do not use FishNet.InstanceFinder.IsServerStarted here; on a host machine
            // IsServerStarted is always true, so every remote-visual projectile spawned by
            // ShowProjectileOnClientsRpc would incorrectly deal damage to whoever it hits.
            bool isAuthoritativeInstance = _isOwnerShot;
            if (isAuthoritativeInstance)
            {
                var hitbox = ComponentResolver.Find<PlayerHitboxMarker>(other)
                                            .OnSelf()
                                            .InChildren()
                                            .Resolve();
                if (hitbox != null && hitbox.HealthSystem != null)
                {
                    // Apply head multiplier from weapon config (matches hitscan path in WeaponBase)
                    float finalDamage = hitbox.IsHeadshot
                        ? _damage * (_config.DamageHeadMul > 0f ? _config.DamageHeadMul : 2f)
                        : _damage;

                    var info = new DamageInfo
                    {
                        Damage                 = finalDamage,
                        IsHeadshot             = hitbox.IsHeadshot,
                        HitPoint               = transform.position,
                        HitNormal              = normal,
                        ShooterNetworkObjectId = _shooterNetObjId,
                        WeaponId               = _weaponId ?? string.Empty,
                    };

                    LogProjectile($"[PROJ.HIT] Player hitbox hit - collider='{other.name}'  " +
                              $"dmg={finalDamage:F1}  headshot={hitbox.IsHeadshot}  " +
                              $"weaponId='{_weaponId}'  shooterNetObjId={_shooterNetObjId}");

                    hitbox.HealthSystem.RequestDamage(info);
                }
                else
                {
                    // Non-player hittable (boss, deployable, objective, etc.)
                    var hittable = other.GetComponentInParent<IHittable>();
                    if (hittable != null)
                    {
                        LogProjectile($"[PROJ.HIT] IHittable hit - collider='{other.name}'  hittable='{hittable.GetType().Name}'  " +
                                  $"dmg={_damage:F1}  weaponId='{_weaponId}'");
                        hittable.RequestDamage(new DamageInfo
                        {
                            Damage                 = _damage,
                            IsHeadshot             = false,
                            HitPoint               = transform.position,
                            HitNormal              = normal,
                            ShooterNetworkObjectId = _shooterNetObjId,
                            WeaponId               = _weaponId ?? string.Empty,
                        });
                    }
                    else
                    {
                        LogProjectile($"[PROJ.HIT] No damageable target on '{other.name}' (root='{otherRoot.name}') - " +
                                  $"no PlayerHitboxMarker, no IHittable. Detonating only.");
                    }
                }
            }
            else
            {
                // Visual-only projectile on a remote client copy; no damage applied.
                LogProjectile($"[PROJ.HIT] Visual-only projectile hit '{other.name}' - isOwnerShot=false, detonating only.");
            }

            Detonate();
        }

        // -----------------------------------------------------------------
        // Fuse timer
        // -----------------------------------------------------------------
        private IEnumerator FuseRoutine()
        {
            yield return new WaitForSeconds(fuseTime);
            if (!_hasDetonated) Detonate();
        }

        // Detonation
        private void Detonate()
        {
            if (_hasDetonated) return;
            _hasDetonated = true;

            if (_fuseRoutine != null)
            {
                StopCoroutine(_fuseRoutine);
                _fuseRoutine = null;
            }

            if (_useHitscan && hideTrailOnImpact && mainVisualChild != null && _hitscanTrailLingerDuration > 0f)
            {
                // For hitscan: keep the bullet trail visible for a short time so the player can see it,
                // then run the normal detonation (hide trail, show impact VFX, start despawn timer).
                _despawnRoutine = StartCoroutine(HitscanLingerThenDetonate());
            }
            else
            {
                // Instant detonation for ballistic projectiles and fuse grenades.
                TriggerDetonation(transform.position, SafeLookRotation(_direction));
                _despawnRoutine = StartCoroutine(DespawnAfter(lifetimeAfterImpact));
            }
        }

        private IEnumerator HitscanLingerThenDetonate()
        {
            yield return new WaitForSeconds(_hitscanTrailLingerDuration);
            TriggerDetonation(transform.position, SafeLookRotation(_direction));
            yield return StartCoroutine(DespawnAfter(lifetimeAfterImpact));
        }

        // -----------------------------------------------------------------
        // Despawn
        // -----------------------------------------------------------------
        private void Despawn(string reason = "manual")
        {
            if (_despawnRoutine != null) return;
            LogProjectile($"[PROJ.DESPAWN] go='{gameObject.name}' reason={reason} pos={transform.position:F2} traveled={_distanceTraveled:F2} useHitscan={_useHitscan}");
            _despawnRoutine = StartCoroutine(DespawnAfter(0f));
        }

        private void EnsurePhysicsSetup()
        {
            _colliders = GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] != null)
                    _colliders[i].isTrigger = true;
            }

            _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody == null)
                _rigidbody = gameObject.AddComponent<Rigidbody>();

            _rigidbody.useGravity = false;
            _rigidbody.isKinematic = true;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        private void MoveProjectile(Vector3 position, Vector3 move)
        {
            transform.position = position;
            if (move.magnitude > 0.001f)
            {
                _direction = move.normalized;
                transform.rotation = SafeLookRotation(_direction);
            }
        }

        private bool TryGetSweptImpact(Vector3 startPosition, Vector3 move, out RaycastHit hit)
        {
            hit = default;

            float distance = move.magnitude;
            if (distance <= 0.001f)
                return false;

            Vector3 direction = move / distance;
            float radius = ResolveSweepRadius();
            int count = Physics.SphereCastNonAlloc(
                startPosition,
                radius,
                direction,
                _sweepHits,
                distance,
                NightHuntLayers.MaskHitscanFull,
                QueryTriggerInteraction.Collide);

            if (count <= 0)
                return false;

            System.Array.Sort(_sweepHits, 0, count, RaycastHitDistanceComparer.Instance);
            for (int i = 0; i < count; i++)
            {
                Collider col = _sweepHits[i].collider;
                if (col == null || IsIgnoredCollider(col))
                    continue;

                hit = _sweepHits[i];
                LogProjectile($"[PROJ.SWEEP] hit collider='{col.name}' point={hit.point:F2} dist={hit.distance:F2} radius={radius:F2}");
                return true;
            }

            return false;
        }

        private bool IsIgnoredCollider(Collider col)
        {
            if (col == null)
                return true;

            if (col.transform == transform || col.transform.IsChildOf(transform))
                return true;

            return _ignoredRoot != null &&
                   (col.transform == _ignoredRoot || col.transform.IsChildOf(_ignoredRoot));
        }

        private float ResolveSweepRadius()
        {
            if (_colliders == null || _colliders.Length == 0)
                _colliders = GetComponentsInChildren<Collider>(includeInactive: true);

            float radius = _sweepRadius;
            for (int i = 0; i < _colliders.Length; i++)
            {
                Collider col = _colliders[i];
                if (col == null || !col.enabled)
                    continue;

                switch (col)
                {
                    case SphereCollider sphere:
                        radius = Mathf.Max(radius, sphere.radius * MaxAbsScale(sphere.transform.lossyScale));
                        break;
                    case CapsuleCollider capsule:
                        radius = Mathf.Max(radius, capsule.radius * MaxAbsScale(capsule.transform.lossyScale));
                        break;
                    default:
                        Vector3 extents = col.bounds.extents;
                        float smallestExtent = Mathf.Min(extents.x, Mathf.Min(extents.y, extents.z));
                        if (smallestExtent > 0.001f)
                            radius = Mathf.Max(radius, smallestExtent);
                        break;
                }
            }

            return Mathf.Max(0.01f, radius);
        }

        private static float MaxAbsScale(Vector3 scale)
            => Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));

        private void LogMovementTick()
        {
            if (!ProjectileDebugEnabled())
                return;

            float interval = Mathf.Max(0.05f, _movementDebugLogInterval);
            if (Time.time < _nextMovementLogTime)
                return;

            _nextMovementLogTime = Time.time + interval;
            LogProjectile($"[PROJ.MOVE] go='{gameObject.name}' pos={transform.position:F2} vel={_velocity:F2} speed={_velocity.magnitude:F1} traveled={_distanceTraveled:F2}/{_maxRange:F2} useHitscan={_useHitscan} targetDist={_hitscanTargetDistance:F2}");
        }

        private static Quaternion SafeLookRotation(Vector3 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
                return Quaternion.identity;

            return Quaternion.LookRotation(direction.normalized);
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

        private static Vector3 SanitizeHitscanEndpoint(Vector3 origin, Vector3 direction, Vector3 endpoint, float maxRange)
        {
            bool finite = IsFinite(endpoint);
            float range = maxRange > 0f ? maxRange : 150f;
            float sqrDist = (endpoint - origin).sqrMagnitude;
            float maxAllowed = Mathf.Max(range * 2f, 250f);
            bool plausible = sqrDist <= maxAllowed * maxAllowed;

            if (finite && plausible)
                return endpoint;

            Vector3 fallback = origin + direction.normalized * range;
            Debug.LogWarning($"[PROJ.INIT] Invalid hitscan endpoint rejected. origin={origin:F1} endpoint={endpoint:F1} finite={finite} sqrDist={sqrDist:F1} maxRange={range:F1} fallback={fallback:F1}");
            return fallback;
        }

        private static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsInfinity(v.x) &&
                   !float.IsNaN(v.y) && !float.IsInfinity(v.y) &&
                   !float.IsNaN(v.z) && !float.IsInfinity(v.z);
        }

        private sealed class RaycastHitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly RaycastHitDistanceComparer Instance = new RaycastHitDistanceComparer();

            public int Compare(RaycastHit x, RaycastHit y) => x.distance.CompareTo(y.distance);
        }

        private IEnumerator DespawnAfter(float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            // Return to pool if available, otherwise just deactivate.
            if (ProjectilePool.Instance != null)
                ProjectilePool.Instance.Return(this);
            else
                gameObject.SetActive(false);
        }
    }
}

