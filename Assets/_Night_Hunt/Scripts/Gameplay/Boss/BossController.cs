using System;
using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Data;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Gameplay.Character.Combat.Weapons;
using NightHunt.Gameplay.Core.Events;
using NightHunt.Gameplay.FogOfWar;
using NightHunt.Gameplay.Match;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Loot;
using NightHunt.GameplaySystems.World;
using NightHunt.Networking.Player;
using NightHunt.Diagnostics;
using UnityEngine;

namespace NightHunt.Gameplay.Boss
{
    /// <summary>
    /// BossController is the area/orchestrator for a boss point.
    /// It is not a damage target. Each child TurretGun owns its own HP and weapon config.
    /// Boss dies only when all registered turrets are destroyed.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FogTeamVisibilityBinder))]
    public sealed class BossController : NetworkBehaviour, IHealthSource, IFogTeamOwned
    {
        private readonly SyncVar<float> _syncHp = new SyncVar<float>();
        private readonly SyncVar<BossState> _syncState = new SyncVar<BossState>();

        [Header("Boss Area")]
        [SerializeField] private string _bossId = "turret_boss";
        [SerializeField, Min(1f)] private float _aggroRadius = 25f;
        [SerializeField, Min(1f)] private float _attackRadius = 25f;
        [SerializeField] private LayerMask _playerLayerMask;
        [SerializeField] private LayerMask _obstacleLayerMask;
        [Tooltip("Seconds between player scans. Turrets still rotate/fire every server tick.")]
        [SerializeField, Min(0.05f)] private float _scanInterval = 0.3f;

        [Header("Turrets")]
        [Tooltip("Auto-collected from children. Add/remove TurretGun prefabs under this boss area.")]
        [SerializeField] private List<TurretGun> _turretGuns = new List<TurretGun>();

        [Header("Reward")]
        [SerializeField] private WorldSpawnConfig _bossRewardConfig;

        [Header("Scoring")]
        [SerializeField, Min(0)] private int _bossKillScore = 500;

        [Header("Hitscan Timing")]
        [Tooltip("Delay boss hitscan damage by distance / projectile speed so damage lines up with the visual tracer.")]
        [SerializeField] private bool _delayHitscanDamageByProjectileSpeed = true;
        [Tooltip("Safety clamp for boss hitscan damage delay. 0 = no clamp.")]
        [SerializeField, Min(0f)] private float _maxHitscanDamageDelay = 5f;

        [Header("Fog of War")]
        [Tooltip("Use a non-player team id so every player team treats this boss area as enemy and sees it only in vision.")]
        [SerializeField] private int _fogTeamId = 999;
        [SerializeField] private bool _fogAlwaysVisible;

        [Header("Debug")]
        [SerializeField] private bool _showDebug = false;

        [Header("Death")]
        [SerializeField, Min(0f)] private float _despawnDelay = 3f;

        private Transform _currentTarget;
        private float _scanTimer;
        private float _maxHp = 1f;
        private bool _deathStarted;
        private readonly Vector3[] _turretVolleyOrigins = new Vector3[16];
        private readonly int[] _turretVolleyVisualChannels = new int[16];
        private readonly Dictionary<NetworkObject, float> _threatTable = new();
        private MatchEndManager _matchEndManager;
        // _phaseManager removed -- SafeZoneManager used for zone-aware logic

        public string BossId => _bossId;
        public float CurrentHp => _syncHp.Value;
        public float MaxHp => _maxHp > 0f ? _maxHp : 1f;
        public float AggroRadius => _aggroRadius;
        public float AttackRadius => _attackRadius;
        public bool IsDead => _syncState.Value == BossState.Dead;
        public float CurrentHealth => CurrentHp;
        public float MaxHealth => MaxHp;
        public int FogOwnerTeamId => _fogTeamId;
        public bool FogAlwaysVisible => _fogAlwaysVisible;

        public event Action<BossController> Died;
        public event Action<float, float> OnHealthChanged;
        public event Action<HealthChangeEvent> HealthChanged;

