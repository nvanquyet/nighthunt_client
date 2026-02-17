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
    /// Equipment system - NetworkBehaviour
    /// Manages equipped items (head, chest, back, etc.)
    /// 
    /// Design:
    /// - Dictionary-based (SlotType → InstanceID)
    /// - Items stored in inventory, referenced here
    /// - Auto-applies stat modifiers to PlayerStatSystem
    /// - Handles weight modification when equipped
    /// </summary>
    public class EquipmentSystem : NetworkBehaviour, IEquipmentSystem
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
        /// Network-synced equipped items
        /// Key: SlotType, Value: ItemInstanceID
        /// </summary>
        private readonly SyncDictionary<EquipmentSlotType, string> _equippedItems = new SyncDictionary<EquipmentSlotType, string>();
        
        #endregion
        
        #region Local Cache
        
        /// <summary>
        /// Local cache of equipped items (all clients)
        /// Built from synced IDs + inventory lookup
        /// </summary>
        private Dictionary<EquipmentSlotType, ItemInstance> _equipmentCache = new Dictionary<EquipmentSlotType, ItemInstance>();
        
        #endregion
        
        #region Events
        
        public event Action<EquipmentSlotType, ItemInstance> OnItemEquipped;
        public event Action<EquipmentSlotType, ItemInstance> OnItemUnequipped;
        
        #endregion
        
        #region NetworkBehaviour Lifecycle
        
        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            
            _equippedItems.OnChange += OnEquipmentChanged;
            
            if (!IsServerInitialized)
            {
                RebuildEquipmentCache();
            }
        }
        
        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            
            _equippedItems.OnChange -= OnEquipmentChanged;
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
                Debug.LogError("[EquipmentSystem] InventoryConfig is null!");
            
            if (_statSystem == null)
                Debug.LogError("[EquipmentSystem] PlayerStatSystem is null!");
            
            if (_inventorySystem == null)
                Debug.LogError("[EquipmentSystem] InventorySystem is null!");
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
        
        #region IEquipmentSystem - Getters
        
        public ItemInstance GetEquippedItem(EquipmentSlotType slotType)
        {
            if (_equipmentCache.TryGetValue(slotType, out var item))
                return item;
            
            return null;
        }
        
        public Dictionary<EquipmentSlotType, ItemInstance> GetAllEquippedItems()
        {
            return new Dictionary<EquipmentSlotType, ItemInstance>(_equipmentCache);
        }
        
        public bool IsSlotOccupied(EquipmentSlotType slotType)
        {
            return _equippedItems.ContainsKey(slotType);
        }
        
        public bool CanEquipInSlot(string itemDefinitionID, EquipmentSlotType slotType)
        {
            var itemDef = ItemDatabase.GetDefinition(itemDefinitionID);
            if (itemDef == null)
                return false;
            
            // Must be armor/equipment type
            if (!(itemDef is EquipmentDefinition armorDef))
                return false;
            
            // Check if item's designated slot matches
            return armorDef.EquipmentSlot == slotType;
        }
        
        #endregion
        
        #region IEquipmentSystem - Equip/Unequip
        
        public void EquipItem(string instanceID)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[EquipmentSystem] EquipItem can only be called on server!");
                return;
            }
            
            EquipItemServer(instanceID);
        }
        
        [Server]
        private void EquipItemServer(string instanceID)
        {
            // Get item from inventory
            var item = _inventorySystem.GetItemByInstanceID(instanceID);
            if (item == null)
            {
                Debug.LogWarning($"[EquipmentSystem] Item not found in inventory: {instanceID}");
                return;
            }
            
            // Get item definition
            var itemDef = ItemDatabase.GetDefinition(item.DefinitionID);
            if (!(itemDef is EquipmentDefinition armorDef))
            {
                Debug.LogWarning($"[EquipmentSystem] Item is not equipment: {item.DefinitionID}");
                return;
            }
            
            var slotType = armorDef.EquipmentSlot;
            
            // Check if slot already occupied
            if (_equippedItems.TryGetValue(slotType, out var existingID))
            {
                // Unequip existing first
                UnequipItemServer(slotType);
            }
            
            // Equip new item
            _equippedItems[slotType] = instanceID;
            item.InventoryIndex = -1; // Mark as equipped
            
            // Apply stat modifiers
            ApplyEquipmentModifiers(instanceID, armorDef);
            
            // Update weight if needed
            if (armorDef.ModifyWeightWhenEquipped)
            {
                UpdateEquipmentWeight(instanceID, armorDef, true);
            }
            
            if (_enableDebugLogs)
            {
                Debug.Log($"[EquipmentSystem] Equipped {armorDef.DisplayName} to {slotType}");
            }
        }
        
        public void UnequipItem(EquipmentSlotType slotType)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[EquipmentSystem] UnequipItem can only be called on server!");
                return;
            }
            
            UnequipItemServer(slotType);
        }
        
        [Server]
        private void UnequipItemServer(EquipmentSlotType slotType)
        {
            if (!_equippedItems.TryGetValue(slotType, out var instanceID))
            {
                Debug.LogWarning($"[EquipmentSystem] No item equipped in slot: {slotType}");
                return;
            }
            
            var item = _inventorySystem.GetItemByInstanceID(instanceID);
            if (item == null)
            {
                Debug.LogWarning($"[EquipmentSystem] Equipped item not found: {instanceID}");
                _equippedItems.Remove(slotType);
                return;
            }
            
            var itemDef = ItemDatabase.GetDefinition(item.DefinitionID);
            if (!(itemDef is EquipmentDefinition armorDef))
            {
                Debug.LogWarning($"[EquipmentSystem] Item is not equipment: {item.DefinitionID}");
                _equippedItems.Remove(slotType);
                return;
            }
            
            // Remove from equipment
            _equippedItems.Remove(slotType);
            
            // Return to inventory (find available index)
            item.InventoryIndex = FindNextAvailableInventoryIndex();
            
            // Remove stat modifiers
            RemoveEquipmentModifiers(instanceID);
            
            // Update weight if needed
            if (armorDef.ModifyWeightWhenEquipped)
            {
                UpdateEquipmentWeight(instanceID, armorDef, false);
            }
            
            if (_enableDebugLogs)
            {
                Debug.Log($"[EquipmentSystem] Unequipped {armorDef.DisplayName} from {slotType}");
            }
        }
        
        public void SwapEquipment(EquipmentSlotType slot1, EquipmentSlotType slot2)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[EquipmentSystem] SwapEquipment can only be called on server!");
                return;
            }
            
            SwapEquipmentServer(slot1, slot2);
        }
        
        [Server]
        private void SwapEquipmentServer(EquipmentSlotType slot1, EquipmentSlotType slot2)
        {
            bool hasItem1 = _equippedItems.TryGetValue(slot1, out var instanceID1);
            bool hasItem2 = _equippedItems.TryGetValue(slot2, out var instanceID2);
            
            if (!hasItem1 && !hasItem2)
            {
                Debug.LogWarning("[EquipmentSystem] Both slots are empty");
                return;
            }
            
            // Swap
            if (hasItem1 && hasItem2)
            {
                _equippedItems[slot1] = instanceID2;
                _equippedItems[slot2] = instanceID1;
            }
            else if (hasItem1)
            {
                _equippedItems.Remove(slot1);
                _equippedItems[slot2] = instanceID1;
            }
            else // hasItem2
            {
                _equippedItems.Remove(slot2);
                _equippedItems[slot1] = instanceID2;
            }
            
            if (_enableDebugLogs)
            {
                Debug.Log($"[EquipmentSystem] Swapped equipment between {slot1} and {slot2}");
            }
        }
        
        #endregion
        
        #region Stat Modifiers
        
        [Server]
        private void ApplyEquipmentModifiers(string instanceID, EquipmentDefinition equipmentDef)
        {
            if (_statSystem == null || equipmentDef.PlayerModifiers == null)
                return;
            
            foreach (var modifier in equipmentDef.PlayerModifiers)
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
            
            if (_enableDebugLogs)
            {
                Debug.Log($"[EquipmentSystem] Applied {equipmentDef.PlayerModifiers.Length} stat modifiers from {equipmentDef.DisplayName}");
            }
        }
        
        [Server]
        private void RemoveEquipmentModifiers(string instanceID)
        {
            if (_statSystem == null)
                return;
            
            _statSystem.RemoveAllModifiersFromSource(instanceID);
            
            if (_enableDebugLogs)
            {
                Debug.Log($"[EquipmentSystem] Removed stat modifiers from {instanceID}");
            }
        }
        
        [Server]
        private void UpdateEquipmentWeight(string instanceID, EquipmentDefinition equipmentDef, bool isEquipping)
        {
            if (_statSystem == null)
                return;
            
            float weightModifier = isEquipping ? equipmentDef.EquippedWeightModifier : -equipmentDef.EquippedWeightModifier;
            
            var statMod = StatModifier.CreateFlat(
                $"{instanceID}_weight",
                weightModifier,
                priority: 0,
                description: $"{equipmentDef.DisplayName} weight modifier"
            );
            
            if (isEquipping)
            {
                _statSystem.AddModifier(PlayerStatType.CurrentWeight, statMod);
            }
            else
            {
                _statSystem.RemoveModifier(PlayerStatType.CurrentWeight, $"{instanceID}_weight");
            }
            
            if (_enableDebugLogs)
            {
                Debug.Log($"[EquipmentSystem] {(isEquipping ? "Applied" : "Removed")} weight modifier: {weightModifier:F1}kg");
            }
        }
        
        #endregion
        
        #region Helper Methods
        
        private int FindNextAvailableInventoryIndex()
        {
            if (_inventorySystem == null)
                return 0;
            
            int maxIndex = _inventorySystem.GetMaxIndex();
            return maxIndex + 1;
        }
        
        private void RebuildEquipmentCache()
        {
            _equipmentCache.Clear();
            
            foreach (var kvp in _equippedItems)
            {
                var item = _inventorySystem?.GetItemByInstanceID(kvp.Value);
                if (item != null)
                {
                    _equipmentCache[kvp.Key] = item;
                }
            }
        }
        
        #endregion
        
        #region Network Callbacks
        
        private void OnEquipmentChanged(SyncDictionaryOperation op, EquipmentSlotType key, string value, bool asServer)
        {
            if (asServer)
                return;
            
            // Client: Update cache and trigger events
            switch (op)
            {
                case SyncDictionaryOperation.Add:
                case SyncDictionaryOperation.Set:
                    var item = _inventorySystem?.GetItemByInstanceID(value);
                    if (item != null)
                    {
                        _equipmentCache[key] = item;
                        OnItemEquipped?.Invoke(key, item);
                    }
                    break;
                
                case SyncDictionaryOperation.Remove:
                    if (_equipmentCache.TryGetValue(key, out var removedItem))
                    {
                        _equipmentCache.Remove(key);
                        OnItemUnequipped?.Invoke(key, removedItem);
                    }
                    break;
                
                case SyncDictionaryOperation.Clear:
                    _equipmentCache.Clear();
                    break;
            }
        }
        
        #endregion
        
        #region Debug
        
        private void OnGUI()
        {
            if (!_showDebugUI || !IsOwner)
                return;
            
            GUILayout.BeginArea(new Rect(470, 400, 300, 400));
            GUILayout.Label("=== EQUIPMENT ===");
            
            if (_inventoryConfig != null && _inventoryConfig.EquipmentSlots != null)
            {
                foreach (var slotConfig in _inventoryConfig.EquipmentSlots)
                {
                    var item = GetEquippedItem(slotConfig.SlotType);
                    
                    if (item != null)
                    {
                        var def = ItemDatabase.GetDefinition(item.DefinitionID);
                        string name = def != null ? def.DisplayName : item.DefinitionID;
                        GUILayout.Label($"{slotConfig.DisplayName}: {name}");
                    }
                    else
                    {
                        GUILayout.Label($"{slotConfig.DisplayName}: [Empty]");
                    }
                }
            }
            
            GUILayout.EndArea();
        }
        
        [ContextMenu("Log Equipment State")]
        public void LogEquipmentState()
        {
            Debug.Log($"=== Equipment State ({_equippedItems.Count} items) ===");
            
            foreach (var kvp in _equippedItems)
            {
                var item = _inventorySystem?.GetItemByInstanceID(kvp.Value);
                var def = item != null ? ItemDatabase.GetDefinition(item.DefinitionID) : null;
                string name = def != null ? def.DisplayName : kvp.Value;
                
                Debug.Log($"  {kvp.Key}: {name}");
            }
        }
        
        #endregion
    }
}