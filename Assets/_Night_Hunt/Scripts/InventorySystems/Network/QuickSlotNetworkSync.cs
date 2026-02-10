using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Systems;
using System.Collections.Generic;
using System;
using NightHunt.Inventory.Database;

namespace NightHunt.Inventory.Network
{
    /// <summary>
    /// Network synchronization for QuickSlotSystem.
    /// Syncs quick slot assignments across network.
    /// </summary>
    public class QuickSlotNetworkSync : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private QuickSlotSystem quickSlotSystem;
        [SerializeField] private InventorySystem inventorySystem;
        
        [Header("Network Settings")]
        [SerializeField] private bool enableClientPrediction = true;
        [SerializeField] private float reconciliationTimeout = 2f;
        
        [Header("Anti-Cheat")]
        [SerializeField] private bool enableValidation = true;
        [SerializeField] private int maxOperationsPerSecond = 20;
        [SerializeField] private bool logSuspiciousActivity = true;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        // Synced quick slot state
        private readonly SyncDictionary<int, ItemInstanceData> syncedQuickSlots = 
            new SyncDictionary<int, ItemInstanceData>();
        
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
            quickSlotSystem = transform.parent.GetComponentInChildren<QuickSlotSystem>();
            
        }
#endif
        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Subscribe to quick slot events (server-side)
            QuickSlotSystem.OnQuickSlotAssigned += OnQuickSlotAssigned_Server;
            QuickSlotSystem.OnQuickSlotCleared += OnQuickSlotCleared_Server;
            
            Log("Server started - quick slot sync enabled");
        }
        
        public override void OnStopServer()
        {
            base.OnStopServer();
            
            QuickSlotSystem.OnQuickSlotAssigned -= OnQuickSlotAssigned_Server;
            QuickSlotSystem.OnQuickSlotCleared -= OnQuickSlotCleared_Server;
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            // Subscribe to SyncDictionary changes
            syncedQuickSlots.OnChange += OnSyncedQuickSlotsChanged_Client;
            
            Log("Client started - listening for quick slot updates");
        }
        
        public override void OnStopClient()
        {
            base.OnStopClient();
            
            syncedQuickSlots.OnChange -= OnSyncedQuickSlotsChanged_Client;
        }
        
        // === SERVER EVENT HANDLERS ===
        
        private void OnQuickSlotAssigned_Server(ItemInstance item, int quickSlotIndex)
        {
            if (!IsServer)
                return;
            
            syncedQuickSlots[quickSlotIndex] = item.Serialize();
            
            Log($"[SERVER] Quick slot assigned: {item.Definition.DisplayName} to slot {quickSlotIndex}");
        }
        
        private void OnQuickSlotCleared_Server(int quickSlotIndex)
        {
            if (!IsServer)
                return;
            
            syncedQuickSlots.Remove(quickSlotIndex);
            
            Log($"[SERVER] Quick slot cleared: {quickSlotIndex}");
        }
        
        // === CLIENT SYNC ===
        
        private void OnSyncedQuickSlotsChanged_Client(SyncDictionaryOperation op, int key, ItemInstanceData value, bool asServer)
        {
            if (asServer)
                return;
            
            Log($"[CLIENT] Quick slot sync: {op} for slot {key}");
            
            if (quickSlotSystem == null)
                return;
            
            switch (op)
            {
                case SyncDictionaryOperation.Add:
                case SyncDictionaryOperation.Set:
                    UpdateQuickSlotFromSync(key, value);
                    break;
                
                case SyncDictionaryOperation.Remove:
                    quickSlotSystem.ClearQuickSlot(key);
                    break;
                
                case SyncDictionaryOperation.Clear:
                    // Clear all quick slots
                    for (int i = 0; i < quickSlotSystem.GetQuickSlotCount(); i++)
                    {
                        quickSlotSystem.ClearQuickSlot(i);
                    }
                    break;
            }
        }
        
        /// <summary>
        /// Update local quick slot from synced data.
        /// </summary>
        private void UpdateQuickSlotFromSync(int quickSlotIndex, ItemInstanceData data)
        {
            if (inventorySystem == null)
                return;
            
            // Get item definition
            var definition = GetItemDefinition(data.ItemId);
            if (definition == null)
            {
                LogWarning($"[CLIENT] Unknown item definition: {data.ItemId}");
                return;
            }
            
            // Deserialize item
            var item = ItemInstance.Deserialize(data, definition);
            
            // Check if item still exists in inventory
            if (!inventorySystem.HasItem(item.InstanceId))
            {
                LogWarning($"[CLIENT] Item no longer in inventory: {item.InstanceId}");
                quickSlotSystem.ClearQuickSlot(quickSlotIndex);
                return;
            }
            
            // Get item from inventory (use reference, not deserialized copy)
            var inventoryItem = inventorySystem.FindItem(item.InstanceId);
            if (inventoryItem != null)
            {
                quickSlotSystem.AssignToQuickSlot(inventoryItem, quickSlotIndex);
            }
        }
        
        // === PUBLIC API ===
        
        /// <summary>
        /// Request to assign item from inventory to quick slot.
        /// Step 1: Check IsOwner
        /// Step 2: Optional client prediction
        /// Step 3: Call ServerRpc
        /// </summary>
        public void RequestAssignQuickSlot(string instanceId, int quickSlotIndex)
        {
            if (!IsOwner)
                return;
            
            string operationId = $"assignQuickSlot_{instanceId}_{quickSlotIndex}";
            
            // Optional: Client prediction
            if (enableClientPrediction)
            {
                PredictOperation(operationId, () =>
                {
                    // Local prediction handled by QuickSlotSystem events
                });
            }
            
            // Send to server
            RequestAssignQuickSlot_ServerRpc(instanceId, quickSlotIndex);
        }
        
        /// <summary>
        /// Request to clear quick slot.
        /// Step 1: Check IsOwner
        /// Step 2: Optional client prediction
        /// Step 3: Call ServerRpc
        /// </summary>
        public void RequestClearQuickSlot(int quickSlotIndex)
        {
            if (!IsOwner)
                return;
            
            string operationId = $"clearQuickSlot_{quickSlotIndex}";
            
            // Optional: Client prediction
            if (enableClientPrediction)
            {
                PredictOperation(operationId, () =>
                {
                    // Local prediction handled by QuickSlotSystem events
                });
            }
            
            // Send to server
            RequestClearQuickSlot_ServerRpc(quickSlotIndex);
        }
        
        // === SERVER RPCs ===
        
        /// <summary>
        /// Request to assign item from inventory to quick slot.
        /// Execution order:
        /// Step 1: ValidateRequest → Rate limit, connection
        /// Step 2: ValidateOwnership → Check item ownership
        /// Step 3: ValidateItemExists → Check item exists and valid
        /// Step 4: ValidateTypeCompatibility → (quick slots accept any type)
        /// Step 5: ValidateLimits → Check quick slot index
        /// Step 6: Execute Local System → AssignToQuickSlot
        /// Step 7: ConfirmOperation_TargetRpc → Notify client
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        private void RequestAssignQuickSlot_ServerRpc(string instanceId, int quickSlotIndex, NetworkConnection conn = null)
        {
            string operationId = $"assignQuickSlot_{instanceId}_{quickSlotIndex}";
            
            // Step 1: ValidateRequest
            if (!ValidateRequest(conn, "AssignQuickSlot"))
            {
                ConfirmQuickSlotOperation_TargetRpc(conn, OperationResult.UnknownError, quickSlotIndex);
                return;
            }
            
            // Step 2: ValidateOwnership
            if (inventorySystem == null)
            {
                LogWarning("[SERVER] InventorySystem not assigned!");
                ConfirmQuickSlotOperation_TargetRpc(conn, OperationResult.UnknownError, quickSlotIndex);
                return;
            }
            
            if (!inventorySystem.HasItem(instanceId))
            {
                LogWarning($"[SERVER] Client {conn.ClientId} tried to assign quick slot for item they don't own: {instanceId}");
                ConfirmQuickSlotOperation_TargetRpc(conn, OperationResult.ItemNotFound, quickSlotIndex);
                return;
            }
            
            // Step 3: ValidateItemExists
            var item = inventorySystem.FindItem(instanceId);
            if (item == null || item.Definition == null)
            {
                LogWarning($"[SERVER] Item not found or invalid: {instanceId}");
                ConfirmQuickSlotOperation_TargetRpc(conn, OperationResult.ItemNotFound, quickSlotIndex);
                return;
            }
            
            // Step 4: ValidateTypeCompatibility (quick slots can accept any item type)
            
            // Step 5: ValidateLimits (check quick slot index)
            if (quickSlotSystem == null)
            {
                LogWarning("[SERVER] QuickSlotSystem not assigned!");
                ConfirmQuickSlotOperation_TargetRpc(conn, OperationResult.UnknownError, quickSlotIndex);
                return;
            }
            
            if (quickSlotIndex < 0 || quickSlotIndex >= quickSlotSystem.GetQuickSlotCount())
            {
                LogWarning($"[SERVER] Invalid quick slot index: {quickSlotIndex}");
                ConfirmQuickSlotOperation_TargetRpc(conn, OperationResult.InvalidSlotIndex, quickSlotIndex);
                return;
            }
            
            // Step 6: Execute Local System
            var result = quickSlotSystem.AssignToQuickSlot(item, quickSlotIndex);
            
            // Step 7: Confirm to client
            ConfirmQuickSlotOperation_TargetRpc(conn, result, quickSlotIndex);
            
            Log($"[SERVER] Assign quick slot: {item.Definition.DisplayName} to slot {quickSlotIndex} - {result}");
        }
        
        /// <summary>
        /// Request to clear quick slot.
        /// Execution order: Same as RequestAssignQuickSlot_ServerRpc
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        private void RequestClearQuickSlot_ServerRpc(int quickSlotIndex, NetworkConnection conn = null)
        {
            string operationId = $"clearQuickSlot_{quickSlotIndex}";
            
            // Step 1: ValidateRequest
            if (!ValidateRequest(conn, "ClearQuickSlot"))
            {
                ConfirmQuickSlotOperation_TargetRpc(conn, OperationResult.UnknownError, quickSlotIndex);
                return;
            }
            
            // Step 2: ValidateOwnership (not needed for clear)
            
            // Step 3: ValidateItemExists (not needed for clear)
            
            // Step 4: ValidateTypeCompatibility (not needed for clear)
            
            // Step 5: ValidateLimits (check quick slot index)
            if (quickSlotSystem == null)
            {
                LogWarning("[SERVER] QuickSlotSystem not assigned!");
                ConfirmQuickSlotOperation_TargetRpc(conn, OperationResult.UnknownError, quickSlotIndex);
                return;
            }
            
            if (quickSlotIndex < 0 || quickSlotIndex >= quickSlotSystem.GetQuickSlotCount())
            {
                LogWarning($"[SERVER] Invalid quick slot index: {quickSlotIndex}");
                ConfirmQuickSlotOperation_TargetRpc(conn, OperationResult.InvalidSlotIndex, quickSlotIndex);
                return;
            }
            
            // Step 6: Execute Local System
            quickSlotSystem.ClearQuickSlot(quickSlotIndex);
            
            // Step 7: Confirm to client
            ConfirmQuickSlotOperation_TargetRpc(conn, OperationResult.Success, quickSlotIndex);
            
            Log($"[SERVER] Clear quick slot: {quickSlotIndex}");
        }
        
        // === TARGET RPCs ===
        
        [TargetRpc]
        private void ConfirmQuickSlotOperation_TargetRpc(NetworkConnection conn, OperationResult result, int quickSlotIndex)
        {
            string operationId = $"quickslot_{quickSlotIndex}";
            
            // Find pending operation
            var pending = pendingOperations.Find(op => op.OperationId == operationId);
            if (pending != null)
            {
                pendingOperations.Remove(pending);
                
                if (result == OperationResult.Success)
                {
                    Log($"[CLIENT] Quick slot operation confirmed: {operationId}");
                }
                else
                {
                    LogWarning($"[CLIENT] Quick slot operation rejected: {operationId} - {result}");
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
            
            var pending = new PendingOperation
            {
                OperationId = operationId,
                Timestamp = Time.time,
                Operation = operation
            };
            
            pendingOperations.Add(pending);
            operation?.Invoke();
            
            Log($"[CLIENT] Predicted operation: {operationId}");
        }
        
        /// <summary>
        /// Rollback prediction if server rejects.
        /// </summary>
        private void RollbackPrediction(PendingOperation pending)
        {
            LogWarning($"[CLIENT] Rolling back prediction: {pending.OperationId}");
        }
        
        void Update()
        {
            if (!IsOwner)
                return;
            
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
                Debug.Log($"[QuickSlotNetworkSync] {message}");
        }
        
        void LogWarning(string message)
        {
            Debug.LogWarning($"[QuickSlotNetworkSync] {message}");
        }
    }
}
