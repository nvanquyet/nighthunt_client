using System;
using System.Collections;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Gameplay.FogOfWar;

namespace NightHunt.Gameplay.Deployables
{
    /// <summary>
    /// Lớp cơ sở (Base Class) cho tất cả các thiết bị/item có thể đặt xuống đất.
    /// Manages Máu, Trạng thái (Placed/Active), Logic bị phá huỷ.
    /// Visual nên được handle thông qua prefab hierarchy, không cần script tham chiếu ở base.
    ///
    /// FOW: [RequireComponent] ensures FogTeamVisibilityBinder is always present on the GO.
    /// Runtime EnsureFogBinder() covers existing prefabs that were created before this attribute.
    /// </summary>
    [RequireComponent(typeof(FogTeamVisibilityBinder))]
    public abstract class BaseDeployable : NetworkBehaviour, IHittable, IFogTeamOwned
    {
        // ── IFogTeamOwned ──────────────────────────────────────────────────────────────
        /// <inheritdoc/>
        public int  FogOwnerTeamId  => OwnerTeamId; // SyncVar-backed — always current on all clients
        /// <inheritdoc/>
        public bool FogAlwaysVisible => false;       // Deployables obey FOW based on owner team
        // Synchronized state
        protected readonly SyncVar<int> _currentHP = new SyncVar<int>();
        protected readonly SyncVar<int> _ownerTeamId = new SyncVar<int>();
        protected readonly SyncVar<bool> _isPlaced = new SyncVar<bool>();
        protected readonly SyncVar<bool> _isActive = new SyncVar<bool>();

        private FogTeamVisibilityBinder _fogVisibilityBinder;

        public int CurrentHP => _currentHP.Value;
        public int OwnerTeamId => _ownerTeamId.Value;
        public bool IsPlaced => _isPlaced.Value;
        public bool IsActive => _isActive.Value;

        /// <summary>Sự kiện bắn ra trên Server khi Entity này hết máu.</summary>
        public event Action Destroyed;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            // Runtime safety: [RequireComponent] only auto-adds when script is dragged onto a new GO.
            // Existing prefabs won't have the component until they are opened+saved in the Editor.
            // EnsureFogBinder() covers this gap so all spawned GOs always have the binder.
            _fogVisibilityBinder = EnsureFogBinder();

            _currentHP.OnChange += OnHPChanged;
            _isPlaced.OnChange  += OnIsPlacedChanged;
            _ownerTeamId.OnChange += HandleOwnerTeamIdChanged;
            _isActive.OnChange += HandleActiveStateChanged;

            ApplyPlacedVisual(_isPlaced.Value);
            RefreshFogVisibility("OnStartNetwork");
        }

        /// <summary>
        /// Ensures <see cref="FogTeamVisibilityBinder"/> exists on this GO.
        /// Called from <see cref="OnStartNetwork"/> to cover prefabs created before
        /// [RequireComponent(FogTeamVisibilityBinder)] was added to this class.
        /// No-op if the component already exists.
        /// </summary>
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

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _currentHP.OnChange -= OnHPChanged;
            _isPlaced.OnChange -= OnIsPlacedChanged;
            _ownerTeamId.OnChange -= HandleOwnerTeamIdChanged;
            _isActive.OnChange -= HandleActiveStateChanged;
        }

        /// <summary>Server: Khởi tạo thông tin chủ sở hữu và trạng thái cơ bản.</summary>
        [Server]
        public virtual void Initialize(int teamId, int maxHP)
        {
            _ownerTeamId.Value = teamId;
            _currentHP.Value = maxHP;
            _isPlaced.Value = false;
            _isActive.Value = true;
        }

        /// <summary>Server: Bắt đầu quá trình thiết lập (cắm xuống đất).</summary>
        [Server]
        public virtual void StartPlacement()
        {
            if (_isPlaced.Value) return;
            
            // Placement logic (delay, validation) is moved to dedicated systems if needed.
            // Base class now just confirms placement.
            _isPlaced.Value = true;
            OnDeployablePlaced();
        }

        /// <summary>Server: Receive sát thương.</summary>
        [Server]
        public virtual void TakeDamage(int damage)
        {
            if (!_isActive.Value || !_isPlaced.Value) return;

            _currentHP.Value = Mathf.Max(0, _currentHP.Value - damage);

            if (_currentHP.Value <= 0)
            {
                OnDeployableDestroyed();
            }
        }

        // ── IHittable ──────────────────────────────────────────────────────────
        public void RequestDamage(DamageInfo info) => RequestDamageServerRpc((int)info.Damage);

        [ServerRpc(RequireOwnership = false)]
        private void RequestDamageServerRpc(int damage) => TakeDamage(damage);

        /// <summary>Server: Khi thiết bị bị phá huỷ (hết máu).</summary>
        [Server]
        protected virtual void OnDeployableDestroyed()
        {
            _isActive.Value = false;
            Debug.Log($"[{GetType().Name}] destroyed (Team {_ownerTeamId.Value})");

            Destroyed?.Invoke();

            // Huỷ (Despawn) object khỏi Network sau vài giây delay diễn hoạt ảnh vỡ vụn
            Invoke(nameof(DespawnDeployable), 2f);
        }

        /// <summary>Server: Lifecycle method gọi khi cắm success.</summary>
        [Server]
        protected virtual void OnDeployablePlaced() { }

        [Server]
        protected void DespawnDeployable()
        {
            if (IsSpawned) Despawn();
        }

        // --- Client / Visual Updates ---

        protected virtual void OnHPChanged(int oldHP, int newHP, bool asServer) { }

        protected virtual void OnIsPlacedChanged(bool oldVal, bool newVal, bool asServer)
        {
            ApplyPlacedVisual(newVal);
            RefreshFogVisibility("PlacedChanged");
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

        /// <summary>Xử lý visual dựa trên trạng thái Placed. Subclass có thể override.</summary>
        protected virtual void ApplyPlacedVisual(bool placed) { }
    }
}
