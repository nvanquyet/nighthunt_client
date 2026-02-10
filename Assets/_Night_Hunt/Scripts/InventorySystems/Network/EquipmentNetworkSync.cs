using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Systems;
using System.Collections.Generic;
using System.Linq;
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
        [SerializeField] private InventorySystem inventorySystem;
        [SerializeField] private InventoryNetworkSync inventorySync;
        
        [Header("Network Settings")]
        [SerializeField] private bool enableClientPrediction = true;
        [SerializeField] private float reconciliationTimeout = 2f;
        
        [Header("Anti-Cheat")]
        [SerializeField] private bool enableValidation = true;
        [SerializeField] private int maxOperationsPerSecond = 20;
        [SerializeField] private bool logSuspiciousActivity = true;
        
        [Header("Visual Sync")]
        [SerializeField] private Transform[] equipmentModelSlots; // Visual slots for equipped items
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        // Synced equipment state
        private readonly SyncDictionary<EquipmentSlotType, ItemInstanceData> syncedEquipment = 
            new SyncDictionary<EquipmentSlotType, ItemInstanceData>();
        
        // Visual models
        private Dictionary<EquipmentSlotType, GameObject> spawnedModels = new Dictionary<EquipmentSlotType, GameObject>();
        
        // Client prediction state
        private List<PendingOperation> pendingOperations = new List<PendingOperation>();
        
        // Anti-cheat tracking
        private Dictionary<NetworkConnection, RateLimitData> rateLimitTracking = new Dictionary<NetworkConnection, RateLimitData>();
        
        // Data structures
        private class PendingOperation
        {
            public string OperationId;
            public float Timestamp;
            public System.Action Operation;
        }
        
        private class RateLimitData
        {
            public int OperationCount;
            public float WindowStart;
        }
        
        // === Lifecycle ===
#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            inventorySystem = transform.parent.GetComponentInChildren<InventorySystem>();
            equipmentSystem = transform.parent.GetComponentInChildren<EquipmentSystem>();
            inventorySync = transform.parent.GetComponentInChildren<InventoryNetworkSync>();
        }
