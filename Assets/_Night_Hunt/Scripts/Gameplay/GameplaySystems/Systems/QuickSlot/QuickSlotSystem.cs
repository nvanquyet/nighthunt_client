using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;
using UnityEngine;
using GameplaySystems.Core.Interfaces;
using GameplaySystems.Core.Configs;
using GameplaySystems.Core.Data;

namespace GameplaySystems.Inventory
{
    /// <summary>
    /// Quick slot system - NetworkBehaviour
    /// Manages quick access slots (hotkeys 1-4)
    /// Only allows consumables and throwables
    /// Items stay in inventory, just referenced here
    /// </summary>
    public class QuickSlotSystem : NetworkBehaviour, IQuickSlotSystem
    {
        #region Serialized Fields
        
        [Header("Configuration")]
        [SerializeField] private InventoryConfig _inventoryConfig;
        
        [Header("References")]
        [SerializeField] private InventorySystem _inventorySystem;
        
        [Header("Debug")]
        [SerializeField] private bool _showDebugUI = false;
        [SerializeField] private bool _enableDebugLogs = false;
        
        #endregion
        
        #region Network Synced Data
        
        /// <summary>
        /// Quick slot item instance IDs
        /// Index corresponds to slot number (0-3)
        /// Empty string = empty slot
        /// </summary>
        private readonly SyncList<string> _quickSlots = new SyncList<string>();
        
        #endregion
        
        #region Events
        
        public event Action<int, ItemInstance> OnQuickSlotAssigned;
        public event Action<int> OnQuickSlotRemoved;
        public event Action<int, ItemInstance> OnQuickSlotUsed;
        
        #endregion
        
        #region NetworkBehaviour Lifecycle
        
        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            
            _quickSlots.OnChange += OnQuickSlotsChanged;
            
            if (IsServerInitialized)
            {
                InitializeQuickSlots();
            }
        }
        
        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            
            _quickSlots.OnChange -= OnQuickSlotsChanged;
        }
        
        #endregion
        
        #region Initialization
        
        private void Awake()
        {
#if UNITY_EDITOR
            if (_inventorySystem == null)
                _inventorySystem = GetComponent<InventorySystem>();
#endif
            
            if (_inventoryConfig == null)
                Debug.LogError("[QuickSlotSystem] InventoryConfig is null!");
            
            if (_inventorySystem == null)
                Debug.LogError("[QuickSlotSystem] InventorySystem is null!");
        }
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_inventorySystem == null)
                _inventorySystem = GetComponent<InventorySystem>();
        }
