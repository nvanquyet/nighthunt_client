using System;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NightHunt.Utilities;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.StatSystem.Core.Data;
using NightHunt.Gameplay.StatSystem.Systems;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Gameplay.Character.Combat.Weapons;
using NightHunt.Data;

namespace NightHunt.GameplaySystems.Weapon
{
    /// <summary>
    /// Coordinator — manages weapon slots, equip/unequip, firing, reload for a networked player.
    ///
    /// PREFAB SETUP (Child GO named "WeaponSystem"):
    ///   Attach WeaponSystem + WeaponModelController + WeaponVFXController to a child GO.
    ///   All cross-sibling refs are resolved in Awake via transform.parent lookups.
    ///
    /// PARTIAL CLASS SPLIT:
    ///   WeaponSystem.Core.cs        — this file: fields, lifecycle, helpers
    ///   WeaponSystem.Fire.cs        — StartFire, TryFireOnce, auto-fire, spread
    ///   WeaponSystem.Reload.cs      — RequestReload, ReloadCoroutine, ammo getters
    ///   WeaponSystem.EquipUnequip.cs— Equip / Unequip / Swap / Select / Holster
    ///   WeaponSystem.NetworkSync.cs — all RPCs for remote client synchronisation
    /// </summary>
    public partial class WeaponSystem : NetworkBehaviour, IWeaponSystem, IDisposable
    {
        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("Configuration")]
        [SerializeField] private InventoryConfig _inventoryConfig;

        [Header("References (auto-resolved if blank)")]
        [SerializeField] private PlayerStatSystem _statSystemSource;
        [SerializeField] private InventorySystem _inventorySystemSource;

        [Header("Slot Priority (auto-built from InventoryConfig — only used as fallback)")]
        [Tooltip("Override slot auto-equip priority. Usually leave empty and let InventoryConfig.WeaponConfig.Priority drive it.")]
        [SerializeField] private WeaponSlotType[] _slotPriorityOverride;

        [Header("Debug")]
        [SerializeField] private bool _enableDebugLogs = false;

        [Header("Network Sync")]
        [Tooltip("Aim direction sync rate in seconds (default 0.05 = 20 Hz).")]
        [SerializeField] [Range(0.02f, 0.2f)] private float _aimSyncInterval = 0.05f;
        
        [Header("Targeting")]
        [Tooltip("Optional Bullet Target Config asset. Leave null to disable registry and use physics raycast fallback.")]
        [SerializeField] private NightHunt.GameplaySystems.Core.Configs.BulletTargetConfig _bulletTargetConfig;

        // ── Private service refs ───────────────────────────────────────────────
        private IPlayerStatSystem  _statSystem;
        private IInventorySystem   _inventorySystem;
        private IAttachmentSystem  _attachmentSystem;

        // ── Network-synced state ───────────────────────────────────────────────
        // Server-authoritative slot→instanceID map, replicated to all observers.
        internal readonly SyncDictionary<WeaponSlotType, string> _weapons =
            new SyncDictionary<WeaponSlotType, string>();

        // Server-authoritative active slot.
        internal readonly SyncVar<WeaponSlotType?> _activeSlot =
            new SyncVar<WeaponSlotType?>();

        // ── Local cache ────────────────────────────────────────────────────────
        internal Dictionary<WeaponSlotType, ItemInstance> _weaponCache =
            new Dictionary<WeaponSlotType, ItemInstance>();

        // Built at runtime from InventoryConfig (or _slotPriorityOverride inspector field).
        // Defines auto-equip order and which slots are available for this game-mode.
        internal WeaponSlotType[] _slotPriority;

        // ── Local fire state ───────────────────────────────────────────────────
        internal readonly Dictionary<WeaponSlotType, FireMode> _fireModes =
            new Dictionary<WeaponSlotType, FireMode>();

        internal bool      _isFiring;
        internal bool      _isReloading;
        internal Coroutine _autoFireCoroutine;
        internal Vector3   _aimDirection = Vector3.forward;
        internal Transform _fireOrigin;
        internal WeaponBase _currentWeaponBase;
        
        // Optional systems / cached state used by Fire + NetworkSync
        private WeaponModelController _weaponModelController;
        private float _currentElevationAngle = 0f;
        private Vector3 _lastFireEndpoint = Vector3.zero;

