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
        
        #endregion

        #region Local Fire State (owner-client + server)

        // Fire mode per slot — persisted to PlayerPrefs key "firemode_{slotIndex}"
        private readonly Dictionary<WeaponSlotType, FireMode> _fireModes =
            new Dictionary<WeaponSlotType, FireMode>();

        private bool _isFiring;
        private bool _isReloading;
        private Coroutine _autoFireCoroutine;
        private Vector3 _aimDirection = Vector3.forward; // updated by SetAimDirection()

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
            if (slot == null || _isReloading) return;
            if (_weaponCache.TryGetValue(slot.Value, out var inst))
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

        /// <summary>Get current fire mode of active weapon (reads PlayerPrefs default from definition).</summary>
        public FireMode GetCurrentFireMode()
        {
            var slot = _activeSlot.Value;
            if (slot == null) return FireMode.Auto;

            if (_fireModes.TryGetValue(slot.Value, out var mode)) return mode;

            // Read from PlayerPrefs, fallback to weapon definition default
            int saved = UnityEngine.PlayerPrefs.GetInt($"firemode_{(int)slot.Value}", -1);
            if (saved >= 0) { _fireModes[slot.Value] = (FireMode)saved; return (FireMode)saved; }

            if (_weaponCache.TryGetValue(slot.Value, out var inst))
            {
                var def = ItemDatabase
                    .GetDefinition(inst.DefinitionID) as WeaponDefinition;
                if (def != null) { _fireModes[slot.Value] = def.DefaultFireMode; return def.DefaultFireMode; }
            }

            return FireMode.Auto;
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
                // Auto-reload if reserve available
                float totalAmmo = inst.GetCurrentValue(ItemStatType.MaxAmmo);
                if (totalAmmo > 0)
                    StartCoroutine(ReloadCoroutine(slot.Value, inst));
                else
                    OnWeaponDepleted?.Invoke(slot.Value);
                return;
            }

            inst.AdjustCurrentValue(ItemStatType.MagazineSize, -1f);
            float mag  = inst.GetComputedStat(ItemStatType.MagazineSize);
            OnAmmoChanged?.Invoke(
                (int)inst.GetCurrentValue(ItemStatType.MagazineSize),
                (int)inst.GetCurrentValue(ItemStatType.MaxAmmo),
                (int)mag);
            OnShotFired?.Invoke(slot.Value, _aimDirection);
        }

        private System.Collections.IEnumerator ReloadCoroutine(WeaponSlotType slot, ItemInstance inst)
        {
            if (_isReloading) yield break;

            var def = ItemDatabase
                .GetDefinition(inst.DefinitionID) as WeaponDefinition;
            if (def == null) yield break;

            float magCap     = inst.GetComputedStat(ItemStatType.MagazineSize);
            float reloadTime = inst.GetComputedStat(ItemStatType.ReloadSpeed);
            if (reloadTime <= 0f) reloadTime = 2.5f;

            int currentMag = (int)inst.GetCurrentValue(ItemStatType.MagazineSize);
            int needed = (int)magCap - currentMag;
            if (needed <= 0) yield break;

            _isReloading = true;
            OnReloadStateChanged?.Invoke(true);

            yield return new WaitForSeconds(reloadTime);

            int reserve = (int)inst.GetCurrentValue(ItemStatType.MaxAmmo);
            int actual  = Mathf.Min(needed, reserve);
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
            return _weaponCache.TryGetValue(slotType, out var weapon) ? weapon : null;
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
            
            // All weapon slots can accept any weapon
            // Can add specific restrictions in config if needed
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
            item.InventoryIndex = -1; // Mark as equipped
            _inventorySystem.SyncItemState(instanceID);

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
            item.InventoryIndex = -1; // Mark as equipped
            _inventorySystem.SyncItemState(instanceID);
            
            if (_enableDebugLogs)
                Debug.Log($"[WeaponSystem] Equipped {weaponDef.DisplayName} → {targetSlot}");
        }
        
        public void UnequipWeapon(WeaponSlotType slotType)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[WeaponSystem] UnequipWeapon: server-only!");
                return;
            }
            
            UnequipWeaponServer(slotType);
        }
        
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
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[WeaponSystem] SelectWeapon: server-only!");
                return;
            }
            
            SelectWeaponServer(slotType);
        }
        
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
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[WeaponSystem] HolsterWeapon: server-only!");
                return;
            }
            
            HolsterWeaponServer();
        }
        
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
                if (!weaponDef.CanTacticalReload)
                {
                    Debug.LogWarning("[WeaponSystem] Magazine full");
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
            
            // Magazine not full or can tactical reload
            int magSize = Mathf.RoundToInt(weaponDef.GetStatValue(ItemStatType.MagazineSize));
            if (magSize <= 0) magSize = 30;
            if ((int)weapon.GetCurrentValue(ItemStatType.MagazineSize) >= magSize && !weaponDef.CanTacticalReload)
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
                    if (_weaponCache.TryGetValue(key, out var removedWeapon))
                    {
                        _weaponCache.Remove(key);
                        OnWeaponUnequipped?.Invoke(key, removedWeapon);
                    }
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