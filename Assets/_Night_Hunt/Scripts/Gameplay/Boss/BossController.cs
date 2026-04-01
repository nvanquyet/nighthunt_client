using System;
using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Data;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Gameplay.Core.Events;
using NightHunt.Gameplay.Match;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.ItemUse;
using NightHunt.GameplaySystems.Loot;
using NightHunt.GameplaySystems.World;
using UnityEngine;

namespace NightHunt.Gameplay.Boss
{
    /// <summary>
    /// BossController — Server-authoritative coordinator của một Boss Point.
    ///
    /// MỘT BossController điều phối NHIỀU TurretGun (multi-barrel support).
    /// Mỗi TurretGun có FirePoint + MuzzleFlash riêng và xử lý VFX cục bộ.
    ///
    /// FLOW HOÀN CHỈNH:
    ///
    /// [Server] BossSpawnManager.SpawnBoss(cfg)
    ///   → Instantiate BossPrefab → ServerManager.Spawn
    ///   → BossController.Initialize(cfg)   ← inject HP/stats từ Inspector config
    ///
    /// [Server] Update() mỗi frame
    ///   → TickScanForTargets() (OverlapSphere interval, không phải mỗi frame)
    ///   → Foreach TurretGun: gun.ServerTick(target)
    ///     → TurretGun check LoS → trả về hasLoS
    ///     → Nếu hasLoS và cooldown sẵn: PerformAttack(gun, target)
    ///
    /// [Server] PerformAttack(gun, target)
    ///   Hitscan:
    ///     → PlayerHealthSystem.ApplyDamageServer(DamageInfo)  ← đúng pipeline
    ///     → gun.BroadcastMuzzleFlash(hitPoint)  → [ObserversRpc] → Client VFX
    ///   Rocket:
    ///     → Instantiate RocketPrefab + set velocity
    ///     → ServerManager.Spawn → tất cả Client thấy đạn bay
    ///     → ProjectileNetworked.Initialize(def)  ← logic nổ/AoE từ ThrowableDefinition
    ///     → gun.BroadcastMuzzleFlash(targetPos)  → Client VFX
    ///
    /// [Server] TakeDamage(damage, attacker)
    ///   → Threat Table: ghi nhớ ai đã gây bao nhiêu damage
    ///   → Target switch tức thì nếu attacker mới gây damage cao
    ///
    /// [Server] Die()
    ///   → _state = Dead
    ///   → WorldSpawnManager.SpawnWorldContainer(_bossRewardConfig, pos)
    ///   → GameplayEventBus.Publish(BossKilledEvent)
    ///   → [ObserversRpc] RpcOnBossDied → Client event (death VFX, UI)
    ///   → Invoke(DespawnBoss, delay) → ServerManager.Despawn
    ///
    /// DETECT PLAYERS dựa trên:
    ///   Physics.OverlapSphere(pos, _aggroRadius, _playerLayerMask) — mỗi 0.3s interval
    ///   Collider cần nằm trên Layer "Player" (gán trong Inspector _playerLayerMask)
    ///   Sau đó TurretGun.ServerTick → Raycast LoS qua _obstacleLayerMask
    ///
    /// SYNC:
    ///   _syncHp      : SyncVar<float>      — HP thanh máu UI Client
    ///   _syncState   : SyncVar<BossState>  — state machine
    ///   TurretGun._syncLookDir : SyncVar<Vector3> — hướng xoay mỗi barrel
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossController : NetworkBehaviour, IHittable
    {
        // ── SyncVars ──────────────────────────────────────────────────────────────
        private readonly SyncVar<float>     _syncHp    = new SyncVar<float>();
        private readonly SyncVar<BossState> _syncState = new SyncVar<BossState>();

        // ── Inspector ─────────────────────────────────────────────────────────────
        [Header("Config (Overridden by BossSpawnManager.Initialize)")]
        [SerializeField] private string _bossId  = "turret_boss";
        [SerializeField] private float  _maxHp   = 5000f;

        [Header("AI Turret Stats")]
        [SerializeField] private float    _aggroRadius    = 25f;
        [SerializeField] private float    _attackRadius   = 25f;
        [SerializeField] private float    _attackDamage   = 30f;
        [SerializeField] private float    _attackCooldown = 0.5f;
        [SerializeField] private LayerMask _playerLayerMask;
        [SerializeField] private LayerMask _obstacleLayerMask;
        [Tooltip("Interval (s) để quét OverlapSphere tìm Player. Không quét mỗi frame để tiết kiệm CPU.")]
        [SerializeField] private float    _scanInterval   = 0.3f;

        [Header("Turret Guns — auto-collected from children")]
        [Tooltip("Auto-fill bởi OnValidate. Không cần drag tay.\nĐặt TurretGun GameObjects làm con của BossPoint trong Prefab là xong.")]
        [SerializeField] private List<TurretGun> _turretGuns = new List<TurretGun>();

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Tự động gom toàn bộ TurretGun trong children — designer chỉ cần đặt prefab
            var found = GetComponentsInChildren<TurretGun>(includeInactive: true);
            _turretGuns = new List<TurretGun>(found);
        }
#endif

