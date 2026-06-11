using System;
using FishNet.Object;
using NightHunt.Gameplay.Camera.Spectator;
using NightHunt.Gameplay.Core.State;
using NightHunt.Gameplay.Respawn;
using NightHunt.Networking;
using NightHunt.Networking.Player;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.StatSystem.Systems;
using UnityEngine;
using NightHunt.Utilities;
using NightHunt.Diagnostics;

namespace NightHunt.Gameplay.Core.State
{
    /// <summary>
    /// Bridges PlayerStatSystem (Health) với CharacterStateMachine
    /// và phát event lifecycle (OnDied/OnRespawned) cho các hệ khác nghe.
    /// Không phụ thuộc movement hay combat.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterLifecycleController : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerStatSystem _statSystemSource;
        [SerializeField] private CharacterStateMachine _stateMachine;
        [SerializeField] private NetworkPlayer _networkPlayer;
        [SerializeField] private RespawnSystem _respawnSystem;

        [Header("Settings")]
        [SerializeField] private bool _autoRequestRespawnOnDeath = true;

        /// <summary>
        /// Fired when health crosses from >0 to <=0.
        /// </summary>
        public event Action OnDied;

        /// <summary>
        /// Fired when health crosses from <=0 to >0.
        /// </summary>
        public event Action OnRespawned;

        public bool IsDead { get; private set; }

        /// <summary>
        /// Name of the player/entity that dealt the killing blow.
        /// Set via <see cref="SetKillerInfo"/> before the death event fires.
        /// </summary>
        public string LastKillerName { get; private set; } = string.Empty;

        /// <summary>
        /// Call this (e.g. from CharacterCombat or damage RPC) before health
        /// reaches zero so the DeathScreen can display the killer's name.
        /// </summary>
        public void SetKillerInfo(string killerName)
        {
            LastKillerName = killerName ?? string.Empty;
            PhaseTestLog.Log(PhaseTestLogCategory.Death, "KillerInfoSet", $"player={_networkPlayer?.DisplayName ?? "null"} killer={LastKillerName}", this);
        }

        private IPlayerStatSystem _statSystem;
        private bool _statsSubscribed;
        private bool _aliveStateSubscribed;

        #region Unity Lifecycle

        private void Awake()
        {
            if (_stateMachine == null)
                _stateMachine = ComponentResolver.Find<CharacterStateMachine>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] CharacterStateMachine not found")
        .Resolve();

            if (_networkPlayer == null)
                _networkPlayer = ComponentResolver.Find<NetworkPlayer>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] NetworkPlayer not found")
        .Resolve();

            if (_respawnSystem == null)
                _respawnSystem = FindFirstObjectByType<RespawnSystem>();

            ResolveStatSystem();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            ResolveStatSystem();
        }
