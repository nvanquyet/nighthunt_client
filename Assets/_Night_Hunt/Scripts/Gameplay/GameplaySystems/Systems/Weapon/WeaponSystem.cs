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

        [Header("Slot Priority")]
        [SerializeField] private WeaponSlotType[] _slotPriority =
        {
            WeaponSlotType.Primary,
            WeaponSlotType.Secondary,
            WeaponSlotType.Melee
        };

        [Header("Debug")]
        [SerializeField] private bool _enableDebugLogs = false;

        [Header("Network Sync")]
        [Tooltip("Aim direction sync rate in seconds (default 0.05 = 20 Hz).")]
        [SerializeField] [Range(0.02f, 0.2f)] private float _aimSyncInterval = 0.05f;

        // ── Private service refs ───────────────────────────────────────────────
        private IPlayerStatSystem  _statSystem;
        private IInventorySystem   _inventorySystem;

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

        // ── Local fire state ───────────────────────────────────────────────────
        internal readonly Dictionary<WeaponSlotType, FireMode> _fireModes =
            new Dictionary<WeaponSlotType, FireMode>();

        internal bool      _isFiring;
        internal bool      _isReloading;
        internal Coroutine _autoFireCoroutine;
        internal Vector3   _aimDirection = Vector3.forward;
        internal Transform _fireOrigin;
        internal WeaponBase _currentWeaponBase;

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
            var def = ItemDatabase.GetDefinition(defID);
            return def is WeaponDefinition;
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

            if (_inventoryConfig == null)
                Debug.LogError("[WeaponSystem] InventoryConfig not assigned.");
            if (_inventorySystem == null)
                Debug.LogError("[WeaponSystem] IInventorySystem not found.");
            if (_statSystem == null)
                Debug.LogWarning("[WeaponSystem] IPlayerStatSystem not found — stat modifiers disabled.");

            if (_slotPriority == null || _slotPriority.Length == 0)
                _slotPriority = new[] { WeaponSlotType.Primary, WeaponSlotType.Secondary, WeaponSlotType.Melee };
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