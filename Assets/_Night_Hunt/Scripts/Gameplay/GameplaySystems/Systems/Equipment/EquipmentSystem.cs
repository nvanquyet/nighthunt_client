using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;
using System.Collections.Generic;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.StatSystem.Core.Data;
using NightHunt.Gameplay.StatSystem.Systems;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.Equipment
{
    /// <summary>
    /// Manages equipment slot assignment (head, body, legs, feet) for a networked player.
    /// All slot mutations are server-authoritative via SyncDictionary.
    /// </summary>
    public class EquipmentSystem : NetworkBehaviour, IEquipmentSystem, IDisposable
    {
        #region Serialized Fields
        
        [Header("Configuration")]
        [SerializeField] private InventoryConfig _inventoryConfig;
        
        [Header("References")]
        [SerializeField] private PlayerStatSystem _statSystemComponent;
        [SerializeField] private InventorySystem _inventorySystemComponent;
        private IPlayerStatSystem _statSystem;
        private IInventorySystem _inventorySystem;
        
        [Header("Debug")]
        [SerializeField] private bool _enableDebugLogs = false;
        
        #endregion
        
        #region Network Synced Data
        
        private readonly SyncDictionary<EquipmentSlotType, string> _equippedItems = new SyncDictionary<EquipmentSlotType, string>();
        
        #endregion
        
        #region Local Cache
        
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
                RebuildEquipmentCache();
        }
        
        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            
            _equippedItems.OnChange -= OnEquipmentChanged;
            _equipmentCache.Clear();
        }
        
        #endregion
        
        #region IDisposable Implementation
        
        public void Dispose()
        {
            // Unsubscribe from network events
            _equippedItems.OnChange -= OnEquipmentChanged;
            
            // Clear cache
            _equipmentCache.Clear();
        }
        
        #endregion
        
        #region Initialization
        
        private void Awake()
        {
            ValidateReferences();
        }
        
        private void ValidateReferences()
        {
            _statSystem = ComponentResolver.Find<IPlayerStatSystem>(this)
        .UseExisting(_statSystemComponent)
        .OnSelf()
        .InChildren()
        .InParent()
        .InRootChildren()
        .OrLogWarning("[Auto] IPlayerStatSystem not found")
        .Resolve();

            if (_statSystem is PlayerStatSystem statConcrete)
                _statSystemComponent = statConcrete;

            _inventorySystem = ComponentResolver.Find<IInventorySystem>(this)
        .UseExisting(_inventorySystemComponent)
        .OnSelf()
        .InChildren()
        .InParent()
        .InRootChildren()
        .OrLogWarning("[Auto] IInventorySystem not found")
        .Resolve();

            if (_inventorySystem is InventorySystem invConcrete)
                _inventorySystemComponent = invConcrete;
            
            if (_inventoryConfig == null)
                Debug.LogError("[EquipmentSystem] InventoryConfig is null!");
            
            if (_statSystem == null)
                Debug.LogWarning("[EquipmentSystem] IPlayerStatSystem is null - stat modifiers will not work!");
            
            if (_inventorySystem == null)
                Debug.LogError("[EquipmentSystem] IInventorySystem is null!");
        }
        
#if UNITY_EDITOR
        [ContextMenu("Validate References")]
        protected override void OnValidate()
        {
            ValidateReferences();
        }