#endif

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            ResolveStatSystem();
            SubscribeToStats();
            SubscribeToAliveState();
            ReconcileAliveState();

            // Bug #22 fix: push local lifecycle to GameCameraController so it doesn't
            // use FindFirstObjectByType (which finds the wrong player in multiplayer).
            if (base.Owner.IsLocalClient)
            {
                GameCameraController.RegisterLocalLifecycle(this);
                NightHunt.Graphics.PostProcessStateManager.RegisterLocalPlayer(this, _statSystem);
            }
        }

        public override void OnStopNetwork()
        {
            UnsubscribeFromStats();
            UnsubscribeFromAliveState();
            base.OnStopNetwork();
        }

        private void OnEnable()
        {
            SubscribeToStats();
            SubscribeToAliveState();
            ReconcileAliveState();
        }

        private void OnDisable()
        {
            UnsubscribeFromStats();
            UnsubscribeFromAliveState();
        }

        #endregion

        #region Stat Handling

        private void ResolveStatSystem()
        {
            if (_statSystem != null)
                return;

            _statSystem = ComponentResolver.Find<IPlayerStatSystem>(this)
        .UseExisting(_statSystemSource)
        .OnSelf()
        .InChildren()
        .InParent()
        .InRootChildren()
        .OrLogWarning("[Auto] IPlayerStatSystem not found")
        .Resolve();

            if (_statSystem is PlayerStatSystem statConcrete)
                _statSystemSource = statConcrete;
        }

        private void SubscribeToStats()
        {
            if (_statsSubscribed)
                return;

            if (_statSystem is null)
                ResolveStatSystem();

            if (_statSystem is null)
                return;

            // IPlayerStatSystem trong project dùng event trên implementation PlayerStatSystem,
            // nên ta cast sang concrete nếu có để lấy event.
            if (_statSystem is Component comp)
            {
                var concrete = comp as NightHunt.Gameplay.StatSystem.Systems.PlayerStatSystem;
                if (concrete != null)
                {
                    concrete.OnStatChanged += HandleStatChanged;
                    _statsSubscribed = true;
                }
            }
        }

        private void UnsubscribeFromStats()
        {
            if (!_statsSubscribed)
                return;

            if (_statSystem is Component comp)
            {
                var concrete = comp as NightHunt.Gameplay.StatSystem.Systems.PlayerStatSystem;
                if (concrete != null)
                {
                    concrete.OnStatChanged -= HandleStatChanged;
                }
            }

            _statsSubscribed = false;
        }

        private void SubscribeToAliveState()
        {
            if (_aliveStateSubscribed || _networkPlayer == null)
                return;

            _networkPlayer.OnAliveChanged += HandleAliveStateChanged;
            _aliveStateSubscribed = true;
        }

        private void UnsubscribeFromAliveState()
        {
            if (!_aliveStateSubscribed || _networkPlayer == null)
                return;

            _networkPlayer.OnAliveChanged -= HandleAliveStateChanged;
            _aliveStateSubscribed = false;
        }

        private void HandleAliveStateChanged(bool isAlive)
        {
            if (isAlive)
            {
                if (IsDead)
                    HandleRespawnedFromHealth();
            }
            else if (!IsDead)
            {
                HandleDeath();
            }
        }

        private void ReconcileAliveState()
        {
            if (_networkPlayer != null)
                HandleAliveStateChanged(_networkPlayer.IsAlive);
        }

        private void HandleStatChanged(PlayerStatType type, float oldValue, float newValue)
        {
            if (type != PlayerStatType.Health)
                return;

            // Death: from >0 to <=0
            if (!IsDead && oldValue > 0f && newValue <= 0f)
            {
                HandleDeath();
            }
            // Respawn / revive: from <=0 to >0
            else if (IsDead && oldValue <= 0f && newValue > 0f)
            {
                HandleRespawnedFromHealth();
            }
        }

        #endregion

        #region Lifecycle Logic

        private void HandleDeath()
        {
            IsDead = true;
            PhaseTestLog.Log(
                PhaseTestLogCategory.Death,
                "LifecycleDeath",
                $"player={_networkPlayer?.DisplayName ?? "null"} obj={_networkPlayer?.ObjectId ?? 0} team={_networkPlayer?.TeamId ?? -1} killer={LastKillerName} autoRespawn={_autoRequestRespawnOnDeath}",
                this);

            if (_stateMachine != null)
            {
                _stateMachine.TransitionTo(CharacterLifecycleState.Dead);
            }

            if (IsServerInitialized)
            {
                // Establish the authoritative death/respawn disposition before match-end
                // subscribers evaluate whether the team has been eliminated.
                _networkPlayer?.SetAlive(false);

                if (_autoRequestRespawnOnDeath &&
                    _respawnSystem != null &&
                    _networkPlayer != null)
                {
                    _respawnSystem.ServerInitiateRespawn(_networkPlayer);
                }
            }

            OnDied?.Invoke();

            // Reset killer name after firing so it doesn't linger
            LastKillerName = string.Empty;
        }

        private void HandleRespawnedFromHealth()
        {
            IsDead = false;
            PhaseTestLog.Log(
                PhaseTestLogCategory.Death,
                "LifecycleRespawned",
                $"player={_networkPlayer?.DisplayName ?? "null"} obj={_networkPlayer?.ObjectId ?? 0} team={_networkPlayer?.TeamId ?? -1} pos={transform.position:F2}",
                this);

            if (_stateMachine != null)
            {
                _stateMachine.TransitionTo(CharacterLifecycleState.Alive);
            }

            OnRespawned?.Invoke();

            // GLOBAL EVENT
            NightHunt.Gameplay.Core.Events.GameplayEventBus.Instance?.Publish(new NightHunt.Gameplay.Core.Events.PlayerRespawnedEvent
            {
                PlayerName = _networkPlayer != null ? _networkPlayer.DisplayName : string.Empty,
                TeamId = _networkPlayer != null ? _networkPlayer.TeamId : -1,
                Position = transform.position
            });

            if (IsServerInitialized)
                _networkPlayer?.SetAlive(true);
        }

        #endregion
    }
}

