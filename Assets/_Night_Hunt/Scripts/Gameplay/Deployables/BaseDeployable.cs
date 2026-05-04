using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Gameplay.FogOfWar;

namespace NightHunt.Gameplay.Deployables
{
    /// <summary>
    /// Base class for all placeable deployables.
    /// Handles owner team, health, placed/active state, damage, despawn, and fog visibility.
    /// </summary>
    [RequireComponent(typeof(FogTeamVisibilityBinder))]
    public abstract class BaseDeployable : NetworkBehaviour, IHittable, IFogTeamOwned, IHealthSource
    {
        protected readonly SyncVar<int> _currentHP = new SyncVar<int>();
        protected readonly SyncVar<int> _maxHP = new SyncVar<int>();
        protected readonly SyncVar<int> _ownerTeamId = new SyncVar<int>();
        protected readonly SyncVar<bool> _isPlaced = new SyncVar<bool>();
        protected readonly SyncVar<bool> _isActive = new SyncVar<bool>();

        private FogTeamVisibilityBinder _fogVisibilityBinder;

        public int CurrentHP => _currentHP.Value;
        public int MaxHP => _maxHP.Value;
        public int OwnerTeamId => _ownerTeamId.Value;
        public bool IsPlaced => _isPlaced.Value;
        public bool IsActive => _isActive.Value;
        public float CurrentHealth => CurrentHP;
        public float MaxHealth => MaxHP > 0 ? MaxHP : 100f;
        public bool IsDead => CurrentHP <= 0;

        public int FogOwnerTeamId => OwnerTeamId;
        public bool FogAlwaysVisible => false;

        public event Action Destroyed;
        public event Action<HealthChangeEvent> HealthChanged;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            _fogVisibilityBinder = EnsureFogBinder();

            _currentHP.OnChange += OnHPChanged;
            _isPlaced.OnChange += OnIsPlacedChanged;
            _ownerTeamId.OnChange += HandleOwnerTeamIdChanged;
            _isActive.OnChange += HandleActiveStateChanged;

            ApplyPlacedVisual(_isPlaced.Value);
            RefreshFogVisibility("OnStartNetwork");

        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();

            _currentHP.OnChange -= OnHPChanged;
            _isPlaced.OnChange -= OnIsPlacedChanged;
            _ownerTeamId.OnChange -= HandleOwnerTeamIdChanged;
            _isActive.OnChange -= HandleActiveStateChanged;
        }

        [Server]
        public virtual void Initialize(int teamId, int maxHP)
        {
            _ownerTeamId.Value = teamId;
            _maxHP.Value = maxHP;
            _currentHP.Value = maxHP;
            _isPlaced.Value = false;
            _isActive.Value = true;
        }

        [Server]
        public virtual void StartPlacement()
        {
            if (_maxHP.Value <= 0)
                _maxHP.Value = Mathf.Max(1, _currentHP.Value);
            if (_currentHP.Value <= 0)
                _currentHP.Value = _maxHP.Value;
            if (!_isActive.Value)
                _isActive.Value = true;

            if (_isPlaced.Value)
                return;

            _isPlaced.Value = true;
            OnDeployablePlaced();
        }

        [Server]
        public virtual void TakeDamage(int damage)
        {
            if (!_isActive.Value || !_isPlaced.Value)
                return;

            _currentHP.Value = Mathf.Max(0, _currentHP.Value - damage);
            if (_currentHP.Value <= 0)
                OnDeployableDestroyed();
        }

        public void RequestDamage(DamageInfo info) => RequestDamageServerRpc((int)info.Damage);

        [ServerRpc(RequireOwnership = false)]
        private void RequestDamageServerRpc(int damage) => TakeDamage(damage);

        [Server]
        protected virtual void OnDeployableDestroyed()
        {
            _isActive.Value = false;
            Debug.Log($"[{GetType().Name}] destroyed (Team {_ownerTeamId.Value})");

            Destroyed?.Invoke();
            Invoke(nameof(DespawnDeployable), 2f);
        }

        [Server]
        protected virtual void OnDeployablePlaced()
        {
        }

        [Server]
        protected void DespawnDeployable()
        {
            if (IsSpawned)
                Despawn();
        }

        protected virtual void OnHPChanged(int oldHP, int newHP, bool asServer)
        {
            if (!asServer)
                HealthChanged?.Invoke(new HealthChangeEvent(oldHP, newHP, MaxHealth, forceReveal: newHP < oldHP));
        }

        protected virtual void OnIsPlacedChanged(bool oldVal, bool newVal, bool asServer)
        {
            ApplyPlacedVisual(newVal);
            RefreshFogVisibility("PlacedChanged");
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

        private void HandleOwnerTeamIdChanged(int oldVal, int newVal, bool asServer)
        {
            RefreshFogVisibility($"OwnerTeamChanged {oldVal}->{newVal}");
        }

        private void HandleActiveStateChanged(bool oldVal, bool newVal, bool asServer)
        {
            RefreshFogVisibility($"ActiveChanged {oldVal}->{newVal}");
        }

        protected void RefreshFogVisibility(string reason)
        {
#if !UNITY_SERVER
            if (_fogVisibilityBinder == null)
                _fogVisibilityBinder = GetComponent<FogTeamVisibilityBinder>();

            _fogVisibilityBinder?.RefreshVisibilityForLocalTeam();
#endif
        }

        protected virtual void ApplyPlacedVisual(bool placed)
        {
        }

    }
}