#endif
        
        [Server]
        private void InitializeQuickSlots()
        {
            int slotCount = _inventoryConfig != null ? _inventoryConfig.QuickSlotCount : 4;
            
            _quickSlots.Clear();
            for (int i = 0; i < slotCount; i++)
            {
                _quickSlots.Add(string.Empty);
            }
        }
        
        #endregion
        
        #region IQuickSlotSystem - Getters
        
        public ItemInstance GetQuickSlotItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _quickSlots.Count)
                return null;
            
            string instanceID = _quickSlots[slotIndex];
            if (string.IsNullOrEmpty(instanceID))
                return null;
            
            return _inventorySystem?.GetItemByInstanceID(instanceID);
        }
        
        public ItemInstance[] GetAllQuickSlots()
        {
            var slots = new ItemInstance[_quickSlots.Count];
            
            for (int i = 0; i < _quickSlots.Count; i++)
            {
                slots[i] = GetQuickSlotItem(i);
            }
            
            return slots;
        }
        
        public bool IsSlotOccupied(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _quickSlots.Count)
                return false;
            
            return !string.IsNullOrEmpty(_quickSlots[slotIndex]);
        }
        
        public bool CanPlaceInQuickSlot(string itemDefinitionID)
        {
            if (_inventoryConfig == null)
                return false;
            
            var itemDef = ItemDatabase.GetDefinition(itemDefinitionID);
            if (itemDef == null)
                return false;
            
            return _inventoryConfig.IsAllowedInQuickSlot(itemDef.Type);
        }
        
        public int GetQuickSlotCount()
        {
            return _quickSlots.Count;
        }
        
        #endregion
        
        #region IQuickSlotSystem - Assign/Remove
        
        public void AssignToQuickSlot(string instanceID, int slotIndex)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[QuickSlotSystem] AssignToQuickSlot can only be called on server!");
                return;
            }
            
            AssignToQuickSlotServer(instanceID, slotIndex);
        }
        
        [Server]
        private void AssignToQuickSlotServer(string instanceID, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _quickSlots.Count)
            {
                Debug.LogWarning($"[QuickSlotSystem] Invalid slot index: {slotIndex}");
                return;
            }
            
            var item = _inventorySystem.GetItemByInstanceID(instanceID);
            if (item == null)
            {
                Debug.LogWarning($"[QuickSlotSystem] Item not found: {instanceID}");
                return;
            }
            
            var itemDef = ItemDatabase.GetDefinition(item.DefinitionID);
            if (!CanPlaceInQuickSlot(item.DefinitionID))
            {
                Debug.LogWarning($"[QuickSlotSystem] Item type not allowed in quick slot: {itemDef?.Type}");
                return;
            }
            
            _quickSlots[slotIndex] = instanceID;
            
            if (_enableDebugLogs)
            {
                Debug.Log($"[QuickSlotSystem] Assigned {itemDef?.DisplayName} to slot {slotIndex}");
            }
        }
        
        public void RemoveFromQuickSlot(int slotIndex)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[QuickSlotSystem] RemoveFromQuickSlot can only be called on server!");
                return;
            }
            
            RemoveFromQuickSlotServer(slotIndex);
        }
        
        [Server]
        private void RemoveFromQuickSlotServer(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _quickSlots.Count)
            {
                Debug.LogWarning($"[QuickSlotSystem] Invalid slot index: {slotIndex}");
                return;
            }
            
            _quickSlots[slotIndex] = string.Empty;
            
            if (_enableDebugLogs)
            {
                Debug.Log($"[QuickSlotSystem] Removed item from slot {slotIndex}");
            }
        }
        
        public void SwapQuickSlots(int slotIndex1, int slotIndex2)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[QuickSlotSystem] SwapQuickSlots can only be called on server!");
                return;
            }
            
            SwapQuickSlotsServer(slotIndex1, slotIndex2);
        }
        
        [Server]
        private void SwapQuickSlotsServer(int slotIndex1, int slotIndex2)
        {
            if (slotIndex1 < 0 || slotIndex1 >= _quickSlots.Count ||
                slotIndex2 < 0 || slotIndex2 >= _quickSlots.Count)
            {
                Debug.LogWarning("[QuickSlotSystem] Invalid slot indices");
                return;
            }
            
            string temp = _quickSlots[slotIndex1];
            _quickSlots[slotIndex1] = _quickSlots[slotIndex2];
            _quickSlots[slotIndex2] = temp;
        }
        
        public void ClearAllQuickSlots()
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[QuickSlotSystem] ClearAllQuickSlots can only be called on server!");
                return;
            }
            
            ClearAllQuickSlotsServer();
        }
        
        [Server]
        private void ClearAllQuickSlotsServer()
        {
            for (int i = 0; i < _quickSlots.Count; i++)
            {
                _quickSlots[i] = string.Empty;
            }
        }
        
        #endregion
        
        #region IQuickSlotSystem - Usage
        
        public void UseQuickSlot(int slotIndex)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[QuickSlotSystem] UseQuickSlot can only be called on server!");
                return;
            }
            
            UseQuickSlotServer(slotIndex);
        }
        
        [Server]
        private void UseQuickSlotServer(int slotIndex)
        {
            if (!CanUseQuickSlot(slotIndex))
            {
                Debug.LogWarning($"[QuickSlotSystem] Cannot use quick slot: {slotIndex}");
                return;
            }
            
            string instanceID = _quickSlots[slotIndex];
            var item = _inventorySystem.GetItemByInstanceID(instanceID);
            
            if (item == null)
            {
                Debug.LogWarning($"[QuickSlotSystem] Item not found: {instanceID}");
                RemoveFromQuickSlotServer(slotIndex);
                return;
            }
            
            // TODO: Apply consumable effects
            // For now, just consume the item
            
            OnQuickSlotUsed?.Invoke(slotIndex, item);
            
            // Remove 1 quantity
            _inventorySystem.RemoveItem(instanceID, 1);
            
            // If item is gone, clear slot
            var updatedItem = _inventorySystem.GetItemByInstanceID(instanceID);
            if (updatedItem == null)
            {
                RemoveFromQuickSlotServer(slotIndex);
            }
            
            if (_enableDebugLogs)
            {
                var def = ItemDatabase.GetDefinition(item.DefinitionID);
                Debug.Log($"[QuickSlotSystem] Used {def?.DisplayName} from slot {slotIndex}");
            }
        }
        
        public bool CanUseQuickSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _quickSlots.Count)
                return false;
            
            string instanceID = _quickSlots[slotIndex];
            if (string.IsNullOrEmpty(instanceID))
                return false;
            
            var item = _inventorySystem?.GetItemByInstanceID(instanceID);
            return item != null && item.Quantity > 0;
        }
        
        #endregion
        
        #region Network Callbacks
        
        private void OnQuickSlotsChanged(SyncListOperation op, int index, string oldValue, string newValue, bool asServer)
        {
            if (asServer)
                return;
            
            switch (op)
            {
                case SyncListOperation.Set:
                    if (!string.IsNullOrEmpty(newValue))
                    {
                        var item = _inventorySystem?.GetItemByInstanceID(newValue);
                        if (item != null)
                        {
                            OnQuickSlotAssigned?.Invoke(index, item);
                        }
                    }
                    else
                    {
                        OnQuickSlotRemoved?.Invoke(index);
                    }
                    break;
            }
        }
        
        #endregion
        
        #region Debug
        
        private void OnGUI()
        {
            if (!_showDebugUI || !IsOwner)
                return;
            
            GUILayout.BeginArea(new Rect(10, 810, 400, 100));
            GUILayout.Label("=== QUICK SLOTS ===");
            
            string slotsStr = "";
            for (int i = 0; i < _quickSlots.Count; i++)
            {
                var item = GetQuickSlotItem(i);
                if (item != null)
                {
                    var def = ItemDatabase.GetDefinition(item.DefinitionID);
                    slotsStr += $"[{i+1}] {def?.DisplayName} x{item.Quantity}  ";
                }
                else
                {
                    slotsStr += $"[{i+1}] Empty  ";
                }
            }
            
            GUILayout.Label(slotsStr);
            GUILayout.EndArea();
        }
        
        #endregion
    }
}