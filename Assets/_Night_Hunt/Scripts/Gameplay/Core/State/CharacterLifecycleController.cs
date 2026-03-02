using System;
using FishNet.Object;
using NightHunt.Gameplay.Core.State;
using NightHunt.Gameplay.Respawn;
using NightHunt.Networking;
using NightHunt.StatSystem.Core.Interfaces;
using NightHunt.StatSystem.Core.Types;
using UnityEngine;

namespace NightHunt.Gameplay.Core.State
{
    /// <summary>
    /// Bridges PlayerStatSystem (Health) với CharacterStateMachine
    /// và phát sự kiện lifecycle (OnDied/OnRespawned) cho các hệ khác nghe.
    /// Không phụ thuộc movement hay combat.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterLifecycleController : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private MonoBehaviour _statSystemSource;
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
        }

        private IPlayerStatSystem _statSystem;

        #region Unity Lifecycle

        private void Awake()
        {
            if (_stateMachine == null)
                _stateMachine = GetComponent<CharacterStateMachine>();

            if (_networkPlayer == null)
                _networkPlayer = GetComponent<NetworkPlayer>();

            if (_respawnSystem == null)
                _respawnSystem = FindFirstObjectByType<RespawnSystem>();

            if (_statSystemSource == null)
                _statSystemSource = GetComponent<MonoBehaviour>() as IPlayerStatSystem as MonoBehaviour;

            ResolveStatSystem();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            ResolveStatSystem();
            SubscribeToStats();
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            UnsubscribeFromStats();
        }

        private void OnEnable()
        {
            SubscribeToStats();
        }

        private void OnDisable()
        {
            UnsubscribeFromStats();
        }

        #endregion

        #region Stat Handling

        private void ResolveStatSystem()
        {
            if (_statSystem != null)
                return;

            if (_statSystemSource != null)
            {
                _statSystem = _statSystemSource as IPlayerStatSystem;
            }

            if (_statSystem == null)
            {
                _statSystem = GetComponent<IPlayerStatSystem>();
            }
        }

        private void SubscribeToStats()
        {
            if (_statSystem is null)
                ResolveStatSystem();

            if (_statSystem is null)
                return;

            // IPlayerStatSystem trong project dùng event trên implementation PlayerStatSystem,
            // nên ta cast sang concrete nếu có để lấy event.
            if (_statSystem is Component comp)
            {
                var concrete = comp as NightHunt.StatSystem.Systems.PlayerStatSystem;
                if (concrete != null)
                {
                    concrete.OnStatChanged += HandleStatChanged;
                }
            }
        }

        private void UnsubscribeFromStats()
        {
            if (_statSystem is Component comp)
            {
                var concrete = comp as NightHunt.StatSystem.Systems.PlayerStatSystem;
                if (concrete != null)
                {
                    concrete.OnStatChanged -= HandleStatChanged;
                }
            }
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

            if (_stateMachine != null)
            {
                _stateMachine.TransitionTo(CharacterLifecycleState.Dead);
            }

            OnDied?.Invoke();

            // Reset killer name after firing so it doesn't linger
            LastKillerName = string.Empty;

            if (!IsServer)
                return;

            if (_autoRequestRespawnOnDeath &&
                _respawnSystem != null &&
                _networkPlayer != null)
            {
                _respawnSystem.RequestRespawn(_networkPlayer);
            }
        }

        private void HandleRespawnedFromHealth()
        {
            IsDead = false;

            if (_stateMachine != null)
            {
                _stateMachine.TransitionTo(CharacterLifecycleState.Alive);
            }

            OnRespawned?.Invoke();
        }

        #endregion
    }
}

