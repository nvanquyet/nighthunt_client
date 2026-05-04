using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace NightHunt.Gameplay.Boss
{
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
    public class TurretGun : NetworkBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────────
        [Header("Turret Parts")]
        [Tooltip("Transform phần xoay của model (đầu/thân trên của turret)")]
        [SerializeField] private Transform _turretHead;
        [Tooltip("Empty Transform đặt ngay họng súng — origin của vị trí đạn bay ra")]
        [SerializeField] private Transform _firePoint;

        [Header("Rotation Config")]
        [Tooltip("Tốc độ xoay bám target (deg/s). Client dùng để Slerp _turretHead")]
        [SerializeField] private float _trackSpeed = 180f;
        [Tooltip("Tốc độ quét idle khi not available target (deg/s). Quét trái/phải quanh góc prefab")]
        [SerializeField] private float _idleSweepSpeed = 20f;
        [Tooltip("Biên độ quét idle sang mỗi bên (độ). 0 = không quét, đứng yên")]
        [SerializeField] private float _idleSweepAngle = 45f;

        // ── SyncVar ──────────────────────────────────────────────────────────────
        // Server ghi hướng nhìn → Client Slerp turret head
        private readonly SyncVar<Vector3> _syncLookDir = new SyncVar<Vector3>();

        // ── Runtime ───────────────────────────────────────────────────────────────
        // Hướng mặc định = rotation set trong prefab, lưu lúc Awake (Server + Client)
        private Vector3 _defaultForward;
        // Phase quét idle (tăng/giảm để đánh qua lại)
        private float   _sweepPhase;
        private bool    _sweepDirection = true;

        // ── Public API ────────────────────────────────────────────────────────────
        public Transform FirePoint => _firePoint;

        /// <summary>
        /// Set the client-side visual projectile prefab. Called by BossController.OnStartServer
        /// so the prefab reference lives on the component and never needs to cross the network.
        /// </summary>
        public void SetProjectilePrefab(GameObject prefab) => _projectilePrefab = prefab;

        // ── Projectile prefab (client visual only — NOT serialized via RPC) ─────────
        // Set by BossController.OnStartServer via SetProjectilePrefab().
        // All TurretGuns on a single boss share the same prefab.
        private GameObject _projectilePrefab;

        // ── Unity Lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            // Save hướng mặc định từ rotation của prefab.
            // Đây là sector turret này phụ trách — idle sweep sẽ quét quanh đây.
            _defaultForward = transform.forward;
        }

        // ── Network Lifecycle ─────────────────────────────────────────────────────

        public override void OnStartServer()
        {
            base.OnStartServer();
            // Khởi tạo SyncVar về hướng mặc định ngay on spawn
            _syncLookDir.Value = _defaultForward;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            // Snap rotation về đúng hướng hiện tại khi Client nhận spawn packet
            // Tránh turret head nhảy giật từ local default về hướng server
            if (_turretHead != null && _syncLookDir.Value.sqrMagnitude > 0.01f)
                _turretHead.rotation = Quaternion.LookRotation(_syncLookDir.Value);
        }

        // ── Client Visual Update ──────────────────────────────────────────────────

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

        /// <summary>
        /// BossController gọi mỗi Server tick.
        /// - Có target: xoay về phía target, check LoS, trả về true nếu ready bắn.
        /// - Không target: quét idle quanh góc prefab, trả về false.
        /// </summary>
        [Server]
        public bool ServerTick(Transform target, float attackRadius, LayerMask obstacleMask)
        {
            if (target == null)
            {
                IdleSweep();
                return false;
            }

            // Tính hướng bắn về phía ngực target (chest height)
            Vector3 aimPoint = target.position + Vector3.up * 1.1f;
            Vector3 dir      = (aimPoint - GetFireOrigin()).normalized;

            // Sync hướng nhìn xuống Client để Slerp mượt
            _syncLookDir.Value = dir;

            // LoS check: trong tầm bắn + không bị chặn
            float dist = Vector3.Distance(GetFireOrigin(), aimPoint);
            if (dist > attackRadius) return false;
            if (Physics.Raycast(GetFireOrigin(), dir, dist, obstacleMask)) return false;

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
        public Vector3 GetFireOrigin() => _firePoint != null ? _firePoint.position : transform.position;

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
        public void RpcSpawnProjectileVisual(Vector3 hitPointOrDir, bool isHitscan, float speed)
        {
            if (_projectilePrefab == null)
            {
                Debug.LogWarning($"[VFX.BOSS] TurretGun.RpcSpawnProjectileVisual — _projectilePrefab is NULL. " +
                                 $"BossController.OnStartClient may not have called SetProjectilePrefab yet on this client.");
                return;
            }

            var pool = NightHunt.Gameplay.Character.Combat.Weapons.ProjectilePool.Instance;
            if (pool == null)
            {
                Debug.LogWarning("[VFX.BOSS] TurretGun.RpcSpawnProjectileVisual — ProjectilePool.Instance is NULL. " +
                                 "Add ProjectilePool to a persistent scene GameObject.");
                return;
            }

            Vector3 origin = GetFireOrigin();
            Vector3 dir    = isHitscan ? (hitPointOrDir - origin).normalized : hitPointOrDir.normalized;

            // ★ BUG FIX: For hitscan, pass the world-space impact point as hitscanEndpoint.
            // ProjectileComponent.Initialize() will fly the visual to that position and then
            // detonate there, so hit VFX is timed by distance / speed.
            Vector3? endpoint = isHitscan ? (Vector3?)hitPointOrDir : null;

            Debug.Log($"[VFX.BOSS] RpcSpawnProjectileVisual — isHitscan={isHitscan}  " +
                      $"origin={origin:F1}  hitPointOrDir={hitPointOrDir:F1}  dir={dir:F2}  " +
                      $"endpoint={endpoint?.ToString("F1") ?? "null (ballistic)"}");

            var proj = pool.Get(_projectilePrefab, origin, Quaternion.LookRotation(dir));
            if (proj != null)
            {
                var fakeConfig = new NightHunt.Data.WeaponConfigData
                {
                    ProjectileSpeed = speed,
                    MaxRange = 200f,
                    DamageBody = 0,     // Visual only — damage already applied server-side.
                    BallisticType = isHitscan ? "Hitscan" : "Projectile"
                };
                // Pass endpoint so hitscan visuals reach the correct hit position.
                if (isHitscan)
                    proj.SetHitscanHitType(true, -dir);

                proj.Initialize(fakeConfig, dir, isHitscan, endpoint);
            }
            else
            {
                Debug.LogWarning($"[VFX.BOSS] RpcSpawnProjectileVisual — ProjectilePool.Get() returned null. " +
                                 $"Increase pool capacity for prefab '{_projectilePrefab.name}'.");
            }
        }
    }
}
