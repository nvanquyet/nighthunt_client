using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using NightHunt.Core;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Gameplay.FogOfWar;

namespace NightHunt.Gameplay.Deployables
{
    /// <summary>
    /// Base class for all placeable deployables.
    /// Handles owner team, health, placed/active state, damage, despawn, and fog visibility.
    /// </summary>
    [RequireComponent(typeof(FogTeamVisibilityBinder))]
    public abstract class BaseDeployable : NetworkBehaviour, IHittable, IHealthSource, IBulletTarget, IFogTeamOwned
    {
        [Header("Health")]
        [Tooltip("Prefab-owned HP. DeployableDefinition only overrides this when OverridePrefabHealth is enabled.")]
        [SerializeField, Min(1)] private int _defaultMaxHP = 100;

        [Header("Bullet Targeting")]
        [Tooltip("Point offset used by BulletTargetRegistry when weapons acquire this deployable as a target.")]
        [SerializeField] private Vector3 _bulletAcquirePointOffset = Vector3.zero;

        [Tooltip("Broadphase radius used by BulletTargetRegistry target acquisition.")]
        [SerializeField, Min(0.1f)] private float _bulletAcquireRadius = 0.75f;

        [Header("Hit Setup")]
        [Tooltip("Runtime safety for custom deployable prefabs. Colliders left on Default/Items are moved to Interactable so weapon raycasts can hit them.")]
        [SerializeField] private bool _normalizeDefaultColliderLayers = true;

        protected readonly SyncVar<int> _currentHP = new SyncVar<int>();
        protected readonly SyncVar<int> _maxHP = new SyncVar<int>();
        protected readonly SyncVar<int> _ownerTeamId = new SyncVar<int>();
        protected readonly SyncVar<int> _ownerNetworkObjectId = new SyncVar<int>();
        protected readonly SyncVar<bool> _isPlaced = new SyncVar<bool>();
        protected readonly SyncVar<bool> _isActive = new SyncVar<bool>();
        protected readonly SyncVar<int> _lastDamageInstigatorNetworkObjectId = new SyncVar<int>();

        private FogTeamVisibilityBinder _fogVisibilityBinder;

        public int CurrentHP => _currentHP.Value;
        public int MaxHP => _maxHP.Value > 0 ? _maxHP.Value : _defaultMaxHP;
        public int DefaultMaxHP => Mathf.Max(1, _defaultMaxHP);
        public int OwnerTeamId => _ownerTeamId.Value;
        public int OwnerNetworkObjectId => _ownerNetworkObjectId.Value;
        public bool IsPlaced => _isPlaced.Value;
        public bool IsActive => _isActive.Value;
        public float CurrentHealth => CurrentHP;
        public float MaxHealth => MaxHP > 0 ? MaxHP : 100f;
        public bool IsDead => CurrentHP <= 0;

        public int FogOwnerTeamId => OwnerTeamId;
        public bool FogAlwaysVisible => false;

        public event Action Destroyed;
        public event Action<HealthChangeEvent> HealthChanged;

        public virtual HittableTargetType TargetType => HittableTargetType.Deployable;
        public Vector3 AcquirePoint => transform.position + transform.TransformDirection(_bulletAcquirePointOffset);
        public float AcquireRadius => Mathf.Max(0.1f, _bulletAcquireRadius);
        public IHittable HitTarget => this;
        public bool IsAcquirable => isActiveAndEnabled && _isPlaced.Value && _isActive.Value && !IsDead;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            _fogVisibilityBinder = EnsureFogBinder();
            EnsureWeaponHitSetup();

            _currentHP.OnChange += OnHPChanged;
            _isPlaced.OnChange += OnIsPlacedChanged;
            _ownerTeamId.OnChange += HandleOwnerTeamIdChanged;
            _isActive.OnChange += HandleActiveStateChanged;

            BulletTargetRegistry.Register(this);
            ApplyPlacedVisual(_isPlaced.Value);
            RefreshFogVisibility("OnStartNetwork");

        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();

            CancelInvoke();
            _currentHP.OnChange -= OnHPChanged;
            _isPlaced.OnChange -= OnIsPlacedChanged;
            _ownerTeamId.OnChange -= HandleOwnerTeamIdChanged;
            _isActive.OnChange -= HandleActiveStateChanged;
            BulletTargetRegistry.Unregister(this);
        }

        [Server]
        public virtual void Initialize(int teamId, int maxHP)
            => Initialize(teamId, maxHP, 0);

        [Server]
        public virtual void Initialize(int teamId, int maxHP, int ownerNetworkObjectId)
        {
            int resolvedMaxHP = ResolveInitialMaxHP(maxHP);
            _ownerTeamId.Value = teamId;
            _ownerNetworkObjectId.Value = ownerNetworkObjectId;
            _maxHP.Value = resolvedMaxHP;
            _currentHP.Value = resolvedMaxHP;
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
            ApplyDamageServer(damage, 0);
        }

        [Server]
        public virtual void TakeDamage(DamageInfo info)
        {
            ApplyDamageServer(Mathf.RoundToInt(info.Damage), info.ShooterNetworkObjectId);
        }

        public void RequestDamage(DamageInfo info)
        {
            if (IsServerStarted)
            {
                TakeDamage(info);
                return;
            }

            RequestDamageServerRpc(info);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestDamageServerRpc(DamageInfo info) => TakeDamage(info);

        [Server]
        protected virtual void ApplyDamageServer(int damage, int instigatorNetworkObjectId)
        {
            if (!_isActive.Value || !_isPlaced.Value)
                return;

            int finalDamage = Mathf.Max(0, damage);
            if (finalDamage <= 0)
                return;

            _lastDamageInstigatorNetworkObjectId.Value = Mathf.Max(0, instigatorNetworkObjectId);
            _currentHP.Value = Mathf.Max(0, _currentHP.Value - finalDamage);
            if (_currentHP.Value <= 0)
                OnDeployableDestroyed();
        }

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

        protected int ResolveInitialMaxHP(int maxHPOverride)
            => maxHPOverride > 0 ? maxHPOverride : DefaultMaxHP;

        [Server]
        protected void DespawnDeployable()
        {
            if (IsSpawned)
                Despawn();
        }

        protected virtual void OnHPChanged(int oldHP, int newHP, bool asServer)
        {
            if (!asServer)
                HealthChanged?.Invoke(new HealthChangeEvent(
                    oldHP,
                    newHP,
                    MaxHealth,
                    _lastDamageInstigatorNetworkObjectId.Value,
                    forceReveal: newHP < oldHP));
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

        private void EnsureWeaponHitSetup()
        {
            var colliders = GetComponentsInChildren<Collider>(includeInactive: true);
            if (colliders == null || colliders.Length == 0)
            {
                Debug.LogWarning($"[{GetType().Name}] '{name}' has no Collider. It can register as a bullet target, but direct raycast/projectile hits need a collider.", this);
                return;
            }

            if (!_normalizeDefaultColliderLayers)
                return;

            int defaultLayer = LayerMask.NameToLayer(NightHuntLayers.Default);
            int itemsLayer = NightHuntLayers.IdItems;
            int interactableLayer = NightHuntLayers.IdInteractable;
            if (defaultLayer < 0 || interactableLayer < 0)
                return;

            if (gameObject.layer == defaultLayer || gameObject.layer == itemsLayer)
                gameObject.layer = interactableLayer;

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider col = colliders[i];
                if (col != null && (col.gameObject.layer == defaultLayer || col.gameObject.layer == itemsLayer))
                    col.gameObject.layer = interactableLayer;
            }
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
