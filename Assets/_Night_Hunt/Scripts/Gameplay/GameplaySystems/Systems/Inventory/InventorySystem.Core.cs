using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameplaySystems.Core.Interfaces;
using GameplaySystems.Core.Configs;
using GameplaySystems.Core.Data;
using GameplaySystems.Stat;

namespace GameplaySystems.Inventory
{
    /// <summary>
    /// Main inventory system - NetworkBehaviour
    /// Manages player inventory with server authority
    /// 
    /// Design:
    /// - List-based storage (NO null values)
    /// - Index-based positioning (can have gaps)
    /// - Server-authoritative with SyncList
    /// - Auto-stacks configurable
    /// - Weight updates PlayerStatSystem
    /// 
    /// Usage:
    /// - Access via: player.GetComponent<IInventorySystem>()
    /// - All operations server-side, auto-syncs to clients
    /// </summary>
    public class InventorySystem : NetworkBehaviour, IInventorySystem
    {
        #region Serialized Fields
        
        [Header("Configuration")]
        [SerializeField] private GameplayConfig _gameplayConfig;
        [SerializeField] private InventoryConfig _inventoryConfig;
        
        [Header("References")]
        [SerializeField] private PlayerStatSystem _statSystem;
        
        [Header("Debug")]
        [SerializeField] private bool _showDebugUI = false;
        [SerializeField] private bool _enableDebugLogs = false;
        
        #endregion
        
        #region Network Synced Data
        
        /// <summary>
        /// Network-synced item data
        /// Contains ONLY items with data (no nulls)
        /// </summary>
        private readonly SyncList<ItemInstanceData> _items = new SyncList<ItemInstanceData>();
        
        #endregion
        
        #region Local Data
        
        /// <summary>
        /// Local cache for fast lookups (all clients)
        /// Rebuilt when sync data changes
        /// Key: InstanceID
        /// </summary>
        private Dictionary<string, ItemInstance> _itemCache = new Dictionary<string, ItemInstance>();
        
        #endregion
        
        #region Events
        
        public event Action<ItemInstance> OnItemAdded;
        public event Action<ItemInstance, int> OnItemRemoved;
        public event Action<ItemInstance, int, int> OnItemMoved;
        public event Action<ItemInstance, ItemInstance> OnItemsSwapped;
        public event Action<ItemInstance, ItemInstance, int> OnItemsStacked;
        public event Action OnInventoryCleared;
        
        #endregion
        
        #region NetworkBehaviour Lifecycle
        
        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            
            // Subscribe to sync events
            _items.OnChange += OnItemsChanged;
            
            if (!IsServerInitialized)
            {
                // Client: Build cache from synced data
                RebuildItemCache();
            }
        }
        
        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            
            // Unsubscribe
            _items.OnChange -= OnItemsChanged;
        }
        
        #endregion
        
        #region Initialization & Validation
        
        private void Awake()
        {
            ValidateReferences();
        }
        
        private void ValidateReferences()
        {
            // Auto-assign if not set (Editor only to avoid runtime GetComponent)
#if UNITY_EDITOR
            if (_statSystem == null)
            {
                _statSystem = GetComponent<PlayerStatSystem>();
            }
#endif
            
            if (_gameplayConfig == null)
            {
                Debug.LogError("[InventorySystem] GameplayConfig is null! Please assign in Inspector.");
            }
            
            if (_inventoryConfig == null)
            {
                Debug.LogError("[InventorySystem] InventoryConfig is null! Please assign in Inspector.");
            }
            
            if (_statSystem == null)
            {
                Debug.LogError("[InventorySystem] PlayerStatSystem is null! Please assign in Inspector.");
            }
        }
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_statSystem == null)
            {
                _statSystem = GetComponent<PlayerStatSystem>();
            }
        }
