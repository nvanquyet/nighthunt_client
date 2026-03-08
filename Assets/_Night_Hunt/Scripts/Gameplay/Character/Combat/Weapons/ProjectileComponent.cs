using System.Collections;
using UnityEngine;
using NightHunt.Data;

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
        // runtime state — KHÔNG serialized
        private WeaponConfigData _config;
        private Vector3          _direction;
        private float            _speed;
        private float            _maxRange;
        private bool             _useHitscan;
        private float            _distanceTraveled;
        private bool             _hasDetonated;
        private Coroutine        _fuseRoutine;
        private Coroutine        _despawnRoutine;

        // -----------------------------------------------------------------
        // Pool reset
        // -----------------------------------------------------------------
        protected override void OnEnable()
        {
            base.OnEnable();      // reset VFX children
            _hasDetonated     = false;
            _distanceTraveled = 0f;
            _fuseRoutine      = null;
            _despawnRoutine   = null;
        }

        // -----------------------------------------------------------------
        // Init — gọi mỗi lần spawn/reuse
        // -----------------------------------------------------------------
        public void Initialize(WeaponConfigData config, Vector3 dir, bool useHitscan)
        {
            _config       = config;
            _direction    = dir.normalized;
            _speed        = config.ProjectileSpeed;
            _maxRange     = config.MaxRange;
            _useHitscan   = useHitscan;

            PlayMainVisual();

            // Bắt đầu fuse timer nếu có
            if (!isImpact && fuseTime > 0f)
                _fuseRoutine = StartCoroutine(FuseRoutine());
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

        // -----------------------------------------------------------------
        // Va chạm
        // -----------------------------------------------------------------
        private void OnTriggerEnter(Collider other)
        {
            if (_hasDetonated) return;

            // Hitscan: damage xử lý bên ProjectileWeapon rồi, nhưng vẫn Detonate để chạy VFX
            if (_useHitscan)
            {
                Detonate();
                return;
            }

            // Collider-based
            if (isImpact)
                Detonate();
            // isImpact = false → chỉ bounce/stick, fuse timer tự Detonate sau
        }

        // -----------------------------------------------------------------
        // Fuse timer
        // -----------------------------------------------------------------
        private IEnumerator FuseRoutine()
        {
            yield return new WaitForSeconds(fuseTime);
            if (!_hasDetonated) Detonate();
        }

        // -----------------------------------------------------------------
        // Detonation
        // -----------------------------------------------------------------
        private void Detonate()
        {
            if (_hasDetonated) return;
            _hasDetonated = true;

            if (_fuseRoutine != null)
            {
                StopCoroutine(_fuseRoutine);
                _fuseRoutine = null;
            }

            // Bật DetonationVFX, tắt trail/mesh nếu cần
            TriggerDetonation(transform.position, Quaternion.LookRotation(_direction));

            // TODO: thông báo server để apply damage / AoE (Server RPC)

            // Đợi VFX xong rồi trả về pool
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

            // SetActive(false) để trả về pool; nếu không dùng pool thì Destroy
            gameObject.SetActive(false);
            // Destroy(gameObject);   ← dùng cái này nếu chưa có pool
        }
    }
}