        private void Awake()
        {
            EnsureFogBinder();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            CollectTurrets();
            _maxHp = CalculateAggregateMaxHealth();
        }
#endif

        [Server]
        public void SetDynamicRewardConfig(WorldSpawnConfig rewardConfig)
        {
            if (rewardConfig != null)
                _bossRewardConfig = rewardConfig;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _matchEndManager = FindFirstObjectByType<MatchEndManager>();

            CollectTurrets();
            RegisterTurrets();
            _deathStarted = false;
            _syncState.Value = BossState.Idle;
            RecalculateAggregateHealth(forceEvent: true);

            if (_turretGuns.Count == 0)
                Debug.LogWarning($"[BossController] '{_bossId}' has no TurretGun children.");
            else
                Debug.Log($"[BossController] '{_bossId}' initialized as area boss with {_turretGuns.Count} turret(s), aggregate HP {_syncHp.Value:F0}/{MaxHp:F0}.");
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            EnsureFogBinder();
            CollectTurrets();
            RegisterTurrets();
            _maxHp = CalculateAggregateMaxHealth();
            _syncHp.OnChange += OnHpSyncChanged;
        }

        public override void OnStopClient()
        {
            _syncHp.OnChange -= OnHpSyncChanged;
            base.OnStopClient();
        }

