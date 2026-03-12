using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;
using System.Collections.Generic;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.StatSystem.Core.Interfaces;
using NightHunt.StatSystem.Core.Types;
using NightHunt.StatSystem.Core.Data;
using NightHunt.Gameplay.Character.Combat;
using NightHunt.Gameplay.Character.Combat.Weapons;
using NightHunt.Data;

namespace NightHunt.GameplaySystems.Weapon
{
    /// <summary>
    /// Manages weapon slot assignment, equip/unequip, firing, and reload for a networked player.
    /// Slot priorities are config-driven; all slot mutations are server-authoritative via SyncDictionary.
    /// </summary>
    public class WeaponSystem : NetworkBehaviour, IWeaponSystem, IDisposable
    {
        #region Serialized Fields
        
        [Header("Configuration")]
        [SerializeField] private InventoryConfig _inventoryConfig;
        
        [Header("References")]
        [SerializeField] private MonoBehaviour _statSystemComponent;
        [SerializeField] private MonoBehaviour _inventorySystemComponent;
        private IPlayerStatSystem _statSystem;
        private IInventorySystem _inventorySystem;
        
        [Header("Weapon Slot Configuration")]
        [Tooltip("Weapon slot priority order for auto-equip")]
        [SerializeField] private WeaponSlotType[] _slotPriority = new WeaponSlotType[]
        {
            WeaponSlotType.Primary,
            WeaponSlotType.Secondary,
            WeaponSlotType.Melee
        };
        
        [Header("Debug")]
        [SerializeField] private bool _enableDebugLogs = false;
        
        #endregion
        
        #region Network Synced Data
        
        private readonly SyncDictionary<WeaponSlotType, string> _weapons = new SyncDictionary<WeaponSlotType, string>();
        private readonly SyncVar<WeaponSlotType?> _activeSlot = new SyncVar<WeaponSlotType?>();
        
        #endregion
        
        #region Local Cache
        
        private Dictionary<WeaponSlotType, ItemInstance> _weaponCache = new Dictionary<WeaponSlotType, ItemInstance>();
        
        #endregion
        
        #region Events
        
        public event Action<WeaponSlotType, ItemInstance> OnWeaponEquipped;
        public event Action<WeaponSlotType, ItemInstance> OnWeaponUnequipped;
        public event Action<WeaponSlotType?, WeaponSlotType?> OnActiveWeaponChanged;
        public event Action<WeaponSlotType, int> OnWeaponReloaded;

        // ── Combat events (HUD + Camera) ──────────────────────────────────────
        /// <inheritdoc cref="IWeaponSystem.OnAmmoChanged"/>
        public event Action<int, int, int> OnAmmoChanged;
        /// <inheritdoc cref="IWeaponSystem.OnReloadStateChanged"/>
        public event Action<bool> OnReloadStateChanged;
        /// <inheritdoc cref="IWeaponSystem.OnWeaponDepleted"/>
        public event Action<WeaponSlotType> OnWeaponDepleted;
        /// <summary>Raised on each successful shot; VFX controllers subscribe for muzzle/trail effects.</summary>
        public event Action<WeaponSlotType, Vector3> OnShotFired;

        /// <inheritdoc cref="IWeaponSystem.OnHitscanResult"/>
        public event Action<WeaponSlotType, Vector3, Vector3> OnHitscanResult;
        
        #endregion

        #region Local Fire State (owner-client + server)

        // Fire mode per slot — persisted to PlayerPrefs key "firemode_{slotIndex}"
        private readonly Dictionary<WeaponSlotType, FireMode> _fireModes =
            new Dictionary<WeaponSlotType, FireMode>();

        private bool _isFiring;
        private bool _isReloading;
        private Coroutine _autoFireCoroutine;
        private Vector3 _aimDirection = Vector3.forward; // updated by SetAimDirection()

        /// <summary>Muzzle-tip Transform — set by WeaponModelController after each weapon swap.</summary>
        private Transform _fireOrigin;

        /// <summary>
        /// WeaponBase on the active weapon model — set by WeaponModelController after each weapon swap.
        /// Null on server (no weapon models) and before any weapon is equipped.
        /// </summary>
        private NightHunt.Gameplay.Character.Combat.Weapons.WeaponBase _currentWeaponBase;

        #endregion
        
        #region NetworkBehaviour Lifecycle
        
        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            
            _weapons.OnChange += OnWeaponsChanged;
            _activeSlot.OnChange += OnActiveSlotChanged;
            
            if (!IsServerInitialized)
                RebuildWeaponCache();
        }
        
        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            
            _weapons.OnChange -= OnWeaponsChanged;
            _activeSlot.OnChange -= OnActiveSlotChanged;
            
