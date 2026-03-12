using System.Collections;
using UnityEngine;
using NightHunt.Data;
using NightHunt.Gameplay.Character.Combat;

namespace NightHunt.Gameplay.Character.Combat.Weapons
{
    /// <summary>
    /// Component cho đạn/projectile di chuyển (bullet, grenade, smoke…).
    /// Kế thừa ProjectileBase — config VFX + detonation đặt trực tiếp trên prefab.
    ///
    /// Lifecycle:
    ///   OnEnable  → reset state (pool-friendly)
    ///   Initialize → set direction/speed, khởi động fuse nếu có
    ///   Update     → di chuyển, kiểm tra tầm bay
    ///   OnTrigger  → kích nổ nếu isImpact = true
    ///   Detonate   → tắt MainVisual, bật DetonationVFX, đợi lifetimeAfterImpact rồi tắt object
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
        public void Initialize(WeaponConfigData config, Vector3 dir, bool useHitscan)
        {
            _config     = config;
            _direction  = dir.normalized;
            _speed      = config.ProjectileSpeed;
            _maxRange   = config.MaxRange;
            _useHitscan = useHitscan;
            _damage     = config.DamageBody;
            _weaponId   = config.WeaponId;

            PlayMainVisual();

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
        }

        // -----------------------------------------------------------------
        // Di chuyển
        // -----------------------------------------------------------------
        private void Update()
        {
            if (_hasDetonated || _config == null) return;

            Vector3 move = _direction * _speed * Time.deltaTime;

            // Gravity nếu đạn có ballistic
            if (_config.BallisticType == "Projectile")
                move.y -= _config.GravityScale * 9.81f * Time.deltaTime;

            transform.position    += move;
            _distanceTraveled     += move.magnitude;

            if (move.magnitude > 0.001f)
            {
                _direction          = move.normalized;
                transform.rotation  = Quaternion.LookRotation(_direction);
            }

            // Quá tầm và không phải throwable → tắt
            if (_distanceTraveled >= _maxRange)
                Despawn();
        }

        // Collision
        private void OnTriggerEnter(Collider other)
        {
            if (_hasDetonated) return;

            // Hitscan-logic mode: damage was already applied via WeaponSystem raycast.
            // Only run detonation VFX.
            if (_useHitscan)
            {
                Detonate();
                return;
            }

            // For true ballistic projectiles: only the owner instance (or server in dedicated mode)
            // sends damage to avoid duplicate hits from all client copies.
            bool isAuthoritativeInstance = _isOwnerShot || FishNet.InstanceFinder.IsServerStarted;
            if (isAuthoritativeInstance)
            {
                var hitbox = other.GetComponent<PlayerHitboxMarker>();
                if (hitbox != null && hitbox.HealthSystem != null)
                {
                    var info = new DamageInfo
                    {
                        Damage                 = _damage,
                        IsHeadshot             = hitbox.IsHeadshot,
                        HitPoint               = transform.position,
                        HitNormal              = -_direction,
                        ShooterNetworkObjectId = _shooterNetObjId,
                        WeaponId               = _weaponId ?? string.Empty,
                    };

                    Debug.Log($"[ProjectileComponent] Projectile hit '{other.name}' — damage: {_damage:F1}" +
                              $", headshot: {hitbox.IsHeadshot}, owner shot: {_isOwnerShot}");

                    hitbox.HealthSystem.RequestDamage(info);
                }
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

