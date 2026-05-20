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

        [Header("Slot Priority Override")]
        [Tooltip("Override slot auto-equip priority. If empty, defaults to Primary, Secondary, Melee.")]
        [SerializeField] private WeaponSlotType[] _slotPriorityOverride;

        [Header("Debug")]
        [SerializeField] private bool _enableDebugLogs = false;

        [Header("Network Sync")]
        [Tooltip("Aim direction sync rate in seconds (default 0.05 = 20 Hz).")]
        [SerializeField] [Range(0.02f, 0.2f)] private float _aimSyncInterval = 0.05f;
        
        [Header("Targeting")]
        [Tooltip("Optional Bullet Target Config asset. Leave null to disable registry and use physics raycast fallback.")]
        [SerializeField] private NightHunt.GameplaySystems.Core.Configs.BulletTargetConfig _bulletTargetConfig;

        [Tooltip("Maximum pitch angle allowed for bullet/projectile visuals. Keeps top-down shots from flying into the sky while preserving head/body elevation.")]
        [SerializeField, Range(0f, 75f)] private float _maxFireElevationAngle = 35f;

        [Header("Range Overrides")]
        [Tooltip("When enabled, sniper shots may travel past the player's vision radius. Other weapons remain clamped to vision.")]
        [SerializeField] private bool _sniperCanExceedVisionRange = true;

        [Tooltip("Sniper max range = min(weapon prefab maxRange, VisionRange x this multiplier). 2.5 means bullets can travel 2.5x beyond vision.")]
        [SerializeField, Min(1f)] private float _sniperVisionRangeMultiplier = 2.5f;

        [Header("Owner Camera Shake")]
        [SerializeField] private bool _enableOwnerCameraShake = true;
        [SerializeField, Range(1f, 90f)] private float _cameraShakeFrequency = 34f;
        [SerializeField, Min(0f)] private float _pistolCameraShakeAmplitude = 0.025f;
        [SerializeField, Min(0f)] private float _smgCameraShakeAmplitude = 0.022f;
        [SerializeField, Min(0f)] private float _rifleCameraShakeAmplitude = 0.04f;
        [SerializeField, Min(0f)] private float _machineGunCameraShakeAmplitude = 0.032f;
        [SerializeField, Min(0f)] private float _shotgunCameraShakeAmplitude = 0.07f;
        [SerializeField, Min(0f)] private float _sniperCameraShakeAmplitude = 0.11f;
        [SerializeField, Min(0f)] private float _launcherCameraShakeAmplitude = 0.13f;
        [SerializeField, Min(0.01f)] private float _cameraShakeShortDuration = 0.075f;
        [SerializeField, Min(0.01f)] private float _cameraShakeHeavyDuration = 0.14f;

        [Header("Server Anti-Cheat")]
        [Tooltip("Maximum accepted aim direction turn rate between authoritative shots, in degrees per second.")]
        [SerializeField, Range(90f, 1440f)] private float _maxServerAimDeltaDegPerSecond = 720f;

        [Tooltip("Extra per-shot grace angle to avoid rejecting normal low-FPS or latency jitter.")]
        [SerializeField, Range(0f, 45f)] private float _serverAimDeltaGraceDegrees = 12f;

        [Header("Server Hitscan Timing")]
        [Tooltip("When enabled, server-authoritative hitscan damage lands after distance / ProjectileSpeed so damage timing matches the visible bullet trail.")]
        [SerializeField] private bool _delayHitscanDamageByProjectileSpeed = true;

        [Tooltip("Safety cap for hitscan damage delay in seconds. 0 = no cap.")]
        [SerializeField, Min(0f)] private float _maxHitscanDamageDelay = 5f;

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
        internal Coroutine _reloadCoroutine;
        internal Vector3   _aimDirection = Vector3.forward;
        internal Transform _fireOrigin;
        internal WeaponBase _currentWeaponBase;

        private Transform _fireOriginParent;
        private Vector3 _fireOriginLocalPosition;
        private Quaternion _fireOriginLocalRotation;
        private Vector3 _fireOriginLocalScale = Vector3.one;
        private bool _hasFireOriginSnapshot;
        
        // Optional systems / cached state used by Fire + NetworkSync
        private WeaponModelController _weaponModelController;
        private float _currentElevationAngle = 0f;
        private Vector3 _lastFireEndpoint = Vector3.zero;
        private bool _lastFireHitHittable = false;
        private Vector3 _lastFireHitNormal = Vector3.up;
        private Vector3 _lastServerShotDirection = Vector3.zero;
        private float _lastServerShotTime = -1f;

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
            _lastServerShotDirection = Vector3.zero;
            _lastServerShotTime = -1f;
        }

        public void Dispose()
        {
            _weapons.OnChange   -= OnWeaponsChangedCallback;
            _activeSlot.OnChange -= OnActiveSlotChangedCallback;
            _weaponCache.Clear();
            _lastServerShotDirection = Vector3.zero;
            _lastServerShotTime = -1f;
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
        public bool IsFireInputHeld => _isFiring;

        public bool CanEquipInSlot(string defID, WeaponSlotType slot)
        {
            var def = ItemDatabase.GetDefinition(defID) as WeaponDefinition;
            if (def == null) return false;

            return CanEquipDefinitionInSlot(def, slot);
        }

        internal bool CanEquipDefinitionInSlot(WeaponDefinition def, WeaponSlotType slot)
        {
            if (def == null) return false;

            // WeaponSlotType constraints are now hardcoded or ignored (accept all).
            switch (slot)
            {
                case WeaponSlotType.Primary:
                case WeaponSlotType.Secondary:
                    return def.WeaponClass != WeaponClass.Melee;
                case WeaponSlotType.Melee:
                    return def.WeaponClass == WeaponClass.Melee;
                default:
                    return true;
            }
        }

        // ── Internal helpers ───────────────────────────────────────────────────
        internal WeaponSlotType FindAvailableSlot()
        {
            foreach (var s in _slotPriority)
                if (!_weapons.ContainsKey(s)) return s;
            return _slotPriority[0];
        }

        internal WeaponSlotType FindAvailableSlot(WeaponDefinition def)
        {
            if (def == null) return FindAvailableSlot();

            foreach (var s in _slotPriority)
                if (!_weapons.ContainsKey(s) && CanEquipDefinitionInSlot(def, s))
                    return s;

            foreach (var s in _slotPriority)
                if (CanEquipDefinitionInSlot(def, s))
                    return s;

            return def.WeaponClass == WeaponClass.Melee
                ? WeaponSlotType.Melee
                : WeaponSlotType.Primary;
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
                    _fireModes.Remove(key);
                    var w = _inventorySystem?.GetItemByInstanceID(value);
                    if (w != null) { _weaponCache[key] = w; OnWeaponEquipped?.Invoke(key, w); }
                    break;
                case SyncDictionaryOperation.Remove:
                    if (!_weaponCache.TryGetValue(key, out var removed) && !string.IsNullOrEmpty(value))
                        removed = _inventorySystem?.GetItemByInstanceID(value);
                    _weaponCache.Remove(key);
                    _fireModes.Remove(key);
                    if (removed != null) OnWeaponUnequipped?.Invoke(key, removed);
                    break;
                case SyncDictionaryOperation.Clear:
                    _weaponCache.Clear();
                    break;
            }
        }

        private void OnActiveSlotChangedCallback(WeaponSlotType? oldV, WeaponSlotType? newV, bool asServer)
        {
            if (oldV != newV)
                CancelReload($"activeSlotChanged {oldV?.ToString() ?? "none"}->{newV?.ToString() ?? "none"}");

            if (newV.HasValue)
                _fireModes.Remove(newV.Value);
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
