using System;
using System.Collections;
using UnityEngine;
using FishNet.Object;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.StatSystem.Core.Interfaces;

namespace NightHunt.GameplaySystems.QuickSlot
{
    /// <summary>
    /// Orchestrates item usage on the server, routing consumables to
    /// <see cref="ConsumableHandler"/> and throwables to <see cref="ThrowableHandler"/>.
    /// </summary>
    public class ItemUseSystem : NetworkBehaviour, IItemUseSystem, IDisposable
    {
        #region Serialized Fields
        
        [Header("References")]
        [SerializeField] private MonoBehaviour _weaponSystemComponent;
        [SerializeField] private MonoBehaviour _statSystemComponent;
        [SerializeField] private MonoBehaviour _inventorySystemComponent;
        
        [Header("Handlers")]
        [SerializeField] private ConsumableHandler _consumableHandler;
        [SerializeField] private ThrowableHandler _throwableHandler;
        
        [Header("Settings")]
        [Tooltip("Fallback use duration when ConsumableDefinition.UsageDuration == 0")]
        [SerializeField] private float _defaultUseTime = 3.5f;
        
        #endregion
        
        #region Runtime State
        
        private IWeaponSystem _weaponSystem;
        private IPlayerStatSystem _statSystem;
        private IInventorySystem _inventorySystem;
        private bool _isUsingItem;
        private ItemInstance _currentItem;
        private WeaponSlotType? _previousWeaponSlot;
        private Coroutine _useCoroutine;
        
        #endregion
        
        #region Properties
        
        public bool IsUsingItem => _isUsingItem;
        public ItemInstance CurrentItem => _currentItem;
        
        #endregion
        
        #region Events
        
        public event Action<ItemInstance> OnItemUseStarted;
        public event Action<ItemInstance> OnItemUseCompleted;
        public event Action<ItemInstance> OnItemUseCancelled;
        public event Action<ItemInstance, float> OnItemUseProgress;
        
        #endregion
        
        #region Lifecycle
        
        private void Awake()
        {
            ValidateReferences();
            InitializeHandlers();
        }
        
        private void ValidateReferences()
        {
            // Get components and cast to interfaces
            if (_weaponSystemComponent == null)
                _weaponSystemComponent = GetComponent<MonoBehaviour>();
            
            _weaponSystem = _weaponSystemComponent as IWeaponSystem;
            _statSystem = _statSystemComponent as IPlayerStatSystem;
            _inventorySystem = _inventorySystemComponent as IInventorySystem;
            
#if UNITY_EDITOR
            // Auto-find if not assigned
            if (_weaponSystem == null)
            {
                var ws = GetComponent<IWeaponSystem>();
                if (ws != null) _weaponSystemComponent = ws as MonoBehaviour;
                _weaponSystem = ws;
            }
            
            if (_statSystem == null)
            {
                var ss = GetComponent<IPlayerStatSystem>();
                if (ss != null) _statSystemComponent = ss as MonoBehaviour;
                _statSystem = ss;
            }
            
            if (_inventorySystem == null)
            {
                var inv = GetComponent<IInventorySystem>();
                if (inv != null) _inventorySystemComponent = inv as MonoBehaviour;
                _inventorySystem = inv;
            }
#endif
            
            if (_weaponSystem == null)
                Debug.LogError("[ItemUseSystem] IWeaponSystem is null!");
            
            if (_statSystem == null)
                Debug.LogError("[ItemUseSystem] IPlayerStatSystem is null!");
            
            if (_inventorySystem == null)
                Debug.LogError("[ItemUseSystem] IInventorySystem is null!");
        }
        