#endif
        
        #endregion
        
        #region IInventorySystem - Getters
        
        public IReadOnlyList<ItemInstance> GetAllItems()
        {
            return _itemCache.Values.ToList();
        }
        
        public ItemInstance GetItemAt(int index)
        {
            return _itemCache.Values.FirstOrDefault(i => i.InventoryIndex == index);
        }
        
        public ItemInstance GetItemByInstanceID(string instanceID)
        {
            if (string.IsNullOrEmpty(instanceID))
                return null;
            
            if (_itemCache.TryGetValue(instanceID, out var item))
                return item;
            
            return null;
        }
        
        public int GetItemCount(string itemDefinitionID)
        {
            return _itemCache.Values
                .Where(i => i.DefinitionID == itemDefinitionID)
                .Sum(i => i.Quantity);
        }
        
        public List<ItemInstance> GetItemsByDefinition(string itemDefinitionID)
        {
            return _itemCache.Values
                .Where(i => i.DefinitionID == itemDefinitionID)
                .ToList();
        }
        
        public bool HasItem(string itemDefinitionID, int minQuantity = 1)
        {
            return GetItemCount(itemDefinitionID) >= minQuantity;
        }
        
        public int GetMaxIndex()
        {
            if (_itemCache.Count == 0)
                return -1;
            
            return _itemCache.Values.Max(i => i.InventoryIndex);
        }
        
        #endregion
        
        #region IInventorySystem - Add Item
        
        public void AddItem(string itemDefinitionID, int quantity)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[InventorySystem] AddItem can only be called on server!");
                return;
            }
            
            AddItemServer(itemDefinitionID, quantity);
        }
        
        [Server]
        private void AddItemServer(string itemDefinitionID, int quantity)
        {
            var itemDef = ItemDatabase.GetDefinition(itemDefinitionID);
            if (itemDef == null)
            {
                Debug.LogError($"[InventorySystem] Item definition not found: {itemDefinitionID}");
                return;
            }
            
            if (quantity <= 0)
            {
                Debug.LogWarning($"[InventorySystem] Invalid quantity: {quantity}");
                return;
            }
            
            if (_enableDebugLogs)
            {
                Debug.Log($"[InventorySystem] Adding {quantity}x {itemDef.DisplayName}");
            }
            
            // Try to stack with existing items if stackable
            if (itemDef.IsStackable && _inventoryConfig.AutoStackOnAdd)
            {
                quantity = TryStackWithExisting(itemDef, quantity);
                
                if (quantity <= 0)
                {
                    // Fully stacked
                    UpdateTotalWeight();
                    return;
                }
            }
            
            // Create new item instance(s)
            while (quantity > 0)
            {
                int stackSize = itemDef.IsStackable ? Mathf.Min(quantity, itemDef.MaxStackSize) : 1;
                
                var newItem = CreateItemInstance(itemDef, stackSize);
                
                _itemCache[newItem.InstanceID] = newItem;
                _items.Add(newItem.ToData());
                
                ItemDatabase.RegisterInstance(newItem);
                
                OnItemAdded?.Invoke(newItem);
                
                quantity -= stackSize;
                
                if (_enableDebugLogs)
                {
                    Debug.Log($"[InventorySystem] Created item: {newItem.InstanceID} x{stackSize} @ index {newItem.InventoryIndex}");
                }
            }
            
            // Update weight
            UpdateTotalWeight();
        }
        
        #endregion
        
        #region IInventorySystem - Remove Item
        
        public void RemoveItem(string instanceID, int quantity)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[InventorySystem] RemoveItem can only be called on server!");
                return;
            }
            
            RemoveItemServer(instanceID, quantity);
        }
        
        [Server]
        private void RemoveItemServer(string instanceID, int quantity)
        {
            if (!_itemCache.TryGetValue(instanceID, out var item))
            {
                Debug.LogWarning($"[InventorySystem] Item not found: {instanceID}");
                return;
            }
            
            if (quantity <= 0)
            {
                Debug.LogWarning($"[InventorySystem] Invalid quantity: {quantity}");
                return;
            }
            
            if (quantity >= item.Quantity)
            {
                // Remove entire item
                int removedQty = item.Quantity;
                RemoveItemCompletely(item);
                OnItemRemoved?.Invoke(item, removedQty);
                
                if (_enableDebugLogs)
                {
                    var def = ItemDatabase.GetDefinition(item.DefinitionID);
                    Debug.Log($"[InventorySystem] Removed {removedQty}x {def?.DisplayName} (entire stack)");
                }
            }
            else
            {
                // Remove partial quantity
                item.Quantity -= quantity;
                UpdateItemData(item);
                OnItemRemoved?.Invoke(item, quantity);
                
                if (_enableDebugLogs)
                {
                    var def = ItemDatabase.GetDefinition(item.DefinitionID);
                    Debug.Log($"[InventorySystem] Removed {quantity}x {def?.DisplayName} (remaining: {item.Quantity})");
                }
            }
            
            UpdateTotalWeight();
        }
        
        public void RemoveItemByDefinition(string itemDefinitionID, int quantity)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[InventorySystem] RemoveItemByDefinition can only be called on server!");
                return;
            }
            
            RemoveItemByDefinitionServer(itemDefinitionID, quantity);
        }
        
        [Server]
        private void RemoveItemByDefinitionServer(string itemDefinitionID, int quantity)
        {
            var items = GetItemsByDefinition(itemDefinitionID)
                .OrderBy(i => i.CreatedTimestamp) // Remove oldest first
                .ToList();
            
            int remaining = quantity;
            
            foreach (var item in items)
            {
                if (remaining <= 0)
                    break;
                
                int toRemove = Mathf.Min(remaining, item.Quantity);
                
                if (toRemove >= item.Quantity)
                {
                    RemoveItemCompletely(item);
                }
                else
                {
                    item.Quantity -= toRemove;
                    UpdateItemData(item);
                }
                
                OnItemRemoved?.Invoke(item, toRemove);
                remaining -= toRemove;
            }
            
            UpdateTotalWeight();
            
            if (_enableDebugLogs)
            {
                Debug.Log($"[InventorySystem] Removed {quantity - remaining}/{quantity} of {itemDefinitionID}");
            }
        }
        
        public void DropItem(string instanceID, int quantity)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[InventorySystem] DropItem can only be called on server!");
                return;
            }
            
            DropItemServer(instanceID, quantity);
        }
        
        [Server]
        private void DropItemServer(string instanceID, int quantity)
        {
            // TODO: Implement drop logic - spawn item in world
            // For now, just remove from inventory
            RemoveItemServer(instanceID, quantity);
            
            if (_enableDebugLogs)
            {
                Debug.Log($"[InventorySystem] Dropped item (TODO: spawn in world)");
            }
        }
        
        public void ClearInventory()
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[InventorySystem] ClearInventory can only be called on server!");
                return;
            }
            
            ClearInventoryServer();
        }
        
        [Server]
        private void ClearInventoryServer()
        {
            foreach (var item in _itemCache.Values.ToList())
            {
                RemoveItemCompletely(item);
            }
            
            UpdateTotalWeight();
            OnInventoryCleared?.Invoke();
            
            if (_enableDebugLogs)
            {
                Debug.Log("[InventorySystem] Cleared all items");
            }
        }
        
        #endregion
        
        #region IInventorySystem - Move/Swap
        
        public void MoveItem(string instanceID, int targetIndex)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[InventorySystem] MoveItem can only be called on server!");
                return;
            }
            
            MoveItemServer(instanceID, targetIndex);
        }
        
        [Server]
        private void MoveItemServer(string instanceID, int targetIndex)
        {
            if (!_itemCache.TryGetValue(instanceID, out var item))
            {
                Debug.LogWarning($"[InventorySystem] Item not found: {instanceID}");
                return;
            }
            
            int oldIndex = item.InventoryIndex;
            
            // Check if target index has an item
            var targetItem = GetItemAt(targetIndex);
            
            if (targetItem != null && targetItem.InstanceID != instanceID)
            {
                // Try to stack if same item
                if (_inventoryConfig.AutoMergeOnMove && CanStackWith(item, targetItem))
                {
                    StackItemsServer(targetItem.InstanceID, instanceID);
                    return;
                }
                
                // Swap items
                item.InventoryIndex = targetIndex;
                targetItem.InventoryIndex = oldIndex;
                
                UpdateItemData(item);
                UpdateItemData(targetItem);
                
                OnItemsSwapped?.Invoke(item, targetItem);
                
                if (_enableDebugLogs)
                {
                    Debug.Log($"[InventorySystem] Swapped items at {oldIndex} <-> {targetIndex}");
                }
            }
            else
            {
                // Simple move
                item.InventoryIndex = targetIndex;
                UpdateItemData(item);
                
                OnItemMoved?.Invoke(item, oldIndex, targetIndex);
                
                if (_enableDebugLogs)
                {
                    Debug.Log($"[InventorySystem] Moved item from {oldIndex} to {targetIndex}");
                }
            }
        }
        
        public void SwapItems(string instanceID1, string instanceID2)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[InventorySystem] SwapItems can only be called on server!");
                return;
            }
            
            SwapItemsServer(instanceID1, instanceID2);
        }
        
        [Server]
        private void SwapItemsServer(string instanceID1, string instanceID2)
        {
            if (!_itemCache.TryGetValue(instanceID1, out var item1))
            {
                Debug.LogWarning($"[InventorySystem] Item 1 not found: {instanceID1}");
                return;
            }
            
            if (!_itemCache.TryGetValue(instanceID2, out var item2))
            {
                Debug.LogWarning($"[InventorySystem] Item 2 not found: {instanceID2}");
                return;
            }
            
            // Swap indices
            int temp = item1.InventoryIndex;
            item1.InventoryIndex = item2.InventoryIndex;
            item2.InventoryIndex = temp;
            
            UpdateItemData(item1);
            UpdateItemData(item2);
            
            OnItemsSwapped?.Invoke(item1, item2);
            
            if (_enableDebugLogs)
            {
                Debug.Log($"[InventorySystem] Swapped items");
            }
        }
        
        #endregion
        
        #region IInventorySystem - Stack Operations
        
        public bool CanStackWith(ItemInstance item1, ItemInstance item2)
        {
            if (item1 == null || item2 == null)
                return false;
            
            if (item1.InstanceID == item2.InstanceID)
                return false;
            
            if (item1.DefinitionID != item2.DefinitionID)
                return false;
            
            var itemDef = ItemDatabase.GetDefinition(item1.DefinitionID);
            if (itemDef == null || !itemDef.IsStackable)
                return false;
            
            // Check if either item can accept more
            if (item1.Quantity >= itemDef.MaxStackSize && item2.Quantity >= itemDef.MaxStackSize)
                return false;
            
            return true;
        }
        
        public void StackItems(string targetInstanceID, string sourceInstanceID)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[InventorySystem] StackItems can only be called on server!");
                return;
            }
            
            StackItemsServer(targetInstanceID, sourceInstanceID);
        }
        
        [Server]
        private void StackItemsServer(string targetInstanceID, string sourceInstanceID)
        {
            if (!_itemCache.TryGetValue(targetInstanceID, out var targetItem))
            {
                Debug.LogWarning($"[InventorySystem] Target item not found: {targetInstanceID}");
                return;
            }
            
            if (!_itemCache.TryGetValue(sourceInstanceID, out var sourceItem))
            {
                Debug.LogWarning($"[InventorySystem] Source item not found: {sourceInstanceID}");
                return;
            }
            
            if (!CanStackWith(targetItem, sourceItem))
            {
                Debug.LogWarning("[InventorySystem] Items cannot be stacked");
                return;
            }
            
            var itemDef = ItemDatabase.GetDefinition(targetItem.DefinitionID);
            
            // Calculate how much can be stacked
            int availableSpace = itemDef.MaxStackSize - targetItem.Quantity;
            int amountToStack = Mathf.Min(availableSpace, sourceItem.Quantity);
            
            // Update quantities
            targetItem.Quantity += amountToStack;
            sourceItem.Quantity -= amountToStack;
            
            UpdateItemData(targetItem);
            
            if (sourceItem.Quantity <= 0)
            {
                // Remove source if empty
                RemoveItemCompletely(sourceItem);
            }
            else
            {
                UpdateItemData(sourceItem);
            }
            
            OnItemsStacked?.Invoke(targetItem, sourceItem, amountToStack);
            
            if (_enableDebugLogs)
            {
                Debug.Log($"[InventorySystem] Stacked {amountToStack}x {itemDef.DisplayName}");
            }
        }
        
        public void SplitStack(string instanceID, int splitQuantity)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[InventorySystem] SplitStack can only be called on server!");
                return;
            }
            
            SplitStackServer(instanceID, splitQuantity);
        }
        
        [Server]
        private void SplitStackServer(string instanceID, int splitQuantity)
        {
            if (!_itemCache.TryGetValue(instanceID, out var item))
            {
                Debug.LogWarning($"[InventorySystem] Item not found: {instanceID}");
                return;
            }
            
            if (splitQuantity <= 0 || splitQuantity >= item.Quantity)
            {
                Debug.LogWarning($"[InventorySystem] Invalid split quantity: {splitQuantity}");
                return;
            }
            
            var itemDef = ItemDatabase.GetDefinition(item.DefinitionID);
            if (itemDef == null || !itemDef.IsStackable)
            {
                Debug.LogWarning("[InventorySystem] Item is not stackable");
                return;
            }
            
            // Create new stack
            var newItem = new ItemInstance(item.DefinitionID, splitQuantity, GetNextAvailableIndex())
            {
                CurrentResource = item.CurrentResource,
                CurrentMagazine = item.CurrentMagazine,
                CustomData = item.CustomData
            };
            
            // Reduce original stack
            item.Quantity -= splitQuantity;
            
            _itemCache[newItem.InstanceID] = newItem;
            _items.Add(newItem.ToData());
            UpdateItemData(item);
            
            ItemDatabase.RegisterInstance(newItem);
            
            OnItemAdded?.Invoke(newItem);
            
            if (_enableDebugLogs)
            {
                Debug.Log($"[InventorySystem] Split stack: {item.Quantity} + {splitQuantity}");
            }
        }
        
        #endregion
        
        #region IInventorySystem - Weight
        
        public float CalculateTotalWeight()
        {
            float total = 0f;
            
            foreach (var item in _itemCache.Values)
            {
                var itemDef = ItemDatabase.GetDefinition(item.DefinitionID);
                if (itemDef == null)
                    continue;
                
                total += itemDef.GetTotalWeight(item.Quantity);
            }
            
            return total;
        }
        
        public (float current, float capacity, float percent) GetWeightInfo()
        {
            float current = CalculateTotalWeight();
            float capacity = _statSystem != null ? _statSystem.GetWeightCapacity() : 100f;
            float percent = capacity > 0 ? current / capacity : 0f;
            
            return (current, capacity, percent);
        }
        
        #endregion
        
        #region Helper Methods (Server Only)
        
        [Server]
        private ItemInstance CreateItemInstance(ItemDefinition itemDef, int quantity)
        {
            var newItem = new ItemInstance(itemDef.ItemID, quantity, GetNextAvailableIndex());
            
            newItem.CurrentResource = itemDef.GetDefaultResource();
            
            if (itemDef is WeaponDefinition weaponDef)
            {
                newItem.CurrentMagazine = weaponDef.MagazineSize;
            }
            
            if (itemDef.AttachmentSlots != null && itemDef.AttachmentSlots.Length > 0)
            {
                newItem.AttachedItems = new string[itemDef.AttachmentSlots.Length];
            }
            
            return newItem;
        }
        
        [Server]
        private int TryStackWithExisting(ItemDefinition itemDef, int quantity)
        {
            if (!itemDef.IsStackable)
                return quantity;
            
            var existingItems = GetItemsByDefinition(itemDef.ItemID)
                .Where(i => i.Quantity < itemDef.MaxStackSize)
                .OrderBy(i => i.CreatedTimestamp)
                .ToList();
            
            foreach (var item in existingItems)
            {
                if (quantity <= 0)
                    break;
                
                int availableSpace = itemDef.MaxStackSize - item.Quantity;
                int amountToAdd = Mathf.Min(availableSpace, quantity);
                
                item.Quantity += amountToAdd;
                UpdateItemData(item);
                
                quantity -= amountToAdd;
                
                if (_enableDebugLogs)
                {
                    Debug.Log($"[InventorySystem] Stacked {amountToAdd}x {itemDef.DisplayName} (now {item.Quantity})");
                }
            }
            
            return quantity;
        }
        
        [Server]
        private int GetNextAvailableIndex()
        {
            if (_itemCache.Count == 0)
                return 0;
            
            var indices = _itemCache.Values
                .Select(i => i.InventoryIndex)
                .OrderBy(i => i)
                .ToList();
            
            for (int i = 0; i < indices.Count; i++)
            {
                if (indices[i] != i)
                    return i;
            }
            
            return indices.Max() + 1;
        }
        
        [Server]
        private void UpdateItemData(ItemInstance item)
        {
            int index = FindItemSyncIndex(item.InstanceID);
            if (index >= 0)
            {
                _items[index] = item.ToData();
            }
        }
        
        [Server]
        private void RemoveItemCompletely(ItemInstance item)
        {
            if (string.IsNullOrEmpty(item.InstanceID))
            {
                Debug.LogWarning("[InventorySystem] Attempted to remove item with null/empty InstanceID");
                return;
            }
            
            _itemCache.Remove(item.InstanceID);
            
            int index = FindItemSyncIndex(item.InstanceID);
            if (index >= 0)
            {
                _items.RemoveAt(index);
            }
            
            ItemDatabase.UnregisterInstance(item.InstanceID);
        }
        
        private int FindItemSyncIndex(string instanceID)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].InstanceID == instanceID)
                    return i;
            }
            return -1;
        }
        
        [Server]
        private void UpdateTotalWeight()
        {
            if (_statSystem == null)
                return;
            
            float totalWeight = CalculateTotalWeight();
            
            var weightModifier = StatModifier.CreateFlat(
                "Inventory",
                totalWeight,
                priority: 0,
                description: "Total inventory weight"
            );
            
            _statSystem.AddModifier(PlayerStatType.CurrentWeight, weightModifier);
        }
        
        #endregion
        
        #region Network Callbacks
        
        private void OnItemsChanged(SyncListOperation op, int index, ItemInstanceData oldValue, ItemInstanceData newValue, bool asServer)
        {
            if (asServer)
                return;
            
            switch (op)
            {
                case SyncListOperation.Add:
                    var newItem = newValue.ToInstance();
                    if (!string.IsNullOrEmpty(newItem.InstanceID))
                    {
                        _itemCache[newItem.InstanceID] = newItem;
                        ItemDatabase.RegisterInstance(newItem);
                        OnItemAdded?.Invoke(newItem);
                    }
                    break;
                
                case SyncListOperation.RemoveAt:
                    var removedItem = oldValue.ToInstance();
                    if (!string.IsNullOrEmpty(removedItem.InstanceID))
                    {
                        _itemCache.Remove(removedItem.InstanceID);
                        ItemDatabase.UnregisterInstance(removedItem.InstanceID);
                    }
                    break;
                
                case SyncListOperation.Set:
                    var updatedItem = newValue.ToInstance();
                    if (!string.IsNullOrEmpty(updatedItem.InstanceID))
                    {
                        _itemCache[updatedItem.InstanceID] = updatedItem;
                    }
                    break;
                
                case SyncListOperation.Clear:
                    _itemCache.Clear();
                    OnInventoryCleared?.Invoke();
                    break;
            }
        }
        
        private void RebuildItemCache()
        {
            _itemCache.Clear();
            
            foreach (var itemData in _items)
            {
                var item = itemData.ToInstance();
                if (!string.IsNullOrEmpty(item.InstanceID))
                {
                    _itemCache[item.InstanceID] = item;
                    ItemDatabase.RegisterInstance(item);
                }
            }
        }
        
        #endregion
        
        #region Debug
        
        private void OnGUI()
        {
            if (!_showDebugUI || !IsOwner)
                return;
            
            GUILayout.BeginArea(new Rect(10, 400, 450, 400));
            GUILayout.Label("=== INVENTORY ===");
            
            var weightInfo = GetWeightInfo();
            GUILayout.Label($"Weight: {weightInfo.current:F1} / {weightInfo.capacity:F1} ({weightInfo.percent:P0})");
            GUILayout.Label($"Items: {_itemCache.Count}");
            
            GUILayout.Space(10);
            
            foreach (var item in _itemCache.Values.OrderBy(i => i.InventoryIndex))
            {
                var def = ItemDatabase.GetDefinition(item.DefinitionID);
                string name = def != null ? def.DisplayName : item.DefinitionID;
                
                string resourceInfo = "";
                if (item.CurrentResource > 0)
                {
                    resourceInfo = $" [{item.CurrentResource:F0}";
                    if (item.CurrentMagazine > 0)
                        resourceInfo += $"/{item.CurrentMagazine}";
                    resourceInfo += "]";
                }
                
                GUILayout.Label($"[{item.InventoryIndex}] {name} x{item.Quantity}{resourceInfo}");
            }
            
            GUILayout.EndArea();
        }
        
        [ContextMenu("Log Inventory State")]
        public void LogInventoryState()
        {
            Debug.Log($"=== Inventory State ({_itemCache.Count} items) ===");
            
            foreach (var item in _itemCache.Values.OrderBy(i => i.InventoryIndex))
            {
                var def = ItemDatabase.GetDefinition(item.DefinitionID);
                Debug.Log($"  [{item.InventoryIndex}] {def?.DisplayName} x{item.Quantity} (ID: {item.InstanceID})");
            }
            
            var weightInfo = GetWeightInfo();
            Debug.Log($"  Weight: {weightInfo.current:F1} / {weightInfo.capacity:F1}");
        }
        
        #endregion
    }
}   