        // ── Events (IWeaponSystem) ─────────────────────────────────────────────
        public event Action<WeaponSlotType, ItemInstance>      OnWeaponEquipped;
        public event Action<WeaponSlotType, ItemInstance>      OnWeaponUnequipped;
        public event Action<WeaponSlotType?, WeaponSlotType?>  OnActiveWeaponChanged;
        public event Action<WeaponSlotType, int>               OnWeaponReloaded;
        public event Action<int, int, int>                     OnAmmoChanged;
        public event Action<bool>                              OnReloadStateChanged;
        public event Action<WeaponSlotType>                    OnWeaponDepleted;
        public event Action<WeaponSlotType, Vector3>           OnShotFired;
        public event Action<WeaponSlotType, Vector3, Vector3>  OnHitscanResult;

        // ── Unity / FishNet lifecycle ──────────────────────────────────────────
        private void Awake() => ResolveReferences();

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _weapons.OnChange   += OnWeaponsChangedCallback;
            _activeSlot.OnChange += OnActiveSlotChangedCallback;

            if (!IsServerInitialized)
                RebuildWeaponCache();
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _weapons.OnChange   -= OnWeaponsChangedCallback;
            _activeSlot.OnChange -= OnActiveSlotChangedCallback;
            _weaponCache.Clear();
        }

        public void Dispose()
        {
            _weapons.OnChange   -= OnWeaponsChangedCallback;
            _activeSlot.OnChange -= OnActiveSlotChangedCallback;
            _weaponCache.Clear();
        }

        // ── IWeaponSystem — simple getters ─────────────────────────────────────
        public ItemInstance GetWeapon(WeaponSlotType slot)
        {
            if (_weaponCache.TryGetValue(slot, out var cached)) return cached;

            // Cache-miss: rebuild from network dict (late-join race condition).
            if (_weapons.TryGetValue(slot, out var id))
            {
                var item = _inventorySystem?.GetItemByInstanceID(id);
                if (item != null) { _weaponCache[slot] = item; return item; }
            }
            return null;
        }

        public Dictionary<WeaponSlotType, ItemInstance> GetAllWeapons() =>
            new Dictionary<WeaponSlotType, ItemInstance>(_weaponCache);

        public WeaponSlotType? GetActiveWeaponSlot()  => _activeSlot.Value;
        public ItemInstance    GetActiveWeapon()       => _activeSlot.Value.HasValue
                                                            ? GetWeapon(_activeSlot.Value.Value)
                                                            : null;
        public bool IsSlotOccupied(WeaponSlotType slot) => _weapons.ContainsKey(slot);

        public bool CanEquipInSlot(string defID, WeaponSlotType slot)
        {
            var def = ItemDatabase.GetDefinition(defID) as WeaponDefinition;
            if (def == null) return false;

            // Slot must be configured in InventoryConfig to be usable.
            var slotCfg = _inventoryConfig?.GetWeaponSlot(slot);
            if (slotCfg == null)
            {
                // No config = allow all (backward-compat when no config is assigned)
                return true;
            }

            // AllowedClasses empty = accept all weapon classes.
            return slotCfg.Value.AcceptsWeaponClass(def.WeaponClass);
        }

        // ── Internal helpers ───────────────────────────────────────────────────
        internal WeaponSlotType FindAvailableSlot()
        {
            foreach (var s in _slotPriority)
                if (!_weapons.ContainsKey(s)) return s;
            return _slotPriority[0];
        }

        internal int FindNextAvailableInventoryIndex() =>
            _inventorySystem?.GetNextFreeInventoryIndex() ?? 0;

        internal void RebuildWeaponCache()
        {
            _weaponCache.Clear();
            foreach (var kvp in _weapons)
            {
                var w = _inventorySystem?.GetItemByInstanceID(kvp.Value);
                if (w != null) _weaponCache[kvp.Key] = w;
            }
        }

        internal bool DebugLogs => _enableDebugLogs;
        internal InventoryConfig InventoryConfig => _inventoryConfig;
        internal IInventorySystem InventorySystem => _inventorySystem;
        internal IPlayerStatSystem StatSystem     => _statSystem;

        // ── Reference resolution ───────────────────────────────────────────────
        private void ResolveReferences()
        {
            _statSystem ??= ComponentResolver.Find<IPlayerStatSystem>(this)
                .UseExisting(_statSystemSource)
                .OnSelf().InChildren().InParent()
                .OrLogWarning("[WeaponSystem] IPlayerStatSystem not found — stat modifiers disabled")
                .Resolve();

            _inventorySystem ??= ComponentResolver.Find<IInventorySystem>(this)
                .UseExisting(_inventorySystemSource)
                .OnSelf().InChildren().InParent()
                .OrLogError("[WeaponSystem] IInventorySystem not found")
                .Resolve();

            _attachmentSystem ??= ComponentResolver.Find<IAttachmentSystem>(this)
                .OnSelf().InChildren().InParent()
                .OrLogWarning("[WeaponSystem] IAttachmentSystem not found — attachment detach on unequip disabled")
                .Resolve();

            if (_inventoryConfig == null)
                Debug.LogError("[WeaponSystem] InventoryConfig not assigned.");
            if (_inventorySystem == null)
                Debug.LogError("[WeaponSystem] IInventorySystem not found.");
            if (_statSystem == null)
                Debug.LogWarning("[WeaponSystem] IPlayerStatSystem not found — stat modifiers disabled.");

            // ── Build slot priority from config (single source of truth) ──────────
            // Inspector override takes precedence only when explicitly set.
            if (_slotPriorityOverride != null && _slotPriorityOverride.Length > 0)
            {
                _slotPriority = _slotPriorityOverride;
            }
            else if (_inventoryConfig?.WeaponConfig != null && _inventoryConfig.WeaponConfig.Length > 0)
            {
                _slotPriority = _inventoryConfig.GetWeaponSlotOrder();
            }
            else
            {
                // Hard fallback: classic 3-slot layout.
                _slotPriority = new[] { WeaponSlotType.Primary, WeaponSlotType.Secondary, WeaponSlotType.Melee };
            }

            // Optional: WeaponModelController used to apply pitch/elevation on local model
            _weaponModelController ??= ComponentResolver.Find<WeaponModelController>(this)
                .OnSelf().InChildren().InParent()
                .OrLogWarning("[WeaponSystem] WeaponModelController not found — remote gun pitch disabled")
                .Resolve();

            // Bullet target config may be left null (physics fallback).
        }

