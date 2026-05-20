using System;
using FishNet;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Data;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Gameplay.Character.Combat.Weapons;
using NightHunt.Gameplay.FogOfWar;
using UnityEngine.Events;

namespace NightHunt.Gameplay.Boss
{
    public enum BossTurretFireMode
    {
        Hitscan = 0,
        Projectile = 1
    }

    /// <summary>
    /// Một nòng súng turret độc lập trong Boss Point.
    ///
    /// SETUP TRONG PREFAB:
    ///   - Đặt GameObject này làm con của BossController.
    ///   - Set position/rotation tùy ý trong prefab — rotation đó trở thành
    ///     "góc trực chiến" (default facing) của turret này.
    ///   - BossController tự gom tất cả TurretGun qua OnValidate.
    ///
    /// DEFAULT FACING (hướng mặc định):
    ///   Save transform.forward lúc Awake (= rotation được đặt trong prefab).
    ///   Uses để:
    ///     1. Idle sweep — khi not available target, turret quét sang trái/phải
    ///        quanh góc trực chiến này (giống radar), KHÔNG quay 360°.
    ///        Điều này đảm bảo mỗi turret phụ trách đúng sector của nó.
    ///     2. Return to default — khi vừa mất target, quay về góc gốc trước
    ///        khi start idle sweep.
    ///
    /// NETWORK SYNC:
    ///   _syncLookDir (SyncVar Vector3): Server ghi hướng nhìn hiện tại,
    ///   Client dùng để RotateTowards turret head mỗi Update (smooth visual).
    ///
    /// VFX:
    ///   Sử dụng Object Pooling của game (ProjectilePool). TurretGun KHÔNG tự chứa ParticleSystem
    ///   hay Tracer. Mọi flow hình ảnh bắn đạn được ProjectileComponent lo trọn gói.
    /// </summary>
    [RequireComponent(typeof(FogTeamVisibilityBinder))]
    public class TurretGun : NetworkBehaviour, IHittable, IHealthSource, IBulletTarget, IFogTeamOwned
    {
        [Serializable]
        private sealed class TurretVisualChannel
        {
            [Tooltip("Optional animator for this muzzle/barrel.")]
            public Animator Animator;
            [Tooltip("Animator trigger used when this muzzle/barrel shoots. Empty = no animator trigger.")]
            public string ShootTrigger = "Fire";
            [Tooltip("Animator trigger used when this turret is destroyed. Empty = no animator trigger.")]
            public string DestroyedTrigger = "Destroyed";
            public UnityEvent OnShoot;
            public UnityEvent OnDestroyed;

            public void InvokeShoot()
            {
                SetTrigger(Animator, ShootTrigger);
                OnShoot?.Invoke();
            }

            public void InvokeDestroyed()
            {
                SetTrigger(Animator, DestroyedTrigger);
                OnDestroyed?.Invoke();
            }

            private static void SetTrigger(Animator animator, string triggerName)
            {
                if (animator == null || !animator.isActiveAndEnabled || string.IsNullOrEmpty(triggerName))
                    return;

                int hash = Animator.StringToHash(triggerName);
                animator.ResetTrigger(hash);
                animator.SetTrigger(hash);
            }
        }

        // ── Inspector ────────────────────────────────────────────────────────────
        [Header("Turret Parts")]
        [Tooltip("Transform phần xoay của model (đầu/thân trên của turret)")]
        [SerializeField] private Transform _turretHead;
        [Tooltip("Empty Transform đặt ngay họng súng — origin của vị trí đạn bay ra")]
        [SerializeField] private Transform _firePoint;
        [Tooltip("Optional multi-barrel fire points. If empty, _firePoint is used.")]
        [SerializeField] private Transform[] _firePoints;

