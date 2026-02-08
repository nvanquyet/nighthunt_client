using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Config;
using System.Collections.Generic;
using NightHunt.Inventory.Database;
using NightHunt.Inventory.Systems;

namespace NightHunt.Inventory.Network
{
    /// <summary>
    /// Network synchronization for InventorySystem using FishNet Pro V4.
    /// Server-authoritative with client prediction for UI responsiveness.
    /// Handles: Item add/remove, move, swap, stack operations.
    /// </summary>
    public class InventoryNetworkSync : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private InventorySystem inventorySystem;
        [SerializeField] private InventoryConfig config;
        
        [Header("Network Settings")]
        [SerializeField] private bool enableClientPrediction = true;
        [SerializeField] private float reconciliationTimeout = 2f;
        
        [Header("Anti-Cheat")]
        [SerializeField] private bool enableValidation = true;
        [SerializeField] private int maxOperationsPerSecond = 20;
        [SerializeField] private bool logSuspiciousActivity = true;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        // Synced inventory state (server → clients)
        private readonly SyncList<ItemInstanceData> syncedInventory = new SyncList<ItemInstanceData>();
        
        // Client prediction state
        private Dictionary<string, ItemInstance> predictedItems = new Dictionary<string, ItemInstance>();
        private List<PendingOperation> pendingOperations = new List<PendingOperation>();
        
        // Anti-cheat tracking
        private Dictionary<NetworkConnection, RateLimitData> rateLimitTracking = new Dictionary<NetworkConnection, RateLimitData>();
        
        //Public getter for synced inventory
        public InventorySystem Inventory => inventorySystem;
        
        
        // === Lifecycle ===
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Subscribe to inventory events (server-side)
            InventoryEvents.OnItemAdded += OnItemAdded_Server;
            InventoryEvents.OnItemRemoved += OnItemRemoved_Server;
            InventoryEvents.OnItemMoved += OnItemMoved_Server;
            InventoryEvents.OnItemsSwapped += OnItemsSwapped_Server;
            InventoryEvents.OnStackSplit += OnStackSplit_Server;
            InventoryEvents.OnStacksMerged += OnStacksMerged_Server;
            
