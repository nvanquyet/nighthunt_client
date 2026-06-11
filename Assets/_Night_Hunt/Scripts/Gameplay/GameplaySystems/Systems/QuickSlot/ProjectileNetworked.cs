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
using NightHunt.Gameplay.Feedback;
using NightHunt.Gameplay.StatSystem.Core.Data;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Networking.Player;
using NightHunt.Audio;
using NightHunt.GameplaySystems.Inventory;

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

        private readonly SyncVar<string> _definitionId = new SyncVar<string>(string.Empty, new SyncTypeSettings
        {
            SendRate = 0,
            Channel = FishNet.Transporting.Channel.Reliable,
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
        private bool _areaEffectFinished = true;

        [Tooltip("Layers polled by ProximityDetection. Set to PlayerHitBox to skip terrain/props.")]
        [SerializeField] private LayerMask _proximityLayers = ~0;

        // Shared static PhysicsMaterial — allocated once, reused by all non-bounce projectiles.
        private static PhysicsMaterial s_noBounce;

        // Pre-allocated overlap buffers — no GC alloc per frame/tick.
        private static readonly Collider[] s_proximityBuffer = new Collider[32];
        private static readonly Collider[] s_aoeBuffer       = new Collider[64];
        private static readonly List<PlayerHealthSystem> s_areaRemoveList = new List<PlayerHealthSystem>(16);

        private readonly Dictionary<PlayerHealthSystem, string> _activeAreaEffectSources = new Dictionary<PlayerHealthSystem, string>(16);

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
            _definitionId.Value = def != null ? def.ItemID : string.Empty;

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

            RpcPlaySpawnVfx(transform.position, transform.rotation, _definitionId.Value);
        }

        [ObserversRpc]
        private void RpcPlaySpawnVfx(Vector3 position, Quaternion rotation, string definitionId)
        {
            transform.SetPositionAndRotation(position, rotation);
            var resolvedDef = ResolveDefinition(definitionId);
            _projectileBase ??= GetComponent<ProjectileBase>();
            if (_projectileBase == null)
                Debug.LogWarning($"[PROJ_VFX] ProjectileBase missing on '{name}' — no spawn VFX.");
            _projectileBase?.PlayMainVisual();
            _projectileBase?.PlayMuzzleFlash();
            PlayThrowableReleaseAudio(resolvedDef, position);
            LogThrowable($"Spawn VFX projectile='{name}' pos={position:F2}");
        }

        private ThrowableDefinition ResolveDefinition(string definitionId = null)
        {
            if (_def != null)
                return _def;

            string id = !string.IsNullOrEmpty(definitionId) ? definitionId : _definitionId.Value;
            if (string.IsNullOrEmpty(id))
                return null;

            _def = ItemDatabase.GetDefinition(id) as ThrowableDefinition;
            return _def;
        }

        private static void PlayThrowableReleaseAudio(ThrowableDefinition def, Vector3 position)
        {
            AudioClip clip = def != null ? def.ThrowSound : null;
            if (clip == null && AudioManager.HasInstance)
                clip = AudioManager.Instance.Library?.throwablePull;

            if (clip == null)
                return;

            if (AudioManager.HasInstance)
                AudioManager.Instance.PlayWeapon3D(clip, position);
            else
                AudioSource.PlayClipAtPoint(clip, position);
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

                    if (TryResolvePlayerHealth(hit, out var healthSystem))
                    {
                        if (!ShouldAffectPlayer(healthSystem))
                            continue;

                        Explode();
                        yield break;
                    }

                    var hittable = hit.GetComponentInParent<IHittable>();
                    if (hittable != null && ShouldAffectHittable(hittable))
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

            RpcExplode(transform.position, transform.rotation, _definitionId.Value);
            OnExploded?.Invoke(this);
            if (HasPersistentAreaEffect())
                StartCoroutine(PersistentAreaEffect());
            StartCoroutine(DespawnAfterExplosion());
        }

        [ObserversRpc]
        private void RpcExplode(Vector3 position, Quaternion rotation, string definitionId)
        {
            _projectileBase?.TriggerDetonation(position, rotation);

            var resolvedDef = ResolveDefinition(definitionId);
            AudioClip impactClip = resolvedDef?.ImpactSound;
            if (impactClip == null && AudioManager.HasInstance)
                impactClip = AudioManager.Instance.Library?.explosionGrenade
                             ?? AudioManager.Instance.Library?.explosionRocket;

            if (impactClip != null)
            {
                if (AudioManager.HasInstance)
                    AudioManager.Instance.PlayExplosion3D(impactClip, position);
                else
                    AudioSource.PlayClipAtPoint(impactClip, position);
            }
        }

        [Server]
        private IEnumerator DespawnAfterExplosion()
        {
            float delay = ResolvePostExplosionLifetime();
            yield return new WaitForSeconds(delay);
            RemoveAllAreaEffectModifiers();
            if (NetworkObject != null)
                ServerManager.Despawn(gameObject);
        }

        private float ResolvePostExplosionLifetime()
        {
            float visualLifetime = _projectileBase != null ? _projectileBase.lifetimeAfterImpact : 3f;
            if (!HasPersistentAreaEffect())
                return visualLifetime;

            return Mathf.Max(visualLifetime, _def.AreaEffectDuration + 0.1f);
        }

        private bool HasPersistentAreaEffect()
        {
            return _def != null
                   && _def.AreaEffectDuration > 0f
                   && _def.ExplosionRadius > 0f
                   && ((_def.AreaPlayerModifiers != null && _def.AreaPlayerModifiers.Length > 0)
                       || !Mathf.Approximately(_def.AreaHealthDeltaPerSecond, 0f)
                       || !Mathf.Approximately(_def.AreaStaminaDeltaPerSecond, 0f));
        }

        [Server]
        private IEnumerator PersistentAreaEffect()
        {
            _areaEffectFinished = false;
            float endTime = Time.time + _def.AreaEffectDuration;
            float tickInterval = Mathf.Max(0.05f, _def.AreaEffectTickInterval);

            while (!_areaEffectFinished && Time.time < endTime)
            {
                TickPersistentAreaEffect(tickInterval);
                yield return new WaitForSeconds(tickInterval);
            }

            RemoveAllAreaEffectModifiers();
            _areaEffectFinished = true;
        }

        [Server]
        private void TickPersistentAreaEffect(float deltaTime)
        {
            s_areaPlayers.Clear();

            int count = Physics.OverlapSphereNonAlloc(transform.position, _def.ExplosionRadius, s_aoeBuffer);
            for (int i = 0; i < count; i++)
            {
                Collider hit = s_aoeBuffer[i];
                if (!TryResolvePlayerHealth(hit, out var hs) || !ShouldAffectPlayer(hs))
                    continue;

                s_areaPlayers.Add(hs);
                ApplyAreaModifiers(hs);
                ApplyAreaTick(hs, deltaTime);
            }

            s_areaRemoveList.Clear();
            foreach (var kv in _activeAreaEffectSources)
            {
                if (!s_areaPlayers.Contains(kv.Key))
                    s_areaRemoveList.Add(kv.Key);
            }

            for (int i = 0; i < s_areaRemoveList.Count; i++)
                RemoveAreaEffectModifiers(s_areaRemoveList[i]);
        }

        private static readonly HashSet<PlayerHealthSystem> s_areaPlayers = new HashSet<PlayerHealthSystem>();

        [Server]
        private void ApplyAreaModifiers(PlayerHealthSystem healthSystem)
        {
            if (_activeAreaEffectSources.ContainsKey(healthSystem)
                || _def.AreaPlayerModifiers == null
                || _def.AreaPlayerModifiers.Length == 0)
                return;

            var stats = ResolveStats(healthSystem);
            if (stats == null)
                return;

            string sourceId = $"throwable:{ObjectId}:{_definitionId.Value}:{healthSystem.ObjectId}";
            for (int i = 0; i < _def.AreaPlayerModifiers.Length; i++)
            {
                var mod = _def.AreaPlayerModifiers[i];
                stats.AddModifier(mod.StatType, CreateRuntimeModifier(sourceId, mod));
            }

            _activeAreaEffectSources[healthSystem] = sourceId;
        }

        [Server]
        private void ApplyAreaTick(PlayerHealthSystem healthSystem, float deltaTime)
        {
            var stats = ResolveStats(healthSystem);
            if (stats == null)
                return;

            if (!Mathf.Approximately(_def.AreaHealthDeltaPerSecond, 0f))
            {
                float delta = _def.AreaHealthDeltaPerSecond * deltaTime;
                if (delta < 0f)
                {
                    var damageInfo = new DamageInfo
                    {
                        Damage = -delta,
                        IsHeadshot = false,
                        HitPoint = healthSystem.transform.position,
                        HitNormal = Vector3.up,
                        ShooterNetworkObjectId = _ownerNetworkObjectId,
                        WeaponId = _def?.ItemID ?? string.Empty,
                    };
                    healthSystem.ApplyDamageServer(damageInfo);
                }
                else
                {
                    float health = stats.GetStat(PlayerStatType.Health);
                    float maxHealth = Mathf.Max(1f, stats.GetStat(PlayerStatType.MaxHealth));
                    stats.SetCurrentStat(PlayerStatType.Health, Mathf.Min(maxHealth, health + delta));
                }
            }

            if (!Mathf.Approximately(_def.AreaStaminaDeltaPerSecond, 0f))
            {
                float stamina = stats.GetStat(PlayerStatType.Stamina);
                float maxStamina = Mathf.Max(1f, stats.GetStat(PlayerStatType.MaxStamina));
                stats.SetCurrentStat(
                    PlayerStatType.Stamina,
                    Mathf.Clamp(stamina + _def.AreaStaminaDeltaPerSecond * deltaTime, 0f, maxStamina));
            }
        }

        [Server]
        private void RemoveAllAreaEffectModifiers()
        {
            _areaEffectFinished = true;
            s_areaRemoveList.Clear();
            foreach (var kv in _activeAreaEffectSources)
                s_areaRemoveList.Add(kv.Key);

            for (int i = 0; i < s_areaRemoveList.Count; i++)
                RemoveAreaEffectModifiers(s_areaRemoveList[i]);
        }

        [Server]
        private void RemoveAreaEffectModifiers(PlayerHealthSystem healthSystem)
        {
            if (healthSystem == null || !_activeAreaEffectSources.TryGetValue(healthSystem, out string sourceId))
                return;

            ResolveStats(healthSystem)?.RemoveAllModifiersFromSource(sourceId);
            _activeAreaEffectSources.Remove(healthSystem);
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
            var hitHittables = new HashSet<IHittable>();

            for (int i = 0; i < count; i++)
            {
                var hit = s_aoeBuffer[i];
                if (hit.transform.IsChildOf(transform) || hit.transform == transform) continue;

                float dist    = Vector3.Distance(origin, hit.transform.position);
                float falloff = Mathf.Clamp01(1f - dist / radius);

                // Path A: Player hitbox
                if (TryResolvePlayerHealth(hit, out var hs))
                {
                    if (!ShouldAffectPlayer(hs) || !hitSystems.Add(hs) || hs.IsDead)
                        continue;

                    var damageInfo = new DamageInfo
                    {
                        Damage                 = _def.Damage * falloff,
                        IsHeadshot             = false,
                        HitPoint               = hit.transform.position,
                        HitNormal              = (hit.transform.position - origin).normalized,
                        ShooterNetworkObjectId = _ownerNetworkObjectId,
                        WeaponId               = _def?.ItemID ?? string.Empty,
                    };

                    if (hs.TryApplyDamageServer(damageInfo))
                        SendLocalHitFeedback(damageInfo, CombatHitFeedbackTargetKind.Player);
                }
                // Path B: Generic IHittable (boss, destructibles)
                else if (hit.GetComponentInParent<IHittable>() is { } hittable)
                {
                    if (!ShouldAffectHittable(hittable) || !hitHittables.Add(hittable))
                        continue;

                    var damageInfo = new DamageInfo
                    {
                        Damage                 = _def.Damage * falloff,
                        IsHeadshot             = false,
                        HitPoint               = hit.transform.position,
                        HitNormal              = (hit.transform.position - origin).normalized,
                        ShooterNetworkObjectId = _ownerNetworkObjectId,
                        WeaponId               = _def?.ItemID ?? string.Empty,
                    };

                    var targetKind = CombatHitFeedbackTargetKind.GenericHittable;
                    if (hittable is PlayerHealthSystem playerHealth)
                    {
                        if (!playerHealth.TryApplyDamageServer(damageInfo))
                            continue;

                        targetKind = CombatHitFeedbackTargetKind.Player;
                    }
                    else if (hittable is NightHunt.Gameplay.Deployables.BaseDeployable deployable)
                    {
                        deployable.TakeDamage(damageInfo);
                        targetKind = CombatHitFeedbackTargetKind.Deployable;
                    }
                    else
                    {
                        hittable.RequestDamage(damageInfo);
                        if (hittable is NightHunt.Gameplay.Boss.TurretGun)
                            targetKind = CombatHitFeedbackTargetKind.Boss;
                    }

                    SendLocalHitFeedback(damageInfo, targetKind);
                }

                // Rigidbody impulse for physics crates etc.
                if (hit.TryGetComponent<Rigidbody>(out var rb) && !rb.isKinematic)
                    rb.AddForce((hit.transform.position - origin).normalized * (500f * falloff), ForceMode.Impulse);
            }

            DealAoeDamageToRegisteredPlayers(origin, radius, hitSystems);
        }

        [Server]
        private void DealAoeDamageToRegisteredPlayers(Vector3 origin, float radius, HashSet<PlayerHealthSystem> hitSystems)
        {
            var allHealthSystems = UnityEngine.Object.FindObjectsByType<PlayerHealthSystem>(FindObjectsSortMode.None);
            float safeRadius = Mathf.Max(radius, 0.01f);

            foreach (var hs in allHealthSystems)
            {
                if (hs == null || hs.IsDead || !ShouldAffectPlayer(hs))
                    continue;

                Vector3 targetPos = hs.transform.position;
                float dist = Vector3.Distance(origin, targetPos);
                if (dist > radius || !hitSystems.Add(hs))
                    continue;

                float falloff = Mathf.Clamp01(1f - dist / safeRadius);
                var damageInfo = new DamageInfo
                {
                    Damage                 = _def.Damage * falloff,
                    IsHeadshot             = false,
                    HitPoint               = targetPos,
                    HitNormal              = (targetPos - origin).normalized,
                    ShooterNetworkObjectId = _ownerNetworkObjectId,
                    WeaponId               = _def?.ItemID ?? string.Empty,
                };

                if (hs.TryApplyDamageServer(damageInfo))
                    SendLocalHitFeedback(damageInfo, CombatHitFeedbackTargetKind.Player);
            }
        }

        private bool ShouldAffectPlayer(PlayerHealthSystem healthSystem)
        {
            if (healthSystem == null || healthSystem.IsDead)
                return false;

            var targetPlayer = healthSystem.GetComponentInParent<NetworkPlayer>();
            if (targetPlayer != null && (int)targetPlayer.ObjectId == _ownerNetworkObjectId)
                return true;

            if (_def == null || _def.AllowFriendlyFire)
                return true;

            int ownerTeam = _ownerTeamId.Value;
            int targetTeam = ResolveTeamId(healthSystem);
            return targetTeam < 0 || ownerTeam < 0 || targetTeam != ownerTeam;
        }

        private bool ShouldAffectHittable(IHittable hittable)
        {
            if (hittable == null)
                return false;

            if (_def == null || _def.AllowFriendlyFire)
                return true;

            if (hittable is NightHunt.Gameplay.Deployables.BaseDeployable deployable)
            {
                int ownerTeam = _ownerTeamId.Value;
                int targetTeam = deployable.OwnerTeamId;
                return targetTeam < 0 || ownerTeam < 0 || targetTeam != ownerTeam;
            }

            return true;
        }

        private static int ResolveTeamId(PlayerHealthSystem healthSystem)
        {
            if (healthSystem == null)
                return -1;

            var player = healthSystem.GetComponentInParent<NetworkPlayer>();
            return player != null ? player.TeamId : -1;
        }

        private static bool TryResolvePlayerHealth(Collider hit, out PlayerHealthSystem healthSystem)
        {
            healthSystem = null;
            if (hit == null)
                return false;

            if (hit.TryGetComponent<PlayerHitboxMarker>(out var marker) && marker.HealthSystem != null)
            {
                healthSystem = marker.HealthSystem;
                return true;
            }

            healthSystem = hit.GetComponentInParent<PlayerHealthSystem>();
            return healthSystem != null;
        }

        private static IPlayerStatSystem ResolveStats(PlayerHealthSystem healthSystem)
        {
            if (healthSystem == null)
                return null;

            return healthSystem.GetComponent<IPlayerStatSystem>()
                   ?? healthSystem.GetComponentInChildren<IPlayerStatSystem>(true)
                   ?? healthSystem.GetComponentInParent<IPlayerStatSystem>();
        }

        private static StatModifier CreateRuntimeModifier(string sourceId, PlayerStatModifier mod)
        {
            return mod.ModifierType switch
            {
                ModifierType.Percentage => StatModifier.CreatePercentage(sourceId, mod.Value, -100, mod.Description),
                ModifierType.Override => StatModifier.CreateOverride(sourceId, mod.Value, mod.Description),
                _ => StatModifier.CreateFlat(sourceId, mod.Value, -100, mod.Description)
            };
        }

        [Server]
        private void SendLocalHitFeedback(DamageInfo info, CombatHitFeedbackTargetKind targetKind)
        {
            if (targetKind == CombatHitFeedbackTargetKind.None || _ownerNetworkObjectId <= 0)
                return;

            if (!FishNet.InstanceFinder.ServerManager.Objects.Spawned.TryGetValue(_ownerNetworkObjectId, out var ownerObject))
                return;

            NetworkConnection ownerConnection = ownerObject.Owner;
            if (ownerConnection == null)
                return;

            TargetLocalHitFeedbackRpc(ownerConnection, info, (byte)targetKind);
        }

        [TargetRpc]
        private void TargetLocalHitFeedbackRpc(NetworkConnection conn, DamageInfo info, byte targetKind)
        {
            CombatFeedbackEvents.PublishLocalHitConfirmed(
                info,
                (CombatHitFeedbackTargetKind)targetKind);
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