        private void OnHpSyncChanged(float prev, float next, bool asServer)
        {
            OnHealthChanged?.Invoke(next, MaxHp);
            HealthChanged?.Invoke(new HealthChangeEvent(prev, next, MaxHp, forceReveal: next < prev));
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

        private void Update()
        {
            if (!IsServerStarted || IsDead)
                return;

            _scanTimer -= Time.deltaTime;
            if (_scanTimer <= 0f)
            {
                _scanTimer = _scanInterval;
                RefreshTarget();
            }

            bool anyLineOfSight = false;
            foreach (var gun in _turretGuns)
            {
                if (gun == null || gun.IsDead)
                    continue;

                gun.TickCooldown(Time.deltaTime);
                bool hasLoS = gun.ServerTick(_currentTarget, _attackRadius, _obstacleLayerMask);
                if (!hasLoS || _currentTarget == null)
                    continue;

                anyLineOfSight = true;
                if (!gun.CanFire)
                    continue;

                PerformAttack(gun, _currentTarget);
                gun.MarkFired();
            }

            if (_currentTarget != null)
                _syncState.Value = anyLineOfSight ? BossState.Attack : BossState.Aggro;
            else
                _syncState.Value = BossState.Idle;
        }

        private void CollectTurrets()
        {
            var found = GetComponentsInChildren<TurretGun>(includeInactive: true);
            _turretGuns = new List<TurretGun>(found);
        }

        private void RegisterTurrets()
        {
            if (_turretGuns == null)
                _turretGuns = new List<TurretGun>();

            for (int i = _turretGuns.Count - 1; i >= 0; i--)
            {
                if (_turretGuns[i] == null)
                {
                    _turretGuns.RemoveAt(i);
                    continue;
                }

                _turretGuns[i].SetBossOwner(this);
            }
        }

        private float CalculateAggregateMaxHealth()
        {
            float total = 0f;
            if (_turretGuns != null)
            {
                foreach (var turret in _turretGuns)
                    if (turret != null)
                        total += Mathf.Max(1f, turret.MaxHealth);
            }

            return Mathf.Max(1f, total);
        }

        [Server]
        internal void NotifyTurretDamaged(TurretGun turret, DamageInfo info, NetworkObject attacker)
        {
            if (IsDead)
                return;

            RegisterThreat(attacker, info.Damage);
            RecalculateAggregateHealth();
        }

        [Server]
        internal void NotifyTurretDestroyed(TurretGun turret, DamageInfo info, NetworkObject attacker)
        {
            if (IsDead)
                return;

            RecalculateAggregateHealth();

            if (AreAllTurretsDestroyed())
                Die();
        }

        [Server]
        private void RegisterThreat(NetworkObject attacker, float damage)
        {
            if (attacker == null)
                return;

            _threatTable.TryGetValue(attacker, out float existing);
            _threatTable[attacker] = existing + Mathf.Max(0f, damage);

            if (_currentTarget == null || _threatTable[attacker] > GetHighestThreat() * 0.6f)
                _currentTarget = attacker.transform;
        }

        [Server]
        private void RecalculateAggregateHealth(bool forceEvent = false)
        {
            _maxHp = CalculateAggregateMaxHealth();

            float current = 0f;
            foreach (var turret in _turretGuns)
            {
                if (turret == null)
                    continue;

                if (turret.IsDead)
                    continue;

                float turretHp = turret.CurrentHealth > 0f ? turret.CurrentHealth : turret.MaxHealth;
                current += Mathf.Clamp(turretHp, 0f, turret.MaxHealth);
            }

            current = Mathf.Clamp(current, 0f, MaxHp);
            float previous = _syncHp.Value;
            _syncHp.Value = current;

            if (forceEvent || !Mathf.Approximately(previous, current))
            {
                OnHealthChanged?.Invoke(current, MaxHp);
                HealthChanged?.Invoke(new HealthChangeEvent(previous, current, MaxHp, forceReveal: current < previous));
            }
        }

        [Server]
        private bool AreAllTurretsDestroyed()
        {
            if (_turretGuns == null || _turretGuns.Count == 0)
                return false;

            foreach (var turret in _turretGuns)
                if (turret != null && !turret.IsDead)
                    return false;

            return true;
        }

        [Server]
        private void RefreshTarget()
        {
            _currentTarget = GetHighestThreatTarget();
            if (_currentTarget == null)
                _currentTarget = FindClosestVisiblePlayer();
        }

        [Server]
        private Transform GetHighestThreatTarget()
        {
            Transform best = null;
            float highScore = 0f;

            foreach (var kvp in _threatTable)
            {
                if (kvp.Key == null || !kvp.Key.gameObject.activeInHierarchy)
                    continue;
                if (kvp.Value <= highScore)
                    continue;
                if (!HasAnyLoS(kvp.Key.transform))
                    continue;

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
                if (d >= minDist)
                    continue;
                if (!HasAnyLoS(c.transform))
                    continue;

                minDist = d;
                closest = c.transform;
            }

            return closest;
        }

        [Server]
        private bool HasAnyLoS(Transform target)
        {
            if (target == null)
                return false;

            Vector3 aimPoint = target.position + Vector3.up * 1.1f;
            foreach (var gun in _turretGuns)
            {
                if (gun == null || gun.IsDead)
                    continue;

                Vector3 origin = gun.GetFireOrigin();
                Vector3 toTarget = aimPoint - origin;
                if (toTarget.sqrMagnitude <= 0.0001f)
                    continue;

                if (!Physics.Raycast(origin, toTarget.normalized, toTarget.magnitude, _obstacleLayerMask))
                    return true;
            }

            return false;
        }

        [Server]
        private void PerformAttack(TurretGun gun, Transform target)
        {
            if (gun == null || gun.IsDead || target == null)
                return;

            Vector3 aimPoint = target.position + Vector3.up * 1.1f;
            int fireOriginCount = gun.FillVolleyFireOrigins(_turretVolleyOrigins, _turretVolleyVisualChannels);
            for (int i = 0; i < fireOriginCount; i++)
            {
                Vector3 origin = _turretVolleyOrigins[i];
                int visualChannelIndex = _turretVolleyVisualChannels[i];
                if (gun.FireMode == BossTurretFireMode.Hitscan)
                    HitscanAttack(gun, target, aimPoint, origin, visualChannelIndex);
                else
                    RocketAttack(gun, aimPoint, origin, visualChannelIndex);
            }
        }

        [Server]
        private void HitscanAttack(TurretGun gun, Transform target, Vector3 aimPoint, Vector3 origin, int visualChannelIndex)
        {
            Vector3 shotVector = aimPoint - origin;
            float shotDistance = shotVector.magnitude;
            if (shotDistance <= 0.001f)
                return;

            Vector3 shotDir = shotVector / shotDistance;
            if (_obstacleLayerMask.value != 0 &&
                Physics.Raycast(origin, shotDir, out RaycastHit blockerHit, shotDistance, _obstacleLayerMask, QueryTriggerInteraction.Ignore))
            {
                PhaseTestLog.Log(
                    PhaseTestLogCategory.Weapon,
                    "BossHitscanBlocked",
                    $"boss={_bossId} gun={gun.name} target={target.name} origin={origin:F2} blockPoint={blockerHit.point:F2} blockDist={blockerHit.distance:F2} blocker={blockerHit.collider?.name ?? "null"} layer={(blockerHit.collider != null ? PhaseTestLog.DescribeLayer(blockerHit.collider.gameObject) : "null")} mask={_obstacleLayerMask.value}",
                    this);

                gun.RpcSpawnProjectileVisual(origin, blockerHit.point, visualChannelIndex, true, gun.ProjectileSpeed, 0f, true, blockerHit.point, false);
                return;
            }

            var hittable = target.GetComponentInChildren<IHittable>()
                        ?? target.GetComponentInParent<IHittable>();

            var info = new DamageInfo
            {
                Damage = gun.Damage,
                IsHeadshot = false,
                HitPoint = aimPoint,
                HitNormal = -shotDir,
                ShooterNetworkObjectId = (int)ObjectId,
                WeaponId = $"Boss_{_bossId}_{gun.name}"
            };

            float delay = ResolveBossHitscanDamageDelay(origin, aimPoint, gun.ProjectileSpeed, out bool clamped);
            PhaseTestLog.Log(
                PhaseTestLogCategory.Weapon,
                "BossHitscanQueued",
                $"boss={_bossId} gun={gun.name} target={target.name} targetHittable={(hittable != null ? hittable.GetType().Name : "null")} origin={origin:F2} point={aimPoint:F2} dist={shotDistance:F2} speed={gun.ProjectileSpeed:F1} damageDelay={delay:F3} clamped={clamped}",
                this);

            gun.RpcSpawnProjectileVisual(origin, aimPoint, visualChannelIndex, true, gun.ProjectileSpeed, 0f, true, aimPoint, false);

            if (hittable == null)
            {
                PhaseTestLog.Warning(
                    PhaseTestLogCategory.Weapon,
                    "BossHitscanDamageSkipped",
                    $"boss={_bossId} gun={gun.name} reason=no-hittable target={target.name}",
                    this);
                return;
            }

            if (delay <= 0.001f)
            {
                ApplyBossHitscanDamage(hittable, info, gun.name);
                return;
            }

            StartCoroutine(DelayedBossHitscanDamageRoutine(delay, hittable, info, gun.name));
        }

        [Server]
        private IEnumerator DelayedBossHitscanDamageRoutine(float delay, IHittable hittable, DamageInfo info, string gunName)
        {
            yield return new WaitForSeconds(delay);

            if (IsDestroyedHittable(hittable))
            {
                PhaseTestLog.Warning(
                    PhaseTestLogCategory.Weapon,
                    "BossHitscanDamageSkipped",
                    $"boss={_bossId} gun={gunName} reason=target-destroyed delay={delay:F3} weapon={info.WeaponId}",
                    this);
                yield break;
            }

            ApplyBossHitscanDamage(hittable, info, gunName);
        }

        [Server]
        private void ApplyBossHitscanDamage(IHittable hittable, DamageInfo info, string gunName)
        {
            if (hittable is PlayerHealthSystem playerHealth)
            {
                if (playerHealth.IsDead)
                {
                    PhaseTestLog.Log(
                        PhaseTestLogCategory.Weapon,
                        "BossHitscanDamageSkipped",
                        $"boss={_bossId} gun={gunName} reason=target-dead weapon={info.WeaponId}",
                        this);
                    return;
                }

                playerHealth.ApplyDamageServer(info);
            }
            else
            {
                hittable.RequestDamage(info);
            }

            PhaseTestLog.Log(
                PhaseTestLogCategory.Weapon,
                "BossHitscanDamageApply",
                $"boss={_bossId} gun={gunName} target={hittable.GetType().Name} weapon={info.WeaponId} damage={info.Damage:F1} point={info.HitPoint:F2}",
                this);
        }

        [Server]
        private float ResolveBossHitscanDamageDelay(Vector3 origin, Vector3 hitPoint, float projectileSpeed, out bool clamped)
        {
            clamped = false;
            if (!_delayHitscanDamageByProjectileSpeed)
                return 0f;

            float speed = Mathf.Max(0f, projectileSpeed);
            if (speed <= 0.001f)
                return 0f;

            float delay = Vector3.Distance(origin, hitPoint) / speed;
            if (_maxHitscanDamageDelay > 0f && delay > _maxHitscanDamageDelay)
            {
                delay = _maxHitscanDamageDelay;
                clamped = true;
            }

            return Mathf.Max(0f, delay);
        }

        private static bool IsDestroyedHittable(IHittable hittable)
        {
            if (hittable == null)
                return true;

            return hittable is UnityEngine.Object unityObject && unityObject == null;
        }

        [Server]
        private void RocketAttack(TurretGun gun, Vector3 targetPos, Vector3 origin, int visualChannelIndex)
        {
            GameObject projectilePrefab = gun.ProjectilePrefab;
            if (projectilePrefab == null)
            {
                Debug.LogWarning($"[BossController] Missing projectile prefab on turret '{gun.name}' for boss '{_bossId}'.");
                return;
            }

            Vector3 dir = targetPos - origin;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : gun.transform.forward;
            var config = new WeaponConfigData
            {
                ProjectileSpeed = gun.ProjectileSpeed,
                MaxRange = 200f,
                DamageBody = Mathf.RoundToInt(gun.Damage),
                BallisticType = "Projectile",
                GravityScale = gun.GravityScale,
                HasProjectileTargetPoint = gun.GravityScale > 0f,
                ProjectileTargetPoint = targetPos,
                PreferHighArc = gun.PreferHighArc
            };

            dir = BallisticTrajectory.ResolveLaunchDirection(origin, dir, config);

            var pool = ProjectilePool.Instance;
            if (pool != null)
            {
                var proj = pool.Get(projectilePrefab, origin, Quaternion.LookRotation(dir));
                if (proj != null)
                {
                    var bossCols = GetComponentsInChildren<Collider>(true);
                    var projCols = proj.GetComponentsInChildren<Collider>(true);
                    foreach (var bossCol in bossCols)
                        foreach (var projCol in projCols)
                            Physics.IgnoreCollision(bossCol, projCol, true);

                    proj.SetOwnerData((int)ObjectId, $"Boss_{_bossId}_{gun.name}");
                    proj.Initialize(config, dir, false);
                }
                else
                {
                    Debug.LogWarning($"[BossController] ProjectilePool.Get returned null for turret '{gun.name}' prefab '{projectilePrefab.name}'.");
                }
            }
            else
            {
                Debug.LogWarning("[BossController] ProjectilePool.Instance is null. Add ProjectilePool to a persistent scene object.");
            }

            gun.RpcSpawnProjectileVisual(
                origin,
                dir,
                visualChannelIndex,
                false,
                gun.ProjectileSpeed,
                gun.GravityScale,
                gun.GravityScale > 0f,
                targetPos,
                gun.PreferHighArc);
        }

        [Server]
        private float GetHighestThreat()
        {
            float max = 0f;
            foreach (float value in _threatTable.Values)
                if (value > max)
                    max = value;
            return max;
        }

        [Server]
        private void Die()
        {
            if (_deathStarted)
                return;

            _deathStarted = true;
            _syncHp.Value = 0f;
            _syncState.Value = BossState.Dead;
            Debug.Log($"[BossController] Boss area '{_bossId}' cleared. All turrets destroyed.");

            Vector3 dropPos = transform.position + Vector3.up * 0.3f;
            if (WorldSpawnManager.Instance != null && _bossRewardConfig != null)
                WorldSpawnManager.Instance.SpawnWorldContainer(_bossRewardConfig, dropPos);
            else
                Debug.LogWarning($"[BossController] No reward config for '{_bossId}'. No loot spawned.");

            int killerTeam = ResolveKillerTeam();
            if (killerTeam >= 0 && _matchEndManager != null)
            {
                float multiplier = 1f; // SafeZoneMatchConfig zone bonus applied via ScoringSystem
                int score = Mathf.RoundToInt(_bossKillScore * multiplier);
                _matchEndManager.AddObjectiveScore(killerTeam, score);
                Debug.Log($"[BossController] Boss '{_bossId}' awarded {score} pts (x{multiplier}) to Team {killerTeam}.");
            }

            GameplayEventBus.Instance?.Publish(new BossKilledEvent
            {
                BossId = _bossId,
                ChestPosition = dropPos,
                KillerTeamId = killerTeam
            });

            RpcOnBossDied(dropPos, killerTeam);
            Died?.Invoke(this);
            StartCoroutine(DespawnAfterDelay(_despawnDelay));
        }

        [Server]
        private int ResolveKillerTeam()
        {
            Transform topThreat = GetHighestThreatTarget();
            var np = topThreat != null
                ? topThreat.GetComponentInParent<NetworkPlayer>()
                : null;

            return np != null ? np.TeamId : -1;
        }

        private IEnumerator DespawnAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (IsSpawned)
                ServerManager.Despawn(gameObject);
        }

