using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;
using System.Collections.Generic;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.GameplaySystems.Loot;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.Equipment
{
    /// <summary>
    /// Server-authoritative equipment slot manager for a networked player.
    ///
    /// Manages body-slot equipment (head, chest, back, legs, etc.).
    /// Slot mutations are replicated via SyncDictionary; stat application is
    /// handled externally by <see cref="NightHunt.GameplaySystems.Stat.StatApplyOrchestrator"/>.
    ///
    /// FLOW:
    ///   Equip:   item in inventory → mark InventoryIndex = -1 → add to SyncDict
    ///   Unequip: remove from SyncDict → assign free inventory index → SyncItemState
    ///   Drop:    detach attachments (per config) → spawn world item → remove from SyncDict
    /// </summary>
    public class EquipmentSystem : NetworkBehaviour, IEquipmentSystem, IDisposable
    {
        #region Serialized Fields

        [Header("Configuration")]
        [SerializeField] private InventoryConfig _inventoryConfig;

        [Header("References")]
        [SerializeField] private InventorySystem _inventorySystemComponent;
        private IInventorySystem _inventorySystem;
        private IAttachmentSystem _attachmentSystem;

        [Header("Debug")]
        [SerializeField] private bool _enableDebugLogs = false;

        #endregion

        #region Network Synced Data

        private readonly SyncDictionary<EquipmentSlotType, string> _equippedItems =
            new SyncDictionary<EquipmentSlotType, string>();

        #endregion

        #region Local Cache

        private Dictionary<EquipmentSlotType, ItemInstance> _equipmentCache =
            new Dictionary<EquipmentSlotType, ItemInstance>();

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

        #region IDisposable

        public void Dispose()
        {
            _equippedItems.OnChange -= OnEquipmentChanged;
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
            _inventorySystem = ComponentResolver.Find<IInventorySystem>(this)
                .UseExisting(_inventorySystemComponent)
                .OnSelf().InChildren().InParent().InRootChildren()
                .OrLogWarning("[EquipmentSystem] IInventorySystem not found")
                .Resolve();

            if (_inventorySystem is InventorySystem invConcrete)
                _inventorySystemComponent = invConcrete;

            _attachmentSystem = ComponentResolver.Find<IAttachmentSystem>(this)
                .OnSelf().InChildren().InParent().InRootChildren()
                .OrLogWarning("[EquipmentSystem] IAttachmentSystem not found — DetachAttachmentsOnUnequip will be skipped")
                .Resolve();

            if (_inventoryConfig == null)
                Debug.LogWarning("[EquipmentSystem] InventoryConfig is null!");

            if (_inventorySystem == null)
                Debug.LogWarning("[EquipmentSystem] IInventorySystem is null!");
        }

#if UNITY_EDITOR
        [ContextMenu("Validate References")]
        protected override void OnValidate()
        {
            ValidateReferences();
        }
#endif

        #endregion

        #region IEquipmentSystem — Getters

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

            if (!(itemDef is EquipmentDefinition equipmentDef))
                return false;

            return equipmentDef.EquipmentSlot == slotType;
        }

        #endregion

        #region IEquipmentSystem — Equip / Unequip / Drop (public API with client→server routing)

        /// <summary>Equip an item from inventory. Owning client routes to the server automatically.</summary>
        public void EquipItem(string instanceID)
        {
            if (IsServerInitialized) { EquipItemServer(instanceID); return; }
            if (IsOwner) EquipItemServerRpc(instanceID);
        }

        [ServerRpc(RequireOwnership = true)]
        private void EquipItemServerRpc(string instanceID) => EquipItemServer(instanceID);

        /// <summary>Unequip an item and return it to inventory. Owning client routes to the server automatically.</summary>
        public void UnequipItem(EquipmentSlotType slotType)
        {
            if (IsServerInitialized) { UnequipItemServer(slotType); return; }
            if (IsOwner) UnequipItemServerRpc(slotType);
        }

        [ServerRpc(RequireOwnership = true)]
        private void UnequipItemServerRpc(EquipmentSlotType slotType) => UnequipItemServer(slotType);

        /// <summary>Swap two equipment slots. Owning client routes to the server automatically.</summary>
        public void SwapEquipment(EquipmentSlotType slot1, EquipmentSlotType slot2)
        {
            if (IsServerInitialized) { SwapEquipmentServer(slot1, slot2); return; }
            if (IsOwner) SwapEquipmentServerRpc(slot1, slot2);
            else Debug.LogWarning("[EquipmentSystem] SwapEquipment: caller does not own this object.");
        }

        [ServerRpc(RequireOwnership = true)]
        private void SwapEquipmentServerRpc(EquipmentSlotType slot1, EquipmentSlotType slot2) => SwapEquipmentServer(slot1, slot2);

        /// <summary>
        /// Drop an equipped item directly to the world without returning it to inventory.
        /// Detaches attachments first (per config). Owning client routes to the server automatically.
        /// </summary>
        public void DropEquippedItem(EquipmentSlotType slotType)
        {
            if (IsServerInitialized) { DropEquippedItemServer(slotType); return; }
            if (IsOwner) DropEquippedItemServerRpc(slotType);
            else Debug.LogWarning("[EquipmentSystem] DropEquippedItem: caller does not own this object.");
        }

        [ServerRpc(RequireOwnership = true)]
        private void DropEquippedItemServerRpc(EquipmentSlotType slotType) => DropEquippedItemServer(slotType);

        #endregion

        #region Server Implementations

        [Server]
        private void EquipItemServer(string instanceID)
        {
            var item = _inventorySystem.GetItemByInstanceID(instanceID);
            if (item == null)
            {
                Debug.LogWarning($"[EquipmentSystem] EquipItem: item not found '{instanceID}'");
                return;
            }

            var itemDef = ItemDatabase.GetDefinition(item.DefinitionID);
            if (!(itemDef is EquipmentDefinition equipmentDef))
            {
                Debug.LogWarning($"[EquipmentSystem] EquipItem: '{item.DefinitionID}' is not an EquipmentDefinition.");
                return;
            }

            var slotType = equipmentDef.EquipmentSlot;

            // Unequip any existing item in this slot first.
            if (_equippedItems.ContainsKey(slotType))
                UnequipItemServer(slotType);

            // Assign to slot.
            _equippedItems[slotType] = instanceID;
            item.InventoryIndex = -1; // Remove from inventory grid.
            _inventorySystem.SyncItemState(instanceID);

            // Update server-side cache (host mode: OnEquipmentChanged.asServer=true is skipped).
            _equipmentCache[slotType] = item;
            // Fire event so server-side listeners (StatApplyOrchestrator, host UI) are notified.
            OnItemEquipped?.Invoke(slotType, item);

            if (_enableDebugLogs)
                Debug.Log($"[EquipmentSystem] Equipped '{equipmentDef.DisplayName}' → {slotType}");
        }

        [Server]
        private void UnequipItemServer(EquipmentSlotType slotType)
        {
            if (!_equippedItems.TryGetValue(slotType, out var instanceID))
            {
                Debug.LogWarning($"[EquipmentSystem] UnequipItem: slot {slotType} is empty.");
                return;
            }

            var item = _inventorySystem.GetItemByInstanceID(instanceID);
            if (item == null)
            {
                Debug.LogWarning($"[EquipmentSystem] UnequipItem: item '{instanceID}' not found \u2014 removing stale slot entry.");
                _equippedItems.Remove(slotType);
                _equipmentCache.Remove(slotType);
                return;
            }

            var itemDef = ItemDatabase.GetDefinition(item.DefinitionID);
            if (!(itemDef is EquipmentDefinition equipmentDef))
            {
                Debug.LogWarning($"[EquipmentSystem] UnequipItem: '{item.DefinitionID}' is not an EquipmentDefinition \u2014 removing slot.");
                _equippedItems.Remove(slotType);
                _equipmentCache.Remove(slotType);
                return;
            }

            // Detach attachments according to config.
            if (_inventoryConfig != null && _inventoryConfig.DetachAttachmentsOnUnequip)
                _attachmentSystem?.DetachAllFromItem(instanceID);

            // Remove from equipment.
            _equippedItems.Remove(slotType);

            // Update server-side cache (host mode: OnEquipmentChanged.asServer=true is skipped).
            _equipmentCache.Remove(slotType);
            // Fire event so server-side listeners (StatApplyOrchestrator, host UI) are notified.
            OnItemUnequipped?.Invoke(slotType, item);

            // Return to the first available inventory slot.
            item.InventoryIndex = _inventorySystem.GetNextFreeInventoryIndex();
            _inventorySystem.SyncItemState(instanceID);

            if (_enableDebugLogs)
                Debug.Log($"[EquipmentSystem] Unequipped '{equipmentDef.DisplayName}' from {slotType}.");
        }

        [Server]
        private void SwapEquipmentServer(EquipmentSlotType slot1, EquipmentSlotType slot2)
        {
            bool hasItem1 = _equippedItems.TryGetValue(slot1, out var instanceID1);
            bool hasItem2 = _equippedItems.TryGetValue(slot2, out var instanceID2);

            if (!hasItem1 && !hasItem2)
            {
                Debug.LogWarning("[EquipmentSystem] SwapEquipment: both slots are empty.");
                return;
            }

            if (hasItem1 && hasItem2)
            {
                _equippedItems[slot1] = instanceID2;
                _equippedItems[slot2] = instanceID1;

                // Sync server-side cache — SyncDictionary callback skips asServer=true.
                var item1 = _inventorySystem?.GetItemByInstanceID(instanceID1);
                var item2 = _inventorySystem?.GetItemByInstanceID(instanceID2);
                if (item1 != null) _equipmentCache[slot2] = item1;
                if (item2 != null) _equipmentCache[slot1] = item2;
            }
            else if (hasItem1)
            {
                _equippedItems.Remove(slot1);
                _equippedItems[slot2] = instanceID1;

                var item1 = _inventorySystem?.GetItemByInstanceID(instanceID1);
                _equipmentCache.Remove(slot1);
                if (item1 != null) _equipmentCache[slot2] = item1;
            }
            else
            {
                _equippedItems.Remove(slot2);
                _equippedItems[slot1] = instanceID2;

                var item2 = _inventorySystem?.GetItemByInstanceID(instanceID2);
                _equipmentCache.Remove(slot2);
                if (item2 != null) _equipmentCache[slot1] = item2;
            }

            if (_enableDebugLogs)
                Debug.Log($"[EquipmentSystem] Swapped {slot1} ↔ {slot2}");
        }

        [Server]
        private void DropEquippedItemServer(EquipmentSlotType slotType)
        {
            if (!_equippedItems.TryGetValue(slotType, out var instanceID))
            {
                Debug.LogWarning($"[EquipmentSystem] DropEquippedItem: slot {slotType} is empty.");
                return;
            }

            var item = _inventorySystem.GetItemByInstanceID(instanceID);
            if (item == null)
            {
                Debug.LogWarning($"[EquipmentSystem] DropEquippedItem: item '{instanceID}' not found.");
                _equippedItems.Remove(slotType);
                return;
            }

            // Step 1: Detach attachments — return to inventory or keep on item (per config).
            bool returnAttachments = _inventoryConfig == null || _inventoryConfig.ReturnAttachmentsToInventoryOnDrop;
            if (item.AttachedItems != null && item.AttachedItems.Length > 0)
            {
                if (returnAttachments)
                    _attachmentSystem?.DetachAllFromItem(instanceID);
                // else: attachments remain on the item and drop with it
            }

            // Step 2: Strip empty attachment slot arrays to avoid ghost allocation on pickup.
            bool hasAnyAttachment = false;
            if (item.AttachedItems != null)
            {
                for (int i = 0; i < item.AttachedItems.Length; i++)
                {
                    if (!string.IsNullOrEmpty(item.AttachedItems[i]))
                    { hasAnyAttachment = true; break; }
                }
                if (!hasAnyAttachment)
                    item.AttachedItems = null;
            }

            // Step 3: Build world-item data snapshot.
            var dropData = item.ToData();

            // Step 4: Calculate drop position (slightly in front of the player).
            Transform origin = transform;
            Vector3 dropPos  = origin.position + origin.forward * (_inventoryConfig?.DropDistance ?? 2f);
            dropPos.y = origin.position.y;

            // Step 5: Spawn the world item.
            if (WorldSpawnManager.Instance != null)
                WorldSpawnManager.Instance.SpawnWorldItem(dropData, dropPos, Quaternion.identity);
            else
                Debug.LogError("[EquipmentSystem] DropEquippedItem: WorldSpawnManager.Instance is null — item not spawned.");

            // Step 6: Remove from equipment slot and clear server-side cache.
            _equippedItems.Remove(slotType);
            _equipmentCache.Remove(slotType);

            // Notify listeners (e.g. StatApplyOrchestrator) that the slot is now empty.
            OnItemUnequipped?.Invoke(slotType, item);

            // Step 7: Remove from inventory entirely (item now lives in the world).
            _inventorySystem.RemoveItem(instanceID, item.Quantity);

            if (_enableDebugLogs)
                Debug.Log($"[EquipmentSystem] Dropped '{item.DefinitionID}' from equipment slot {slotType}.");
        }

        #endregion

        #region Cache Management

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

        private void OnEquipmentChanged(SyncDictionaryOperation op, EquipmentSlotType key,
            string value, bool asServer)
        {
            // Server updates _equipmentCache directly in EquipItemServer / UnequipItemServer.
            // The SyncDictionary callback fires on all peers; skip the server to avoid
            // double-updating the cache and double-firing events.
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
            Debug.Log($"=== EquipmentSystem [{gameObject.name}] ({_equippedItems.Count} items) ===");

            foreach (var kvp in _equippedItems)
            {
                var item = _inventorySystem?.GetItemByInstanceID(kvp.Value);
                var def  = item != null ? ItemDatabase.GetDefinition(item.DefinitionID) : null;
                string displayName = def != null ? def.DisplayName : kvp.Value;
                Debug.Log($"  {kvp.Key}: {displayName}");
            }
        }

        #endregion
    }
}