        [Header("Weapon / Projectile Pooling")]
        [Tooltip("True = Hitscan (damage ngay bằng Raycast trên Server), False = Bay chậm")]
        [SerializeField] private bool _isHitscanWeapon = true;

        [Tooltip("Prefab ProjectileComponent lấy từ ProjectilePool (Thay thế hoàn toàn Tracer/Rocket cũ)")]
        [SerializeField] private GameObject _projectilePrefab;

        [Tooltip("Tốc độ bay của đạn (Nếu hitscan thì tốc độ này chỉ dành cho VFX)")]
        [SerializeField] private float _projectileSpeed = 60f;

        [Header("Reward (Mặc định hoặc Được gán đè từ BossSpawnPoint)")]
        [Tooltip("WorldSpawnConfig nếu ko bị gán đè từ BossSpawnPoint. Thường thì Data Drop sẽ do Điểm Spawn Point quyết định, tránh Boss bị trùng quá nhiều map.")]
        [SerializeField] private WorldSpawnConfig _bossRewardConfig;

        [Header("Scoring")]
        [Tooltip("Số điểm thưởng cho team hạ Boss (trước khi nhân Phase Multiplier).")]
        [SerializeField] private int _bossKillScore = 500;

        [Header("Debug")]
        [Tooltip("Bật/tắt hiển thị vòng bo và Menu Text thông số Boss trên Scene Editor")]
        [SerializeField] private bool _showDebug = true;

        [Header("Death")]
        [Tooltip("Giây sau khi Boss chết trước khi NetworkObject bị Despawn")]
        [SerializeField] private float _despawnDelay = 3f;

        // ── Runtime (Server only) ─────────────────────────────────────────────────
        private Transform _currentTarget;
        private float     _attackCooldownTimer;
        private float     _scanTimer;
        private readonly Dictionary<NetworkObject, float> _threatTable = new();
        private MatchEndManager  _matchEndManager;
        private MatchPhaseManager _phaseManager;

        // ── Public ────────────────────────────────────────────────────────────────
        public string    BossId    => _bossId;
        public float     CurrentHp => _syncHp.Value;
        public float     MaxHp     => _maxHp;
        public bool      IsDead    => _syncState.Value == BossState.Dead;
        public event Action<BossController> Died;

        // ── Dependency Setup ───────────────────────────────────────────────────────
        [Server]
        public void SetDynamicRewardConfig(WorldSpawnConfig rewardConfig)
        {
            if (rewardConfig != null)
                _bossRewardConfig = rewardConfig;
        }

        // ── FishNet Lifecycle ──────────────────────────────────────────────────────

        public override void OnStartServer()
        {
            base.OnStartServer();
            _matchEndManager  = FindFirstObjectByType<MatchEndManager>();
            _phaseManager     = FindFirstObjectByType<MatchPhaseManager>();
            
            _syncHp.Value    = _maxHp;
            _syncState.Value = BossState.Idle;

            // Runtime fallback: nếu OnValidate chưa chạy (build), collect lại
            if (_turretGuns == null || _turretGuns.Count == 0)
            {
                var found = GetComponentsInChildren<TurretGun>(includeInactive: true);
                _turretGuns = new List<TurretGun>(found);
                if (_turretGuns.Count == 0)
                    Debug.LogWarning($"[BossController] '{_bossId}': No TurretGun found in children!");
            }

            Debug.Log($"[BossController] '{_bossId}' initialized with {_turretGuns.Count} TurretGun(s).");
        }

        // ── Server Update ──────────────────────────────────────────────────────────

        private void Update()
        {
            if (!IsServerStarted || IsDead) return;

            _attackCooldownTimer -= Time.deltaTime;
            _scanTimer           -= Time.deltaTime;

            // Scan players theo interval — không mỗi frame
            if (_scanTimer <= 0f)
            {
                _scanTimer = _scanInterval;
                RefreshTarget();
            }

            // Tick từng nòng súng
            foreach (var gun in _turretGuns)
            {
                bool hasLoS = gun.ServerTick(_currentTarget, _attackRadius, _obstacleLayerMask);

                if (hasLoS && _attackCooldownTimer <= 0f && _currentTarget != null)
                {
                    _attackCooldownTimer = _attackCooldown;
                    PerformAttack(gun, _currentTarget);
                    break; // Chỉ 1 gun bắn mỗi cooldown cycle — chia nhau bắn theo vòng lặp tiếp theo
                }
            }

            // Update state machine
            if (_currentTarget != null)
                _syncState.Value = BossState.Attack;
            else
                _syncState.Value = BossState.Idle;
        }