        [Header("Rotation Config")]
        [Tooltip("Tốc độ xoay bám target (deg/s). Client dùng để Slerp _turretHead")]
        [SerializeField] private float _trackSpeed = 180f;
        [Tooltip("Tốc độ quét idle khi not available target (deg/s). Quét trái/phải quanh góc prefab")]
        [SerializeField] private float _idleSweepSpeed = 20f;
        [Tooltip("Biên độ quét idle sang mỗi bên (độ). 0 = không quét, đứng yên")]
        [SerializeField] private float _idleSweepAngle = 45f;

        [Header("Health")]
        [SerializeField, Min(1f)] private float _maxHp = 300f;

        [Header("Bullet Targeting")]
        [Tooltip("Additional local offset before BulletTargetConfig.TargetCentreYOffset is applied. Shooting targets the turret body, not BossController.")]
        [SerializeField] private Vector3 _bulletAcquirePointOffset = Vector3.zero;
        [SerializeField, Min(0.1f)] private float _bulletAcquireRadius = 1.0f;

        [Header("Per-Turret Weapon")]
        [SerializeField] private BossTurretFireMode _fireMode = BossTurretFireMode.Hitscan;
        [SerializeField, Min(0f)] private float _damage = 30f;
        [SerializeField, Min(0.01f)] private float _cooldown = 0.5f;
        [SerializeField, Min(1)] private int _firePointsPerVolley = 1;
        [SerializeField] private GameObject _projectilePrefab;
        [SerializeField, Min(1f)] private float _projectileSpeed = 60f;
        [SerializeField, Min(0f)] private float _gravityScale = 0f;
        [SerializeField] private bool _preferHighArc;

        [Header("Fog of War")]
        [Tooltip("Boss/turret team id used only for FOW. Use a non-player team id so players only see it inside vision.")]
        [SerializeField] private int _fogTeamId = 999;
        [SerializeField] private bool _fogAlwaysVisible;

        [Header("Visual Events")]
        [Tooltip("Client-side event fired on every shot, before the per-muzzle visual channel.")]
        [SerializeField] private UnityEvent _onShootVisual;
        [Tooltip("Client-side event fired once when this turret is destroyed, before all per-muzzle destroyed events.")]
        [SerializeField] private UnityEvent _onDestroyedVisual;
        [Tooltip("Optional visual channels by usable fire-point index. Use one channel per muzzle/barrel when each has its own Animator/VFX/SFX.")]
        [SerializeField] private TurretVisualChannel[] _visualChannels;

        // ── SyncVar ──────────────────────────────────────────────────────────────
        // Server ghi hướng nhìn → Client Slerp turret head
        private readonly SyncVar<Vector3> _syncLookDir = new SyncVar<Vector3>();
        private readonly SyncVar<float> _syncHp = new SyncVar<float>();
        private readonly SyncVar<bool> _syncDestroyed = new SyncVar<bool>();

        // ── Runtime ───────────────────────────────────────────────────────────────
        // Hướng mặc định = rotation set trong prefab, lưu lúc Awake (Server + Client)
        private Vector3 _defaultForward;
        // Phase quét idle (tăng/giảm để đánh qua lại)
        private float   _sweepPhase;
        private bool    _sweepDirection = true;
        private float   _cooldownTimer;
        private int     _nextFirePointIndex;
        private BossController _ownerBoss;

        // ── Public API ────────────────────────────────────────────────────────────
        public Transform FirePoint => GetFirePointByUsableIndex(_nextFirePointIndex);
        public bool CanFire => !IsDead && _cooldownTimer <= 0f;
        public bool PreferHighArc => _preferHighArc;
        public BossTurretFireMode FireMode => _fireMode;
        public float Damage => _damage;
        public float Cooldown => _cooldown;
        public GameObject ProjectilePrefab => _projectilePrefab;
        public float ProjectileSpeed => _projectileSpeed;
        public float GravityScale => _gravityScale;
        public float CurrentHealth => _syncHp.Value;
        public float MaxHealth => _maxHp;
        public bool IsDead => _syncDestroyed.Value;
        public HittableTargetType TargetType => HittableTargetType.Structure;
        public Vector3 AcquirePoint => transform.position + transform.TransformDirection(_bulletAcquirePointOffset);
        public float AcquireRadius => Mathf.Max(0.1f, _bulletAcquireRadius);
        public IHittable HitTarget => this;
        public bool IsAcquirable => isActiveAndEnabled && !IsDead;
        public int FogOwnerTeamId => _ownerBoss != null ? _ownerBoss.FogOwnerTeamId : _fogTeamId;
        public bool FogAlwaysVisible => _fogAlwaysVisible;
        public event Action<HealthChangeEvent> HealthChanged;

