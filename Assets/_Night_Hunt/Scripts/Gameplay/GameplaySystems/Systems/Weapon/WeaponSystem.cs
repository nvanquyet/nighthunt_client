using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;
using System.Collections.Generic;
using UnityEngine;
using GameplaySystems.Core.Interfaces;
using GameplaySystems.Core.Configs;
using GameplaySystems.Core.Data;
using GameplaySystems.Stat;

namespace GameplaySystems.Inventory
{
    /// <summary>
    /// Weapon system - NetworkBehaviour
    /// Manages weapon slots (primary, secondary, melee) and active weapon
    /// 
    /// Design:
    /// - Dictionary-based (SlotType → InstanceID)
    /// - Tracks active weapon (drawn/holstered)
    /// - Handles reload logic
    /// - Applies stat modifiers to player
    /// </summary>
    public class WeaponSystem : NetworkBehaviour, IWeaponSystem
    {
        #region Serialized Fields
        
        [Header("Configuration")]
        [SerializeField] private InventoryConfig _inventoryConfig;
        
        [Header("References")]
        [SerializeField] private PlayerStatSystem _statSystem;
        [SerializeField] private InventorySystem _inventorySystem;
        
        [Header("Debug")]
        [SerializeField] private bool _showDebugUI = false;
        [SerializeField] private bool _enableDebugLogs = false;
        
        #endregion
        
        #region Network Synced Data
        
        /// <summary>
        /// Equipped weapons
        /// Key: SlotType, Value: ItemInstanceID
        /// </summary>
        private readonly SyncDictionary<WeaponSlotType, string> _weapons = new SyncDictionary<WeaponSlotType, string>();
        
        /// <summary>
        /// Currently active weapon slot
        /// null = holstered
        /// </summary>
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
        
        #endregion
        
        #region NetworkBehaviour Lifecycle
        
        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            
            _weapons.OnChange += OnWeaponsChanged;
            _activeSlot.OnChange += OnActiveSlotChanged;
            
            if (!IsServerInitialized)
            {
                RebuildWeaponCache();
            }
        }
        
        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            
            _weapons.OnChange -= OnWeaponsChanged;
            _activeSlot.OnChange -= OnActiveSlotChanged;
        }
        
        #endregion
        
        #region Initialization
        
        private void Awake()
        {
            ValidateReferences();
        }
        
        private void ValidateReferences()
        {
#if UNITY_EDITOR
            if (_statSystem == null)
                _statSystem = GetComponent<PlayerStatSystem>();
            
            if (_inventorySystem == null)
                _inventorySystem = GetComponent<InventorySystem>();
#endif
            
            if (_inventoryConfig == null)
                Debug.LogError("[WeaponSystem] InventoryConfig is null!");
            
            if (_statSystem == null)
                Debug.LogError("[WeaponSystem] PlayerStatSystem is null!");
            
            if (_inventorySystem == null)
                Debug.LogError("[WeaponSystem] InventorySystem is null!");
        }
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_statSystem == null)
                _statSystem = GetComponent<PlayerStatSystem>();
            
            if (_inventorySystem == null)
                _inventorySystem = GetComponent<InventorySystem>();
        }
