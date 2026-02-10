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
        
        [Header("Cross-System References")]
        [SerializeField] private EquipmentSystem equipmentSystem;
        [SerializeField] private WeaponSystem weaponSystem;
        [SerializeField] private QuickSlotSystem quickSlotSystem;
        
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
        
#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            inventorySystem = transform.parent.GetComponentInChildren<InventorySystem>();
            equipmentSystem = transform.parent.GetComponentInChildren<EquipmentSystem>();
            weaponSystem = transform.parent.GetComponentInChildren<WeaponSystem>();
            quickSlotSystem = transform.parent.GetComponentInChildren<QuickSlotSystem>();
        }
#endif
        
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
        [TargetRpc]
        private void ConfirmOperation_TargetRpc(NetworkConnection conn, string operationId, OperationResult result)
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
            try
            {
                Log($"[SERVER] RequestAddItem_ServerRpc called: {itemDefinitionId}, stackSize={stackSize}, conn={conn?.ClientId ?? -1}, IsServer={IsServer}");
                
                // Validate request
                Log($"[SERVER] Validating request... enableValidation={enableValidation}");
                if (!ValidateRequest(conn, "AddItem"))
                {
                    LogWarning($"[SERVER] ValidateRequest failed for AddItem - conn={conn?.ClientId ?? -1}, enableValidation={enableValidation}");
                    return;
                }
                Log($"[SERVER] ValidateRequest passed");
                
                // Get item definition
                Log($"[SERVER] Getting item definition for: {itemDefinitionId}");
                var definition = GetItemDefinition(itemDefinitionId);
                if (definition == null)
                {
                    LogWarning($"[SERVER] Invalid item definition: {itemDefinitionId} - GetItemDefinition returned null");
                    return;
                }
                
                Log($"[SERVER] Item definition found: {definition.DisplayName}");
                
                // Create item instance (SERVER ONLY)
                Log($"[SERVER] Creating item instance...");
                var item = ItemInstanceFactory.CreateInstance(definition, stackSize);
                if (item == null)
                {
                    LogWarning($"[SERVER] Failed to create item instance: {itemDefinitionId}");
                    return;
                }
                
                Log($"[SERVER] Item instance created: {item.InstanceId}");
                
                // Validate weight
                if (enableValidation && config.EnableWeightSystem)
                {
                    float currentWeight = inventorySystem.GetCurrentWeight();
                    float newWeight = currentWeight + item.GetTotalWeight();
                    
                    if (newWeight > inventorySystem.GetMaxWeight() && !config.AllowPickupWhenOverweight)
                    {
                        LogWarning($"[SERVER] Weight limit exceeded for {conn.ClientId}");
                        ConfirmOperation_TargetRpc(conn, $"add_{item.InstanceId}", OperationResult.ExceedsWeightLimit);
                        return;
                    }
                }
                
                // Add to inventory
                Log($"[SERVER] Calling inventorySystem.AddItem...");
                var result = inventorySystem.AddItem(item, out int slot);
                
                Log($"[SERVER] inventorySystem.AddItem result: {result}, slot: {slot}");
                
                // Confirm to client
                ConfirmOperation_TargetRpc(conn, $"add_{item.InstanceId}", result);
                
                Log($"[SERVER] Added item {itemDefinitionId} for client {conn?.ClientId ?? -1}: {result}");
            }
            catch (System.Exception ex)
            {
                LogError($"[SERVER] Exception in RequestAddItem_ServerRpc: {ex.Message}\n{ex.StackTrace}");
            }
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
                ConfirmOperation_TargetRpc(conn, $"remove_{instanceId}", OperationResult.ItemNotFound);
                return;
            }
            
            var result = inventorySystem.RemoveItem(instanceId);
            
            ConfirmOperation_TargetRpc(conn, $"remove_{instanceId}", result);
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
                ConfirmOperation_TargetRpc(conn, $"move_{fromSlot}_{toSlot}", OperationResult.InvalidSlotIndex);
                return;
            }
            
            var result = inventorySystem.MoveItem(fromSlot, toSlot);
            
            ConfirmOperation_TargetRpc(conn, $"move_{fromSlot}_{toSlot}", result);
        }
        
        /// <summary>
        /// Request to swap items between slots.
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        public void RequestSwapItems_ServerRpc(int slotA, int slotB, NetworkConnection conn = null)
        {
            if (!ValidateRequest(conn, "SwapItems"))
            {
                ConfirmOperation_TargetRpc(conn, $"swap_{slotA}_{slotB}", OperationResult.UnknownError);
                return;
            }
            
            if (!ValidateSlotIndices(slotA, slotB))
            {
                ConfirmOperation_TargetRpc(conn, $"swap_{slotA}_{slotB}", OperationResult.InvalidSlotIndex);
                return;
            }
            
            var result = inventorySystem.SwapItems(slotA, slotB);
            
            ConfirmOperation_TargetRpc(conn, $"swap_{slotA}_{slotB}", result);
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
            ConfirmOperation_TargetRpc(conn, opId, result);
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
            {
                ConfirmOperation_TargetRpc(conn, $"merge_{sourceSlot}_{targetSlot}", OperationResult.InvalidSlotIndex);
                return;
            }
            
            var result = inventorySystem.MergeStacks(sourceSlot, targetSlot);
            
            ConfirmOperation_TargetRpc(conn, $"merge_{sourceSlot}_{targetSlot}", result);
        }
        
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
                ConfirmOperation_TargetRpc(conn, operationId, OperationResult.UnknownError);
                return;
            }
            
            // Step 2: ValidateOwnership
            if (!ValidateOwnership(instanceId))
            {
                LogWarning($"[SERVER] Client {conn.ClientId} tried to equip item they don't own: {instanceId}");
                ConfirmOperation_TargetRpc(conn, operationId, OperationResult.ItemNotFound);
                return;
            }
            
            // Step 3: ValidateItemExists
            if (!ValidateItemExists(instanceId, out ItemInstance item))
            {
                LogWarning($"[SERVER] Item not found or invalid: {instanceId}");
                ConfirmOperation_TargetRpc(conn, operationId, OperationResult.ItemNotFound);
                return;
            }
            
            // Step 4: ValidateTypeCompatibility
            if (!ValidateItemTypeForEquipment(item, slotType))
            {
                LogWarning($"[SERVER] Item type incompatible with equipment slot: {item.Definition.ItemId} -> {slotType}");
                ConfirmOperation_TargetRpc(conn, operationId, OperationResult.IncompatibleSlot);
                return;
            }
            
            // Step 5: ValidateLimits (weight check is handled by EquipmentSystem)
            if (!ValidateLimits(item, "EquipFromInventory"))
            {
                ConfirmOperation_TargetRpc(conn, operationId, OperationResult.ExceedsWeightLimit);
                return;
            }
            
            // Step 6: Execute Local System
            if (equipmentSystem == null)
            {
                LogWarning("[SERVER] EquipmentSystem not assigned!");
                ConfirmOperation_TargetRpc(conn, operationId, OperationResult.UnknownError);
                return;
            }
            
            var result = equipmentSystem.EquipItem(item, slotType);
            
            // Remove from inventory if success
            if (result == OperationResult.Success)
            {
                inventorySystem.RemoveItem(instanceId);
            }
            
            // Step 7: Confirm to client
            ConfirmOperation_TargetRpc(conn, operationId, result);
            
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
                ConfirmOperation_TargetRpc(conn, operationId, OperationResult.UnknownError);
                return;
            }
            
            // Step 2: ValidateOwnership (check if item is equipped)
            if (equipmentSystem == null)
            {
                LogWarning("[SERVER] EquipmentSystem not assigned!");
                ConfirmOperation_TargetRpc(conn, operationId, OperationResult.UnknownError);
                return;
            }
            
            var equippedItem = equipmentSystem.GetEquippedItem(slotType);
            if (equippedItem == null)
            {
                LogWarning($"[SERVER] No item equipped in slot: {slotType}");
                ConfirmOperation_TargetRpc(conn, operationId, OperationResult.NotEquipped);
                return;
            }
            
            // Step 3: ValidateItemExists (already checked above)
            
            // Step 4: ValidateTypeCompatibility (not needed for unequip)
            
            // Step 5: ValidateLimits (check inventory space and weight)
            if (inventorySystem.GetEmptySlotCount() == 0)
            {
                LogWarning($"[SERVER] Inventory full, cannot unequip to inventory");
                ConfirmOperation_TargetRpc(conn, operationId, OperationResult.InventoryFull);
                return;
            }
            
            // Check weight limit
            if (config.EnableWeightSystem)
            {
                float currentWeight = inventorySystem.GetCurrentWeight();
                float itemWeight = equippedItem.GetTotalWeight();
                float newWeight = currentWeight + itemWeight;
                
                if (newWeight > inventorySystem.GetMaxWeight() && !config.AllowPickupWhenOverweight)
                {
                    LogWarning($"[SERVER] Weight limit exceeded, cannot unequip to inventory");
                    ConfirmOperation_TargetRpc(conn, operationId, OperationResult.ExceedsWeightLimit);
                    return;
                }
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
            ConfirmOperation_TargetRpc(conn, operationId, result);
            
            Log($"[SERVER] Unequip to inventory: {slotType} - {result}");
        }
        
        /// <summary>
        /// Request to equip weapon from inventory to weapon slot.
        /// Execution order: Same as RequestEquipFromInventory_ServerRpc
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        private void RequestEquipWeaponFromInventory_ServerRpc(string instanceId, WeaponSlotType slotType, NetworkConnection conn = null)
        {
            string operationId = $"equipWeapon_{instanceId}_{slotType}";
            
            // Step 1: ValidateRequest
            if (!ValidateRequest(conn, "EquipWeaponFromInventory"))
            {
                ConfirmOperation_TargetRpc(conn, operationId, OperationResult.UnknownError);
                return;
            }
            
            // Step 2: ValidateOwnership
            if (!ValidateOwnership(instanceId))
            {
                LogWarning($"[SERVER] Client {conn.ClientId} tried to equip weapon they don't own: {instanceId}");
                ConfirmOperation_TargetRpc(conn, operationId, OperationResult.ItemNotFound);
                return;
            }
            
            // Step 3: ValidateItemExists
            if (!ValidateItemExists(instanceId, out ItemInstance item))
            {
                LogWarning($"[SERVER] Weapon not found or invalid: {instanceId}");
                ConfirmOperation_TargetRpc(conn, operationId, OperationResult.ItemNotFound);
                return;
            }
            
            // Step 4: ValidateTypeCompatibility
            if (!ValidateItemTypeForWeapon(item, slotType))
            {
                LogWarning($"[SERVER] Item type incompatible with weapon slot: {item.Definition.ItemId} -> {slotType}");
                ConfirmOperation_TargetRpc(conn, operationId, OperationResult.IncompatibleSlot);
                return;
            }
            
            // Step 5: ValidateLimits (not needed for weapons)
            
            // Step 6: Execute Local System
            if (weaponSystem == null)
            {
                LogWarning("[SERVER] WeaponSystem not assigned!");
                ConfirmOperation_TargetRpc(conn, operationId, OperationResult.UnknownError);
                return;
            }
            
            var result = weaponSystem.EquipWeapon(item, slotType);
            
            // Remove from inventory if success
            if (result == OperationResult.Success)
            {
                inventorySystem.RemoveItem(instanceId);
            }
            
            // Step 7: Confirm to client
            ConfirmOperation_TargetRpc(conn, operationId, result);
            
            Log($"[SERVER] Equip weapon from inventory: {item.Definition.DisplayName} to {slotType} - {result}");
        }
        
        /// <summary>
        /// Request to assign item from inventory to quick slot.
        /// Execution order: Same as RequestEquipFromInventory_ServerRpc
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        private void RequestAssignQuickSlot_ServerRpc(string instanceId, int quickSlotIndex, NetworkConnection conn = null)
        {
            string operationId = $"assignQuickSlot_{instanceId}_{quickSlotIndex}";
            
            // Step 1: ValidateRequest
            if (!ValidateRequest(conn, "AssignQuickSlot"))
            {
                ConfirmOperation_TargetRpc(conn, operationId, OperationResult.UnknownError);
                return;
            }
            
            // Step 2: ValidateOwnership
            if (!ValidateOwnership(instanceId))
            {
                LogWarning($"[SERVER] Client {conn.ClientId} tried to assign quick slot for item they don't own: {instanceId}");
                ConfirmOperation_TargetRpc(conn, operationId, OperationResult.ItemNotFound);
                return;
            }
            
            // Step 3: ValidateItemExists
            if (!ValidateItemExists(instanceId, out ItemInstance item))
            {
                LogWarning($"[SERVER] Item not found or invalid: {instanceId}");
                ConfirmOperation_TargetRpc(conn, operationId, OperationResult.ItemNotFound);
                return;
            }
            
            // Step 4: ValidateTypeCompatibility (quick slots can accept any item type)
            
            // Step 5: ValidateLimits (check quick slot index)
            if (quickSlotSystem == null)
            {
                LogWarning("[SERVER] QuickSlotSystem not assigned!");
                ConfirmOperation_TargetRpc(conn, operationId, OperationResult.UnknownError);
                return;
            }
            
            if (quickSlotIndex < 0 || quickSlotIndex >= quickSlotSystem.GetQuickSlotCount())
            {
                LogWarning($"[SERVER] Invalid quick slot index: {quickSlotIndex}");
                ConfirmOperation_TargetRpc(conn, operationId, OperationResult.InvalidSlotIndex);
                return;
            }
            
            // Step 6: Execute Local System
            var result = quickSlotSystem.AssignToQuickSlot(item, quickSlotIndex);
            
            // Step 7: Confirm to client
            ConfirmOperation_TargetRpc(conn, operationId, result);
            
            Log($"[SERVER] Assign quick slot: {item.Definition.DisplayName} to slot {quickSlotIndex} - {result}");
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
            
            // Get current item at slot
            var existingItem = inventorySystem.GetItemAtSlot(index);
            
            // If empty data, remove item
            if (string.IsNullOrEmpty(data.InstanceId))
            {
                if (existingItem != null)
                {
                    inventorySystem.RemoveItemAtSlot(index, out _);
                }
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
            
            // Check if slot already has this exact item (same InstanceId)
            if (existingItem != null && existingItem.InstanceId == item.InstanceId)
            {
                // Same item - no update needed (already synced)
                return;
            }
            
            // If slot has different item, remove it first
            if (existingItem != null && existingItem.InstanceId != item.InstanceId)
            {
                inventorySystem.RemoveItemAtSlot(index, out _);
            }
            
            // Now slot is empty, add new item (will fire event)
            var result = inventorySystem.AddItemAtSlot(item, index);
            if (result != OperationResult.Success)
            {
                LogWarning($"[CLIENT] Failed to add item at slot {index}: {result}");
            }
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
            Log($"[SERVER] ValidateRequest: operationType={operationType}, enableValidation={enableValidation}, conn={conn?.ClientId ?? -1}");
            
            if (!enableValidation)
            {
                Log($"[SERVER] Validation disabled - allowing request");
                return true;
            }
            
            if (conn == null)
            {
                LogWarning($"[SERVER] Null connection for {operationType}");
                return false;
            }
            
            // Rate limiting
            Log($"[SERVER] Checking rate limit for client {conn.ClientId}...");
            if (!CheckRateLimit(conn))
            {
                LogWarning($"[SERVER] Rate limit exceeded for client {conn.ClientId}");
                if (logSuspiciousActivity)
                {
                    Debug.LogError($"[ANTI-CHEAT] Client {conn.ClientId} exceeded rate limit - possible hack attempt");
                }
                return false;
            }
            
            Log($"[SERVER] ValidateRequest passed for {operationType}");
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
        
        /// <summary>
        /// Validate item ownership - check if player owns the item in inventory.
        /// </summary>
        private bool ValidateOwnership(string instanceId)
        {
            if (inventorySystem == null)
                return false;
            
            return inventorySystem.HasItem(instanceId);
        }
        
        /// <summary>
        /// Validate item exists and is valid.
        /// </summary>
        private bool ValidateItemExists(string instanceId, out ItemInstance item)
        {
            item = null;
            
            if (inventorySystem == null)
                return false;
            
            item = inventorySystem.FindItem(instanceId);
            return item != null && item.Definition != null;
        }
        
        /// <summary>
        /// Validate item type is compatible with equipment slot.
        /// </summary>
        private bool ValidateItemTypeForEquipment(ItemInstance item, EquipmentSlotType slotType)
        {
            if (item == null || item.Definition == null)
                return false;
            
            // Check if item can be equipped in this slot
            return item.Definition.CanEquipInSlot(slotType);
        }
        
        /// <summary>
        /// Validate item type is compatible with weapon slot.
        /// </summary>
        private bool ValidateItemTypeForWeapon(ItemInstance item, WeaponSlotType slotType)
        {
            if (item == null || item.Definition == null)
                return false;
            
            // Check if item is a weapon
            if (item.Definition.ItemType != ItemType.Weapon)
                return false;
            
            // Additional validation can be done by WeaponSystem
            if (weaponSystem != null)
            {
                return weaponSystem.CanEquip(item, slotType);
            }
            
            return true;
        }
        
        /// <summary>
        /// Validate limits (weight, capacity, etc.).
        /// </summary>
        private bool ValidateLimits(ItemInstance item, string operationType)
        {
            if (item == null || item.Definition == null)
                return false;
            
            // Weight validation is handled by individual systems
            // This is a placeholder for additional limit checks
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
            Log($"[CLIENT] RequestPickup called: {itemDefinitionId}, stackSize={stackSize}, IsOwner={IsOwner}, IsClient={IsClient}, IsServer={IsServer}");
            
            if (!IsOwner)
            {
                LogWarning($"[CLIENT] RequestPickup rejected - not owner. IsOwner={IsOwner}, IsClient={IsClient}");
                return;
            }
            
            // Predict locally
            if (enableClientPrediction)
            {
                PredictOperation($"add_{itemDefinitionId}", () =>
                {
                    // Local prediction handled by InventorySystem events
                });
            }
            
            // Send to server
            Log($"[CLIENT] Calling RequestAddItem_ServerRpc: {itemDefinitionId}");
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
        
        // === CROSS-SYSTEM PUBLIC API ===
        
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
        
        /// <summary>
        /// Request to equip weapon from inventory to weapon slot.
        /// Step 1: Check IsOwner
        /// Step 2: Optional client prediction
        /// Step 3: Call ServerRpc
        /// </summary>
        public void RequestEquipWeaponFromInventory(string instanceId, WeaponSlotType slotType)
        {
            if (!IsOwner)
                return;
            
            string operationId = $"equipWeapon_{instanceId}_{slotType}";
            
            // Optional: Client prediction
            if (enableClientPrediction)
            {
                PredictOperation(operationId, () =>
                {
                    // Local prediction handled by WeaponSystem events
                });
            }
            
            // Send to server
            RequestEquipWeaponFromInventory_ServerRpc(instanceId, slotType);
        }
        
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
        
        void LogError(string message)
        {
            if (enableDebugLogs)
                Debug.LogError($"[InventoryNetworkSync] {message}");
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