            _weaponCache.Clear();
        }
        
        #endregion
        
        #region IDisposable Implementation
        
        public void Dispose()
        {
            // Unsubscribe from network events
            _weapons.OnChange -= OnWeaponsChanged;
            _activeSlot.OnChange -= OnActiveSlotChanged;
            
            // Clear cache
            _weaponCache.Clear();
        }
        
        #endregion

        #region IWeaponSystem — Combat (Fire / Reload / FireMode)

        /// <summary>Start firing. Spawns auto-fire coroutine if FireMode.Auto.</summary>
        public void StartFire()
        {
            if (_isFiring) return;
            _isFiring = true;

            var mode = GetCurrentFireMode();
            if (mode == FireMode.Auto)
            {
                if (_autoFireCoroutine != null) StopCoroutine(_autoFireCoroutine);
                _autoFireCoroutine = StartCoroutine(AutoFireCoroutine());
            }
            else
            {
                // Single: fire once immediately
                TryFireOnce();
            }
        }

        /// <summary>Release fire input — stop auto-fire.</summary>
        public void StopFire()
        {
            _isFiring = false;
            if (_autoFireCoroutine != null)
            {
                StopCoroutine(_autoFireCoroutine);
                _autoFireCoroutine = null;
            }
        }

        /// <summary>Request reload for the currently active weapon.</summary>
        public void RequestReload()
        {
            var slot = _activeSlot.Value;
            Debug.Log($"[WeaponSystem][Reload] RequestReload: activeSlot={slot?.ToString() ?? "NULL"}, _isReloading={_isReloading}, IsOwner={IsOwner}");
            if (slot == null) { Debug.LogWarning("[WeaponSystem][Reload] BLOCKED: activeSlot is null"); return; }
            if (_isReloading)  { Debug.LogWarning("[WeaponSystem][Reload] BLOCKED: already reloading"); return; }
            if (!_weaponCache.TryGetValue(slot.Value, out var inst))
            {
                Debug.LogWarning($"[WeaponSystem][Reload] BLOCKED: no ItemInstance in cache for slot {slot.Value}");
                return;
            }
            Debug.Log($"[WeaponSystem][Reload] Starting coroutine for slot={slot.Value}, instID={inst.InstanceID}, defID={inst.DefinitionID}");
            StartCoroutine(ReloadCoroutine(slot.Value, inst));
        }

        /// <summary>Toggle or set fire mode for the active weapon.</summary>
        public void SetFireMode(FireMode mode)
        {
            var slot = _activeSlot.Value;
            if (slot == null) return;
            _fireModes[slot.Value] = mode;
            UnityEngine.PlayerPrefs.SetInt($"firemode_{(int)slot.Value}", (int)mode);
        }

        /// <summary>Get current fire mode of active weapon (reads PlayerPrefs, falls back to WeaponBase default).</summary>
        public FireMode GetCurrentFireMode()
        {
            var slot = _activeSlot.Value;
            if (slot == null) return FireMode.Auto;

            if (_fireModes.TryGetValue(slot.Value, out var mode)) return mode;

            // Saved preference takes priority.
            int saved = UnityEngine.PlayerPrefs.GetInt($"firemode_{(int)slot.Value}", -1);
            if (saved >= 0) { _fireModes[slot.Value] = (FireMode)saved; return (FireMode)saved; }

            // Fall back to the prefab-side default set in the Inspector.
            var fallback = _currentWeaponBase?.DefaultFireMode ?? FireMode.Auto;
            _fireModes[slot.Value] = fallback;
            return fallback;
        }

        /// <summary>
        /// Supply the current world-space aim direction each frame / on input change.
        /// WeaponSystem stores it so <see cref="OnShotFired"/> can pass it to VFX controllers.
        /// CombatInputHandler calls this via <c>_weaponSystem.SetAimDirection(GetAimDirection())</c>.
        /// </summary>
        public void SetAimDirection(Vector3 worldDirection)
        {
            _aimDirection = worldDirection.sqrMagnitude > 0.001f ? worldDirection.normalized : Vector3.forward;
        }

        /// <summary>Returns the last aim direction set by the input handler.</summary>
        public Vector3 GetAimDirection() => _aimDirection;

        /// <inheritdoc cref="IWeaponSystem.SetFireOrigin"/>
        public void SetFireOrigin(Transform muzzlePoint)
        {
            _fireOrigin = muzzlePoint;
        }

        /// <inheritdoc cref="IWeaponSystem.SetCurrentWeaponBase"/>
        public void SetCurrentWeaponBase(NightHunt.Gameplay.Character.Combat.Weapons.WeaponBase weaponBase)
        {
            // Unsubscribe from previous weapon's fire result event.
            if (_currentWeaponBase != null)
                _currentWeaponBase.OnFireResult -= HandleWeaponFireResult;

            _currentWeaponBase = weaponBase;

            if (_currentWeaponBase != null)
                _currentWeaponBase.OnFireResult += HandleWeaponFireResult;
        }

        private void HandleWeaponFireResult(Vector3 origin, Vector3 endpoint)
        {
            var slot = _activeSlot.Value;
            if (slot != null)
                OnHitscanResult?.Invoke(slot.Value, origin, endpoint);
        }

        // ── Private fire helpers ─────────────────────────────────────────────

        private System.Collections.IEnumerator AutoFireCoroutine()
        {
            while (_isFiring)
            {
                TryFireOnce();

                // Compute delay from ComputedStats.FireRate (rounds/min)
                float delay = GetCurrentFireDelay();
                yield return new WaitForSeconds(delay);
            }
            _autoFireCoroutine = null;
        }

        private float GetCurrentFireDelay()
        {
            var slot = _activeSlot.Value;
            if (slot == null) return 0.1f;
            if (_weaponCache.TryGetValue(slot.Value, out var inst))
            {
                float rpm = inst.GetComputedStat(ItemStatType.FireRate);
                if (rpm > 0f) return 60f / rpm;
            }
            return 0.1f;
        }

        private void TryFireOnce()
        {
            var slot = _activeSlot.Value;
            if (slot == null || _isReloading) return;
            if (!_weaponCache.TryGetValue(slot.Value, out var inst)) return;

            int currentMag = (int)inst.GetCurrentValue(ItemStatType.MagazineSize);
            if (currentMag <= 0)
            {
                float totalAmmo = inst.GetCurrentValue(ItemStatType.MaxAmmo);
                if (totalAmmo > 0)
                    StartCoroutine(ReloadCoroutine(slot.Value, inst));
                else
                    OnWeaponDepleted?.Invoke(slot.Value);
                return;
            }

            inst.AdjustCurrentValue(ItemStatType.MagazineSize, -1f);
            float mag = inst.GetComputedStat(ItemStatType.MagazineSize);
            OnAmmoChanged?.Invoke(
                (int)inst.GetCurrentValue(ItemStatType.MagazineSize),
                (int)inst.GetCurrentValue(ItemStatType.MaxAmmo),
                (int)mag);

            OnShotFired?.Invoke(slot.Value, _aimDirection);

            // Delegate ballistic logic to the weapon model prefab's WeaponBase component.
            if (_currentWeaponBase != null)
            {
                Vector3 origin = _fireOrigin != null ? _fireOrigin.position : transform.position;
                var config = BuildWeaponConfigData(inst);
                _currentWeaponBase.Fire(origin, _aimDirection, config, (int)ObjectId);

                // Broadcast bullet visual to remote clients (both hitscan trail and projectile).
                if (IsOwner && _currentWeaponBase.ProjectilePrefab != null)
                    BroadcastProjectileServerRpc(origin, _aimDirection, config);
            }
        }

        private WeaponConfigData BuildWeaponConfigData(ItemInstance inst)
        {
            float spreadBase = inst.GetComputedStat(ItemStatType.SpreadBase);
            Debug.Log($"[WeaponSystem][Spread] BuildWeaponConfigData: SpreadBase={spreadBase:F3}");
            return new WeaponConfigData
            {
                WeaponId        = inst.DefinitionID,
                DisplayName     = inst.DefinitionID,
                BallisticType   = _currentWeaponBase?.BallisticType.ToString() ?? "Hitscan",
                DamageBody      = (int)inst.GetComputedStat(ItemStatType.Damage),
                DamageHeadMul   = _currentWeaponBase?.DamageHeadMultiplier ?? 2f,
                FireRate        = inst.GetComputedStat(ItemStatType.FireRate),
                ReloadTime      = inst.GetComputedStat(ItemStatType.ReloadSpeed),
                MagazineSize    = (int)inst.GetComputedStat(ItemStatType.MagazineSize),
                ReserveAmmo     = (int)inst.GetCurrentValue(ItemStatType.MaxAmmo),
                ProjectileSpeed = _currentWeaponBase?.ProjectileSpeed ?? 50f,
                MaxRange        = _currentWeaponBase?.MaxRange ?? 150f,
                GravityScale    = _currentWeaponBase?.GravityScale ?? 0f,
                SpreadBase      = spreadBase,
            };
        }

        // ── Network: projectile broadcast ─────────────────────────────────────

        [ServerRpc(RequireOwnership = true)]
        private void BroadcastProjectileServerRpc(Vector3 origin, Vector3 direction, WeaponConfigData config)
        {
            // Server validates and immediately broadcasts to all observers.
            ShowProjectileOnClientsRpc(origin, direction, config);
        }

        [ObserversRpc]
        private void ShowProjectileOnClientsRpc(Vector3 origin, Vector3 direction, WeaponConfigData config)
        {
            // Owner already spawned the authoritative copy locally — skip.
            if (IsOwner) return;

            var pool = NightHunt.Gameplay.Character.Combat.Weapons.ProjectilePool.Instance;
            if (pool == null || _currentWeaponBase == null
                || _currentWeaponBase.ProjectilePrefab == null) return;

            var proj = pool.Get(_currentWeaponBase.ProjectilePrefab, origin, Quaternion.LookRotation(direction));
            // Visual-only copy on remote clients. useHitscan=true for hitscan (no collision damage), false for projectile.
            bool isHitscan = config.BallisticType == "Hitscan";
            proj?.Initialize(config, direction, useHitscan: isHitscan);
        }

        private System.Collections.IEnumerator ReloadCoroutine(WeaponSlotType slot, ItemInstance inst)
        {
            if (_isReloading) { Debug.LogWarning("[WeaponSystem][Reload] Coroutine entry blocked: _isReloading already true"); yield break; }

            // Lazy-init reserve ammo in case equip-time init was skipped (e.g. stat missing at that moment).
            if (inst.GetCurrentValue(ItemStatType.MaxAmmo) == 0f)
            {
                float computedMax = inst.GetComputedStat(ItemStatType.MaxAmmo);
                if (computedMax > 0f)
                {
                    inst.SetCurrentValue(ItemStatType.MaxAmmo, computedMax);
                    Debug.Log($"[WeaponSystem][Reload] Lazy-init MaxAmmo={computedMax:F0} for '{inst.DefinitionID}'");
                }
                else
                {
                    Debug.LogError($"[WeaponSystem][Reload] MaxAmmo stat MISSING in ItemStatConfig for '{inst.DefinitionID}'. " +
                                   "Open the weapon's WeaponStatConfig ScriptableObject and add ItemStatType.MaxAmmo " +
                                   "(DefaultValue=300 for rifles). Reload will add 0 ammo until fixed.");
                }
            }

            float magCap     = inst.GetComputedStat(ItemStatType.MagazineSize);
            float reloadTime = inst.GetComputedStat(ItemStatType.ReloadSpeed);
            if (reloadTime <= 0f) reloadTime = 2.5f;

            int currentMag = (int)inst.GetCurrentValue(ItemStatType.MagazineSize);
            int needed     = (int)magCap - currentMag;

            Debug.Log($"[WeaponSystem][Reload] Coroutine: slot={slot}, magCap={magCap}, currentMag={currentMag}, needed={needed}, reloadTime={reloadTime:F2}s, reserve={(int)inst.GetCurrentValue(ItemStatType.MaxAmmo)}");

            if (needed <= 0)
            {
                Debug.LogWarning($"[WeaponSystem][Reload] BLOCKED: magazine already full (currentMag={currentMag}, cap={magCap})");
                yield break;
            }

            _isReloading = true;
            OnReloadStateChanged?.Invoke(true);
            Debug.Log($"[WeaponSystem][Reload] Started — waiting {reloadTime:F2}s");

            yield return new WaitForSeconds(reloadTime);

            int reserve = (int)inst.GetCurrentValue(ItemStatType.MaxAmmo);
            int actual  = Mathf.Min(needed, reserve);
            Debug.Log($"[WeaponSystem][Reload] Complete: reserve={reserve}, actual_added={actual}");

            inst.AdjustCurrentValue(ItemStatType.MagazineSize, actual);
            inst.AdjustCurrentValue(ItemStatType.MaxAmmo, -actual);

            int newMag = (int)inst.GetCurrentValue(ItemStatType.MagazineSize);
            OnWeaponReloaded?.Invoke(slot, newMag);
            OnAmmoChanged?.Invoke(
                newMag,
                (int)inst.GetCurrentValue(ItemStatType.MaxAmmo),
                (int)magCap);

            _isReloading = false;
            OnReloadStateChanged?.Invoke(false);
            Debug.Log($"[WeaponSystem][Reload] Done: newMag={newMag}/{magCap}");
        }

        #endregion
        
        #region Initialization
        
        private void Awake()
        {
            ValidateReferences();
        }
        
        private void ValidateReferences()
        {
            // Get components and cast to interfaces
            if (_statSystemComponent != null)
                _statSystem = _statSystemComponent as IPlayerStatSystem;
            
            if (_inventorySystemComponent != null)
                _inventorySystem = _inventorySystemComponent as IInventorySystem;
            
#if UNITY_EDITOR
            // Auto-find if not assigned
            if (_statSystem == null)
            {
                var statSys = GetComponent<IPlayerStatSystem>();
                if (statSys != null)
                {
                    _statSystemComponent = statSys as MonoBehaviour;
                    _statSystem = statSys;
                }
            }
            
            if (_inventorySystem == null)
            {
                var invSys = GetComponent<IInventorySystem>();
                if (invSys != null)
                {
                    _inventorySystemComponent = invSys as MonoBehaviour;
                    _inventorySystem = invSys;
                }
            }
#endif
            
            if (_inventoryConfig == null)
                Debug.LogError("[WeaponSystem] InventoryConfig is null!");
            
            if (_statSystem == null)
                Debug.LogWarning("[WeaponSystem] IPlayerStatSystem is null - stat modifiers will not work!");
            
            if (_inventorySystem == null)
                Debug.LogError("[WeaponSystem] IInventorySystem is null!");
            
            // Validate slot priority configuration
            if (_slotPriority == null || _slotPriority.Length == 0)
            {
                Debug.LogWarning("[WeaponSystem] No slot priority configured, using defaults");
                _slotPriority = new WeaponSlotType[]
                {
                    WeaponSlotType.Primary,
                    WeaponSlotType.Secondary,
                    WeaponSlotType.Melee
                };
            }
        }
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_statSystemComponent != null)
                _statSystem = _statSystemComponent as IPlayerStatSystem;
            
            if (_inventorySystemComponent != null)
                _inventorySystem = _inventorySystemComponent as IInventorySystem;
            
            if (_statSystem == null)
            {
                var statSys = GetComponent<IPlayerStatSystem>();
                if (statSys != null)
                {
                    _statSystemComponent = statSys as MonoBehaviour;
                    _statSystem = statSys;
                }
            }
            
            if (_inventorySystem == null)
            {
                var invSys = GetComponent<IInventorySystem>();
                if (invSys != null)
                {
                    _inventorySystemComponent = invSys as MonoBehaviour;
                    _inventorySystem = invSys;
                }
            }
        }