        public void SetBossOwner(BossController boss)
        {
            _ownerBoss = boss;
            GetComponent<FogTeamVisibilityBinder>()?.RefreshVisibilityForLocalTeam();
        }

        public void TickCooldown(float deltaTime)
        {
            if (_cooldownTimer > 0f)
                _cooldownTimer -= deltaTime;
        }

        public void MarkFired()
        {
            _cooldownTimer = Mathf.Max(0f, _cooldown);
        }

        public int FillVolleyFireOrigins(Vector3[] buffer, int[] visualChannelIndices)
        {
            if (IsDead || buffer == null || buffer.Length == 0)
                return 0;

            int usableCount = GetUsableFirePointCount();
            int shotCount = Mathf.Clamp(_firePointsPerVolley, 1, Mathf.Min(buffer.Length, usableCount));
            int startIndex = Mathf.Clamp(_nextFirePointIndex, 0, Mathf.Max(0, usableCount - 1));
            for (int i = 0; i < shotCount; i++)
            {
                int usableIndex = (startIndex + i) % usableCount;
                Transform point = GetFirePointByUsableIndex(usableIndex);
                buffer[i] = point != null ? point.position : transform.position;
                if (visualChannelIndices != null && i < visualChannelIndices.Length)
                    visualChannelIndices[i] = usableIndex;
            }

            _nextFirePointIndex = (startIndex + shotCount) % usableCount;
            return shotCount;
        }

        // ── Projectile prefab (client visual only — NOT serialized via RPC) ─────────
        // Projectile prefab is owned by this turret prefab. BossController does not override it.

        // ── Unity Lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            EnsureFogBinder();
            // Save hướng mặc định từ rotation của prefab.
            // Đây là sector turret này phụ trách — idle sweep sẽ quét quanh đây.
            _defaultForward = transform.forward;
        }

        private FogTeamVisibilityBinder EnsureFogBinder()
        {
#if !UNITY_SERVER
            var binder = GetComponent<FogTeamVisibilityBinder>();
            if (binder == null)
                binder = gameObject.AddComponent<FogTeamVisibilityBinder>();

            return binder;
#else
            return null;
#endif
        }

