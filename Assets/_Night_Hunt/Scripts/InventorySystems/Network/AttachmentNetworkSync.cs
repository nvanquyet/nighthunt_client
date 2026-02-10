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
    /// Network synchronization for AttachmentSystem.
    /// Syncs attachment state across network.
    /// Handles complex operations like attachment swap between two items.
    /// </summary>
    public class AttachmentNetworkSync : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private AttachmentSystem attachmentSystem;
        [SerializeField] private InventorySystem inventorySystem;
        [SerializeField] private EquipmentSystem equipmentSystem;
        [SerializeField] private WeaponSystem weaponSystem;
        
        [Header("Network Settings")]
        [SerializeField] private bool enableClientPrediction = true;
        [SerializeField] private float reconciliationTimeout = 2f;
        
        [Header("Anti-Cheat")]
        [SerializeField] private bool enableValidation = true;
        [SerializeField] private int maxOperationsPerSecond = 20;
        [SerializeField] private bool logSuspiciousActivity = true;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        // Synced attachment state: Parent Item ID → Array of Attachment Data
        private readonly SyncDictionary<string, ItemInstanceData[]> syncedAttachments = 
            new SyncDictionary<string, ItemInstanceData[]>();
        
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
            attachmentSystem = transform.parent.GetComponentInChildren<AttachmentSystem>();
            equipmentSystem = transform.parent.GetComponentInChildren<EquipmentSystem>();
            weaponSystem = transform.parent.GetComponentInChildren<WeaponSystem>();
            
        }