#endif
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
                    UpdateEquipmentFromSync(key, value);
                    UpdateEquipmentVisual(key, value);
                    break;
                
                case SyncDictionaryOperation.Remove:
                    // Update local system - unequip item
                    if (equipmentSystem != null && equipmentSystem.IsSlotEquipped(key))
                    {
                        equipmentSystem.UnequipItem(key, out _);
                    }
                    RemoveEquipmentVisual(key);
                    break;
                
                case SyncDictionaryOperation.Clear:
                    // Update local system - unequip all
                    if (equipmentSystem != null)
                    {
                        equipmentSystem.UnequipAll();
                    }
                    ClearAllEquipmentVisuals();
                    break;
            }
        }
        
        /// <summary>
        /// Update local equipment system from synced data.
        /// According to Flow 4: Client updates Local System state → Local System fires Events.
        /// </summary>
        private void UpdateEquipmentFromSync(EquipmentSlotType slotType, ItemInstanceData data)
        {
            if (equipmentSystem == null)
                return;
            
            // Check if empty data
            if (string.IsNullOrEmpty(data.InstanceId))
            {
                // Unequip if currently equipped
                if (equipmentSystem.IsSlotEquipped(slotType))
                {
                    equipmentSystem.UnequipItem(slotType, out _);
                }
                return;
            }
            
            // Get item definition
            var definition = GetItemDefinition(data.ItemId);
            if (definition == null)
            {
                LogWarning($"[CLIENT] Unknown item definition: {data.ItemId}");
                return;
            }
            
            // Deserialize item
            var item = ItemInstance.Deserialize(data, definition);
            
            // Check if already equipped (to avoid duplicate events)
            var currentEquipped = equipmentSystem.GetEquippedItem(slotType);
            if (currentEquipped != null && currentEquipped.InstanceId == item.InstanceId)
            {
                // Already equipped with same item - no update needed
                return;
            }
            
            // Find item in inventory or equipment
            ItemInstance actualItem = null;
            
            // Check inventory first
            if (inventorySystem != null)
            {
                actualItem = inventorySystem.FindItem(item.InstanceId);
            }
            
            // If not in inventory, check if it's already equipped in another slot
            if (actualItem == null && equipmentSystem != null)
            {
                var allEquipped = equipmentSystem.GetAllEquippedItems();
                actualItem = allEquipped.FirstOrDefault(i => i.InstanceId == item.InstanceId);
            }
            
            // If still not found, use deserialized item (for observers)
            if (actualItem == null)
            {
                actualItem = item;
            }
            
            // Update local equipment system
            // This will fire EquipmentEvents automatically
            equipmentSystem.EquipItem(actualItem, slotType);
            
            Log($"[CLIENT] Updated equipment from sync: {definition.DisplayName} in {slotType}");
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
        
        // === PUBLIC API ===
        
        /// <summary>
        /// Request to equip item from inventory to equipment slot.
        /// Step 1: Check IsOwner
        /// Step 2: Optional client prediction
        /// Step 3: Call ServerRpc
        /// </summary>
        public void RequestEquipFromInventory(string instanceId, EquipmentSlotType slotType)
        {
            if (!IsOwner)
                return;
            
            string operationId = $"equip_{instanceId}_{slotType}";
            
            // Optional: Client prediction
            if (enableClientPrediction)
            {
                PredictOperation(operationId, () =>
                {
                    // Local prediction handled by EquipmentSystem events
                });
            }
            
            // Send to server
            RequestEquipFromInventory_ServerRpc(instanceId, slotType);
        }
        
        /// <summary>
        /// Request to unequip item from equipment slot back to inventory.
        /// Step 1: Check IsOwner
        /// Step 2: Optional client prediction
        /// Step 3: Call ServerRpc
        /// </summary>
        public void RequestUnequipToInventory(EquipmentSlotType slotType)
        {
            if (!IsOwner)
                return;
            
            string operationId = $"unequip_{slotType}";
            
            // Optional: Client prediction
            if (enableClientPrediction)
            {
                PredictOperation(operationId, () =>
                {
                    // Local prediction handled by EquipmentSystem events
                });
            }
            
            // Send to server
            RequestUnequipToInventory_ServerRpc(slotType);
        }
        
        // === SERVER RPCs ===
        
        /// <summary>
        /// Request to equip item from inventory to equipment slot.
        /// Execution order:
        /// Step 1: ValidateRequest → Rate limit, connection
        /// Step 2: ValidateOwnership → Check item ownership
        /// Step 3: ValidateItemExists → Check item exists and valid
        /// Step 4: ValidateTypeCompatibility → Check item type matches slot
        /// Step 5: ValidateLimits → Check weight limits
        /// Step 6: Execute Local System → EquipItem, RemoveItem
        /// Step 7: ConfirmOperation_TargetRpc → Notify client
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        private void RequestEquipFromInventory_ServerRpc(string instanceId, EquipmentSlotType slotType, NetworkConnection conn = null)
        {
            string operationId = $"equip_{instanceId}_{slotType}";
            
            // Step 1: ValidateRequest
            if (!ValidateRequest(conn, "EquipFromInventory"))
            {
                ConfirmEquipOperation_TargetRpc(conn, OperationResult.UnknownError, slotType);
                return;
            }
            
            // Step 2: ValidateOwnership
            if (inventorySystem == null)
            {
                LogWarning("[SERVER] InventorySystem not assigned!");
                ConfirmEquipOperation_TargetRpc(conn, OperationResult.UnknownError, slotType);
                return;
            }
            
            if (!inventorySystem.HasItem(instanceId))
            {
                LogWarning($"[SERVER] Client {conn.ClientId} tried to equip item they don't own: {instanceId}");
                ConfirmEquipOperation_TargetRpc(conn, OperationResult.ItemNotFound, slotType);
                return;
            }
            
            // Step 3: ValidateItemExists
            var item = inventorySystem.FindItem(instanceId);
            if (item == null || item.Definition == null)
            {
                LogWarning($"[SERVER] Item not found or invalid: {instanceId}");
                ConfirmEquipOperation_TargetRpc(conn, OperationResult.ItemNotFound, slotType);
                return;
            }
            
            // Step 4: ValidateTypeCompatibility
            if (!equipmentSystem.CanEquip(item, slotType))
            {
                LogWarning($"[SERVER] Item type incompatible with equipment slot: {item.Definition.ItemId} -> {slotType}");
                ConfirmEquipOperation_TargetRpc(conn, OperationResult.IncompatibleSlot, slotType);
                return;
            }
            
            // Step 5: ValidateLimits (weight check is handled by EquipmentSystem)
            
            // Step 6: Execute Local System
            var result = equipmentSystem.EquipItem(item, slotType);
            
            // Remove from inventory if success
            if (result == OperationResult.Success)
            {
                inventorySystem.RemoveItem(instanceId);
            }
            
            // Step 7: Confirm to client
            ConfirmEquipOperation_TargetRpc(conn, result, slotType);
            
            Log($"[SERVER] Equip from inventory: {item.Definition.DisplayName} to {slotType} - {result}");
        }
        
        /// <summary>
        /// Request to unequip item from equipment slot back to inventory.
        /// Execution order: Same as RequestEquipFromInventory_ServerRpc
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        private void RequestUnequipToInventory_ServerRpc(EquipmentSlotType slotType, NetworkConnection conn = null)
        {
            string operationId = $"unequip_{slotType}";
            
            // Step 1: ValidateRequest
            if (!ValidateRequest(conn, "UnequipToInventory"))
            {
                ConfirmEquipOperation_TargetRpc(conn, OperationResult.UnknownError, slotType);
                return;
            }
            
            // Step 2: ValidateOwnership (check if item is equipped)
            var equippedItem = equipmentSystem.GetEquippedItem(slotType);
            if (equippedItem == null)
            {
                LogWarning($"[SERVER] No item equipped in slot: {slotType}");
                ConfirmEquipOperation_TargetRpc(conn, OperationResult.NotEquipped, slotType);
                return;
            }
            
            // Step 3: ValidateItemExists (already checked above)
            
            // Step 4: ValidateTypeCompatibility (not needed for unequip)
            
            // Step 5: ValidateLimits (check inventory space)
            if (inventorySystem == null)
            {
                LogWarning("[SERVER] InventorySystem not assigned!");
                ConfirmEquipOperation_TargetRpc(conn, OperationResult.UnknownError, slotType);
                return;
            }
            
            if (inventorySystem.GetEmptySlotCount() == 0)
            {
                LogWarning($"[SERVER] Inventory full, cannot unequip to inventory");
                ConfirmEquipOperation_TargetRpc(conn, OperationResult.InventoryFull, slotType);
                return;
            }
            
            // Step 6: Execute Local System
            var result = equipmentSystem.UnequipItem(slotType, out ItemInstance unequippedItem);
            
            // Add to inventory if success
            if (result == OperationResult.Success && unequippedItem != null)
            {
                var addResult = inventorySystem.AddItem(unequippedItem, out _);
                if (addResult != OperationResult.Success)
                {
                    // Inventory full - this shouldn't happen after check, but handle it
                    LogWarning($"[SERVER] Failed to add unequipped item to inventory: {addResult}");
                }
            }
            
            // Step 7: Confirm to client
            ConfirmEquipOperation_TargetRpc(conn, result, slotType);
            
            Log($"[SERVER] Unequip to inventory: {slotType} - {result}");
        }
        
        // === TARGET RPCs ===
        
        [TargetRpc]
        private void ConfirmEquipOperation_TargetRpc(NetworkConnection conn, OperationResult result, EquipmentSlotType slotType)
        {
            string operationId = result == OperationResult.Success ? $"equip_{slotType}" : $"unequip_{slotType}";
            
            // Find pending operation
            var pending = pendingOperations.Find(op => op.OperationId == operationId);
            if (pending != null)
            {
                pendingOperations.Remove(pending);
                
                if (result == OperationResult.Success)
                {
                    Log($"[CLIENT] Equipment operation confirmed: {operationId}");
                }
                else
                {
                    LogWarning($"[CLIENT] Equipment operation rejected: {operationId} - {result}");
                    // Rollback prediction
                    RollbackPrediction(pending);
                }
            }
        }
        
        // === CLIENT PREDICTION ===
        
        /// <summary>
        /// Predict operation locally for UI responsiveness.
        /// </summary>
        private void PredictOperation(string operationId, System.Action operation)
        {
            if (!enableClientPrediction || !IsOwner)
                return;
            
            // Store pending operation
            var pending = new PendingOperation
            {
                OperationId = operationId,
                Timestamp = Time.time,
                Operation = operation
            };
            
            pendingOperations.Add(pending);
            
            // Execute prediction
            operation?.Invoke();
            
            Log($"[CLIENT] Predicted operation: {operationId}");
        }
        
        /// <summary>
        /// Rollback prediction if server rejects.
        /// </summary>
        private void RollbackPrediction(PendingOperation pending)
        {
            LogWarning($"[CLIENT] Rolling back prediction: {pending.OperationId}");
            // Force re-sync from server - SyncDictionary will automatically reconcile
        }
        
        void Update()
        {
            if (!IsOwner)
                return;
            
            // Cleanup timed-out pending operations
            float currentTime = Time.time;
            pendingOperations.RemoveAll(op =>
            {
                if (currentTime - op.Timestamp > reconciliationTimeout)
                {
                    LogWarning($"[CLIENT] Operation timed out: {op.OperationId}");
                    return true;
                }
                return false;
            });
        }
        
        // === VALIDATION & ANTI-CHEAT ===
        
        /// <summary>
        /// Validate server RPC request (anti-cheat).
        /// </summary>
        private bool ValidateRequest(NetworkConnection conn, string operationType)
        {
            if (!enableValidation)
                return true;
            
            if (conn == null)
            {
                LogWarning($"[SERVER] Null connection for {operationType}");
                return false;
            }
            
            // Rate limiting
            if (!CheckRateLimit(conn))
            {
                LogWarning($"[SERVER] Rate limit exceeded for client {conn.ClientId}");
                if (logSuspiciousActivity)
                {
                    Debug.LogError($"[ANTI-CHEAT] Client {conn.ClientId} exceeded rate limit - possible hack attempt");
                }
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Check rate limit for connection.
        /// </summary>
        private bool CheckRateLimit(NetworkConnection conn)
        {
            if (!rateLimitTracking.ContainsKey(conn))
            {
                rateLimitTracking[conn] = new RateLimitData();
            }
            
            var data = rateLimitTracking[conn];
            float currentTime = Time.time;
            
            // Reset counter if 1 second passed
            if (currentTime - data.WindowStart >= 1f)
            {
                data.OperationCount = 0;
                data.WindowStart = currentTime;
            }
            
            data.OperationCount++;
            
            return data.OperationCount <= maxOperationsPerSecond;
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