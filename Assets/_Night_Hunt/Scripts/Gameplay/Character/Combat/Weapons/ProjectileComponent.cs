using System.Collections;
using UnityEngine;
using NightHunt.Data;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Utilities;

namespace NightHunt.Gameplay.Character.Combat.Weapons
{
    /// <summary>
    /// Component for moving projectiles (bullets, grenades, smoke…).
    /// Inherits ProjectileBase — all VFX config is set on the prefab.
    ///
    /// Lifecycle:
    ///   OnEnable    → reset state (pool-friendly)
    ///   Initialize  → set direction/speed, play muzzle flash, start fuse if needed
    ///   Update      → move, check range
    ///   OnTrigger   → detonate if isImpact = true
    ///   Detonate    → hide MainVisual, activate DetonationVFX, wait lifetimeAfterImpact, deactivate
    ///
    /// For hitscan visual mode (useHitscan = true):
    ///   The projectile teleports to hitscanEndpoint immediately — damage was already applied
    ///   by the raycast; this is a purely visual trail.
    /// </summary>
    public class ProjectileComponent : ProjectileBase
    {
        // Runtime state — not serialized.
        private WeaponConfigData _config;
        private Vector3          _direction;
        private float            _speed;
        private float            _maxRange;
        private bool             _useHitscan;
        private float            _distanceTraveled;
        private bool             _hasDetonated;
        private Coroutine        _fuseRoutine;
        private Coroutine        _despawnRoutine;

        // Damage identity — set via SetOwnerData() by the spawner on the owner machine.
        private float  _damage;
        private bool   _isOwnerShot;   // Only owner may send damage RPCs to server.
        private int    _shooterNetObjId = -1;
        private string _weaponId;

        // Pool reset
        protected override void OnEnable()
        {
            base.OnEnable();
            _hasDetonated     = false;
            _distanceTraveled = 0f;
            _fuseRoutine      = null;
            _despawnRoutine   = null;
            _isOwnerShot      = false;
            _shooterNetObjId  = -1;
        }

        // Init — called on every spawn/reuse from pool.
        // hitscanEndpoint: if not null, the projectile jumps to this point immediately
        //                  (visual trail only — damage was already applied via raycast).
        public void Initialize(WeaponConfigData config, Vector3 dir, bool useHitscan,
                               Vector3? hitscanEndpoint = null)
        {
            _config     = config;
            _direction  = dir.normalized;
            _speed      = config.ProjectileSpeed;
            _maxRange   = config.MaxRange;
            _useHitscan = useHitscan;
            _damage     = config.DamageBody;
            _weaponId   = config.WeaponId;

            Debug.Log($"[PROJ.INIT] Initialize — go='{gameObject.name}'  pos={transform.position:F1}  " +
                      $"dir={_direction:F2}  speed={_speed}  maxRange={_maxRange}  " +
                      $"useHitscan={useHitscan}  isOwnerShot={_isOwnerShot}  dmg={_damage}  " +
                      $"endpoint={hitscanEndpoint?.ToString("F1") ?? "null"}");

            // Muzzle flash plays on the projectile (spawned at the muzzle point).
            PlayMuzzleFlash();

            PlayMainVisual();

            if (useHitscan && hitscanEndpoint.HasValue)
            {
                // Teleport visual trail to the impact point and detonate immediately.
                transform.position = hitscanEndpoint.Value;
                Debug.Log($"[PROJ.INIT] Hitscan teleport — go='{gameObject.name}'  endpoint={hitscanEndpoint.Value:F1}");
                Detonate();
                return;
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

            Debug.Log($"[PROJ.INIT] SetOwnerData — go='{gameObject.name}'  shooterNetObjId={shooterNetworkObjectId}  " +
                      $"weaponId='{_weaponId}'");
        }

        // -----------------------------------------------------------------
        // Movement
        // -----------------------------------------------------------------
        private void Update()
        {
            if (_hasDetonated || _config == null) return;

            Vector3 move = _direction * _speed * Time.deltaTime;

            // Apply gravity for ballistic projectiles (GravityScale > 0).
            if (_config.GravityScale > 0f)
                move.y -= _config.GravityScale * 9.81f * Time.deltaTime;

            transform.position    += move;
            _distanceTraveled     += move.magnitude;

            if (move.magnitude > 0.001f)
            {
                _direction          = move.normalized;
                transform.rotation  = Quaternion.LookRotation(_direction);
            }

            // Exceeded max range — despawn.
            if (_distanceTraveled >= _maxRange)
                Despawn();
        }

        // Collision
        private void OnTriggerEnter(Collider other)
        {
            if (_hasDetonated) return;

            var otherRoot = other.transform.root;
            Debug.Log($"[PROJ.HIT] OnTriggerEnter — proj='{gameObject.name}'  pos={transform.position:F1}  " +
                      $"collider='{other.name}'  root='{otherRoot.name}'  " +
                      $"isOwnerShot={_isOwnerShot}  useHitscan={_useHitscan}  dmg={_damage}");

            // Hitscan-logic mode: damage was already applied via WeaponSystem raycast.
            // Only run detonation VFX.
            if (_useHitscan)
            {
                Debug.Log($"[PROJ.HIT] Hitscan visual — detonating at {transform.position:F1}, no damage applied.");
                Detonate();
                return;
            }

            // For true ballistic projectiles: ONLY the instance that called SetOwnerData()
            // (i.e. the weapon-owner's local copy) may send damage RPCs.
            // DO NOT use FishNet.InstanceFinder.IsServerStarted here — on a host machine
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
                        HitNormal              = -_direction,
                        ShooterNetworkObjectId = _shooterNetObjId,
                        WeaponId               = _weaponId ?? string.Empty,
                    };

                    Debug.Log($"[PROJ.HIT] Player hitbox hit — collider='{other.name}'  " +
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
                        Debug.Log($"[PROJ.HIT] IHittable hit — collider='{other.name}'  hittable='{hittable.GetType().Name}'  " +
                                  $"dmg={_damage:F1}  weaponId='{_weaponId}'");
                        hittable.RequestDamage(new DamageInfo
                        {
                            Damage                 = _damage,
                            IsHeadshot             = false,
                            HitPoint               = transform.position,
                            HitNormal              = -_direction,
                            ShooterNetworkObjectId = _shooterNetObjId,
                            WeaponId               = _weaponId ?? string.Empty,
                        });
                    }
                    else
                    {
                        Debug.Log($"[PROJ.HIT] No damageable target on '{other.name}' (root='{otherRoot.name}') — " +
                                  $"no PlayerHitboxMarker, no IHittable. Detonating only.");
                    }
                }
            }
            else
            {
                // Visual-only projectile (remote client copy) — no damage applied.
                Debug.Log($"[PROJ.HIT] Visual-only projectile hit '{other.name}' — isOwnerShot=false, detonating only.");
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

            // Activate DetonationVFX child, hide trail/mesh.
            TriggerDetonation(transform.position, Quaternion.LookRotation(_direction));

            _despawnRoutine = StartCoroutine(DespawnAfter(lifetimeAfterImpact));
        }

        // -----------------------------------------------------------------
        // Despawn
        // -----------------------------------------------------------------
        private void Despawn()
        {
            if (_despawnRoutine != null) return;
            _despawnRoutine = StartCoroutine(DespawnAfter(0f));
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