#endif
        
        #endregion
        
        #region IWeaponSystem - Getters
        
        public ItemInstance GetWeapon(WeaponSlotType slotType)
        {
            if (_weaponCache.TryGetValue(slotType, out var weapon))
                return weapon;
            
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
            if (itemDef == null)
                return false;
            
            // Must be weapon type
            if (!(itemDef is WeaponDefinition))
                return false;
            
            // All weapon slots can accept any weapon for now
            // Can add specific restrictions later (e.g., Melee slot only accepts melee)
            return true;
        }
        
        #endregion
        
        #region IWeaponSystem - Equip/Unequip
        
        public void EquipWeapon(string instanceID)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[WeaponSystem] EquipWeapon can only be called on server!");
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
                Debug.LogWarning($"[WeaponSystem] Item is not a weapon: {item.DefinitionID}");
                return;
            }
            
            // Find available slot or swap with primary
            WeaponSlotType targetSlot = FindAvailableWeaponSlot();
            
            // If no available slot, swap with primary
            if (_weapons.ContainsKey(targetSlot))
            {
                UnequipWeaponServer(targetSlot);
            }
            
            // Equip weapon
            _weapons[targetSlot] = instanceID;
            item.InventoryIndex = -1;
            
            // Apply stat modifiers
            ApplyWeaponModifiers(instanceID, weaponDef);
            
            if (_enableDebugLogs)
            {
                Debug.Log($"[WeaponSystem] Equipped {weaponDef.DisplayName} to {targetSlot}");
            }
        }
        
        public void UnequipWeapon(WeaponSlotType slotType)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[WeaponSystem] UnequipWeapon can only be called on server!");
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
            {
                HolsterWeaponServer();
            }
            
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
                Debug.LogWarning("[WeaponSystem] SwapWeapons can only be called on server!");
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
                Debug.LogWarning("[WeaponSystem] Both slots are empty");
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
            else
            {
                _weapons.Remove(slot2);
                _weapons[slot1] = instanceID2;
            }
            
            if (_enableDebugLogs)
            {
                Debug.Log($"[WeaponSystem] Swapped weapons between {slot1} and {slot2}");
            }
        }
        
        #endregion
        
        #region IWeaponSystem - Selection
        
        public void SelectWeapon(WeaponSlotType slotType)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[WeaponSystem] SelectWeapon can only be called on server!");
                return;
            }
            
            SelectWeaponServer(slotType);
        }
        
        [Server]
        private void SelectWeaponServer(WeaponSlotType slotType)
        {
            // Check if weapon exists in slot
            if (!_weapons.ContainsKey(slotType))
            {
                Debug.LogWarning($"[WeaponSystem] No weapon in slot: {slotType}");
                return;
            }
            
            // If already selected, holster
            if (_activeSlot.Value == slotType)
            {
                HolsterWeaponServer();
                return;
            }
            
            // Select weapon
            _activeSlot.Value = slotType;
            
            if (_enableDebugLogs)
            {
                Debug.Log($"[WeaponSystem] Selected weapon: {slotType}");
            }
        }
        
        public void HolsterWeapon()
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[WeaponSystem] HolsterWeapon can only be called on server!");
                return;
            }
            
            HolsterWeaponServer();
        }
        
        [Server]
        private void HolsterWeaponServer()
        {
            if (_activeSlot.Value == null)
            {
                Debug.LogWarning("[WeaponSystem] No weapon is active");
                return;
            }
            
            _activeSlot.Value = null;
            
            if (_enableDebugLogs)
            {
                Debug.Log("[WeaponSystem] Holstered weapon");
            }
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
                Debug.LogWarning("[WeaponSystem] Reload can only be called on server!");
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
            
            // Check if can reload
            if (weapon.CurrentResource <= 0)
            {
                Debug.LogWarning($"[WeaponSystem] No ammo remaining");
                return;
            }
            
            if (weapon.CurrentMagazine >= weaponDef.MagazineSize)
            {
                if (!weaponDef.CanTacticalReload)
                {
                    Debug.LogWarning($"[WeaponSystem] Magazine is full");
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
                Debug.Log($"[WeaponSystem] Reloaded {ammoToReload} rounds. Magazine: {weapon.CurrentMagazine}/{weaponDef.MagazineSize}, Total: {weapon.CurrentResource}");
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
            
            // Has ammo remaining
            if (weapon.CurrentResource <= 0)
                return false;
            
            // Magazine not full (or can tactical reload)
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
        
        #region Helper Methods
        
        private WeaponSlotType FindAvailableWeaponSlot()
        {
            // Priority: Primary → Secondary → Melee
            if (!_weapons.ContainsKey(WeaponSlotType.Primary))
                return WeaponSlotType.Primary;
            
            if (!_weapons.ContainsKey(WeaponSlotType.Secondary))
                return WeaponSlotType.Secondary;
            
            if (!_weapons.ContainsKey(WeaponSlotType.Melee))
                return WeaponSlotType.Melee;
            
            // All slots occupied, default to Primary (will swap)
            return WeaponSlotType.Primary;
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
                {
                    _weaponCache[kvp.Key] = weapon;
                }
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
        
        private void OnGUI()
        {
            if (!_showDebugUI || !IsOwner)
                return;
            
            GUILayout.BeginArea(new Rect(780, 400, 300, 300));
            GUILayout.Label("=== WEAPONS ===");
            
            var activeSlot = _activeSlot.Value;
            if (activeSlot != null)
            {
                GUILayout.Label($"Active: {activeSlot.Value}");
            }
            else
            {
                GUILayout.Label("Active: [Holstered]");
            }
            
            GUILayout.Space(10);
            
            foreach (WeaponSlotType slot in System.Enum.GetValues(typeof(WeaponSlotType)))
            {
                var weapon = GetWeapon(slot);
                
                if (weapon != null)
                {
                    var def = ItemDatabase.GetDefinition(weapon.DefinitionID) as WeaponDefinition;
                    string name = def != null ? def.DisplayName : weapon.DefinitionID;
                    GUILayout.Label($"{slot}: {name} [{weapon.CurrentMagazine}/{weapon.CurrentResource:F0}]");
                }
                else
                {
                    GUILayout.Label($"{slot}: [Empty]");
                }
            }
            
            GUILayout.EndArea();
        }
        
        [ContextMenu("Log Weapon State")]
        public void LogWeaponState()
        {
            Debug.Log($"=== Weapon State ===");
            Debug.Log($"  Active: {_activeSlot.Value?.ToString() ?? "[Holstered]"}");
            
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