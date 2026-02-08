using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Systems;
using System.Collections.Generic;
using NightHunt.Inventory.Database;

namespace NightHunt.Inventory.Network
{
    /// <summary>
    /// Network synchronization for EquipmentSystem.
    /// Syncs equipped items and visual representation across network.
    /// </summary>
    public class EquipmentNetworkSync : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private EquipmentSystem equipmentSystem;
        [SerializeField] private InventoryNetworkSync inventorySync;
        
        [Header("Visual Sync")]
        [SerializeField] private Transform[] equipmentModelSlots; // Visual slots for equipped items
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        // Synced equipment state
        private readonly SyncDictionary<EquipmentSlotType, ItemInstanceData> syncedEquipment = 
            new SyncDictionary<EquipmentSlotType, ItemInstanceData>();
        
        // Visual models
        private Dictionary<EquipmentSlotType, GameObject> spawnedModels = new Dictionary<EquipmentSlotType, GameObject>();
        
        // === Lifecycle ===
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Subscribe to equipment events
            EquipmentEvents.OnItemEquipped += OnItemEquipped_Server;
            EquipmentEvents.OnItemUnequipped += OnItemUnequipped_Server;
            EquipmentEvents.OnEquipmentSwapped += OnEquipmentSwapped_Server;
            
            Log("Server started - equipment sync enabled");
        }
        
        public override void OnStopServer()
        {
            base.OnStopServer();
            
            EquipmentEvents.OnItemEquipped -= OnItemEquipped_Server;
            EquipmentEvents.OnItemUnequipped -= OnItemUnequipped_Server;
            EquipmentEvents.OnEquipmentSwapped -= OnEquipmentSwapped_Server;
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            // Subscribe to SyncDictionary changes
            syncedEquipment.OnChange += OnSyncedEquipmentChanged_Client;
            
            Log("Client started - listening for equipment updates");
        }
        
        public override void OnStopClient()
        {
            base.OnStopClient();
            
            syncedEquipment.OnChange -= OnSyncedEquipmentChanged_Client;
        }
        
        // === SERVER EVENT HANDLERS ===
        
        private void OnItemEquipped_Server(ItemInstance item, EquipmentSlotType slotType)
        {
            if (!IsServer)
                return;
            
            syncedEquipment[slotType] = item.Serialize();
            
            Log($"[SERVER] Equipment synced: {item.Definition.DisplayName} in {slotType}");
        }
        
        private void OnItemUnequipped_Server(ItemInstance item, EquipmentSlotType slotType)
        {
            if (!IsServer)
                return;
            
            syncedEquipment.Remove(slotType);
            
            Log($"[SERVER] Equipment removed: {slotType}");
        }
        
        private void OnEquipmentSwapped_Server(ItemInstance oldItem, ItemInstance newItem, EquipmentSlotType slotType)
        {
            if (!IsServer)
                return;
            
            syncedEquipment[slotType] = newItem.Serialize();
            
            Log($"[SERVER] Equipment swapped in {slotType}");
        }
        
        // === CLIENT SYNC ===
        
        private void OnSyncedEquipmentChanged_Client(SyncDictionaryOperation op, EquipmentSlotType key, ItemInstanceData value, bool asServer)
        {
            if (asServer)
                return;
            
            Log($"[CLIENT] Equipment sync: {op} for {key}");
            
            switch (op)
            {
                case SyncDictionaryOperation.Add:
                case SyncDictionaryOperation.Set:
                    UpdateEquipmentVisual(key, value);
                    break;
                
                case SyncDictionaryOperation.Remove:
                    RemoveEquipmentVisual(key);
                    break;
                
                case SyncDictionaryOperation.Clear:
                    ClearAllEquipmentVisuals();
                    break;
            }
        }
        
        // === VISUAL SYNC ===
        
        /// <summary>
        /// Update visual equipment model.
        /// </summary>
        private void UpdateEquipmentVisual(EquipmentSlotType slotType, ItemInstanceData data)
        {
            // Remove old model
            RemoveEquipmentVisual(slotType);
            
            // Get item definition
            var definition = GetItemDefinition(data.ItemId);
            if (definition == null || definition.EquippedModelPrefab == null)
            {
                LogWarning($"No equipped model for {data.ItemId}");
                return;
            }
            
            // Get parent transform for this slot
            Transform parent = GetEquipmentSlot(slotType);
            if (parent == null)
            {
                LogWarning($"No visual slot for {slotType}");
                return;
            }
            
            // Spawn model
            GameObject model = Instantiate(definition.EquippedModelPrefab, parent);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            
            spawnedModels[slotType] = model;
            
            Log($"Spawned equipment visual: {definition.DisplayName} in {slotType}");
        }
        
        /// <summary>
        /// Remove visual equipment model.
        /// </summary>
        private void RemoveEquipmentVisual(EquipmentSlotType slotType)
        {
            if (spawnedModels.ContainsKey(slotType))
            {
                Destroy(spawnedModels[slotType]);
                spawnedModels.Remove(slotType);
            }
        }
        
        /// <summary>
        /// Clear all equipment visuals.
        /// </summary>
        private void ClearAllEquipmentVisuals()
        {
            foreach (var model in spawnedModels.Values)
            {
                if (model != null)
                    Destroy(model);
            }
            
            spawnedModels.Clear();
        }
        
        /// <summary>
        /// Get transform for equipment slot.
        /// </summary>
        private Transform GetEquipmentSlot(EquipmentSlotType slotType)
        {
            if (equipmentModelSlots == null || equipmentModelSlots.Length == 0)
                return transform;
            
            int index = (int)slotType;
            if (index >= 0 && index < equipmentModelSlots.Length)
                return equipmentModelSlots[index];
            
            return transform;
        }
        
        // === SERVER RPCs ===
        
        [ServerRpc(RequireOwnership = true)]
        public void RequestEquipItem_ServerRpc(string instanceId, EquipmentSlotType slotType, NetworkConnection conn = null)
        {
            // Get item from inventory
            var item = inventorySync.Inventory.FindItem(instanceId);
            if (item == null)
            {
                LogWarning($"[SERVER] Item not found: {instanceId}");
                return;
            }
            
            // Equip
            var result = equipmentSystem.EquipItem(item, slotType);
            
            if (result == OperationResult.Success)
            {
                // Remove from inventory
                inventorySync.Inventory.RemoveItem(instanceId);
            }
            
            ConfirmEquipOperation_TargetRpc(conn, result, slotType);
        }
        
        [ServerRpc(RequireOwnership = true)]
        public void RequestUnequipItem_ServerRpc(EquipmentSlotType slotType, NetworkConnection conn = null)
        {
            var result = equipmentSystem.UnequipItem(slotType, out ItemInstance unequipped);
            
            if (result == OperationResult.Success && unequipped != null)
            {
                // Add back to inventory
                inventorySync.Inventory.AddItem(unequipped, out _);
            }
            
            ConfirmEquipOperation_TargetRpc(conn, result, slotType);
        }
        
        // === TARGET RPCs ===
        
        [TargetRpc]
        private void ConfirmEquipOperation_TargetRpc(NetworkConnection conn, OperationResult result, EquipmentSlotType slotType)
        {
            Log($"[CLIENT] Equipment operation confirmed: {result} for {slotType}");
        }
        
        // === HELPERS ===
        
        private ItemDefinition GetItemDefinition(string itemId)
        {
            return ItemDefinitionDatabase.Instance.GetDefinition(itemId);
        }
        
        void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[EquipmentNetworkSync] {message}");
        }
        
        void LogWarning(string message)
        {
            Debug.LogWarning($"[EquipmentNetworkSync] {message}");
        }
    }
}