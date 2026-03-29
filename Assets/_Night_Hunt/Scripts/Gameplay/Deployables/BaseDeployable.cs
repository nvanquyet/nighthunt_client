using System;
using System.Collections;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace NightHunt.Gameplay.Deployables
{
    /// <summary>
    /// Lớp cơ sở (Base Class) cho tất cả các thiết bị/vật phẩm có thể đặt xuống đất.
    /// Quản lý Máu, Trạng thái (Placed/Active), Logic bị phá huỷ.
    /// Visual nên được xử lý thông qua prefab hierarchy, không cần script tham chiếu ở base.
    /// </summary>
    public abstract class BaseDeployable : NetworkBehaviour
    {
        // Synchronized state
        protected readonly SyncVar<int> _currentHP = new SyncVar<int>();
        protected readonly SyncVar<int> _ownerTeamId = new SyncVar<int>();
        protected readonly SyncVar<bool> _isPlaced = new SyncVar<bool>();
        protected readonly SyncVar<bool> _isActive = new SyncVar<bool>();

        public int CurrentHP => _currentHP.Value;
        public int OwnerTeamId => _ownerTeamId.Value;
        public bool IsPlaced => _isPlaced.Value;
        public bool IsActive => _isActive.Value;

        /// <summary>Sự kiện bắn ra trên Server khi Entity này hết máu.</summary>
        public event Action Destroyed;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            _currentHP.OnChange += OnHPChanged;
            _isPlaced.OnChange += OnIsPlacedChanged;

            ApplyPlacedVisual(_isPlaced.Value);
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _currentHP.OnChange -= OnHPChanged;
            _isPlaced.OnChange -= OnIsPlacedChanged;
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

        /// <summary>Server: Nhận sát thương.</summary>
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

        /// <summary>Server: Lifecycle method gọi khi cắm thành công.</summary>
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
        }

        /// <summary>Xử lý visual dựa trên trạng thái Placed. Subclass có thể override.</summary>
        protected virtual void ApplyPlacedVisual(bool placed) { }
    }
}
