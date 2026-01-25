using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using NightHunt.InteractionSystem.Core;
using UnityEngine;

namespace NightHunt.InteractionSystem.Pickup
{
     public class PickupHandler : NetworkBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private GridInventoryComponent inventory;
    [SerializeField] private PickupAnimator animator;
    
    [Header("Validation")]
    [SerializeField] private float maxPickupDistance = 5f;
    [SerializeField] private LayerMask obstacleLayer;
    
    private readonly List<IPickupable> pickupQueue = new List<IPickupable>();
    
    // Manual pickup (from input)
    public void RequestPickup(IPickupable pickupable)
    {
        if (!IsOwner) return;
        
        ServerPickup(pickupable);
    }
    
    // Auto pickup (from trigger)
    public void RequestAutoPickup(IPickupable pickupable)
    {
        if (!IsOwner) return;
        
        // Add to queue to batch process
        if (!pickupQueue.Contains(pickupable))
        {
            pickupQueue.Add(pickupable);
        }
    }
    
    private void LateUpdate()
    {
        if (!IsOwner || pickupQueue.Count == 0) return;
        
        // Process max 3 items per frame to avoid lag
        int processed = 0;
        while (pickupQueue.Count > 0 && processed < 3)
        {
            IPickupable item = pickupQueue[0];
            pickupQueue.RemoveAt(0);
            
            ServerPickup(item);
            processed++;
        }
    }
    
    [ServerRpc]
    private void ServerPickup(IPickupable pickupable)
    {
        // Server validation
        if (!ValidatePickup(pickupable, out string error))
        {
            TargetShowError(LocalConnection, error);
            return;
        }
        
        // Try stack first
        ItemInstance itemInstance = CreateItemInstance(pickupable);
        
        if (ItemStackingService.TryAutoStack(inventory, itemInstance, out ItemInstance merged))
        {
            // Stacked successfully
            OnPickupSuccess(pickupable, merged, isStacked: true);
        }
        else
        {
            // Add new item
            if (inventory.TryAddItem(itemInstance))
            {
                OnPickupSuccess(pickupable, itemInstance, isStacked: false);
            }
            else
            {
                TargetShowError(LocalConnection, "Inventory full!");
            }
        }
    }
    
    private bool ValidatePickup(IPickupable pickupable, out string error)
    {
        error = null;
        
        // Check null
        if (pickupable == null)
        {
            error = "Invalid item";
            return false;
        }
        
        // Check distance
        float distance = Vector3.Distance(transform.position, pickupable.WorldPosition);
        if (distance > maxPickupDistance)
        {
            error = "Too far away";
            return false;
        }
        
        // Check line of sight
        Vector3 direction = pickupable.WorldPosition - transform.position;
        if (Physics.Raycast(transform.position, direction, distance, obstacleLayer))
        {
            error = "Blocked by obstacle";
            return false;
        }
        
        // Check item-specific validation
        if (!pickupable.CanPickup(LocalConnection))
        {
            error = "Cannot pickup this item";
            return false;
        }
        
        return true;
    }
    
    private void OnPickupSuccess(IPickupable pickupable, ItemInstance item, bool isStacked)
    {
        // Notify item it was picked up
        pickupable.OnPickedUp(LocalConnection);
        
        // Play animation
        ObserversPlayPickupAnimation(pickupable.WorldPosition);
        
        // Notify client
        TargetShowNotification(LocalConnection, item, isStacked);
    }
    
    [ObserversRpc]
    private void ObserversPlayPickupAnimation(Vector3 itemPosition)
    {
        animator?.PlayPickupAnimation(itemPosition);
    }
    
    [TargetRpc]
    private void TargetShowNotification(NetworkConnection conn, ItemInstance item, bool isStacked)
    {
        string message = isStacked 
            ? $"Picked up {item.quantity}x {item.itemDataId}" 
            : $"Picked up {item.itemDataId}";
        
        NotificationManager.Instance?.Show(message);
    }
    
    [TargetRpc]
    private void TargetShowError(NetworkConnection conn, string error)
    {
        NotificationManager.Instance?.ShowError(error);
    }
    
    private ItemInstance CreateItemInstance(IPickupable pickupable)
    {
        return new ItemInstance
        {
            instanceId = Guid.NewGuid().ToString(),
            itemDataId = pickupable.ItemData.itemId,
            quantity = pickupable.Quantity,
            durability = 100f,
            metadata = new Dictionary<string, object>(),
            attachments = new List<AttachmentInstance>()
        };
    }
}
}