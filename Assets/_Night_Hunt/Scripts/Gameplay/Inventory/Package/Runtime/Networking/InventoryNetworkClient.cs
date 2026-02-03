using FishNet.Object;
using FishNet.Connection;
using UnityEngine;
using NightHunt.Inventory.Core;
using NightHunt.Inventory.Domain;
using NightHunt.Inventory.Events;

namespace NightHunt.Inventory.Networking
{
    /// <summary>
    /// Client-side network requests for inventory operations.
    /// Server-authoritative: All operations validated server-side.
    /// </summary>
    public class InventoryNetworkClient : NetworkBehaviour
    {
        private InventoryManager inventoryManager;
        private InventoryOperationValidator validator;
        
        void Awake()
        {
            // Use ComponentFinder to find components in hierarchy
            inventoryManager = ComponentFinder.FindInHierarchy<InventoryManager>(this);
            validator = ComponentFinder.FindInHierarchy<InventoryOperationValidator>(this);
        }
        
        void OnEnable()
        {
            DragDropEvents.OnDrop += OnDropRequested;
            InventoryEvents.OnRequestTrashItem += OnTrashRequested;
            InventoryEvents.OnRequestDropStack += OnDropStackRequested;
        }
        
        void OnDisable()
        {
            DragDropEvents.OnDrop -= OnDropRequested;
            InventoryEvents.OnRequestTrashItem -= OnTrashRequested;
            InventoryEvents.OnRequestDropStack -= OnDropStackRequested;
        }
        
        void OnDropRequested(DragContext context)
        {
            if (!IsOwner) return;
            
            // Apply optimistic update (client prediction)
            // TODO: Implement client prediction
            
            // Send to server - only send primitive data, not ItemInstance
            RequestMoveItemServerRpc(
                context.ItemInstance.InstanceId,
                context.SourceLocation,
                context.SourceIndex,
                context.TargetLocation,
                context.TargetIndex
            );
        }
        
        void OnTrashRequested(ItemInstance item)
        {
            if (!IsOwner) return;
            
            RequestTrashItemServerRpc(item.InstanceId);
        }
        
        void OnDropStackRequested(ItemInstance item, int amount)
        {
            if (!IsOwner) return;
            
            RequestDropStackServerRpc(item.InstanceId, amount);
        }
        
        [ServerRpc]
        void RequestMoveItemServerRpc(
            string itemInstanceId,
            SlotLocationType sourceLocation,
            int sourceIndex,
            SlotLocationType targetLocation,
            int targetIndex)
        {
            // Get item from inventory
            var item = inventoryManager?.GetItem(itemInstanceId);
            if (item == null)
            {
                RejectOperationTargetRpc(Owner, "Item not found");
                return;
            }
            
            // Create DragContext for validation
            var context = new DragContext
            {
                ItemInstance = item,
                SourceLocation = sourceLocation,
                SourceIndex = sourceIndex,
                TargetLocation = targetLocation,
                TargetIndex = targetIndex
            };
            
            // Server validates
            if (validator != null)
            {
                var result = validator.ValidateDrop(context, Owner);
                
                if (result.IsSuccess)
                {
                    // Execute operation
                    // TODO: Execute move operation
                    
                    // Broadcast to all clients
                    var snapshot = inventoryManager.CreateSnapshot();
                    BroadcastInventoryUpdateObserversRpc(snapshot.Serialize());
                }
                else
                {
                    // Reject operation
                    RejectOperationTargetRpc(Owner, result.FailReason);
                }
            }
        }
        
        [ServerRpc]
        void RequestTrashItemServerRpc(string itemInstanceId)
        {
            // Validate ownership
            if (inventoryManager == null || !inventoryManager.HasItem(itemInstanceId))
            {
                Debug.LogWarning($"[Trash] Item not found: {itemInstanceId}");
                return;
            }
            
            var item = inventoryManager.GetItem(itemInstanceId);
            
            // Remove from inventory
            inventoryManager.TryRemoveItem(itemInstanceId);
            
            // TODO: Remove stat modifiers if equipped
            
            // Broadcast update
            var snapshot = inventoryManager.CreateSnapshot();
            BroadcastInventoryUpdateObserversRpc(snapshot.Serialize());
            
            Debug.Log($"[Trash] Item destroyed: {item.Definition.ItemId}");
        }
        
        [ServerRpc]
        void RequestDropStackServerRpc(string itemInstanceId, int amount)
        {
            // TODO: Implement stack drop
            Debug.Log($"[Drop] Dropping {amount} of {itemInstanceId}");
        }
        
        [TargetRpc]
        void RejectOperationTargetRpc(NetworkConnection target, string reason)
        {
            // Rollback optimistic update
            // TODO: Implement rollback
            Debug.LogWarning($"[Inventory] Operation rejected: {reason}");
        }
        
        [ObserversRpc]
        void BroadcastInventoryUpdateObserversRpc(byte[] snapshotData)
        {
            var snapshot = InventorySnapshot.Deserialize(snapshotData);
            if (inventoryManager != null)
            {
                inventoryManager.ApplySnapshot(snapshot);
            }
            InventoryEvents.FireInventoryChanged(snapshot);
        }
    }
}
