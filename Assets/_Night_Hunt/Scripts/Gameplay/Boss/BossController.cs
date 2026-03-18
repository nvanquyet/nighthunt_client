using System;
using FishNet;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Data;
using NightHunt.Gameplay.Core.Events;
using UnityEngine;
using UnityEngine.AI;
using NightHunt.Utilities;

namespace NightHunt.Gameplay.Boss
{
    /// <summary>
    /// Server-authoritative boss NetworkBehaviour.
    ///
    /// State machine: Idle → Aggro → Attack → Dead
    ///   • HP is a SyncVar — all clients see health bar changes.
    ///   • On death: fires <see cref="BossKilledEvent"/> and spawns
    ///     a <see cref="BossChest"/> at its position.
    ///   • AI movement uses NavMeshAgent (server-only);
    ///     position is replicated via FishNet transform sync.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [DisallowMultipleComponent]
    public sealed class BossController : NetworkBehaviour
    {
        // ── SyncVars ──────────────────────────────────────────────────────────
        private readonly SyncVar<float> _currentHp = new SyncVar<float>();
        private readonly SyncVar<BossState> _state  = new SyncVar<BossState>();

        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Config (overridden at spawn by BossSpawnManager)")]
        [SerializeField] private string _bossId = "boss_default";
        [SerializeField] private float  _maxHp  = 5000f;

        [Header("AI")]
        [SerializeField] private float _aggroRadius    = 20f;
        [SerializeField] private float _attackRadius   = 3f;
        [SerializeField] private float _attackDamage   = 50f;
        [SerializeField] private float _attackCooldown = 2f;
        [SerializeField] private LayerMask _playerLayerMask;

        [Header("Loot")]
        [SerializeField] private GameObject _bossChestPrefab;

        // ── Runtime ───────────────────────────────────────────────────────────
        private NavMeshAgent _agent;
        private Transform    _target;
        private float        _attackTimer;
        private BossSpawnConfigData _config;

        // ── Public ────────────────────────────────────────────────────────────
        public string BossId    => _bossId;
        public float  CurrentHp => _currentHp.Value;
        public float  MaxHp     => _maxHp;
        public bool   IsDead    => _state.Value == BossState.Dead;

        /// <summary>Raised on the server when the boss is killed.</summary>
        public event Action<BossController> Died;

        // ──────────────────────────────────────────────────────────────────────
        #region FishNet Lifecycle

        private void Awake()
        {
            _agent = ComponentResolver.Find<NavMeshAgent>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] NavMeshAgent not found")
        .Resolve();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _currentHp.Value = _maxHp;
            _state.Value     = BossState.Idle;
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region Configuration