        // ── Network Lifecycle ─────────────────────────────────────────────────────

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            BulletTargetRegistry.Register(this);
        }

        public override void OnStopNetwork()
        {
            BulletTargetRegistry.Unregister(this);
            base.OnStopNetwork();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            // Khởi tạo SyncVar về hướng mặc định ngay on spawn
            if (_ownerBoss == null)
                _ownerBoss = GetComponentInParent<BossController>();

            _syncHp.Value = Mathf.Max(1f, _maxHp);
            _syncDestroyed.Value = false;
            _syncLookDir.Value = _defaultForward;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _syncHp.OnChange += OnHpSyncChanged;
            _syncDestroyed.OnChange += OnDestroyedSyncChanged;

            if (_ownerBoss == null)
                _ownerBoss = GetComponentInParent<BossController>();
            // Snap rotation về đúng hướng hiện tại khi Client nhận spawn packet
            // Tránh turret head nhảy giật từ local default về hướng server
            if (_turretHead != null && _syncLookDir.Value.sqrMagnitude > 0.01f)
                _turretHead.rotation = Quaternion.LookRotation(_syncLookDir.Value);

            if (_syncDestroyed.Value)
                InvokeDestroyedVisual();
        }

        // ── Client Visual Update ──────────────────────────────────────────────────

        public override void OnStopClient()
        {
            _syncHp.OnChange -= OnHpSyncChanged;
            _syncDestroyed.OnChange -= OnDestroyedSyncChanged;
            base.OnStopClient();
        }

        private void OnHpSyncChanged(float prev, float next, bool asServer)
        {
            HealthChanged?.Invoke(new HealthChangeEvent(prev, next, MaxHealth, forceReveal: next < prev));
        }

        private void OnDestroyedSyncChanged(bool prev, bool next, bool asServer)
        {
            if (!next || prev == next)
                return;

            InvokeDestroyedVisual();
            HealthChanged?.Invoke(new HealthChangeEvent(_syncHp.Value, 0f, MaxHealth, forceReveal: true));
        }

        private void InvokeShootVisual(int visualChannelIndex)
        {
            _onShootVisual?.Invoke();

            TurretVisualChannel channel = GetVisualChannel(visualChannelIndex);
            channel?.InvokeShoot();
        }

        private void InvokeDestroyedVisual()
        {
            _onDestroyedVisual?.Invoke();

            if (_visualChannels == null)
                return;

            for (int i = 0; i < _visualChannels.Length; i++)
                _visualChannels[i]?.InvokeDestroyed();
        }

        private TurretVisualChannel GetVisualChannel(int visualChannelIndex)
        {
            if (_visualChannels == null || _visualChannels.Length == 0)
                return null;

            if (visualChannelIndex < 0 || visualChannelIndex >= _visualChannels.Length)
                return null;

            return _visualChannels[visualChannelIndex];
        }

        private void Update()
        {
            // Chỉ chạy visual rotation trên Client
            if (!IsClientInitialized || _turretHead == null) return;
            if (_syncLookDir.Value.sqrMagnitude < 0.01f) return;

            // RotateTowards: mượt hơn Slerp, không overshoot
            Quaternion targetRot = Quaternion.LookRotation(_syncLookDir.Value);
            _turretHead.rotation = Quaternion.RotateTowards(
                _turretHead.rotation, targetRot,
                _trackSpeed * Time.deltaTime
            );
        }

        // ── Server API (gọi bởi BossController) ──────────────────────────────────

        public void RequestDamage(DamageInfo info)
        {
            if (IsServerStarted)
            {
                TakeDamageServer(info, ResolveAttacker(info.ShooterNetworkObjectId));
                return;
            }

            RequestDamageServerRpc(info);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestDamageServerRpc(DamageInfo info)
        {
            TakeDamageServer(info, ResolveAttacker(info.ShooterNetworkObjectId));
        }

        [Server]
        public void TakeDamageServer(DamageInfo info, NetworkObject attacker = null)
        {
            if (IsDead)
                return;

            float damage = Mathf.Max(0f, info.Damage);
            if (damage <= 0f)
                return;

            float previous = _syncHp.Value > 0f ? _syncHp.Value : Mathf.Max(1f, _maxHp);
            float next = Mathf.Max(0f, previous - damage);
            _syncHp.Value = next;

            if (_ownerBoss == null)
                _ownerBoss = GetComponentInParent<BossController>();

            attacker ??= ResolveAttacker(info.ShooterNetworkObjectId);
            _ownerBoss?.NotifyTurretDamaged(this, info, attacker);

            HealthChanged?.Invoke(new HealthChangeEvent(previous, next, MaxHealth, info.ShooterNetworkObjectId, forceReveal: true));

            if (next <= 0f)
                DestroyTurretServer(info, attacker);
        }

        [Server]
        private void DestroyTurretServer(DamageInfo info, NetworkObject attacker)
        {
            if (_syncDestroyed.Value)
                return;

            _syncDestroyed.Value = true;
            _cooldownTimer = float.PositiveInfinity;
            _syncLookDir.Value = _defaultForward;
            _ownerBoss?.NotifyTurretDestroyed(this, info, attacker);
        }

        private static NetworkObject ResolveAttacker(int shooterNetObjId)
        {
            if (shooterNetObjId <= 0 || InstanceFinder.ServerManager == null)
                return null;

            InstanceFinder.ServerManager.Objects.Spawned.TryGetValue(shooterNetObjId, out var attacker);
            return attacker;
        }

        /// <summary>
        /// BossController gọi mỗi Server tick.
        /// - Có target: xoay về phía target, check LoS, trả về true nếu ready bắn.
        /// - Không target: quét idle quanh góc prefab, trả về false.
        /// </summary>
        [Server]
        public bool ServerTick(Transform target, float attackRadius, LayerMask obstacleMask)
        {
            if (IsDead)
                return false;

            if (target == null)
            {
                IdleSweep();
                return false;
            }

            // Tính hướng bắn về phía ngực target (chest height)
            Vector3 aimPoint = target.position + Vector3.up * 1.1f;
            Vector3 origin = GetFireOrigin();
            Vector3 toTarget = aimPoint - origin;
            if (toTarget.sqrMagnitude <= 0.0001f)
                return false;

            Vector3 dir = toTarget.normalized;

            // Sync hướng nhìn xuống Client để Slerp mượt
            _syncLookDir.Value = dir;

            // LoS check: trong tầm bắn + không bị chặn
            float dist = toTarget.magnitude;
            if (dist > attackRadius) return false;
            if (Physics.Raycast(origin, dir, dist, obstacleMask)) return false;

            return true;
        }

        /// <summary>
        /// Quét idle: đánh qua lại quanh _defaultForward trong biên độ _idleSweepAngle.
        /// Giúp từng turret "alert" trong sector của nó khi không phát hiện enemy.
        /// </summary>
        [Server]
        private void IdleSweep()
        {
            if (_idleSweepAngle <= 0f)
            {
                // Nếu biên độ = 0: đứng yên ở góc mặc định
                _syncLookDir.Value = _defaultForward;
                return;
            }

            // Đánh qua lại
            _sweepPhase += (_sweepDirection ? 1f : -1f) * _idleSweepSpeed * Time.deltaTime;
            if (_sweepPhase >= _idleSweepAngle)       { _sweepPhase = _idleSweepAngle;  _sweepDirection = false; }
            else if (_sweepPhase <= -_idleSweepAngle) { _sweepPhase = -_idleSweepAngle; _sweepDirection = true;  }

            // Xoay từ góc mặc định sang trái/phải theo _sweepPhase
            Vector3 sweepDir = Quaternion.Euler(0f, _sweepPhase, 0f) * _defaultForward;
            _syncLookDir.Value = sweepDir.normalized;
        }

        /// <summary>Origin điểm bắn — dùng cho LoS raycast và Instantiate đạn.</summary>
        public Vector3 GetFireOrigin()
        {
            Transform point = GetFirePointByUsableIndex(_nextFirePointIndex);
            return point != null ? point.position : transform.position;
        }

        private int GetUsableFirePointCount()
        {
            int count = 0;
            if (_firePoints != null)
            {
                for (int i = 0; i < _firePoints.Length; i++)
                    if (_firePoints[i] != null)
                        count++;
            }

            return count > 0 ? count : 1;
        }

        private Transform GetFirePointByUsableIndex(int usableIndex)
        {
            if (_firePoints != null && _firePoints.Length > 0)
            {
                int count = GetUsableFirePointCount();
                int target = count > 0 ? Mathf.Abs(usableIndex) % count : 0;
                int seen = 0;
                for (int i = 0; i < _firePoints.Length; i++)
                {
                    if (_firePoints[i] == null)
                        continue;

                    if (seen == target)
                        return _firePoints[i];

                    seen++;
                }
            }

            return _firePoint != null ? _firePoint : transform;
        }

        // ── Visual Bullet Spawn (Client Side Pool) ──────────────────────────────

        /// <summary>
        /// Spawns the client-side projectile visual from the pool.
        /// Only primitive data crosses the network — the prefab is stored locally on each client.
        ///
        /// hitPointOrDir:
        ///   isHitscan=true  → world-space impact point (aimPoint from HitscanAttack).
        ///                     Used as hitscanEndpoint so the visual flies to the hit position
        ///                     before playing hit VFX.
        ///   isHitscan=false → normalized fly direction (from RocketAttack).
        /// </summary>
        [ObserversRpc]
        public void RpcSpawnProjectileVisual(
            Vector3 origin,
            Vector3 hitPointOrDir,
            int visualChannelIndex,
            bool isHitscan,
            float speed,
            float gravityScale,
            bool hasTargetPoint,
            Vector3 targetPoint,
            bool preferHighArc)
        {
            InvokeShootVisual(visualChannelIndex);

            GameObject prefab = _projectilePrefab;
            if (prefab == null)
            {
                Debug.LogWarning($"[VFX.BOSS] TurretGun.RpcSpawnProjectileVisual — _projectilePrefab is NULL. " +
                                 $"Assign Projectile Prefab on this TurretGun prefab.");
                return;
            }

            var pool = NightHunt.Gameplay.Character.Combat.Weapons.ProjectilePool.Instance;
            if (pool == null)
            {
                Debug.LogWarning("[VFX.BOSS] TurretGun.RpcSpawnProjectileVisual — ProjectilePool.Instance is NULL. " +
                                 "Add ProjectilePool to a persistent scene GameObject.");
                return;
            }

            Vector3 dir = isHitscan ? hitPointOrDir - origin : hitPointOrDir;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
            var fakeConfig = new WeaponConfigData
            {
                ProjectileSpeed = speed,
                MaxRange = 200f,
                DamageBody = 0,
                BallisticType = isHitscan ? "Hitscan" : "Projectile",
                GravityScale = gravityScale,
                HasProjectileTargetPoint = hasTargetPoint,
                ProjectileTargetPoint = targetPoint,
                PreferHighArc = preferHighArc
            };

            if (!isHitscan && hasTargetPoint)
                dir = BallisticTrajectory.ResolveLaunchDirection(origin, dir, fakeConfig);

            // ★ BUG FIX: For hitscan, pass the world-space impact point as hitscanEndpoint.
            // ProjectileComponent.Initialize() will fly the visual to that position and then
            // detonate there, so hit VFX is timed by distance / speed.
            Vector3? endpoint = isHitscan ? (Vector3?)hitPointOrDir : null;

            Debug.Log($"[VFX.BOSS] RpcSpawnProjectileVisual — isHitscan={isHitscan}  " +
                      $"origin={origin:F1}  hitPointOrDir={hitPointOrDir:F1}  dir={dir:F2}  " +
                      $"endpoint={endpoint?.ToString("F1") ?? "null (ballistic)"}");

            var proj = pool.Get(prefab, origin, Quaternion.LookRotation(dir));
            if (proj != null)
            {
                fakeConfig = new WeaponConfigData
                {
                    ProjectileSpeed = speed,
                    MaxRange = 200f,
                    DamageBody = 0,     // Visual only — damage already applied server-side.
                    BallisticType = isHitscan ? "Hitscan" : "Projectile",
                    GravityScale = gravityScale,
                    HasProjectileTargetPoint = hasTargetPoint,
                    ProjectileTargetPoint = targetPoint,
                    PreferHighArc = preferHighArc
                };
                // Pass endpoint so hitscan visuals reach the correct hit position.
                if (isHitscan)
                    proj.SetHitscanHitType(true, -dir);

                proj.Initialize(fakeConfig, dir, isHitscan, endpoint);
            }
            else
            {
                Debug.LogWarning($"[VFX.BOSS] RpcSpawnProjectileVisual — ProjectilePool.Get() returned null. " +
                                 $"Increase pool capacity for prefab '{prefab.name}'.");
            }
        }
    }
}