#endif
        
        #endregion
        
        #region IEquipmentSystem - Getters
        
        public ItemInstance GetEquippedItem(EquipmentSlotType slotType)
        {
            return _equipmentCache.TryGetValue(slotType, out var item) ? item : null;
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
            
            // Must be equipment type
            if (!(itemDef is EquipmentDefinition equipmentDef))
                return false;
            
            // Check if item's designated slot matches
            return equipmentDef.EquipmentSlot == slotType;
        }
        
        #endregion
        
        #region IEquipmentSystem - Equip/Unequip
        
        public void EquipItem(string instanceID)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[EquipmentSystem] EquipItem: server-only!");
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
                Debug.LogWarning($"[EquipmentSystem] Item not found: {instanceID}");
                return;
            }
            
            // Get item definition
            var itemDef = ItemDatabase.GetDefinition(item.DefinitionID);
            if (!(itemDef is EquipmentDefinition equipmentDef))
            {
                Debug.LogWarning($"[EquipmentSystem] Not equipment: {item.DefinitionID}");
                return;
            }
            
            var slotType = equipmentDef.EquipmentSlot;
            
            // If slot occupied, unequip existing first
            if (_equippedItems.TryGetValue(slotType, out var existingID))
                UnequipItemServer(slotType);
            
            // Equip new item
            _equippedItems[slotType] = instanceID;
            item.InventoryIndex = -1; // Mark as equipped
            _inventorySystem.SyncItemState(instanceID);

            if (_enableDebugLogs)
                Debug.Log($"[EquipmentSystem] Equipped {equipmentDef.DisplayName} → {slotType}");
        }
        
        public void UnequipItem(EquipmentSlotType slotType)
        {
            if (IsServerInitialized)
            {
                UnequipItemServer(slotType);
                return;
            }
            if (IsOwner)
                UnequipItemServerRpc(slotType);
        }

        [ServerRpc(RequireOwnership = true)]
        private void UnequipItemServerRpc(EquipmentSlotType slotType)
            => UnequipItemServer(slotType);

        [Server]
        private void UnequipItemServer(EquipmentSlotType slotType)
        {
            if (!_equippedItems.TryGetValue(slotType, out var instanceID))
            {
                Debug.LogWarning($"[EquipmentSystem] No item in slot: {slotType}");
                return;
            }
            
            var item = _inventorySystem.GetItemByInstanceID(instanceID);
            if (item == null)
            {
                Debug.LogWarning($"[EquipmentSystem] Item not found: {instanceID}");
                _equippedItems.Remove(slotType);
                return;
            }
            
            var itemDef = ItemDatabase.GetDefinition(item.DefinitionID);
            if (!(itemDef is EquipmentDefinition equipmentDef))
            {
                Debug.LogWarning($"[EquipmentSystem] Invalid equipment: {item.DefinitionID}");
                _equippedItems.Remove(slotType);
                return;
            }
            
            if (_inventoryConfig != null && _inventoryConfig.DetachAttachmentsOnUnequip)
            {
                var attachmentSystem = ComponentResolver.Find<NightHunt.GameplaySystems.Core.Interfaces.IAttachmentSystem>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] NightHunt.GameplaySystems.Core.Interfaces.IAttachmentSystem not found")
        .Resolve();
                if (attachmentSystem == null)
                {
                    // Try to find in parent or siblings
                    attachmentSystem = ComponentResolver.Find<NightHunt.GameplaySystems.Core.Interfaces.IAttachmentSystem>(this)
        .InParent()
        .InRootChildren()
        .OrLogWarning("[Auto] NightHunt.GameplaySystems.Core.Interfaces.IAttachmentSystem not found")
        .Resolve();
                }
                
                if (attachmentSystem != null)
                {
                    attachmentSystem.DetachAllFromItem(instanceID);
                }
            }
            
            // Remove from equipment
            _equippedItems.Remove(slotType);
            
            // Return to inventory
            item.InventoryIndex = FindNextAvailableInventoryIndex();
            _inventorySystem.SyncItemState(instanceID);

            // [SAO] StatApplyOrchestrator handles modifier removal — disabled to prevent double-apply
            // RemoveEquipmentModifiers(instanceID);

            if (_enableDebugLogs)
                Debug.Log($"[EquipmentSystem] Unequipped {equipmentDef.DisplayName} from {slotType}.");
        }
        
        public void SwapEquipment(EquipmentSlotType slot1, EquipmentSlotType slot2)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[EquipmentSystem] SwapEquipment: server-only!");
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
                Debug.LogWarning("[EquipmentSystem] Both slots empty");
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
                Debug.Log($"[EquipmentSystem] Swapped {slot1} ↔ {slot2}");
        }
        
        #endregion
        
        #region Stat Modifiers

        [Server]
        private void ApplyEquipmentModifiers(string instanceID, EquipmentDefinition equipmentDef)
        {
            if (_statSystem == null)
                return;
            
            // Apply player stat modifiers from StatConfig
            var playerModifiers = equipmentDef.GetPlayerModifiers();
            if (playerModifiers != null)
            {
                foreach (var modifier in playerModifiers)
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
            
            // Apply weight modification if needed
            if (equipmentDef.ModifyWeightWhenEquipped)
            {
                var weightMod = StatModifier.CreateFlat(
                    $"{instanceID}_weight",
                    equipmentDef.EquippedWeightModifier,
                    0,
                    $"{equipmentDef.DisplayName} weight modifier"
                );
                
                _statSystem.AddModifier(PlayerStatType.CurrentWeight, weightMod);
            }
            
            // Apply attachment PlayerModifiers (e.g. flashlight VisionRange)
            var equipmentItem = _inventorySystem.GetItemByInstanceID(instanceID);
            if (equipmentItem?.AttachedItems != null)
            {
                foreach (var attachmentInstanceID in equipmentItem.AttachedItems)
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
            
            if (_enableDebugLogs)
            {
                int modCount = (equipmentDef.GetPlayerModifiers()?.Length ?? 0) + 
                              (equipmentDef.ModifyWeightWhenEquipped ? 1 : 0);
                Debug.Log($"[EquipmentSystem] Applied {modCount} modifiers from {equipmentDef.DisplayName}");
            }
        }
        
        [Server]
        private void RemoveEquipmentModifiers(string instanceID)
        {
            if (_statSystem == null)
                return;
            
            // Remove all modifiers from this equipment
            _statSystem.RemoveAllModifiersFromSource(instanceID);
            
            // Remove weight modifier
            _statSystem.RemoveModifier(PlayerStatType.CurrentWeight, $"{instanceID}_weight");
            
            if (_enableDebugLogs)
                Debug.Log($"[EquipmentSystem] Removed modifiers from {instanceID}");
        }
        
        #endregion
        
        #region Helper Methods
        
        private int FindNextAvailableInventoryIndex()
        {
            return _inventorySystem?.GetNextFreeInventoryIndex() ?? 0;
        }
        
        private void RebuildEquipmentCache()
        {
            _equipmentCache.Clear();
            
            foreach (var kvp in _equippedItems)
            {
                var item = _inventorySystem?.GetItemByInstanceID(kvp.Value);
                if (item != null)
                    _equipmentCache[kvp.Key] = item;
            }
        }
        
        #endregion
        
        #region Network Callbacks
        
        private void OnEquipmentChanged(SyncDictionaryOperation op, EquipmentSlotType key, string value, bool asServer)
        {
            if (asServer)
                return;
            
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
        
        [ContextMenu("Log Equipment State")]
        public void LogEquipmentState()
        {
            Debug.Log($"=== Equipment ({_equippedItems.Count} items) ===");
            
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