            Log("Server started - inventory sync enabled");
        }
        
        public override void OnStopServer()
        {
            base.OnStopServer();
            
            // Unsubscribe
            InventoryEvents.OnItemAdded -= OnItemAdded_Server;
            InventoryEvents.OnItemRemoved -= OnItemRemoved_Server;
            InventoryEvents.OnItemMoved -= OnItemMoved_Server;
            InventoryEvents.OnItemsSwapped -= OnItemsSwapped_Server;
            InventoryEvents.OnStackSplit -= OnStackSplit_Server;
            InventoryEvents.OnStacksMerged -= OnStacksMerged_Server;
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            if (!IsOwner)
                return;
            
            // Subscribe to SyncList changes
            syncedInventory.OnChange += OnSyncedInventoryChanged_Client;
            
            Log("Client started - listening for inventory updates");
        }
        
        public override void OnStopClient()
        {
            base.OnStopClient();
            
            if (!IsOwner)
                return;
            
            syncedInventory.OnChange -= OnSyncedInventoryChanged_Client;
        }
        
        void Update()
        {
            if (!IsOwner)
                return;
            
            // Reconciliation timeout check
            CleanupPendingOperations();
        }
        
        // === SERVER-SIDE EVENT HANDLERS ===
        
        #region Server Event Handlers
        
        private void OnItemAdded_Server(ItemInstance item, int slotIndex)
        {
            if (!IsServer)
                return;
            
            // Update synced list
            var data = item.Serialize();
            
            if (slotIndex < syncedInventory.Count)
            {
                syncedInventory[slotIndex] = data;
            }
            else
            {
                syncedInventory.Add(data);
            }
            
            Log($"[SERVER] Item added: {item.Definition.DisplayName} at slot {slotIndex}");
        }
        
        private void OnItemRemoved_Server(ItemInstance item, int slotIndex)
        {
            if (!IsServer)
                return;
            
            // Mark slot as empty
            if (slotIndex >= 0 && slotIndex < syncedInventory.Count)
            {
                var emptyData = new ItemInstanceData { InstanceId = null };
                syncedInventory[slotIndex] = emptyData;
            }
            
            Log($"[SERVER] Item removed from slot {slotIndex}");
        }
        
        private void OnItemMoved_Server(ItemInstance item, int fromSlot, int toSlot)
        {
            if (!IsServer)
                return;
            
            // Swap in synced list
            if (fromSlot < syncedInventory.Count && toSlot < syncedInventory.Count)
            {
                var itemData = syncedInventory[fromSlot];
                var emptyData = new ItemInstanceData { InstanceId = null };
                
                syncedInventory[fromSlot] = emptyData;
                syncedInventory[toSlot] = itemData;
            }
            
            Log($"[SERVER] Item moved: slot {fromSlot} → {toSlot}");
        }
        
        private void OnItemsSwapped_Server(ItemInstance itemA, int slotA, ItemInstance itemB, int slotB)
        {
            if (!IsServer)
                return;
            
            if (slotA < syncedInventory.Count && slotB < syncedInventory.Count)
            {
                var temp = syncedInventory[slotA];
                syncedInventory[slotA] = syncedInventory[slotB];
                syncedInventory[slotB] = temp;
            }
            
            Log($"[SERVER] Items swapped: slot {slotA} ↔ {slotB}");
        }
        
        private void OnStackSplit_Server(ItemInstance original, int originalSlot, ItemInstance split, int newSlot, int amount)
        {
            if (!IsServer)
                return;
            
            // Update both stacks
            syncedInventory[originalSlot] = original.Serialize();
            
            if (newSlot < syncedInventory.Count)
            {
                syncedInventory[newSlot] = split.Serialize();
            }
            else
            {
                syncedInventory.Add(split.Serialize());
            }
            
            Log($"[SERVER] Stack split: {amount} items from slot {originalSlot} to {newSlot}");
        }
        
        private void OnStacksMerged_Server(ItemInstance source, int sourceSlot, ItemInstance target, int targetSlot, int amount)
        {
            if (!IsServer)
                return;
            
            // Update target stack
            syncedInventory[targetSlot] = target.Serialize();
            
            // Update or clear source
            if (source.StackSize > 0)
            {
                syncedInventory[sourceSlot] = source.Serialize();
            }
            else
            {
                var emptyData = new ItemInstanceData { InstanceId = null };
                syncedInventory[sourceSlot] = emptyData;
            }
            
            Log($"[SERVER] Stacks merged: {amount} items from slot {sourceSlot} to {targetSlot}");
        }
        
        #endregion
        
        // === CLIENT RPCs (Server → Client Updates) ===
        
        #region Client RPCs
        
        /// <summary>
        /// Notify client of operation success (for reconciliation).
        /// </summary>
        [ObserversRpc]
        private void ConfirmOperation_ObserversRpc(string operationId, OperationResult result)
        {
            if (!IsOwner)
                return;
            
            // Find pending operation
            var pending = pendingOperations.Find(op => op.OperationId == operationId);
            if (pending != null)
            {
                pendingOperations.Remove(pending);
                
                if (result == OperationResult.Success)
                {
                    Log($"[CLIENT] Operation confirmed: {operationId}");
                }
                else
                {
                    LogWarning($"[CLIENT] Operation rejected: {operationId} - {result}");
                    // Rollback prediction
                    RollbackPrediction(pending);
                }
            }
        }
        
        #endregion
        
        // === SERVER RPCs (Client → Server Requests) ===
        
        #region Server RPCs
        
        /// <summary>
        /// Request to add item to inventory (e.g., from world pickup).
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        public void RequestAddItem_ServerRpc(string itemDefinitionId, int stackSize, NetworkConnection conn = null)
        {
            // Validate request
            if (!ValidateRequest(conn, "AddItem"))
                return;
            
            // Get item definition
            var definition = GetItemDefinition(itemDefinitionId);
            if (definition == null)
            {
                LogWarning($"[SERVER] Invalid item definition: {itemDefinitionId}");
                return;
            }
            
            // Create item instance (SERVER ONLY)
            var item = ItemInstanceFactory.CreateInstance(definition, stackSize);
            
            // Validate weight
            if (enableValidation && config.EnableWeightSystem)
            {
                float currentWeight = inventorySystem.GetCurrentWeight();
                float newWeight = currentWeight + item.GetTotalWeight();
                
                if (newWeight > inventorySystem.GetMaxWeight() && !config.AllowPickupWhenOverweight)
                {
                    LogWarning($"[SERVER] Weight limit exceeded for {conn.ClientId}");
                    ConfirmOperation_ObserversRpc($"add_{item.InstanceId}", OperationResult.ExceedsWeightLimit);
                    return;
                }
            }
            
            // Add to inventory
            var result = inventorySystem.AddItem(item, out int slot);
            
            // Confirm to client
            ConfirmOperation_ObserversRpc($"add_{item.InstanceId}", result);
            
            Log($"[SERVER] Added item {itemDefinitionId} for client {conn.ClientId}: {result}");
        }
        
        /// <summary>
        /// Request to remove item from inventory.
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        public void RequestRemoveItem_ServerRpc(string instanceId, NetworkConnection conn = null)
        {
            if (!ValidateRequest(conn, "RemoveItem"))
                return;
            
            // Validate ownership
            if (!inventorySystem.HasItem(instanceId))
            {
                LogWarning($"[SERVER] Client {conn.ClientId} tried to remove item they don't have: {instanceId}");
                return;
            }
            
            var result = inventorySystem.RemoveItem(instanceId);
            
            ConfirmOperation_ObserversRpc($"remove_{instanceId}", result);
        }
        
        /// <summary>
        /// Request to move item between slots.
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        public void RequestMoveItem_ServerRpc(int fromSlot, int toSlot, NetworkConnection conn = null)
        {
            if (!ValidateRequest(conn, "MoveItem"))
                return;
            
            // Validate slots
            if (!ValidateSlotIndices(fromSlot, toSlot))
            {
                LogWarning($"[SERVER] Invalid slot indices: {fromSlot} → {toSlot}");
                return;
            }
            
            var result = inventorySystem.MoveItem(fromSlot, toSlot);
            
            ConfirmOperation_ObserversRpc($"move_{fromSlot}_{toSlot}", result);
        }
        
        /// <summary>
        /// Request to swap items between slots.
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        public void RequestSwapItems_ServerRpc(int slotA, int slotB, NetworkConnection conn = null)
        {
            if (!ValidateRequest(conn, "SwapItems"))
                return;
            
            if (!ValidateSlotIndices(slotA, slotB))
                return;
            
            var result = inventorySystem.SwapItems(slotA, slotB);
            
            ConfirmOperation_ObserversRpc($"swap_{slotA}_{slotB}", result);
        }
        
        /// <summary>
        /// Request to split stack.
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        public void RequestSplitStack_ServerRpc(int slotIndex, int amount, NetworkConnection conn = null)
        {
            if (!ValidateRequest(conn, "SplitStack"))
                return;
            
            var result = inventorySystem.SplitStack(slotIndex, amount, out ItemInstance splitItem);
            
            string opId = $"split_{slotIndex}_{amount}";
            ConfirmOperation_ObserversRpc(opId, result);
        }
        
        /// <summary>
        /// Request to merge stacks.
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        public void RequestMergeStacks_ServerRpc(int sourceSlot, int targetSlot, NetworkConnection conn = null)
        {
            if (!ValidateRequest(conn, "MergeStacks"))
                return;
            
            if (!ValidateSlotIndices(sourceSlot, targetSlot))
                return;
            
            var result = inventorySystem.MergeStacks(sourceSlot, targetSlot);
            
            ConfirmOperation_ObserversRpc($"merge_{sourceSlot}_{targetSlot}", result);
        }
        
        #endregion
        
        // === CLIENT-SIDE PREDICTION ===
        
        #region Client Prediction
        
        /// <summary>
        /// Called when SyncList changes (server → client sync).
        /// </summary>
        private void OnSyncedInventoryChanged_Client(SyncListOperation op, int index, ItemInstanceData oldValue, ItemInstanceData newValue, bool asServer)
        {
            if (!IsOwner || asServer)
                return;
            
            Log($"[CLIENT] Inventory synced: {op} at index {index}");
            
            // Update local inventory state
            UpdateLocalInventory(index, newValue);
        }
        
        /// <summary>
        /// Update local inventory from synced data.
        /// </summary>
        private void UpdateLocalInventory(int index, ItemInstanceData data)
        {
            if (inventorySystem == null)
                return;
            
            // If empty data, remove item
            if (string.IsNullOrEmpty(data.InstanceId))
            {
                inventorySystem.RemoveItemAtSlot(index, out _);
                return;
            }
            
            // Get or create item instance
            var definition = GetItemDefinition(data.ItemId);
            if (definition == null)
            {
                LogWarning($"[CLIENT] Unknown item definition: {data.ItemId}");
                return;
            }
            
            var item = ItemInstance.Deserialize(data, definition);
            
            // Update inventory
            inventorySystem.AddItemAtSlot(item, index);
        }
        
        /// <summary>
        /// Predict operation locally for UI responsiveness.
        /// </summary>
        public void PredictOperation(string operationId, System.Action operation)
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
            
            // Force re-sync from server
            // In FishNet, SyncList will automatically reconcile
            // We just need to clear our prediction state
            
            predictedItems.Clear();
        }
        
        /// <summary>
        /// Cleanup timed-out pending operations.
        /// </summary>
        private void CleanupPendingOperations()
        {
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
        
        #endregion
        
        // === VALIDATION & ANTI-CHEAT ===
        
        #region Validation
        
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
        
        /// <summary>
        /// Validate slot indices.
        /// </summary>
        private bool ValidateSlotIndices(params int[] slots)
        {
            int maxSlots = inventorySystem.GetSlotCount();
            
            foreach (int slot in slots)
            {
                if (slot < 0 || slot >= maxSlots)
                    return false;
            }
            
            return true;
        }
        
        #endregion
        
        // === HELPERS ===
        
        /// <summary>
        /// Get item definition by ID.
        /// TODO: Replace with actual ItemDefinitionDatabase.
        /// </summary>
        private ItemDefinition GetItemDefinition(string itemId)
        {
            // TODO: Implement ItemDefinitionDatabase lookup
            // For now, return null - this should be replaced with:
            // return ItemDefinitionDatabase.Instance.GetDefinition(itemId);
            return ItemDefinitionDatabase.Instance.GetDefinition(itemId);
        }
        
        // === PUBLIC API ===
        
        /// <summary>
        /// Request item pickup (called by client).
        /// </summary>
        public void RequestPickup(string itemDefinitionId, int stackSize = 1)
        {
            if (!IsOwner)
                return;
            
            // Predict locally
            if (enableClientPrediction)
            {
                PredictOperation($"add_{itemDefinitionId}", () =>
                {
                    // Local prediction handled by InventorySystem events
                });
            }
            
            // Send to server
            RequestAddItem_ServerRpc(itemDefinitionId, stackSize);
        }
        
        /// <summary>
        /// Request item drop (called by client).
        /// </summary>
        public void RequestDrop(string instanceId)
        {
            if (!IsOwner)
                return;
            
            RequestRemoveItem_ServerRpc(instanceId);
        }
        
        // === DEBUG ===
        
        void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[InventoryNetworkSync] {message}");
        }
        
        void LogWarning(string message)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[InventoryNetworkSync] {message}");
        }
        
        // === DATA STRUCTURES ===
        
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
    }
}