        // ── Target Scan ────────────────────────────────────────────────────────────

        [Server]
        private void RefreshTarget()
        {
            // Ưu tiên 1: Threat table — target người gây damage nhiều nhất (đang có LoS)
            _currentTarget = GetHighestThreatTarget();

            // Ưu tiên 2: Người gần nhất trong tầm aggro có LoS
            if (_currentTarget == null)
                _currentTarget = FindClosestVisiblePlayer();
        }

        [Server]
        private Transform GetHighestThreatTarget()
        {
            Transform best  = null;
            float highScore = 0f;

            foreach (var kvp in _threatTable)
            {
                if (kvp.Key == null || !kvp.Key.gameObject.activeInHierarchy) continue;
                if (kvp.Value <= highScore) continue;
                // LoS check từ gun đầu tiên
                if (_turretGuns.Count > 0 && !HasAnyLoS(kvp.Key.transform)) continue;

                highScore = kvp.Value;
                best = kvp.Key.transform;
            }
            return best;
        }

        [Server]
        private Transform FindClosestVisiblePlayer()
        {
            var hits = Physics.OverlapSphere(transform.position, _aggroRadius, _playerLayerMask);
            float minDist = float.MaxValue;
            Transform closest = null;

            foreach (var c in hits)
            {
                float d = Vector3.Distance(transform.position, c.transform.position);
                if (d >= minDist) continue;
                if (!HasAnyLoS(c.transform)) continue;
                minDist = d;
                closest = c.transform;
            }
            return closest;
        }

        // Có bất kỳ gun nào nhìn thấy target không
        [Server]
        private bool HasAnyLoS(Transform target)
        {
            foreach (var gun in _turretGuns)
            {
                Vector3 aimPoint = target.position + Vector3.up * 1.1f;
                Vector3 dir = aimPoint - gun.GetFireOrigin();
                if (!Physics.Raycast(gun.GetFireOrigin(), dir.normalized, dir.magnitude, _obstacleLayerMask))
                    return true;
            }
            return false;
        }

        // ── Attack ─────────────────────────────────────────────────────────────────

        [Server]
        private void PerformAttack(TurretGun gun, Transform target)
        {
            if (target == null || gun == null) return;

            Vector3 aimPoint = target.position + Vector3.up * 1.1f;

            if (_isHitscanWeapon)
            {
                HitscanAttack(gun, target, aimPoint);
            }
            else
            {
                RocketAttack(gun, aimPoint);
            }
        }

        [Server]
        private void HitscanAttack(TurretGun gun, Transform target, Vector3 aimPoint)
        {
            // Damage Logic Server
            var hittable = target.GetComponentInChildren<IHittable>()
                        ?? target.GetComponentInParent<IHittable>();

            if (hittable != null)
            {
                var info = new DamageInfo
                {
                    Damage                 = _attackDamage,
                    IsHeadshot             = false,
                    HitPoint               = aimPoint,
                    HitNormal              = (gun.GetFireOrigin() - aimPoint).normalized,
                    ShooterNetworkObjectId = (int)ObjectId,
                    WeaponId               = $"Boss_{_bossId}"
                };
                (hittable as PlayerHealthSystem)?.ApplyDamageServer(info);
            }

            // Gọi ObserversRpc báo cho Client lôi đạn từ Pool ra bắn (Chỉ là hình ảnh)
            gun.RpcSpawnProjectileVisual(_projectilePrefab, aimPoint, true, _projectileSpeed);
        }

        [Server]
        private void RocketAttack(TurretGun gun, Vector3 targetPos)
        {
            if (_projectilePrefab == null)
            {
                Debug.LogWarning($"[BossController] Missing ProjectilePrefab config for '{_bossId}'");
                return;
            }

            Vector3 origin = gun.GetFireOrigin();
            Vector3 dir    = (targetPos - origin).normalized;

            // Server tự tính va chạm Projectile, không dùng ProjectileNetworked nặng nề
            // Ở đây vì Boss là Server-Authoritative, ta Lôi ProjectileComponent ra khỏi Pool NGAY TRÊN SERVER để lấy hit.
            var pool = NightHunt.Gameplay.Character.Combat.Weapons.ProjectilePool.Instance;
            if (pool != null)
            {
                var proj = pool.Get(_projectilePrefab, origin, Quaternion.LookRotation(dir));
                if (proj != null)
                {
                    proj.SetOwnerData((int)ObjectId, $"Boss_{_bossId}");
                    var fakeConfig = new NightHunt.Data.WeaponConfigData 
                    { 
                        ProjectileSpeed = _projectileSpeed, 
                        MaxRange = 200f, 
                        DamageBody = (int)_attackDamage,
                        BallisticType = "Projectile" 
                    };
                    proj.Initialize(fakeConfig, dir, false);
                }
            }

            // Gọi Clients spawn VFX của đạn đó
            gun.RpcSpawnProjectileVisual(_projectilePrefab, dir, false, _projectileSpeed);
        }