#if UNITY_EDITOR
        [ContextMenu("Validate References")]
        protected override void OnValidate()
        {
            _statSystem = ComponentResolver.Find<IPlayerStatSystem>(this)
                .UseExisting(_statSystemSource)
                .OnSelf().InChildren().InParent().InRootChildren()
                .OrLogWarning("[WeaponSystem] IPlayerStatSystem not found — stat modifiers disabled")
                .Resolve();

            _inventorySystem = ComponentResolver.Find<IInventorySystem>(this)
                .UseExisting(_inventorySystemSource)
                .OnSelf().InChildren().InParent().InRootChildren()
                .OrLogError("[WeaponSystem] IInventorySystem not found")
                .Resolve();

            _weaponModelController = ComponentResolver.Find<WeaponModelController>(this)
                .OnSelf().InChildren().InParent().InRootChildren()
                .OrLogWarning("[WeaponSystem] WeaponModelController not found — remote gun pitch disabled")
                .Resolve();
        }
#endif

        // ── Network callbacks ──────────────────────────────────────────────────
        private void OnWeaponsChangedCallback(SyncDictionaryOperation op,
            WeaponSlotType key, string value, bool asServer)
        {
            if (asServer) return;
            switch (op)
            {
                case SyncDictionaryOperation.Add:
                case SyncDictionaryOperation.Set:
                    var w = _inventorySystem?.GetItemByInstanceID(value);
                    if (w != null) { _weaponCache[key] = w; OnWeaponEquipped?.Invoke(key, w); }
                    break;
                case SyncDictionaryOperation.Remove:
                    if (!_weaponCache.TryGetValue(key, out var removed) && !string.IsNullOrEmpty(value))
                        removed = _inventorySystem?.GetItemByInstanceID(value);
                    _weaponCache.Remove(key);
                    if (removed != null) OnWeaponUnequipped?.Invoke(key, removed);
                    break;
                case SyncDictionaryOperation.Clear:
                    _weaponCache.Clear();
                    break;
            }
        }

        private void OnActiveSlotChangedCallback(WeaponSlotType? oldV, WeaponSlotType? newV, bool asServer)
        {
            if (asServer) return;
            OnActiveWeaponChanged?.Invoke(oldV, newV);
        }

        // ── Debug ──────────────────────────────────────────────────────────────
        [ContextMenu("Log Weapon State")]
        public void LogWeaponState()
        {
            Debug.Log($"=== WeaponSystem [{gameObject.name}] ===");
            Debug.Log($"  Active slot : {_activeSlot.Value?.ToString() ?? "[Holstered]"}");
            Debug.Log($"  Firing      : {_isFiring} | Reloading : {_isReloading}");
            foreach (var kvp in _weapons)
            {
                var w   = _inventorySystem?.GetItemByInstanceID(kvp.Value);
                var def = w != null ? ItemDatabase.GetDefinition(w.DefinitionID) : null;
                Debug.Log($"  {kvp.Key}: {def?.DisplayName ?? kvp.Value}");
            }
        }
    }
}