#endif
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Subscribe to attachment events (server-side)
            AttachmentEvents.OnAttachmentAttached += OnAttachmentAttached_Server;
            AttachmentEvents.OnAttachmentDetached += OnAttachmentDetached_Server;
            AttachmentEvents.OnAttachmentSwapped += OnAttachmentSwapped_Server;
            AttachmentEvents.OnAllAttachmentsDetached += OnAllAttachmentsDetached_Server;
            
            Log("Server started - attachment sync enabled");
        }
        
        public override void OnStopServer()
        {
            base.OnStopServer();
            
            AttachmentEvents.OnAttachmentAttached -= OnAttachmentAttached_Server;
            AttachmentEvents.OnAttachmentDetached -= OnAttachmentDetached_Server;
            AttachmentEvents.OnAttachmentSwapped -= OnAttachmentSwapped_Server;
            AttachmentEvents.OnAllAttachmentsDetached -= OnAllAttachmentsDetached_Server;
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            // Subscribe to SyncDictionary changes
            syncedAttachments.OnChange += OnSyncedAttachmentsChanged_Client;
            
            Log("Client started - listening for attachment updates");
        }
        
        public override void OnStopClient()
        {
            base.OnStopClient();
            
            syncedAttachments.OnChange -= OnSyncedAttachmentsChanged_Client;
        }
        
        // === SERVER EVENT HANDLERS ===
        
        private void OnAttachmentAttached_Server(ItemInstance parentItem, ItemInstance attachment, AttachmentSlotType slotType)
        {
            if (!IsServer)
                return;
            
            UpdateSyncedAttachments(parentItem);
            
            Log($"[SERVER] Attachment attached: {attachment.Definition.DisplayName} to {parentItem.Definition.DisplayName}");
        }
        
        private void OnAttachmentDetached_Server(ItemInstance parentItem, ItemInstance attachment, AttachmentSlotType slotType)
        {
            if (!IsServer)
                return;
            
            UpdateSyncedAttachments(parentItem);
            
            Log($"[SERVER] Attachment detached: {attachment.Definition.DisplayName} from {parentItem.Definition.DisplayName}");
        }
        
        private void OnAttachmentSwapped_Server(ItemInstance parentItem, ItemInstance oldAttachment, ItemInstance newAttachment, AttachmentSlotType slotType)
        {
            if (!IsServer)
                return;
            
            UpdateSyncedAttachments(parentItem);
            
            Log($"[SERVER] Attachment swapped on {parentItem.Definition.DisplayName}");
        }
        
        private void OnAllAttachmentsDetached_Server(ItemInstance parentItem)
        {
            if (!IsServer)
                return;
            
            if (syncedAttachments.ContainsKey(parentItem.InstanceId))
            {
                syncedAttachments.Remove(parentItem.InstanceId);
            }
            
            Log($"[SERVER] All attachments detached from {parentItem.Definition.DisplayName}");
        }
        
        /// <summary>
        /// Update synced attachments for a parent item.
        /// </summary>
        private void UpdateSyncedAttachments(ItemInstance parentItem)
        {
            if (parentItem == null)
                return;
            
            var attachments = parentItem.AttachedItems;
            var attachmentData = attachments.Select(a => a.Serialize()).ToArray();
            
            syncedAttachments[parentItem.InstanceId] = attachmentData;
        }
        
        // === CLIENT SYNC ===
        
        private void OnSyncedAttachmentsChanged_Client(SyncDictionaryOperation op, string key, ItemInstanceData[] value, bool asServer)
        {
            if (asServer)
                return;
            
            Log($"[CLIENT] Attachment sync: {op} for parent {key}");
            
            if (attachmentSystem == null || inventorySystem == null)
                return;
            
            switch (op)
            {
                case SyncDictionaryOperation.Add:
                case SyncDictionaryOperation.Set:
                    UpdateAttachmentsFromSync(key, value);
                    break;
                
                case SyncDictionaryOperation.Remove:
                    // Parent item no longer has attachments
                    break;
                
                case SyncDictionaryOperation.Clear:
                    // All attachments cleared
                    break;
            }
        }
        
        /// <summary>
        /// Update local attachments from synced data.
        /// </summary>
        private void UpdateAttachmentsFromSync(string parentItemId, ItemInstanceData[] attachmentDataArray)
        {
            // Find parent item
            ItemInstance parentItem = FindItemById(parentItemId);
            if (parentItem == null)
            {
                LogWarning($"[CLIENT] Parent item not found: {parentItemId}");
                return;
            }
            
            // Get current attachments
            var currentAttachments = parentItem.AttachedItems.ToList();
            
            // Deserialize synced attachments
            var syncedAttachments = new List<ItemInstance>();
            foreach (var data in attachmentDataArray)
            {
                var definition = GetItemDefinition(data.ItemId);
                if (definition == null)
                {
                    LogWarning($"[CLIENT] Unknown attachment definition: {data.ItemId}");
                    continue;
                }
                
                var attachment = ItemInstance.Deserialize(data, definition);
                syncedAttachments.Add(attachment);
            }
            
            // Remove attachments that are no longer in sync
            foreach (var currentAttachment in currentAttachments.ToList())
            {
                if (!syncedAttachments.Any(a => a.InstanceId == currentAttachment.InstanceId))
                {
                    attachmentSystem.DetachItem(parentItem, currentAttachment.Definition.AttachmentType, out _);
                }
            }
            
            // Add new attachments from sync
            foreach (var syncedAttachment in syncedAttachments)
            {
                if (!currentAttachments.Any(a => a.InstanceId == syncedAttachment.InstanceId))
                {
                    // Find the actual item instance from inventory
                    var actualAttachment = inventorySystem.FindItem(syncedAttachment.InstanceId);
                    if (actualAttachment != null)
                    {
                        attachmentSystem.AttachItem(parentItem, actualAttachment);
                    }
                }
            }
        }
        
        /// <summary>
        /// Find item by ID (check inventory, equipment, weapons).
        /// </summary>
        private ItemInstance FindItemById(string instanceId)
        {
            // Check inventory
            if (inventorySystem != null)
            {
                var item = inventorySystem.FindItem(instanceId);
                if (item != null)
                    return item;
            }
            
            // Check equipment
            if (equipmentSystem != null)
            {
                var allEquipped = equipmentSystem.GetAllEquippedItems();
                var equipped = allEquipped.FirstOrDefault(i => i.InstanceId == instanceId);
                if (equipped != null)
                    return equipped;
            }
            
            // Check weapons
            if (weaponSystem != null)
            {
                var primary = weaponSystem.GetEquippedWeapon(WeaponSlotType.Primary);
                if (primary != null && primary.InstanceId == instanceId)
                    return primary;
                
                var secondary = weaponSystem.GetEquippedWeapon(WeaponSlotType.Secondary);
                if (secondary != null && secondary.InstanceId == instanceId)
                    return secondary;
            }
            
            return null;
        }
        
        // === PUBLIC API ===
        
        /// <summary>
        /// Request to attach item to parent item.
        /// Step 1: Check IsOwner
        /// Step 2: Optional client prediction
        /// Step 3: Call ServerRpc
        /// </summary>
        public void RequestAttach(string parentItemId, string attachmentInstanceId)
        {
            if (!IsOwner)
                return;
            
            string operationId = $"attach_{parentItemId}_{attachmentInstanceId}";
            
            if (enableClientPrediction)
            {
                PredictOperation(operationId, () => { });
            }
            
            RequestAttach_ServerRpc(parentItemId, attachmentInstanceId);
        }
        
        /// <summary>
        /// Request to detach attachment from parent item.
        /// </summary>
        public void RequestDetach(string parentItemId, AttachmentSlotType slotType)
        {
            if (!IsOwner)
                return;
            
            string operationId = $"detach_{parentItemId}_{slotType}";
            
            if (enableClientPrediction)
            {
                PredictOperation(operationId, () => { });
            }
            
            RequestDetach_ServerRpc(parentItemId, slotType);
        }
        
        /// <summary>
        /// Request to swap attachments between two items.
        /// This is a complex operation that requires validation of both items.
        /// </summary>
        public void RequestSwapAttachments(string sourceItemId, AttachmentSlotType sourceSlot, string targetItemId, AttachmentSlotType targetSlot)
        {
            if (!IsOwner)
                return;
            
            string operationId = $"swapAttachments_{sourceItemId}_{targetItemId}";
            
            if (enableClientPrediction)
            {
                PredictOperation(operationId, () => { });
            }
            
            RequestSwapAttachments_ServerRpc(sourceItemId, sourceSlot, targetItemId, targetSlot);
        }
        
        // === SERVER RPCs ===
        
        /// <summary>
        /// Request to attach item to parent item.
        /// Execution order:
        /// Step 1: ValidateRequest → Rate limit, connection
        /// Step 2: ValidateOwnership → Check both items owned
        /// Step 3: ValidateItemExists → Check both items exist
        /// Step 4: ValidateTypeCompatibility → Check attachment compatibility
        /// Step 5: ValidateLimits → Check slot availability
        /// Step 6: Execute Local System → AttachItem
        /// Step 7: ConfirmOperation_TargetRpc → Notify client
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        private void RequestAttach_ServerRpc(string parentItemId, string attachmentInstanceId, NetworkConnection conn = null)
        {
            string operationId = $"attach_{parentItemId}_{attachmentInstanceId}";
            
            // Step 1: ValidateRequest
            if (!ValidateRequest(conn, "Attach"))
            {
                ConfirmAttachmentOperation_TargetRpc(conn, OperationResult.UnknownError, operationId);
                return;
            }
            
            // Step 2: ValidateOwnership
            if (!ValidateOwnership(parentItemId) || !ValidateOwnership(attachmentInstanceId))
            {
                LogWarning($"[SERVER] Client {conn.ClientId} tried to attach item they don't own");
                ConfirmAttachmentOperation_TargetRpc(conn, OperationResult.ItemNotFound, operationId);
                return;
            }
            
            // Step 3: ValidateItemExists
            var parentItem = FindItemById(parentItemId);
            var attachment = inventorySystem.FindItem(attachmentInstanceId);
            
            if (parentItem == null || attachment == null)
            {
                LogWarning($"[SERVER] Items not found: parent={parentItemId}, attachment={attachmentInstanceId}");
                ConfirmAttachmentOperation_TargetRpc(conn, OperationResult.ItemNotFound, operationId);
                return;
            }
            
            // Step 4: ValidateTypeCompatibility
            if (!attachmentSystem.CanAttach(parentItem, attachment))
            {
                LogWarning($"[SERVER] Cannot attach {attachment.Definition.ItemId} to {parentItem.Definition.ItemId}");
                ConfirmAttachmentOperation_TargetRpc(conn, OperationResult.IncompatibleAttachment, operationId);
                return;
            }
            
            // Step 5: ValidateLimits (check slot availability)
            if (attachmentSystem.HasAttachment(parentItem, attachment.Definition.AttachmentType))
            {
                LogWarning($"[SERVER] Attachment slot already occupied");
                ConfirmAttachmentOperation_TargetRpc(conn, OperationResult.AttachmentSlotOccupied, operationId);
                return;
            }
            
            // Step 6: Execute Local System
            var result = attachmentSystem.AttachItem(parentItem, attachment);
            
            // Remove from inventory if success
            if (result == OperationResult.Success)
            {
                inventorySystem.RemoveItem(attachmentInstanceId);
            }
            
            // Step 7: Confirm to client
            ConfirmAttachmentOperation_TargetRpc(conn, result, operationId);
            
            Log($"[SERVER] Attach: {attachment.Definition.DisplayName} to {parentItem.Definition.DisplayName} - {result}");
        }
        
        /// <summary>
        /// Request to detach attachment from parent item.
        /// Execution order: Same as RequestAttach_ServerRpc
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        private void RequestDetach_ServerRpc(string parentItemId, AttachmentSlotType slotType, NetworkConnection conn = null)
        {
            string operationId = $"detach_{parentItemId}_{slotType}";
            
            // Step 1: ValidateRequest
            if (!ValidateRequest(conn, "Detach"))
            {
                ConfirmAttachmentOperation_TargetRpc(conn, OperationResult.UnknownError, operationId);
                return;
            }
            
            // Step 2: ValidateOwnership
            if (!ValidateOwnership(parentItemId))
            {
                LogWarning($"[SERVER] Client {conn.ClientId} tried to detach from item they don't own");
                ConfirmAttachmentOperation_TargetRpc(conn, OperationResult.ItemNotFound, operationId);
                return;
            }
            
            // Step 3: ValidateItemExists
            var parentItem = FindItemById(parentItemId);
            if (parentItem == null)
            {
                LogWarning($"[SERVER] Parent item not found: {parentItemId}");
                ConfirmAttachmentOperation_TargetRpc(conn, OperationResult.ItemNotFound, operationId);
                return;
            }
            
            // Step 4: ValidateTypeCompatibility (not needed for detach)
            
            // Step 5: ValidateLimits (check if attachment exists)
            if (!attachmentSystem.HasAttachment(parentItem, slotType))
            {
                LogWarning($"[SERVER] No attachment in slot: {slotType}");
                ConfirmAttachmentOperation_TargetRpc(conn, OperationResult.ItemNotFound, operationId);
                return;
            }
            
            // Step 6: Execute Local System
            var result = attachmentSystem.DetachItem(parentItem, slotType, out ItemInstance detachedItem);
            
            // Step 7: Confirm to client
            ConfirmAttachmentOperation_TargetRpc(conn, result, operationId);
            
            Log($"[SERVER] Detach: {slotType} from {parentItem.Definition.DisplayName} - {result}");
        }
        
        /// <summary>
        /// Request to swap attachments between two items.
        /// This is the most complex operation - requires validation of both items and both attachments.
        /// Execution order:
        /// Step 1: ValidateRequest
        /// Step 2: ValidateOwnership (both items)
        /// Step 3: ValidateItemExists (both items)
        /// Step 4: ValidateTypeCompatibility (both attachments compatible with target items)
        /// Step 5: ValidateLimits (both items have attachments in specified slots)
        /// Step 6: Execute Local System (detach both, attach swapped)
        /// Step 7: ConfirmOperation_TargetRpc
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        private void RequestSwapAttachments_ServerRpc(string sourceItemId, AttachmentSlotType sourceSlot, string targetItemId, AttachmentSlotType targetSlot, NetworkConnection conn = null)
        {
            string operationId = $"swapAttachments_{sourceItemId}_{targetItemId}";
            
            // Step 1: ValidateRequest
            if (!ValidateRequest(conn, "SwapAttachments"))
            {
                ConfirmAttachmentOperation_TargetRpc(conn, OperationResult.UnknownError, operationId);
                return;
            }
            
            // Step 2: ValidateOwnership (both items)
            if (!ValidateOwnership(sourceItemId) || !ValidateOwnership(targetItemId))
            {
                LogWarning($"[SERVER] Client {conn.ClientId} tried to swap attachments from items they don't own");
                ConfirmAttachmentOperation_TargetRpc(conn, OperationResult.ItemNotFound, operationId);
                return;
            }
            
            // Step 3: ValidateItemExists (both items)
            var sourceItem = FindItemById(sourceItemId);
            var targetItem = FindItemById(targetItemId);
            
            if (sourceItem == null || targetItem == null)
            {
                LogWarning($"[SERVER] Items not found: source={sourceItemId}, target={targetItemId}");
                ConfirmAttachmentOperation_TargetRpc(conn, OperationResult.ItemNotFound, operationId);
                return;
            }
            
            // Step 4: ValidateTypeCompatibility (both attachments must be compatible with target items)
            var sourceAttachment = attachmentSystem.GetAttachment(sourceItem, sourceSlot);
            var targetAttachment = attachmentSystem.GetAttachment(targetItem, targetSlot);
            
            if (sourceAttachment == null || targetAttachment == null)
            {
                LogWarning($"[SERVER] One or both attachments not found");
                ConfirmAttachmentOperation_TargetRpc(conn, OperationResult.ItemNotFound, operationId);
                return;
            }
            
            // Check if sourceAttachment can attach to targetItem
            if (!attachmentSystem.CanAttach(targetItem, sourceAttachment))
            {
                LogWarning($"[SERVER] Source attachment incompatible with target item");
                ConfirmAttachmentOperation_TargetRpc(conn, OperationResult.IncompatibleAttachment, operationId);
                return;
            }
            
            // Check if targetAttachment can attach to sourceItem
            if (!attachmentSystem.CanAttach(sourceItem, targetAttachment))
            {
                LogWarning($"[SERVER] Target attachment incompatible with source item");
                ConfirmAttachmentOperation_TargetRpc(conn, OperationResult.IncompatibleAttachment, operationId);
                return;
            }
            
            // Step 5: ValidateLimits (both items have attachments in specified slots - already checked above)
            
            // Step 6: Execute Local System
            // Detach source attachment
            attachmentSystem.DetachItem(sourceItem, sourceSlot, out ItemInstance sourceDetached);
            
            // Detach target attachment
            attachmentSystem.DetachItem(targetItem, targetSlot, out ItemInstance targetDetached);
            
            // Attach targetAttachment to sourceItem
            var result1 = attachmentSystem.AttachItem(sourceItem, targetDetached);
            
            // Attach sourceAttachment to targetItem
            var result2 = attachmentSystem.AttachItem(targetItem, sourceDetached);
            
            var result = (result1 == OperationResult.Success && result2 == OperationResult.Success) 
                ? OperationResult.Success 
                : OperationResult.UnknownError;
            
            // Step 7: Confirm to client
            ConfirmAttachmentOperation_TargetRpc(conn, result, operationId);
            
            Log($"[SERVER] Swap attachments: {sourceItem.Definition.DisplayName} ↔ {targetItem.Definition.DisplayName} - {result}");
        }
        
        // === TARGET RPCs ===
        
        [TargetRpc]
        private void ConfirmAttachmentOperation_TargetRpc(NetworkConnection conn, OperationResult result, string operationId)
        {
            var pending = pendingOperations.Find(op => op.OperationId == operationId);
            if (pending != null)
            {
                pendingOperations.Remove(pending);
                
                if (result == OperationResult.Success)
                {
                    Log($"[CLIENT] Attachment operation confirmed: {operationId}");
                }
                else
                {
                    LogWarning($"[CLIENT] Attachment operation rejected: {operationId} - {result}");
                    RollbackPrediction(pending);
                }
            }
        }
        
        // === CLIENT PREDICTION ===
        
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
        
        /// <summary>
        /// Validate item ownership - check if player owns the item.
        /// </summary>
        private bool ValidateOwnership(string instanceId)
        {
            // Check inventory
            if (inventorySystem != null && inventorySystem.HasItem(instanceId))
                return true;
            
            // Check equipment
            if (equipmentSystem != null)
            {
                var allEquipped = equipmentSystem.GetAllEquippedItems();
                if (allEquipped.Any(i => i.InstanceId == instanceId))
                    return true;
            }
            
            // Check weapons
            if (weaponSystem != null)
            {
                var primary = weaponSystem.GetEquippedWeapon(WeaponSlotType.Primary);
                if (primary != null && primary.InstanceId == instanceId)
                    return true;
                
                var secondary = weaponSystem.GetEquippedWeapon(WeaponSlotType.Secondary);
                if (secondary != null && secondary.InstanceId == instanceId)
                    return true;
            }
            
            return false;
        }
        
        // === HELPERS ===
        
        private ItemDefinition GetItemDefinition(string itemId)
        {
            return ItemDefinitionDatabase.Instance.GetDefinition(itemId);
        }
        
        void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[AttachmentNetworkSync] {message}");
        }
        
        void LogWarning(string message)
        {
            Debug.LogWarning($"[AttachmentNetworkSync] {message}");
        }
    }
}
