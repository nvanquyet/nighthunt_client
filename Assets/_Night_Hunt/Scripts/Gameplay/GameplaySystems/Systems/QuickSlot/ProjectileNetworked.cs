using System.Collections;
using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using NightHunt.GameplaySystems.Core.Data;

namespace NightHunt.GameplaySystems.QuickSlot
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
        private bool _initialized;
        private bool _exploded;
        
        #endregion
        
        #region Events
        
        public event System.Action<ProjectileNetworked> OnExploded;
        public event System.Action<ProjectileNetworked, Collision> OnImpactHit;
        
        #endregion
        
        #region Lifecycle
        
        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
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
            
            // Physics bounciness
            if (!def.CanBounce)
            {
                var col = GetComponent<Collider>();
                if (col != null)
                {
                    var mat = new PhysicsMaterial("NoBounce")
                    {
                        bounciness = 0f,
                        frictionCombine = PhysicsMaterialCombine.Maximum,
                        bounceCombine = PhysicsMaterialCombine.Minimum,
                    };
                    col.material = mat;
                }
            }
            
            // Start fuse timer if applicable
            if (def.FuseTime > 0f && def.ThrowableType != ThrowableType.Impact)
                StartCoroutine(FuseCountdown(def.FuseTime));
            
            Debug.Log($"[Projectile] Initialized '{def.DisplayName}' " +
                     $"(type={def.ThrowableType}, fuse={def.FuseTime}s)");
        }
        
        private IEnumerator FuseCountdown(float fuse)
        {
            yield return new WaitForSeconds(fuse);
            
            if (!_exploded)
                Explode();
        }
        
        #endregion
        
        #region Collision
        
        private void OnCollisionEnter(Collision col)
        {
            if (!_initialized || _exploded)
                return;
            
            // Only server processes collision logic
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
            
            Debug.Log($"[Projectile] Exploding '{_def?.DisplayName}' at {transform.position}");
            
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
            // VFX
            if (_def?.ExplosionEffectPrefab != null)
            {
                var vfx = Instantiate(_def.ExplosionEffectPrefab, position, rotation);
                Destroy(vfx, 5f); // Auto-cleanup VFX
            }
            
            // SFX
            if (_def?.ImpactSound != null)
                AudioSource.PlayClipAtPoint(_def.ImpactSound, position);
        }
        
        [Server]
        private IEnumerator DespawnAfterExplosion()
        {
            // Small delay to ensure RPC delivered
            yield return new WaitForSeconds(0.1f);
            
            // Despawn from network
            if (NetworkObject != null)
                ServerManager.Despawn(gameObject);
        }
        
        #endregion
        
        #region Damage
        
        [Server]
        private void DealAoeDamage()
        {
            var hits = Physics.OverlapSphere(transform.position, _def.ExplosionRadius);
            int count = 0;
            
            foreach (var hit in hits)
            {
                var damageable = hit.GetComponent<IDamageable>();
                if (damageable == null)
                    continue;
                
                float dist = Vector3.Distance(transform.position, hit.transform.position);
                float falloff = Mathf.Clamp01(1f - dist / _def.ExplosionRadius);
                float dmg = _def.Damage * falloff;
                
                damageable.TakeDamage(dmg);
                count++;
                
                Debug.Log($"[Projectile] Dealt {dmg:F1} dmg to '{hit.name}' (falloff {falloff:P0})");
                
                // Physics impulse
                var rb = hit.GetComponent<Rigidbody>();
                if (rb != null && !rb.isKinematic)
                {
                    Vector3 dir = (hit.transform.position - transform.position).normalized;
                    float force = 500f * falloff;
                    rb.AddForce(dir * force, ForceMode.Impulse);
                }
            }
            
            Debug.Log($"[Projectile] AoE hit {count} target(s) in r={_def.ExplosionRadius}m");
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