        [ObserversRpc]
        private void RpcOnBossDied(Vector3 chestPosition, int killerTeam)
        {
            GameplayEventBus.Instance?.Publish(new BossKilledEvent
            {
                BossId = _bossId,
                ChestPosition = chestPosition,
                KillerTeamId = killerTeam
            });
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_showDebug)
                return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _aggroRadius);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _attackRadius);

            string text = $"[ BOSS AREA: {_bossId} ]\n";
            text += $"Turrets: {(_turretGuns != null ? _turretGuns.Count : 0)}\n";
            text += $"HP: {(Application.isPlaying ? _syncHp.Value : CalculateAggregateMaxHealth()):F0} / {MaxHp:F0}\n";

            if (Application.isPlaying)
            {
                text += $"State: {_syncState.Value}\n";
                text += $"Target: {(_currentTarget != null ? _currentTarget.name : "None")}\n";
                text += $"Threats: {_threatTable?.Count ?? 0}\n";
            }

            GUIStyle style = new GUIStyle
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = Color.red;
            UnityEditor.Handles.Label(transform.position + Vector3.up * 3f, text, style);
        }
#endif

#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!_showDebug || !Application.isPlaying)
                return;

            UnityEngine.Camera cam = UnityEngine.Camera.main;
            if (cam == null)
                return;

            Vector3 screenPos = cam.WorldToScreenPoint(transform.position + Vector3.up * 2.5f);
            if (screenPos.z < 0)
                return;

            float hp = _syncHp.Value;
            float hpPct = MaxHp > 0f ? hp / MaxHp : 0f;
            string targetName = _currentTarget != null ? _currentTarget.name : "-";

            float gx = screenPos.x - 80f;
            float gy = Screen.height - screenPos.y;

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(gx - 2f, gy - 2f, 164f, 62f), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(gx, gy, 160f, 14f), $"BOSS: {_bossId}");
            GUI.Label(new Rect(gx, gy + 14f, 160f, 14f), $"HP: {hp:F0}/{MaxHp:F0}  [{hpPct:P0}]");
            GUI.Label(new Rect(gx, gy + 28f, 160f, 14f), $"State: {_syncState.Value}");
            GUI.Label(new Rect(gx, gy + 42f, 160f, 14f), $"Target: {targetName}  Threats:{_threatTable?.Count ?? 0}");

            GUI.color = Color.red;
            GUI.DrawTexture(new Rect(gx, gy + 58f, 160f * Mathf.Clamp01(hpPct), 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
#endif

        public enum BossState
        {
            Idle,
            Aggro,
            Attack,
            Dead
        }
    }
}
