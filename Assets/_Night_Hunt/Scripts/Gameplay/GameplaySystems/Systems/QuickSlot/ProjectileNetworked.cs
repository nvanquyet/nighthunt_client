using System.Collections;
using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.Gameplay.Character.Combat.Weapons;

namespace NightHunt.GameplaySystems.ItemUse
{
    /// <summary>
    /// PRODUCTION-READY Networked Projectile
    /// 
    /// Requirements:
    /// - Add NetworkObject component to prefab
    /// - Server spawns, all clients see
    /// - Explosion synced via ObserversRpc
    /// 
    /// Lifecycle:
    /// 1. Server: ThrowableHandler.SpawnProjectile()
    /// 2. Server: Spawn(prefab) + Initialize(def)
    /// 3. All Clients: OnStartNetwork() → visuals ready
    /// 4. Fuse expires or impact → Explode()
    /// 5. Server: RpcExplode() → all clients see VFX
    /// 6. Server: Despawn(gameObject)
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(NetworkObject))]
    public class ProjectileNetworked : NetworkBehaviour
    {
        #region Runtime

        private ThrowableDefinition _def;
        private Rigidbody _rb;
        private ProjectileBase _projectileBase;
        private bool _initialized;
        private bool _exploded;

        // Shared static PhysicsMaterial — allocated once, reused by all non-bounce projectiles.
        private static PhysicsMaterial s_noBounce;

        // Pre-allocated overlap buffer for ProximityDetection — avoids a GC allocation every 0.1 s.
        // 32 slots is generous; a grenade is unlikely to be within proximity of more.
        private static readonly Collider[] s_proximityBuffer = new Collider[32];

        // Pre-allocated overlap buffer for AoE damage — sized for realistic explosion area.
        private static readonly Collider[] s_aoeBuffer = new Collider[64];
        
        #endregion
        
        #region Events
        
        public event System.Action<ProjectileNetworked> OnExploded;
        public event System.Action<ProjectileNetworked, Collision> OnImpactHit;
        
        #endregion
        
        #region Lifecycle
        
        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _projectileBase = GetComponent<ProjectileBase>();
        }
        
        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            
            // Client-side: ready to display projectile
            if (!IsServerInitialized)
            {
                // Can enable client-side prediction here if needed
            }
        }
        
        #endregion
        
        #region Initialization
        
        /// <summary>
        /// Called by server immediately after spawn
        /// </summary>
        [Server]
        public void Initialize(ThrowableDefinition def)
        {
            if (_initialized) return;
            
            _initialized = true;
            _def = def;
            
            // Physics bounciness — reuse shared static material to avoid a new allocation per spawn.
            if (!def.CanBounce)
            {
                var col = GetComponent<Collider>();
                if (col != null)
                {
                    if (s_noBounce == null)
                        s_noBounce = new PhysicsMaterial("NoBounce")
                        {
                            bounciness        = 0f,
                            frictionCombine   = PhysicsMaterialCombine.Maximum,
                            bounceCombine     = PhysicsMaterialCombine.Minimum,
                        };
                    col.material = s_noBounce;
                }
            }
            
            // Start fuse timer if applicable.
            // Proximity type uses its own sensor loop; Impact type only fires on collision.
            bool usesFuse = def.FuseTime > 0f
                         && def.ThrowableType != ThrowableType.Impact
                         && def.ThrowableType != ThrowableType.Proximity;

            if (usesFuse)
                StartCoroutine(FuseCountdown(def.FuseTime));

            // Proximity: detect enemies entering detection radius, then explode.
            if (def.ThrowableType == ThrowableType.Proximity)
                StartCoroutine(ProximityDetection());

            // Intentionally no per-spawn debug log in production.
        }
        
        private IEnumerator FuseCountdown(float fuse)
        {
            yield return new WaitForSeconds(fuse);
            
            if (!_exploded)
                Explode();
        }

        /// <summary>
        /// Polls nearby colliders every 0.1 s (server-only).
        /// Detonates when any <see cref="IDamageable"/> enters <see cref="ThrowableDefinition.ProximityDetectionRadius"/>.
        /// Falls back to 50% of <see cref="ThrowableDefinition.ExplosionRadius"/> when detection radius is 0.
        /// </summary>
        [Server]
        private IEnumerator ProximityDetection()
        {
            float detectionR = _def.ProximityDetectionRadius > 0f
                ? _def.ProximityDetectionRadius
                : _def.ExplosionRadius * 0.5f;

            var wait = new WaitForSeconds(0.1f);

            while (!_exploded)
            {
                // NonAlloc: writes into the shared static buffer — zero heap allocation per tick.
                int count = Physics.OverlapSphereNonAlloc(transform.position, detectionR, s_proximityBuffer);
                for (int i = 0; i < count; i++)
                {
                    var hit = s_proximityBuffer[i];
                    if (hit.transform.IsChildOf(transform) || hit.transform == transform)
                        continue;

                    if (hit.TryGetComponent<IDamageable>(out _))
                    {
                        Explode();
                        yield break;
                    }
                }

                yield return wait;
            }
        }

        #endregion
        
        #region Collision
        
        private void OnCollisionEnter(Collision col)
        {
            if (!_initialized || _exploded)
                return;

            // Only server processes collision logic.
            if (!IsServerInitialized)
                return;

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
        
        [Server]
        private void StickToSurface(Collision col)
        {
            _rb.isKinematic = true;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            
            var contact = col.GetContact(0);
            transform.SetParent(col.transform, worldPositionStays: true);
            transform.position = contact.point;
            transform.up = contact.normal;
            
            // Start fuse from stick time
            if (_def.FuseTime > 0f)
                StartCoroutine(FuseCountdown(_def.FuseTime));
            
            Debug.Log($"[Projectile] Stuck to '{col.gameObject.name}'");
        }
        
        #endregion
        
        #region Explosion
        
        /// <summary>
        /// Trigger explosion (server-side)
        /// Syncs to all clients via RPC
        /// </summary>
        [Server]
        public void Explode()
        {
            if (_exploded) return;
            
            _exploded = true;

            // Server: Apply damage
            if (_def != null && _def.ExplosionRadius > 0f)
                DealAoeDamage();
            
            // Clients: Show VFX/SFX
            RpcExplode(transform.position, transform.rotation);
            
            OnExploded?.Invoke(this);
            
            // Despawn from network
            StartCoroutine(DespawnAfterExplosion());
        }
        
        /// <summary>
        /// Sync explosion to all clients
        /// </summary>
        [ObserversRpc]
        private void RpcExplode(Vector3 position, Quaternion rotation)
        {
            // VFX — bật DetonationVFX child thay vì Instantiate prefab mới
            _projectileBase?.TriggerDetonation(position, rotation);

            // SFX
            if (_def?.ImpactSound != null)
                AudioSource.PlayClipAtPoint(_def.ImpactSound, position);
        }
        
        [Server]
        private IEnumerator DespawnAfterExplosion()
        {
            // Đợi VFX chạy xong rồi mới despawn khỏi network
            float delay = _projectileBase != null ? _projectileBase.lifetimeAfterImpact : 3f;
            yield return new WaitForSeconds(delay);

            if (NetworkObject != null)
                ServerManager.Despawn(gameObject);
        }
        
        #endregion
        
        #region Damage
        
        [Server]
        private void DealAoeDamage()
        {
            // NonAlloc: reuse shared static buffer — no GC per explosion.
            int count = Physics.OverlapSphereNonAlloc(transform.position, _def.ExplosionRadius, s_aoeBuffer);
            Vector3 origin = transform.position;
            float radius   = _def.ExplosionRadius;

            for (int i = 0; i < count; i++)
            {
                var hit = s_aoeBuffer[i];
                if (!hit.TryGetComponent<IDamageable>(out var damageable))
                    continue;

                float dist    = Vector3.Distance(origin, hit.transform.position);
                float falloff = Mathf.Clamp01(1f - dist / radius);
                damageable.TakeDamage(_def.Damage * falloff);

                if (hit.TryGetComponent<Rigidbody>(out var rb) && !rb.isKinematic)
                {
                    Vector3 dir = (hit.transform.position - origin).normalized;
                    rb.AddForce(dir * (500f * falloff), ForceMode.Impulse);
                }
            }
        }
        
        #endregion
        
        #region Gizmos
        
        private void OnDrawGizmosSelected()
        {
            if (_def == null) return;
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _def.ExplosionRadius);
        }
        
        #endregion
    }
    
    // ══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Interface for damageable entities
    /// Implement on any MonoBehaviour that can receive damage
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(float damage);
    }
}