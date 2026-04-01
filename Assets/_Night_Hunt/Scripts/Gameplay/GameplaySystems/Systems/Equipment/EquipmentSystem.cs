using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;
using System.Collections.Generic;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Core.Base;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.Equipment
{
    /// <summary>
    /// Manages equipment slot assignment (head, body, legs, feet) for a networked player.
    /// All slot mutations are server-authoritative via SyncDictionary.
    /// </summary>
    public class EquipmentSystem : BaseNetworkGameplaySystem, IEquipmentSystem, IDisposable
    {
        #region Serialized Fields
        
        [Header("Configuration")]
        [SerializeField] private InventoryConfig _inventoryConfig;
        
        [Header("References")]
        [SerializeField] private InventorySystem _inventorySystemComponent;
        private IInventorySystem _inventorySystem;
        private IAttachmentSystem _attachmentSystem;
        
        private bool _enableDebugLogs => _debugConfig != null && _debugConfig.EnableWeaponDebugLogs;
        
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
        
        protected override void OnNetworkStarted()
        {
            _equippedItems.OnChange += OnEquipmentChanged;
            
            if (!IsServerInitialized)
                RebuildEquipmentCache();
        }
        
        protected override void OnNetworkStopped()
        {
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
        
        protected override void OnResolveReferences()
        {
            _inventorySystem = this.ResolveWithFallback<IInventorySystem>(_inventorySystemComponent,
                "[EquipmentSystem] IInventorySystem not found");

            _attachmentSystem = this.ResolveWithFallback<IAttachmentSystem>(null,
                "[EquipmentSystem] IAttachmentSystem not found — DetachAttachmentsOnUnequip will be skipped");

            if (_inventoryConfig == null)
                Debug.LogError("[EquipmentSystem] InventoryConfig is null!");
        }
        
#if UNITY_EDITOR
        [ContextMenu("Validate References")]
        protected override void OnValidate()
        {
            OnResolveReferences();
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
            if (IsServerInitialized) { EquipItemServer(instanceID); return; }
            if (IsOwner) EquipItemServerRpc(instanceID);
        }

        [ServerRpc(RequireOwnership = true)]
        private void EquipItemServerRpc(string instanceID) => EquipItemServer(instanceID);
        
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
                _attachmentSystem?.DetachAllFromItem(instanceID);
            
            // Remove from equipment
            _equippedItems.Remove(slotType);
            
            // Return to inventory
            item.InventoryIndex = _inventorySystem.GetNextFreeInventoryIndex();
            _inventorySystem.SyncItemState(instanceID);

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
        
        // Stat modifier application is handled entirely by StatApplyOrchestrator.
        
        #region Helper Methods

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
