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
    /// PRODUCTION-OPTIMIZED Weapon System
    /// 
    /// Improvements:
    /// ✓ Config-driven slot priorities (no hard-coding)
    /// ✓ Cached weapon lookups
    /// ✓ Proper event cleanup
    /// ✓ Reload validation optimization
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
        
        // OPTIMIZED: Cache weapons for O(1) access
        private Dictionary<WeaponSlotType, ItemInstance> _weaponCache = new Dictionary<WeaponSlotType, ItemInstance>();
        
        #endregion
        
        #region Events
        
        public event Action<WeaponSlotType, ItemInstance> OnWeaponEquipped;
        public event Action<WeaponSlotType, ItemInstance> OnWeaponUnequipped;
        public event Action<WeaponSlotType?, WeaponSlotType?> OnActiveWeaponChanged;
        public event Action<WeaponSlotType, int> OnWeaponReloaded;
        
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
            
            // OPTIMIZED: Config-driven slot selection
            WeaponSlotType targetSlot = FindAvailableWeaponSlotOptimized();
            
            // If no available slot, swap with target slot
            if (_weapons.ContainsKey(targetSlot))
                UnequipWeaponServer(targetSlot);
            
            // Equip weapon
            _weapons[targetSlot] = instanceID;
            item.InventoryIndex = -1; // Mark as equipped
            
            // Apply stat modifiers
            ApplyWeaponModifiers(instanceID, weaponDef);
            
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
            
            // If this weapon is active, holster it
            if (_activeSlot.Value == slotType)
                HolsterWeaponServer();
            
            // Remove from weapons
            _weapons.Remove(slotType);
            
            // Return to inventory
            item.InventoryIndex = FindNextAvailableInventoryIndex();
            
            // Remove stat modifiers
            RemoveWeaponModifiers(instanceID);
            
            if (_enableDebugLogs)
            {
                var def = ItemDatabase.GetDefinition(item.DefinitionID);
                Debug.Log($"[WeaponSystem] Unequipped {def?.DisplayName} from {slotType}");
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
            return weapon?.CurrentMagazine ?? 0;
        }
        
        public float GetTotalAmmo(WeaponSlotType slotType)
        {
            var weapon = GetWeapon(slotType);
            return weapon?.CurrentResource ?? 0f;
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
            if (weapon.CurrentResource <= 0)
            {
                Debug.LogWarning("[WeaponSystem] No ammo remaining");
                return;
            }
            
            if (weapon.CurrentMagazine >= weaponDef.MagazineSize)
            {
                if (!weaponDef.CanTacticalReload)
                {
                    Debug.LogWarning("[WeaponSystem] Magazine full");
                    return;
                }
            }
            
            // Calculate reload amount
            int ammoNeeded = weaponDef.MagazineSize - weapon.CurrentMagazine;
            int ammoAvailable = Mathf.FloorToInt(weapon.CurrentResource);
            int ammoToReload = Mathf.Min(ammoNeeded, ammoAvailable);
            
            // Reload
            weapon.CurrentMagazine += ammoToReload;
            weapon.CurrentResource -= ammoToReload;
            
            OnWeaponReloaded?.Invoke(slotType, weapon.CurrentMagazine);
            
            if (_enableDebugLogs)
            {
                Debug.Log($"[WeaponSystem] Reloaded {ammoToReload} rounds. " +
                         $"Magazine: {weapon.CurrentMagazine}/{weaponDef.MagazineSize}, " +
                         $"Total: {weapon.CurrentResource:F0}");
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
            if (weapon.CurrentResource <= 0)
                return false;
            
            // Magazine not full or can tactical reload
            if (weapon.CurrentMagazine >= weaponDef.MagazineSize && !weaponDef.CanTacticalReload)
                return false;
            
            return true;
        }
        
        #endregion
        
        #region Stat Modifiers
        
        [Server]
        private void ApplyWeaponModifiers(string instanceID, WeaponDefinition weaponDef)
        {
            if (_statSystem == null || weaponDef.PlayerModifiers == null)
                return;
            
            foreach (var modifier in weaponDef.PlayerModifiers)
            {
                var statMod = new StatModifier
                {
                    SourceID = instanceID,
                    Type = modifier.ModifierType,
                    Value = modifier.Value,
                    Priority = 0,
                    Description = modifier.Description
                };
                
                _statSystem.AddModifier(modifier.StatType, statMod);
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
        
        #region Helper Methods - OPTIMIZED
        
        /// <summary>
        /// OPTIMIZED: Config-driven slot selection instead of hard-coded priorities
        /// </summary>
        private WeaponSlotType FindAvailableWeaponSlotOptimized()
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
            if (_inventorySystem == null)
                return 0;
            
            int maxIndex = _inventorySystem.GetMaxIndex();
            return maxIndex + 1;
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