        /// <summary>Called by <see cref="BossSpawnManager"/> right after spawn.</summary>
        [Server]
        public void Initialize(BossSpawnConfigData config)
        {
            _config    = config;
            _bossId    = config.BossId;
            _maxHp     = config.MaxHP > 0 ? config.MaxHP : _maxHp;
            _currentHp.Value = _maxHp;

            // Wire AI params from config; fall back to Inspector defaults if not set
            if (config.AggroRadius   > 0f) _aggroRadius    = config.AggroRadius;
            if (config.AttackRadius  > 0f) _attackRadius   = config.AttackRadius;
            if (config.AttackDamage  > 0f) _attackDamage   = config.AttackDamage;
            if (config.AttackCooldown > 0f) _attackCooldown = config.AttackCooldown;

            if (_agent != null)
            {
                _agent.speed            = config.MoveSpeed > 0f ? config.MoveSpeed : _agent.speed;
                _agent.stoppingDistance = _attackRadius * 0.8f;
            }
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region AI Update (server-only)

        private void Update()
        {
            if (!IsServerStarted || IsDead) return;

            _attackTimer -= Time.deltaTime;

            switch (_state.Value)
            {
                case BossState.Idle:     TickIdle();    break;
                case BossState.Aggro:    TickAggro();   break;
                case BossState.Attack:   TickAttack();  break;
            }
        }

        private void TickIdle()
        {
            var player = FindClosestPlayer();
            if (player != null && Vector3.Distance(transform.position, player.position) <= _aggroRadius)
            {
                _target      = player;
                _state.Value = BossState.Aggro;
            }
        }

        private void TickAggro()
        {
            if (_target == null) { _state.Value = BossState.Idle; return; }

            float dist = Vector3.Distance(transform.position, _target.position);

            if (dist > _aggroRadius * 1.5f)
            {
                // Lost target
                _target      = null;
                _state.Value = BossState.Idle;
                _agent.ResetPath();
                return;
            }

            _agent.SetDestination(_target.position);

            if (dist <= _attackRadius)
                _state.Value = BossState.Attack;
        }

        private void TickAttack()
        {
            if (_target == null) { _state.Value = BossState.Idle; return; }

            float dist = Vector3.Distance(transform.position, _target.position);
            if (dist > _attackRadius * 1.5f) { _state.Value = BossState.Aggro; return; }

            _agent.SetDestination(_target.position);

            if (_attackTimer <= 0f)
            {
                _attackTimer = _attackCooldown;
                PerformAttack();
            }
        }

        private void PerformAttack()
        {
            if (_target == null) return;

            // Damage target via stat system
            var statSystem = ComponentResolver.Find<NightHunt.Gameplay.StatSystem.Core.Interfaces.IPlayerStatSystem>(_target)
                                                .OnSelf()
                                                .InChildren()
                                                .OrLogWarning("[Auto] NightHunt.Gameplay.StatSystem.Core.Interfaces.IPlayerStatSystem not found")
                                                .Resolve();
            if (statSystem != null)
            {
                var modifier = NightHunt.Gameplay.StatSystem.Core.Data.StatModifier.CreateFlat(
                    $"Boss_{_bossId}", -_attackDamage);
                statSystem.AddModifier(NightHunt.Gameplay.StatSystem.Core.Types.PlayerStatType.Health, modifier);
            }

            RpcPlayAttackVFX();
        }

        private Transform FindClosestPlayer()
        {
            float minDist = float.MaxValue;
            Transform closest = null;

            var colliders = Physics.OverlapSphere(transform.position, _aggroRadius, _playerLayerMask);
            foreach (var c in colliders)
            {
                float d = Vector3.Distance(transform.position, c.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    closest = c.transform;
                }
            }
            return closest;
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region Damage

        [Server]
        public void TakeDamage(float damage, string killerDisplayName = "")
        {
            if (IsDead) return;

            _currentHp.Value = Mathf.Max(0f, _currentHp.Value - damage);

            if (_currentHp.Value <= 0f)
                Die();
        }

        [Server]
        private void Die()
        {
            _state.Value = BossState.Dead;
            _agent.isStopped = true;

            Debug.Log($"[BossController] Boss '{_bossId}' killed.");

            // Spawn chest
            SpawnDropChest();

            // Broadcast event
            GameplayEventBus.Instance?.Publish(new BossKilledEvent { BossId = _bossId });
            RpcOnBossDied();

            // Notify manager
            Died?.Invoke(this);

            // Despawn after a short delay so death animation plays
            float despawnDelay = _config != null && _config.DespawnDelay > 0f ? _config.DespawnDelay : 3f;
            Invoke(nameof(DespawnBoss), despawnDelay);
        }

        private void SpawnDropChest()
        {
            if (_bossChestPrefab == null)
            {
                Debug.LogWarning($"[BossController] No chest prefab assigned for boss '{_bossId}'.");
                return;
            }

            GameObject go = Instantiate(
                _bossChestPrefab,
                transform.position + Vector3.up * 0.5f,
                Quaternion.identity);

            var chest = ComponentResolver.Find<BossChest>(go)
                                        .OnSelf()
                                        .InChildren()
                                        .OrLogWarning("[Auto] BossChest not found")
                                        .Resolve();
            if (chest != null && _config != null)
                chest.Initialize(_config.DropTable);

            InstanceFinder.ServerManager.Spawn(go);
        }

        private void DespawnBoss()
        {
            if (IsSpawned)
                ServerManager.Despawn(gameObject);
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────────
        #region RPCs

        [ObserversRpc]
        private void RpcOnBossDied()
        {
            GameplayEventBus.Instance?.Publish(new BossKilledEvent { BossId = _bossId });
        }

        [ObserversRpc]
        private void RpcPlayAttackVFX()
        {
            var anim = ComponentResolver.Find<Animator>(this)
        .OnSelf()
        .InChildren()
        .InParent()
        .OrLogWarning("[Auto] Animator not found")
        .Resolve();
            anim?.SetTrigger("Attack");

            var ps = ComponentResolver.Find<ParticleSystem>(this)
        .OnSelf()
        .InChildren()
        .InParent()
        .OrLogWarning("[Auto] ParticleSystem not found")
        .Resolve();
            if (ps != null && !ps.isPlaying)
                ps.Play();
        }

        #endregion
    }

    public enum BossState { Idle, Aggro, Attack, Dead }
}