        private void InitializeHandlers()
        {
            // Auto-create handlers if not assigned
            if (_consumableHandler == null)
            {
                _consumableHandler = gameObject.AddComponent<ConsumableHandler>();
                _consumableHandler.Initialize(_statSystem);
            }
            
            if (_throwableHandler == null)
            {
                _throwableHandler = gameObject.AddComponent<ThrowableHandler>();
                _throwableHandler.Initialize(transform);
            }
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Main entry point - route to appropriate handler
        /// </summary>
        [Server]
        public bool UseItem(ItemInstance item)
        {
            if (item == null)
            {
                Debug.LogWarning("[ItemUseSystem] UseItem: item is null");
                return false;
            }
            
            if (_isUsingItem)
            {
                Debug.LogWarning("[ItemUseSystem] Already using an item");
                return false;
            }
            
            var def = ItemDatabase.GetDefinition(item.DefinitionID);
            if (def == null)
            {
                Debug.LogError($"[ItemUseSystem] No definition: {item.DefinitionID}");
                return false;
            }
            
            if (def is ConsumableDefinition cd)
                return BeginConsumable(item, cd);
            
            if (def is ThrowableDefinition td)
                return BeginThrowable(item, td);
            
            Debug.LogWarning($"[ItemUseSystem] Unsupported item type: {def.GetType().Name}");
            return false;
        }
        
        /// <summary>
        /// Execute throw (called by input when Fire pressed during throw-mode)
        /// </summary>
        [Server]
        public void ExecuteThrow()
        {
            if (!_isUsingItem || _currentItem == null)
            {
                Debug.LogWarning("[ItemUseSystem] ExecuteThrow: no active throwable");
                return;
            }
            
            var def = ItemDatabase.GetDefinition(_currentItem.DefinitionID) as ThrowableDefinition;
            if (def == null)
            {
                Debug.LogError("[ItemUseSystem] Current item is not throwable");
                return;
            }
            
            // Spawn projectile via handler — pass confirmed aim target from QuickSlotAimController.
            // QuickSlotAimController.AimWorldTarget is set when the player confirms the throw direction.
            _throwableHandler.SpawnProjectile(
                def,
                transform,
                NightHunt.GameplaySystems.UI.Combat.QuickSlotAimController.AimWorldTarget);

            // Consume item & complete
            ConsumeItem(_currentItem);
            CompleteUse(_currentItem);
        }
        
        /// <summary>
        /// Cancel in-progress use
        /// </summary>
        [Server]
        public void CancelUse()
        {
            if (!_isUsingItem)
                return;
            
            // Check if cancellation allowed
            var def = ItemDatabase.GetDefinition(_currentItem?.DefinitionID);
            if (def != null && !def.CanCancelUsage)
            {
                Debug.Log("[ItemUseSystem] This item cannot be cancelled");
                return;
            }
            
            // Stop coroutine if running
            if (_useCoroutine != null)
            {
                StopCoroutine(_useCoroutine);
                _useCoroutine = null;
            }
            
            var item = _currentItem;
            _isUsingItem = false;
            _currentItem = null;

            OnItemUseCancelled?.Invoke(item);
            RestoreWeapon();
        }
        
        #endregion
        
        #region Consumable Flow
        
        private bool BeginConsumable(ItemInstance item, ConsumableDefinition def)
        {
            HolsterAndSave();
            _currentItem = item;
            _isUsingItem = true;
            
            float duration = def.UsageDuration > 0f ? def.UsageDuration : _defaultUseTime;
            _useCoroutine = StartCoroutine(ConsumableRoutine(item, def, duration));
            
            OnItemUseStarted?.Invoke(item);
            Debug.Log($"[ItemUseSystem] Consumable started: '{def.DisplayName}' ({duration}s)");
            
            return true;
        }
        
        private IEnumerator ConsumableRoutine(ItemInstance item, ConsumableDefinition def, float duration)
        {
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                OnItemUseProgress?.Invoke(item, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            
            // Apply effects via handler
            _consumableHandler.ApplyEffects(def);
            
            // Consume & complete
            ConsumeItem(item);
            CompleteUse(item);
        }
        
        #endregion
        
        #region Throwable Flow
        
        private bool BeginThrowable(ItemInstance item, ThrowableDefinition def)
        {
            HolsterAndSave();
            _currentItem = item;
            _isUsingItem = true;
            
            OnItemUseStarted?.Invoke(item);
            Debug.Log($"[ItemUseSystem] Throw mode: '{def.DisplayName}'. Press Fire to throw.");
            
            return true;
        }
        
        #endregion
        
        #region Weapon Holster/Restore
        
        private void HolsterAndSave()
        {
            if (_weaponSystem == null)
                return;

            _previousWeaponSlot = _weaponSystem.GetActiveWeaponSlot();
            if (_previousWeaponSlot.HasValue)
                _weaponSystem.HolsterWeapon();
        }
        
        private void RestoreWeapon()
        {
            if (_weaponSystem == null)
                return;

            if (_previousWeaponSlot.HasValue)
                _weaponSystem.SelectWeapon(_previousWeaponSlot.Value);

            _previousWeaponSlot = null;
        }
        
        #endregion
        
        #region Shared Helpers
        
        private void ConsumeItem(ItemInstance item)
        {
            _inventorySystem?.RemoveItem(item.InstanceID, 1);
        }
        
        private void CompleteUse(ItemInstance item)
        {
            _isUsingItem = false;
            _currentItem = null;
            _useCoroutine = null;

            OnItemUseCompleted?.Invoke(item);
            RestoreWeapon();
        }
        
        #endregion
        
        #region Debug
        
        [ContextMenu("Log State")]
        public void LogState()
        {
            Debug.Log($"[ItemUseSystem] Using={_isUsingItem} | " +
                     $"Item={_currentItem?.DefinitionID ?? "none"} | " +
                     $"PrevSlot={_previousWeaponSlot}");
        }
        
        #endregion
        
        #region IDisposable Implementation
        
        public void Dispose()
        {
            // Cancel any ongoing item use
            if (_isUsingItem)
            {
                CancelUse();
            }
            
            // Stop any running coroutines
            if (_useCoroutine != null)
            {
                StopCoroutine(_useCoroutine);
                _useCoroutine = null;
            }
        }
        
        #endregion
    }
    
}