        // ── Damage & Threat System ─────────────────────────────────────────────────

        [Server]
        public void TakeDamage(float damage, NetworkObject attacker = null)
        {
            if (IsDead) return;

            _syncHp.Value = Mathf.Max(0f, _syncHp.Value - damage);

            if (attacker != null)
            {
                _threatTable.TryGetValue(attacker, out float existing);
                _threatTable[attacker] = existing + damage;

                // Switch target ngay nếu attacker mới / gây damage lớn
                if (_currentTarget == null || _threatTable[attacker] > GetHighestThreat() * 0.6f)
                    _currentTarget = attacker.transform;
            }

            if (_syncHp.Value <= 0f)
                Die();
        }

        // ── IHittable ──────────────────────────────────────────────────────────
        public void RequestDamage(DamageInfo info) => RequestDamageServerRpc(info.Damage);

        [ServerRpc(RequireOwnership = false)]
        private void RequestDamageServerRpc(float damage) => TakeDamage(damage, null);

        [Server]
        private float GetHighestThreat()
        {
            float max = 0f;
            foreach (var v in _threatTable.Values)
                if (v > max) max = v;
            return max;
        }

        // ── Death ──────────────────────────────────────────────────────────────────

        [Server]
        private void Die()
        {
            _syncState.Value = BossState.Dead;
            Debug.Log($"[BossController] Boss '{_bossId}' killed.");

            // Reward drop — WorldSpawnManager pipeline
            Vector3 dropPos = transform.position + Vector3.up * 0.3f;
            if (WorldSpawnManager.Instance != null && _bossRewardConfig != null)
            {
                WorldSpawnManager.Instance.SpawnWorldContainer(_bossRewardConfig, dropPos);
            }
            else
            {
                Debug.LogWarning($"[BossController] No reward config for '{_bossId}' — no loot spawned.");
            }

            int killerTeam = -1;
            Transform topThreat = GetHighestThreatTarget();
            if (topThreat != null)
            {
                var np = topThreat.GetComponentInParent<NightHunt.Networking.NetworkPlayer>();
                if (np != null) killerTeam = np.TeamId;
            }

            // Issue #8: Award boss kill score with Phase multiplier
            if (killerTeam >= 0 && _matchEndManager != null)
            {
                float multiplier = _phaseManager?.GetCurrentPhaseConfig()?.ScoreMultiplier ?? 1f;
                int score = Mathf.RoundToInt(_bossKillScore * multiplier);
                _matchEndManager.AddObjectiveScore(killerTeam, score);
                Debug.Log($"[BossController] Boss '{_bossId}' — awarded {score} pts (x{multiplier}) to Team {killerTeam}");
            }

            GameplayEventBus.Instance?.Publish(new BossKilledEvent { 
                BossId = _bossId,
                ChestPosition = dropPos,
                KillerTeamId = killerTeam
            });
            RpcOnBossDied();
            Died?.Invoke(this);

            StartCoroutine(DespawnAfterDelay(_despawnDelay));
        }

        private IEnumerator DespawnAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (IsSpawned) ServerManager.Despawn(gameObject);
        }

        // ── RPCs ───────────────────────────────────────────────────────────────────

        [ObserversRpc]
        private void RpcOnBossDied()
        {
            // Client: publish event để UI/VFX react (death animation trigger, UI notification)
            GameplayEventBus.Instance?.Publish(new BossKilledEvent { BossId = _bossId });
        }

        // ── Gizmos (Debug) ─────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_showDebug) return;

            // Vòng Aggro (Phát hiện)
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _aggroRadius);
            
            // Vòng Attack (Đánh cự ly)
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _attackRadius);

            // Bảng thông số nổi
            string text = $"[ BOSS: {_bossId} ]\n";
            text += $"Máu: {(Application.isPlaying ? _syncHp.Value : _maxHp):F0} / {_maxHp:F0}\n";

            if (Application.isPlaying)
            {
                text += "───── RUNTIME ─────\n";
                text += $"Trạng Thái: {_syncState.Value}\n";
                string tName = _currentTarget != null ? _currentTarget.name : "Rảnh (None)";
                text += $"Đang cắn: {tName}\n";
                text += $"Số con mồi đang dòm: {_threatTable?.Count ?? 0}\n";
            }

            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.red;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;
            UnityEditor.Handles.Label(transform.position + Vector3.up * 3f, text, style);
        }
#endif
    }

    public enum BossState { Idle, Aggro, Attack, Dead }
}
