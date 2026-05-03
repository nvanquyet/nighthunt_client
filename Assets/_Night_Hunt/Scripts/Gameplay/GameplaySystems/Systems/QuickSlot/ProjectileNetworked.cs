using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using NightHunt.Core;
using NightHunt.Gameplay.FogOfWar;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Gameplay.Character.Combat.Weapons;
using NightHunt.Networking.Player;
using NightHunt.Audio;

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
    /// 2. Server: Spawn(prefab) + Initialize(def, ownerRoot)
    /// 3. All Clients: OnStartNetwork() → visuals ready
    /// 4. Fuse expires or impact → Explode()
    /// 5. Server: RpcExplode() → all clients see VFX
    /// 6. Server: Despawn(gameObject)
    ///
    /// FOW:
    /// Implements IFogTeamOwned so FogTeamVisibilityBinder can hide enemy grenades
    /// while they are outside the local player's vision reveal radius.
    /// _ownerTeamId is a SyncVar set once on the server in Initialize().
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(FogTeamVisibilityBinder))] // auto-add FOW hider — no prefab edits needed
    public class ProjectileNetworked : NetworkBehaviour, IFogTeamOwned
    {
        // ── IFogTeamOwned (FOW visibility) ────────────────────────────────────────
        // Synced once on spawn — same team as local player → always visible; enemy → FOW-hidden.
        // -1 = unknown (pre-Initialize) → treated as neutral → always visible.
        private readonly SyncVar<int> _ownerTeamId = new SyncVar<int>(-1, new SyncTypeSettings
        {
            SendRate = 0,     // only send on first sync (spawn snapshot)
            Channel  = FishNet.Transporting.Channel.Reliable,
        });

        /// <inheritdoc/>
        public int  FogOwnerTeamId  => _ownerTeamId.Value;
        /// <inheritdoc/>
        /// Enemy grenades ARE hidden by FOW (visible only when entering a reveal radius).
        public bool FogAlwaysVisible => false;

        // ─────────────────────────────────────────────────────────────────────────
        //  Runtime
        // ─────────────────────────────────────────────────────────────────────────

        #region Runtime

        private ThrowableDefinition _def;
        private Rigidbody           _rb;
        private ProjectileBase      _projectileBase;

        // Owner identified by PlayerHealthSystem reference — survives rig refactors.
        private PlayerHealthSystem _ownerHealthSystem;
        private int _ownerNetworkObjectId = -1;

        private bool _initialized;
        private bool _exploded;

        [Tooltip("Layers polled by ProximityDetection. Set to PlayerHitBox to skip terrain/props.")]
        [SerializeField] private LayerMask _proximityLayers = ~0;

        // Shared static PhysicsMaterial — allocated once, reused by all non-bounce projectiles.
        private static PhysicsMaterial s_noBounce;

        // Pre-allocated overlap buffers — no GC alloc per frame/tick.
        private static readonly Collider[] s_proximityBuffer = new Collider[32];
        private static readonly Collider[] s_aoeBuffer       = new Collider[64];

        #endregion

        #region Events

        public event System.Action<ProjectileNetworked>           OnExploded;
        public event System.Action<ProjectileNetworked, Collision> OnImpactHit;

        #endregion

        // ─────────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────────

        #region Lifecycle

        private void Awake()
        {
            _rb             = GetComponent<Rigidbody>();
            _projectileBase = GetComponent<ProjectileBase>();

            // Self-heal layer — prevents prefab layer renames breaking physics.
            gameObject.layer = LayerMask.NameToLayer(NightHuntLayers.Throwable);

            if (_rb != null)
            {
                _rb.interpolation           = RigidbodyInterpolation.Interpolate;
                _rb.collisionDetectionMode  = CollisionDetectionMode.ContinuousDynamic;
            }

            // Runtime safety: ensures FogTeamVisibilityBinder exists on prefabs created
            // before [RequireComponent] was added. No-op if already present.
            EnsureFogBinder();
        }

        /// <summary>
        /// Adds <see cref="FogTeamVisibilityBinder"/> at runtime if missing.
        /// Covers prefabs that existed before [RequireComponent] was added to this class.
        /// No-op on dedicated server (client-only FOW component).
        /// </summary>
        private void EnsureFogBinder()
        {
#if !UNITY_SERVER
            if (GetComponent<FogTeamVisibilityBinder>() == null)
                gameObject.AddComponent<FogTeamVisibilityBinder>();
#endif
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            // Clients: visual ready. FogTeamVisibilityBinder (if on prefab) will auto-read
            // _ownerTeamId.Value from the spawn snapshot and apply/remove FogOfWarHider.
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────────
        //  Initialization
        // ─────────────────────────────────────────────────────────────────────────

        #region Initialization

        /// <summary>
        /// Called by server immediately after Spawn().
        /// </summary>
        [Server]
        public void Initialize(ThrowableDefinition def, Transform ownerRoot = null)
        {
            if (_initialized) return;
            _initialized = true;
            _def = def;

            // Owner health system — identifies the thrower for collision/damage skip.
            _ownerHealthSystem = ownerRoot != null
                ? (ownerRoot.GetComponent<PlayerHealthSystem>()
                   ?? ownerRoot.GetComponentInChildren<PlayerHealthSystem>(true))
                : null;

            // Owner network object ID — stored for DamageInfo.ShooterNetworkObjectId.
            var ownerNetworkObject = ownerRoot != null
                ? (ownerRoot.GetComponent<NetworkObject>()
                   ?? ownerRoot.GetComponentInParent<NetworkObject>()
                   ?? ownerRoot.GetComponentInChildren<NetworkObject>(true))
                : null;
            _ownerNetworkObjectId = ownerNetworkObject != null ? (int)ownerNetworkObject.ObjectId : -1;

            // ── FOW: resolve thrower's team ────────────────────────────────────
            // NetworkPlayer.TeamId is a SyncVar and always accurate on the server.
            // FogTeamVisibilityBinder on this GO reads FogOwnerTeamId (= _ownerTeamId.Value)
            // and decides whether to attach FogOfWarHider on remote clients.
            if (ownerRoot != null)
            {
                var ownerNP = ownerRoot.GetComponent<NetworkPlayer>()
                              ?? ownerRoot.GetComponentInParent<NetworkPlayer>()
                              ?? ownerRoot.GetComponentInChildren<NetworkPlayer>(true);
                _ownerTeamId.Value = ownerNP != null ? ownerNP.TeamId : -1;
            }
            // ──────────────────────────────────────────────────────────────────

            // Physics bounciness.
            if (!def.CanBounce)
            {
                var col = GetComponent<Collider>();
                if (col != null)
                {
                    if (s_noBounce == null)
                        s_noBounce = new PhysicsMaterial("NoBounce")
                        {
                            bounciness      = 0f,
                            frictionCombine = PhysicsMaterialCombine.Maximum,
                            bounceCombine   = PhysicsMaterialCombine.Minimum,
                        };
                    col.material = s_noBounce;
                }
            }

            // Fuse timer.
            bool usesFuse = def.FuseTime > 0f
                         && def.ThrowableType != ThrowableType.Impact
                         && def.ThrowableType != ThrowableType.Proximity;
            if (usesFuse)
                StartCoroutine(FuseCountdown(def.FuseTime));

            if (def.ThrowableType == ThrowableType.Proximity)
                StartCoroutine(ProximityDetection());

            RpcPlaySpawnVfx(transform.position, transform.rotation);
        }

        [ObserversRpc]
        private void RpcPlaySpawnVfx(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            _projectileBase ??= GetComponent<ProjectileBase>();
            if (_projectileBase == null)
                Debug.LogWarning($"[PROJ_VFX] ProjectileBase missing on '{name}' — no spawn VFX.");
            _projectileBase?.PlayMainVisual();
            _projectileBase?.PlayMuzzleFlash();
            LogThrowable($"Spawn VFX projectile='{name}' pos={position:F2}");
        }

        private IEnumerator FuseCountdown(float fuse)
        {
            yield return new WaitForSeconds(fuse);
            if (!_exploded) Explode();
        }

        /// <summary>
        /// Polls nearby colliders every 0.1 s (server-only).
        /// Detonates when any IDamageable enters ProximityDetectionRadius.
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
                int count = Physics.OverlapSphereNonAlloc(
                    transform.position, detectionR, s_proximityBuffer, _proximityLayers);

                for (int i = 0; i < count; i++)
                {
                    var hit = s_proximityBuffer[i];
                    if (hit.transform.IsChildOf(transform) || hit.transform == transform)
                        continue;

                    // Skip thrower hitboxes.
                    if (_ownerHealthSystem != null
                        && hit.TryGetComponent<PlayerHitboxMarker>(out var ownerCheck)
                        && ownerCheck.HealthSystem == _ownerHealthSystem)
                        continue;

                    if (hit.TryGetComponent<PlayerHitboxMarker>(out _) ||
                        hit.TryGetComponent<IHittable>(out _))
                    {
                        Explode();
                        yield break;
                    }
                }

                yield return wait;
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────────
        //  Collision
        // ─────────────────────────────────────────────────────────────────────────

        #region Collision

        private void OnCollisionEnter(Collision col)
        {
            if (!_initialized || _exploded) return;
            if (!IsServerInitialized)       return;
            if (IsOwnerCollider(col.collider))
            {
                LogThrowable($"Ignored owner collision projectile='{name}' collider='{col.collider?.name}'");
                return;
            }

            OnImpactHit?.Invoke(this, col);

            switch (_def.ThrowableType)
            {
                case ThrowableType.Impact:  Explode();            break;
                case ThrowableType.Sticky:  StickToSurface(col);  break;
            }
        }

        private bool IsOwnerCollider(Collider col)
        {
            if (col == null || _ownerHealthSystem == null) return false;

            if (col.TryGetComponent<PlayerHitboxMarker>(out var marker) &&
                marker.HealthSystem == _ownerHealthSystem)
                return true;

            var health = col.GetComponentInParent<PlayerHealthSystem>();
            return health != null && health == _ownerHealthSystem;
        }

        [Server]
        private void StickToSurface(Collision col)
        {
            _rb.isKinematic     = true;
            _rb.linearVelocity  = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            var contact = col.GetContact(0);
            transform.SetParent(col.transform, worldPositionStays: true);
            transform.position = contact.point;
            transform.up       = contact.normal;

            if (_def.FuseTime > 0f)
                StartCoroutine(FuseCountdown(_def.FuseTime));

            LogThrowable($"Stuck to '{col.gameObject.name}' pos={transform.position:F2}");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────────
        //  Explosion
        // ─────────────────────────────────────────────────────────────────────────

        #region Explosion

        [Server]
        public void Explode()
        {
            if (_exploded) return;
            _exploded = true;

            if (_def != null && _def.ExplosionRadius > 0f)
                DealAoeDamage();

            RpcExplode(transform.position, transform.rotation);
            OnExploded?.Invoke(this);
            StartCoroutine(DespawnAfterExplosion());
        }

        [ObserversRpc]
        private void RpcExplode(Vector3 position, Quaternion rotation)
        {
            _projectileBase?.TriggerDetonation(position, rotation);

            if (_def?.ImpactSound != null)
            {
                if (AudioManager.HasInstance)
                    AudioManager.Instance.Play3D(_def.ImpactSound, position,
                        AudioManager.Instance.GroupExplosion);
                else
                    AudioSource.PlayClipAtPoint(_def.ImpactSound, position);
            }
        }

        [Server]
        private IEnumerator DespawnAfterExplosion()
        {
            float delay = _projectileBase != null ? _projectileBase.lifetimeAfterImpact : 3f;
            yield return new WaitForSeconds(delay);
            if (NetworkObject != null)
                ServerManager.Despawn(gameObject);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────────
        //  Damage
        // ─────────────────────────────────────────────────────────────────────────

        #region Damage

        [Server]
        private void DealAoeDamage()
        {
            int count  = Physics.OverlapSphereNonAlloc(transform.position, _def.ExplosionRadius, s_aoeBuffer);
            Vector3 origin = transform.position;
            float radius   = _def.ExplosionRadius;

            var hitSystems = new HashSet<PlayerHealthSystem>();

            for (int i = 0; i < count; i++)
            {
                var hit = s_aoeBuffer[i];
                if (hit.transform.IsChildOf(transform) || hit.transform == transform) continue;

                float dist    = Vector3.Distance(origin, hit.transform.position);
                float falloff = Mathf.Clamp01(1f - dist / radius);

                // Path A: Player hitbox
                if (hit.TryGetComponent<PlayerHitboxMarker>(out var marker))
                {
                    var hs = marker.HealthSystem;
                    if (hs != null && hs == _ownerHealthSystem) continue;
                    if (hs != null && hitSystems.Add(hs))
                    {
                        hs.ApplyDamageServer(new DamageInfo
                        {
                            Damage                 = _def.Damage * falloff,
                            IsHeadshot             = false,
                            HitPoint               = hit.transform.position,
                            HitNormal              = (hit.transform.position - origin).normalized,
                            ShooterNetworkObjectId = _ownerNetworkObjectId,
                            WeaponId               = _def?.ItemID ?? string.Empty,
                        });
                    }
                }
                // Path B: Generic IHittable (boss, destructibles)
                else if (hit.TryGetComponent<IHittable>(out var hittable))
                {
                    hittable.RequestDamage(new DamageInfo
                    {
                        Damage                 = _def.Damage * falloff,
                        IsHeadshot             = false,
                        HitPoint               = hit.transform.position,
                        HitNormal              = (hit.transform.position - origin).normalized,
                        ShooterNetworkObjectId = _ownerNetworkObjectId,
                        WeaponId               = _def?.ItemID ?? string.Empty,
                    });
                }

                // Rigidbody impulse for physics crates etc.
                if (hit.TryGetComponent<Rigidbody>(out var rb) && !rb.isKinematic)
                    rb.AddForce((hit.transform.position - origin).normalized * (500f * falloff), ForceMode.Impulse);
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────────
        //  Gizmos / Debug
        // ─────────────────────────────────────────────────────────────────────────

        #region Gizmos

#if UNITY_EDITOR
        [Header("Editor Debug")]
        [Tooltip("Assign a ThrowableDefinition to preview its radii in the Scene View.")]
        [SerializeField] private ThrowableDefinition _debugDef;
#endif

        private void OnDrawGizmosSelected()
        {
            var defToDraw = _def;
#if UNITY_EDITOR
            if (defToDraw == null) defToDraw = _debugDef;
#endif
            if (defToDraw == null) return;

            // Draw Explosion Radius
            Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
            Gizmos.DrawSphere(transform.position, defToDraw.ExplosionRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, defToDraw.ExplosionRadius);

            // Draw Proximity Radius if applicable
            if (defToDraw.ThrowableType == ThrowableType.Proximity)
            {
                float proxR = defToDraw.ProximityDetectionRadius > 0f 
                    ? defToDraw.ProximityDetectionRadius 
                    : defToDraw.ExplosionRadius * 0.5f;
                    
                Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
                Gizmos.DrawSphere(transform.position, proxR);
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, proxR);
            }
        }

        private static bool ThrowableDebugEnabled()
        {
            var cfg = NightHuntDebugConfig.Instance;
            return cfg != null && cfg.EnableThrowableDebugLogs;
        }

        private static void LogThrowable(string message)
        {
            if (ThrowableDebugEnabled())
                Debug.Log($"[THROW_FLOW] {message}");
        }

        #endregion
    }

    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Interface for damageable entities.
    /// Implement on any MonoBehaviour that can receive damage.
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(float damage);
    }
}