#endif
        
        #endregion
        
        #region IWeaponSystem - Getters
        
        public ItemInstance GetWeapon(WeaponSlotType slotType)
        {
            if (_weaponCache.TryGetValue(slotType, out var weapon))
                return weapon;

            // Cache-miss fallback (e.g. first tick on a pure client before OnWeaponsChanged
            // fires, or host-mode before cache was seeded).  Populate cache on hit.
            if (_weapons.TryGetValue(slotType, out var instanceID))
            {
                var item = _inventorySystem?.GetItemByInstanceID(instanceID);
                if (item != null)
                {
                    _weaponCache[slotType] = item;
                    Debug.LogWarning($"[WeaponSystem] GetWeapon: cache miss for {slotType} — rebuilt from _weapons.");
                    return item;
                }
            }
            return null;
        }
        
        public Dictionary<WeaponSlotType, ItemInstance> GetAllWeapons()
        {
            return new Dictionary<WeaponSlotType, ItemInstance>(_weaponCache);
        }
        
        public WeaponSlotType? GetActiveWeaponSlot()
        {
            return _activeSlot.Value;
        }
        
        public ItemInstance GetActiveWeapon()
        {
            var activeSlot = _activeSlot.Value;
            if (activeSlot == null)
                return null;
            
            return GetWeapon(activeSlot.Value);
        }
        
        public bool IsSlotOccupied(WeaponSlotType slotType)
        {
            return _weapons.ContainsKey(slotType);
        }
        
        public bool CanEquipInSlot(string itemDefinitionID, WeaponSlotType slotType)
        {
            var itemDef = ItemDatabase.GetDefinition(itemDefinitionID);
            if (itemDef == null || !(itemDef is WeaponDefinition))
                return false;
            
            // All weapon slots accept any weapon.
            return true;
        }
        
        #endregion
        
        #region IWeaponSystem - Equip/Unequip
        
        public void EquipWeapon(string instanceID)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[WeaponSystem] EquipWeapon: server-only!");
                return;
            }
            
            EquipWeaponServer(instanceID);
        }

        /// <inheritdoc/>
        public void EquipWeaponToSlot(string instanceID, WeaponSlotType targetSlot)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[WeaponSystem] EquipWeaponToSlot: server-only!");
                return;
            }

            EquipWeaponToSlotServer(instanceID, targetSlot);
        }

        [Server]
        private void EquipWeaponToSlotServer(string instanceID, WeaponSlotType targetSlot)
        {
            var item = _inventorySystem.GetItemByInstanceID(instanceID);
            if (item == null)
            {
                Debug.LogWarning($"[WeaponSystem] EquipWeaponToSlot: item not found: {instanceID}");
                return;
            }

            var itemDef = ItemDatabase.GetDefinition(item.DefinitionID);
            if (!(itemDef is WeaponDefinition weaponDef))
            {
                Debug.LogWarning($"[WeaponSystem] EquipWeaponToSlot: not a weapon: {item.DefinitionID}");
                return;
            }

            // If target slot is already occupied, unequip existing weapon first
            if (_weapons.ContainsKey(targetSlot))
                UnequipWeaponServer(targetSlot);

            _weapons[targetSlot] = instanceID;
            _weaponCache[targetSlot] = item;
            item.InventoryIndex = -1; // Mark as equipped

            // Initialize reserve ammo on first equip (CurrentResource=0 means never set).
            float currentReserve = item.GetCurrentValue(ItemStatType.MaxAmmo);
            float maxReserve     = item.GetComputedStat(ItemStatType.MaxAmmo);
            if (currentReserve == 0f && maxReserve > 0f)
            {
                item.SetCurrentValue(ItemStatType.MaxAmmo, maxReserve);
                Debug.Log($"[WeaponSystem] EquipWeaponToSlot: initialised MaxAmmo reserve={maxReserve} for {weaponDef.DisplayName}");
            }
            else if (maxReserve == 0f)
            {
                Debug.LogError($"[WeaponSystem] MaxAmmo stat MISSING in ItemStatConfig for '{weaponDef.DisplayName}' (defID={item.DefinitionID}). " +
                               "Run ItemStatConfigSetup.SetupRifle() on its WeaponStatConfig SO, or add " +
                               "ItemStatType.MaxAmmo (DefaultValue=300) manually. Reload will show reserve=0.");
            }

            _inventorySystem.SyncItemState(instanceID);

            // Auto-select this weapon if the player isn't holding anything yet.
            if (_activeSlot.Value == null)
                _activeSlot.Value = targetSlot;

            if (_enableDebugLogs)
                Debug.Log($"[WeaponSystem] EquipWeaponToSlot: {weaponDef.DisplayName} → {targetSlot} (explicit)");
        }
        
        [Server]
        private void EquipWeaponServer(string instanceID)
        {
            var item = _inventorySystem.GetItemByInstanceID(instanceID);
            if (item == null)
            {
                Debug.LogWarning($"[WeaponSystem] Item not found: {instanceID}");
                return;
            }
            
            var itemDef = ItemDatabase.GetDefinition(item.DefinitionID);
            if (!(itemDef is WeaponDefinition weaponDef))
            {
                Debug.LogWarning($"[WeaponSystem] Not a weapon: {item.DefinitionID}");
                return;
            }
            
            // Use configured priority; if all slots are occupied, the first slot is returned (swap).
            WeaponSlotType targetSlot = FindAvailableSlot();
            
            // If no available slot, swap with target slot
            if (_weapons.ContainsKey(targetSlot))
                UnequipWeaponServer(targetSlot);
            
            // Equip weapon
            _weapons[targetSlot] = instanceID;
            _weaponCache[targetSlot] = item;
            item.InventoryIndex = -1; // Mark as equipped

            // Initialize reserve ammo on first equip (CurrentResource=0 means never set).
            float currentReserve = item.GetCurrentValue(ItemStatType.MaxAmmo);
            float maxReserve     = item.GetComputedStat(ItemStatType.MaxAmmo);
            if (currentReserve == 0f && maxReserve > 0f)
            {
                item.SetCurrentValue(ItemStatType.MaxAmmo, maxReserve);
                if (_enableDebugLogs)
                    Debug.Log($"[WeaponSystem] Equipped: initialised MaxAmmo reserve={maxReserve} for {weaponDef.DisplayName}");
            }
            else if (maxReserve == 0f)
            {
                Debug.LogError($"[WeaponSystem] MaxAmmo stat MISSING in ItemStatConfig for '{weaponDef.DisplayName}' (defID={item.DefinitionID}). " +
                               "Run ItemStatConfigSetup.SetupRifle() on its WeaponStatConfig SO, or add " +
                               "ItemStatType.MaxAmmo (DefaultValue=300) manually. Reload will show reserve=0.");
            }

            _inventorySystem.SyncItemState(instanceID);

            // Auto-select this weapon if the player isn't holding anything yet.
            if (_activeSlot.Value == null)
                _activeSlot.Value = targetSlot;

            if (_enableDebugLogs)
                Debug.Log($"[WeaponSystem] Equipped {weaponDef.DisplayName} → {targetSlot}");
        }
        
        public void UnequipWeapon(WeaponSlotType slotType)
        {
            if (IsServerInitialized)
            {
                UnequipWeaponServer(slotType);
                return;
            }
            if (IsOwner)
                UnequipWeaponServerRpc(slotType);
        }

        [ServerRpc(RequireOwnership = true)]
        private void UnequipWeaponServerRpc(WeaponSlotType slotType)
            => UnequipWeaponServer(slotType);

        [Server]
        private void UnequipWeaponServer(WeaponSlotType slotType)
        {
            if (!_weapons.TryGetValue(slotType, out var instanceID))
            {
                Debug.LogWarning($"[WeaponSystem] No weapon in slot: {slotType}");
                return;
            }
            
            var item = _inventorySystem.GetItemByInstanceID(instanceID);
            if (item == null)
            {
                Debug.LogWarning($"[WeaponSystem] Weapon not found: {instanceID}");
                _weapons.Remove(slotType);
                return;
            }
            
            // If this weapon is active, holster it first
            if (_activeSlot.Value == slotType)
                HolsterWeaponServer();

            // Detach attachments before unequipping if config requires it.
            if (_inventoryConfig != null && _inventoryConfig.DetachAttachmentsOnUnequip)
            {
                var attachmentSystem = GetComponent<NightHunt.GameplaySystems.Core.Interfaces.IAttachmentSystem>()
                                    ?? GetComponentInParent<NightHunt.GameplaySystems.Core.Interfaces.IAttachmentSystem>();
                if (attachmentSystem != null)
                    attachmentSystem.DetachAllFromItem(instanceID);
                else if (_enableDebugLogs)
                    Debug.LogWarning($"[WeaponSystem] DetachAttachmentsOnUnequip=true but IAttachmentSystem not found on {gameObject.name}");
            }

            // Remove from weapon slots
            _weapons.Remove(slotType);
            _weaponCache.Remove(slotType);

            // Return to inventory (mark with a valid index so UIDomainBridge can show it)
            item.InventoryIndex = FindNextAvailableInventoryIndex();
            _inventorySystem.SyncItemState(instanceID);

            if (_enableDebugLogs)
            {
                var def = ItemDatabase.GetDefinition(item.DefinitionID);
                Debug.Log($"[WeaponSystem] Unequipped {def?.DisplayName} from {slotType}.");
            }
        }
        
        public void SwapWeapons(WeaponSlotType slot1, WeaponSlotType slot2)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[WeaponSystem] SwapWeapons: server-only!");
                return;
            }
            
            SwapWeaponsServer(slot1, slot2);
        }
        
        [Server]
        private void SwapWeaponsServer(WeaponSlotType slot1, WeaponSlotType slot2)
        {
            bool hasWeapon1 = _weapons.TryGetValue(slot1, out var instanceID1);
            bool hasWeapon2 = _weapons.TryGetValue(slot2, out var instanceID2);
            
            if (!hasWeapon1 && !hasWeapon2)
            {
                Debug.LogWarning("[WeaponSystem] Both slots empty");
                return;
            }
            
            // Swap
            if (hasWeapon1 && hasWeapon2)
            {
                _weapons[slot1] = instanceID2;
                _weapons[slot2] = instanceID1;
            }
            else if (hasWeapon1)
            {
                _weapons.Remove(slot1);
                _weapons[slot2] = instanceID1;
            }
            else // hasWeapon2
            {
                _weapons.Remove(slot2);
                _weapons[slot1] = instanceID2;
            }
            
            if (_enableDebugLogs)
                Debug.Log($"[WeaponSystem] Swapped {slot1} ↔ {slot2}");
        }
        
        #endregion
        
        #region IWeaponSystem - Selection
        
        public void SelectWeapon(WeaponSlotType slotType)
        {
            if (IsServerInitialized)
            {
                SelectWeaponServer(slotType);
                return;
            }
            // Owner client → ask server to select
            if (IsOwner)
                SelectWeaponServerRpc(slotType);
        }

        [ServerRpc(RequireOwnership = true)]
        private void SelectWeaponServerRpc(WeaponSlotType slotType)
            => SelectWeaponServer(slotType);
        
        [Server]
        private void SelectWeaponServer(WeaponSlotType slotType)
        {
            if (!_weapons.ContainsKey(slotType))
            {
                Debug.LogWarning($"[WeaponSystem] No weapon in slot: {slotType}");
                return;
            }
            
            // Toggle: If already selected, holster
            if (_activeSlot.Value == slotType)
            {
                HolsterWeaponServer();
                return;
            }
            
            _activeSlot.Value = slotType;
            
            if (_enableDebugLogs)
                Debug.Log($"[WeaponSystem] Selected weapon: {slotType}");
        }
        
        public void HolsterWeapon()
        {
            if (IsServerInitialized)
            {
                HolsterWeaponServer();
                return;
            }
            // Owner client → ask server to holster
            if (IsOwner)
                HolsterWeaponServerRpc();
        }

        [ServerRpc(RequireOwnership = true)]
        private void HolsterWeaponServerRpc()
            => HolsterWeaponServer();
        
        [Server]
        private void HolsterWeaponServer()
        {
            if (_activeSlot.Value == null)
            {
                Debug.LogWarning("[WeaponSystem] No weapon active");
                return;
            }
            
            _activeSlot.Value = null;
            
            if (_enableDebugLogs)
                Debug.Log("[WeaponSystem] Holstered weapon");
        }
        
        #endregion
        
        #region IWeaponSystem - Ammo/Reload
        
        public int GetCurrentMagazine(WeaponSlotType slotType)
        {
            var weapon = GetWeapon(slotType);
            return weapon != null ? (int)weapon.GetCurrentValue(ItemStatType.MagazineSize) : 0;
        }
        
        public float GetTotalAmmo(WeaponSlotType slotType)
        {
            var weapon = GetWeapon(slotType);
            return weapon?.GetCurrentValue(ItemStatType.MaxAmmo) ?? 0f;
        }
        
        public void Reload(WeaponSlotType slotType)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[WeaponSystem] Reload: server-only!");
                return;
            }
            
            ReloadServer(slotType);
        }
        
        [Server]
        private void ReloadServer(WeaponSlotType slotType)
        {
            if (!_weapons.TryGetValue(slotType, out var instanceID))
            {
                Debug.LogWarning($"[WeaponSystem] No weapon in slot: {slotType}");
                return;
            }
            
            var weapon = _inventorySystem.GetItemByInstanceID(instanceID);
            if (weapon == null)
            {
                Debug.LogWarning($"[WeaponSystem] Weapon not found: {instanceID}");
                return;
            }
            
            var weaponDef = ItemDatabase.GetDefinition(weapon.DefinitionID) as WeaponDefinition;
            if (weaponDef == null)
            {
                Debug.LogWarning($"[WeaponSystem] Invalid weapon definition: {weapon.DefinitionID}");
                return;
            }
            
            // Validation
            float reserveAmmo = weapon.GetCurrentValue(ItemStatType.MaxAmmo);
            if (reserveAmmo <= 0f)
            {
                Debug.LogWarning("[WeaponSystem] No ammo remaining");
                return;
            }
            
            int magSize = Mathf.RoundToInt(weaponDef.GetStatValue(ItemStatType.MagazineSize));
            if (magSize <= 0) magSize = 30; // fallback

            int currentMag = (int)weapon.GetCurrentValue(ItemStatType.MagazineSize);
            if (currentMag >= magSize)
            {
                // Skip reload when full unless tactical reload is allowed on this weapon.
                bool tacticalAllowed = _currentWeaponBase?.CanTacticalReload ?? true;
                if (!tacticalAllowed)
                {
                    Debug.LogWarning("[WeaponSystem] Magazine full and tactical reload not allowed");
                    return;
                }
            }
            
            // Calculate reload amount
            int ammoNeeded   = magSize - currentMag;
            int ammoAvailable = Mathf.FloorToInt(reserveAmmo);
            int ammoToReload  = Mathf.Min(ammoNeeded, ammoAvailable);
            
            // Reload
            weapon.AdjustCurrentValue(ItemStatType.MagazineSize, ammoToReload);
            weapon.AdjustCurrentValue(ItemStatType.MaxAmmo, -ammoToReload);
            
            int newMag      = (int)weapon.GetCurrentValue(ItemStatType.MagazineSize);
            float newReserve = weapon.GetCurrentValue(ItemStatType.MaxAmmo);
            OnWeaponReloaded?.Invoke(slotType, newMag);
            
            if (_enableDebugLogs)
            {
                Debug.Log($"[WeaponSystem] Reloaded {ammoToReload} rounds. " +
                         $"Magazine: {newMag}/{magSize}, " +
                         $"Total: {newReserve:F0}");
            }
        }
        
        public bool CanReload(WeaponSlotType slotType)
        {
            if (!_weapons.TryGetValue(slotType, out var instanceID))
                return false;
            
            var weapon = _inventorySystem.GetItemByInstanceID(instanceID);
            if (weapon == null)
                return false;
            
            var weaponDef = ItemDatabase.GetDefinition(weapon.DefinitionID) as WeaponDefinition;
            if (weaponDef == null)
                return false;
            
            // Has ammo
            if (weapon.GetCurrentValue(ItemStatType.MaxAmmo) <= 0f)
                return false;
            
            // Magazine not full or tactical reload allowed
            int magSize = Mathf.RoundToInt(weaponDef.GetStatValue(ItemStatType.MagazineSize));
            if (magSize <= 0) magSize = 30;
            bool tacticalReloadAllowed = _currentWeaponBase?.CanTacticalReload ?? true;
            if ((int)weapon.GetCurrentValue(ItemStatType.MagazineSize) >= magSize && !tacticalReloadAllowed)
                return false;
            
            return true;
        }
        
        #endregion
        
        #region Stat Modifiers
        
        [Server]
        private void ApplyWeaponModifiers(string instanceID, WeaponDefinition weaponDef)
        {
            if (_statSystem == null) return;
            
            var modifiers = weaponDef.GetPlayerModifiers();
            if (modifiers != null)
            {
                foreach (var modifier in modifiers)
                {
                    var statMod = new StatModifier { SourceID = instanceID, Type = modifier.ModifierType, Value = modifier.Value, Priority = 0, Description = modifier.Description };
                    _statSystem.AddModifier(modifier.StatType, statMod);
                }
            }
            
            // Apply attachment PlayerModifiers (e.g. flashlight VisionRange)
            var weaponItem = _inventorySystem.GetItemByInstanceID(instanceID);
            if (weaponItem?.AttachedItems != null)
            {
                foreach (var attachmentInstanceID in weaponItem.AttachedItems)
                {
                    if (string.IsNullOrEmpty(attachmentInstanceID)) continue;
                    var attachInstance = ItemDatabase.GetInstance(attachmentInstanceID);
                    if (attachInstance == null) continue;
                    var attachDef = ItemDatabase.GetDefinition(attachInstance.DefinitionID) as AttachmentDefinition;
                    if (attachDef?.StatConfig?.PlayerModifiers == null) continue;
                    foreach (var mod in attachDef.StatConfig.PlayerModifiers)
                    {
                        var statMod = new StatModifier { SourceID = instanceID, Type = mod.ModifierType, Value = mod.Value, Priority = 0, Description = mod.Description };
                        _statSystem.AddModifier(mod.StatType, statMod);
                    }
                }
            }
        }
        
        [Server]
        private void RemoveWeaponModifiers(string instanceID)
        {
            if (_statSystem == null)
                return;
            
            _statSystem.RemoveAllModifiersFromSource(instanceID);
        }
        
        #endregion
        
        #region Helpers

        private WeaponSlotType FindAvailableSlot()
        {
            // Use configured priority order
            foreach (var slotType in _slotPriority)
            {
                if (!_weapons.ContainsKey(slotType))
                    return slotType;
            }
            
            // All slots occupied, return first in priority (will swap)
            return _slotPriority[0];
        }
        
        private int FindNextAvailableInventoryIndex()
        {
            return _inventorySystem?.GetNextFreeInventoryIndex() ?? 0;
        }
        
        private void RebuildWeaponCache()
        {
            _weaponCache.Clear();
            
            foreach (var kvp in _weapons)
            {
                var weapon = _inventorySystem?.GetItemByInstanceID(kvp.Value);
                if (weapon != null)
                    _weaponCache[kvp.Key] = weapon;
            }
        }
        
        #endregion
        
        #region Network Callbacks
        
        private void OnWeaponsChanged(SyncDictionaryOperation op, WeaponSlotType key, string value, bool asServer)
        {
            if (asServer)
                return;
            
            switch (op)
            {
                case SyncDictionaryOperation.Add:
                case SyncDictionaryOperation.Set:
                    var weapon = _inventorySystem?.GetItemByInstanceID(value);
                    if (weapon != null)
                    {
                        _weaponCache[key] = weapon;
                        OnWeaponEquipped?.Invoke(key, weapon);
                    }
                    break;
                
                case SyncDictionaryOperation.Remove:
                    // 'value' is the removed instanceID from the SyncDictionary callback.
                    // On a listen-server host, UnequipWeaponServer already cleared _weaponCache
                    // before this client-side callback fires, so fall back to inventory lookup
                    // to ensure OnWeaponUnequipped always fires and the HUD clears correctly.
                    if (!_weaponCache.TryGetValue(key, out var removedWeapon) && !string.IsNullOrEmpty(value))
                        removedWeapon = _inventorySystem?.GetItemByInstanceID(value);
                    _weaponCache.Remove(key);
                    if (removedWeapon != null)
                        OnWeaponUnequipped?.Invoke(key, removedWeapon);
                    break;
                
                case SyncDictionaryOperation.Clear:
                    _weaponCache.Clear();
                    break;
            }
        }
        
        private void OnActiveSlotChanged(WeaponSlotType? oldValue, WeaponSlotType? newValue, bool asServer)
        {
            if (asServer)
                return;
            
            OnActiveWeaponChanged?.Invoke(oldValue, newValue);
        }
        
        #endregion
        
        #region Debug
        
        [ContextMenu("Log Weapon State")]
        public void LogWeaponState()
        {
            Debug.Log("=== Weapon State ===");
            Debug.Log($"Active: {_activeSlot.Value?.ToString() ?? "[Holstered]"}");
            
            foreach (var kvp in _weapons)
            {
                var weapon = _inventorySystem?.GetItemByInstanceID(kvp.Value);
                var def = weapon != null ? ItemDatabase.GetDefinition(weapon.DefinitionID) : null;
                string name = def != null ? def.DisplayName : kvp.Value;
                
                Debug.Log($"  {kvp.Key}: {name}");
            }
        }
        
        #endregion
    }
}