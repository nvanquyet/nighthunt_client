using System.Collections;
using UnityEngine;
using GameplaySystems.Core.Data;

namespace GameplaySystems.Inventory
{
    /// <summary>
    /// Attach to a throwable projectile prefab (grenade, molotov …).
    /// Requires: Rigidbody, at least one Collider.
    ///
    /// Lifecycle:
    ///   Instantiated by ItemUseSystem.SpawnProjectile()
    ///   → Initialize(def) sets up fuse timer + physics
    ///   → Fuse expires (or Impact type hits surface) → Explode()
    ///   → VFX spawned, damage dealt via IDamageable, GameObject destroyed
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        // ── Runtime ────────────────────────────────────────────────────────────
        private ThrowableDefinition _def;
        private Rigidbody           _rb;
        private bool                _initialized;
        private bool                _exploded;

        // ── Events ─────────────────────────────────────────────────────────────
        public event System.Action<Projectile>           OnExploded;
        public event System.Action<Projectile, Collision> OnImpactHit;

        // ──────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        /// <summary>
        /// Called by ItemUseSystem immediately after Instantiate.
        /// Configures physics material and starts fuse timer if needed.
        /// </summary>
        public void Initialize(ThrowableDefinition def)
        {
            if (_initialized) return;
            _initialized = true;
            _def         = def;

            // Physics bounciness
            if (!def.CanBounce)
            {
                var col = GetComponent<Collider>();
                if (col != null)
                {
                    var mat = new PhysicsMaterial("NoBounce")
                    {
                        bounciness       = 0f,
                        frictionCombine  = PhysicsMaterialCombine.Maximum,
                        bounceCombine    = PhysicsMaterialCombine.Minimum,
                    };
                    col.material = mat;
                }
            }

            // Fuse timer (timed detonation)
            if (def.FuseTime > 0f && def.ThrowableType != ThrowableType.Impact)
                StartCoroutine(FuseCountdown(def.FuseTime));

            Debug.Log($"[Projectile] Initialized '{def.DisplayName}' (type={def.ThrowableType}, fuse={def.FuseTime} s)");
        }

        private IEnumerator FuseCountdown(float fuse)
        {
            yield return new WaitForSeconds(fuse);
            if (!_exploded) Explode();
        }

        // ── Collision ──────────────────────────────────────────────────────────

        private void OnCollisionEnter(Collision col)
        {
            if (!_initialized || _exploded) return;

            OnImpactHit?.Invoke(this, col);

            switch (_def.ThrowableType)
            {
                case ThrowableType.Impact:
                    Explode();
                    break;

                case ThrowableType.Sticky:
                    StickToSurface(col);
                    break;
            }
        }

        private void StickToSurface(Collision col)
        {
            _rb.isKinematic    = true;
            _rb.linearVelocity        = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            var contact = col.GetContact(0);
            transform.SetParent(col.transform, worldPositionStays: true);
            transform.position = contact.point;
            transform.up       = contact.normal;

            // Start fuse from stick time
            if (_def.FuseTime > 0f)
                StartCoroutine(FuseCountdown(_def.FuseTime));

            Debug.Log($"[Projectile] Stuck to '{col.gameObject.name}'");
        }

        // ── Explosion ──────────────────────────────────────────────────────────

        /// <summary>Trigger explosion manually (e.g. from an external system).</summary>
        public void Explode()
        {
            if (_exploded) return;
            _exploded = true;

            Debug.Log($"[Projectile] Exploding '{_def?.DisplayName}' at {transform.position}");

            // AoE damage
            if (_def != null && _def.ExplosionRadius > 0f)
                DealAoeDamage();

            // VFX
            if (_def?.ExplosionEffectPrefab != null)
                Instantiate(_def.ExplosionEffectPrefab, transform.position, Quaternion.identity);

            // SFX
            if (_def?.ImpactSound != null)
                AudioSource.PlayClipAtPoint(_def.ImpactSound, transform.position);

            OnExploded?.Invoke(this);

            Destroy(gameObject);
        }

        private void DealAoeDamage()
        {
            var hits = Physics.OverlapSphere(transform.position, _def.ExplosionRadius);
            int count = 0;

            foreach (var hit in hits)
            {
                var damageable = hit.GetComponent<IDamageable>();
                if (damageable == null) continue;

                float dist    = Vector3.Distance(transform.position, hit.transform.position);
                float falloff = Mathf.Clamp01(1f - dist / _def.ExplosionRadius);
                float dmg     = _def.Damage * falloff;

                damageable.TakeDamage(dmg);
                count++;

                Debug.Log($"[Projectile] Dealt {dmg:F1} dmg to '{hit.name}' (falloff {falloff:P0})");

                // Physics impulse
                var rb = hit.GetComponent<Rigidbody>();
                if (rb != null && !rb.isKinematic)
                {
                    Vector3 dir   = (hit.transform.position - transform.position).normalized;
                    float   force = 500f * falloff;
                    rb.AddForce(dir * force, ForceMode.Impulse);
                }
            }

            Debug.Log($"[Projectile] AoE hit {count} damageable(s) in r={_def.ExplosionRadius} m");
        }

        // ── Gizmos ─────────────────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            if (_def == null) return;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _def.ExplosionRadius);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Implement on any MonoBehaviour that can receive damage.
    /// Decouples Projectile from specific health systems.
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(float damage);
    }
}