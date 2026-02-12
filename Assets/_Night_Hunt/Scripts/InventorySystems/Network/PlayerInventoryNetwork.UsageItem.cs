using FishNet.Object;
using FishNet.Connection;
using System.Collections;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Structs;

namespace NightHunt.Inventory.Network
{
    public partial class PlayerInventoryNetwork
    {
        // ===== ITEM USAGE =====
        
        public bool TryUseItem(string instanceId)
        {
            if (!IsOwner)
            {
                LogWarning("TryUseItem: Not owner!");
                return false;
            }
            
            // If already using an item, try to cancel it first
            if (isUsingItem)
            {
                if (currentUsageItemInstance != null && currentUsageItemInstance.Definition.CanCancelUsage)
                {
                    CancelItemUsage();
                }
                else
                {
                    LogWarning("TryUseItem: Already using an item that cannot be cancelled");
                    return false;
                }
            }
            
            UseItemServerRpc(instanceId);
            return true;
        }
        
        [ServerRpc]
        private void UseItemServerRpc(string instanceId, NetworkConnection sender = null)
        {
            var item = GetItemFromAnywhere(instanceId);
            
            if (item == null)
            {
                SendOperationFailedObserverRpc("UseItem", "Item not found", sender);
                return;
            }
            
            // Validate can use
            if (!validator.ValidateItemUsage(item))
            {
                SendOperationFailedObserverRpc("UseItem", "Item cannot be used", sender);
                return;
            }
            
            // Check resource if applicable
            if (item.Definition.ResourceType != ItemResourceType.None)
            {
                if (item.CurrentResource <= 0f)
                {
                    SendOperationFailedObserverRpc("UseItem", "Item resource depleted", sender);
                    return;
                }
            }
            
            // Get usage duration
            float duration = item.Definition.UsageDuration;
            
            if (duration <= 0f)
            {
                // Instant use
                CompleteItemUsageImmediate(item);
            }
            else
            {
                // Start usage timer
                StartItemUsage(item, duration);
            }
        }
        
        private void StartItemUsage(ItemInstance item, float duration)
        {
            currentUsageItemInstance = item;
            currentUsageItem = item.Serialize();
            usageTimeRemaining = duration;
            isUsingItem = true;
            
            Log($"Started using {item.Definition.DisplayName} (duration: {duration}s)");
            
            // Notify movement system if item blocks movement
            if (item.Definition.BlocksMovementWhileUsing)
            {
                // TODO: Notify movement controller to disable movement
            }
            
            // Raise event (sync to all observers via SyncVar change callback)
        }
        
        public void CancelItemUsage()
        {
            if (!IsOwner)
            {
                LogWarning("CancelItemUsage: Not owner!");
                return;
            }
            
            if (!isUsingItem)
            {
                LogWarning("CancelItemUsage: No item being used");
                return;
            }
            
            if (currentUsageItemInstance == null)
            {
                LogWarning("CancelItemUsage: Current usage item is null");
                return;
            }
            
            if (!currentUsageItemInstance.Definition.CanCancelUsage)
            {
                LogWarning("CancelItemUsage: Current item cannot be cancelled");
                return;
            }
            
            CancelItemUsageServerRpc();
        }
        
        [ServerRpc]
        private void CancelItemUsageServerRpc(NetworkConnection sender = null)
        {
            if (!isUsingItem || currentUsageItemInstance == null)
            {
                SendOperationFailedObserverRpc("CancelItemUsage", "No item being used", sender);
                return;
            }
            
            if (!currentUsageItemInstance.Definition.CanCancelUsage)
            {
                SendOperationFailedObserverRpc("CancelItemUsage", "Item cannot be cancelled", sender);
                return;
            }
            
            Log($"Cancelled using {currentUsageItemInstance.Definition.DisplayName}");
            
            // Clear usage state
            RaiseItemUsageCancelledObserverRpc(currentUsageItemInstance.Serialize());
            
            currentUsageItem = default;
            currentUsageItemInstance = null;
            usageTimeRemaining = 0f;
            isUsingItem = false;
        }
        
        [ServerRpc]
        private void CompleteItemUsageServerRpc(NetworkConnection sender = null)
        {
            if (!isUsingItem || currentUsageItemInstance == null)
            {
                SendOperationFailedObserverRpc("CompleteItemUsage", "No item being used", sender);
                return;
            }
            
            CompleteItemUsageImmediate(currentUsageItemInstance);
        }
        
        private void CompleteItemUsageImmediate(ItemInstance item)
        {
            Log($"Completed using {item.Definition.DisplayName}");
            
            // Apply item effects
            ApplyItemEffects(item);
            
            // Consume resource if applicable
            if (item.Definition.ResourceType != ItemResourceType.None)
            {
                item.DecreaseResource(1f); // Or custom amount
                UpdateItemInAnyLocation(item);
                
                // Raise resource changed event
                RaiseItemResourceChangedObserverRpc(item.InstanceId, item.CurrentResource, item.Definition.MaxResource);
            }
            
            // Reduce stack if consumable
            if (item.Definition.ItemType == ItemType.Consumable)
            {
                if (item.StackSize > 1)
                {
                    item.StackSize--;
                    UpdateItemInAnyLocation(item);
                }
                else
                {
                    // Last item in stack, remove it
                    TryRemoveItem(item.InstanceId);
                }
            }
            
            // Raise completion event
            RaiseItemUsageCompletedObserverRpc(item.Serialize());
            
            // Clear usage state
            currentUsageItem = default;
            currentUsageItemInstance = null;
            usageTimeRemaining = 0f;
            isUsingItem = false;
        }
        
        private void ApplyItemEffects(ItemInstance item)
        {
            // TODO: Implement item effects based on item type
            // For now, just log
            
            switch (item.Definition.ItemType)
            {
                case ItemType.Consumable:
                    // Example: Health potion
                    if (playerStats != null)
                    {
                        // playerStats.RestoreHealth(amount);
                        Log($"Applied consumable effect: {item.Definition.DisplayName}");
                    }
                    break;
                
                // Add other item type effects here
            }
        }
        
        // ===== RESOURCE MANAGEMENT =====
        
        public bool TryRefillResource(string instanceId, float amount)
        {
            if (!IsOwner)
            {
                LogWarning("TryRefillResource: Not owner!");
                return false;
            }
            
            RefillResourceServerRpc(instanceId, amount);
            return true;
        }
        
        [ServerRpc]
        private void RefillResourceServerRpc(string instanceId, float amount, NetworkConnection sender = null)
        {
            var item = GetItemFromAnywhere(instanceId);
            
            if (item == null)
            {
                SendOperationFailedObserverRpc("RefillResource", "Item not found", sender);
                return;
            }
            
            // Validate
            if (!validator.ValidateResourceRefill(item, amount))
            {
                SendOperationFailedObserverRpc("RefillResource", "Invalid resource refill", sender);
                return;
            }
            
            float oldValue = item.CurrentResource;
            item.ModifyResource(amount);
            
            // Update item
            UpdateItemInAnyLocation(item);
            
            Log($"Refilled {item.Definition.DisplayName} resource: {oldValue} -> {item.CurrentResource}");
            
            // Raise event
            RaiseItemResourceChangedObserverRpc(instanceId, item.CurrentResource, item.Definition.MaxResource);
        }
        
        // ===== EVENT RPCS =====
        
        [ObserversRpc]
        private void RaiseItemUsageCompletedObserverRpc(ItemInstanceData itemData)
        {
            if (!IsOwner)
                return;
            
            var item = DeserializeItem(itemData);
            
            InventoryEvents.RaiseItemUsageCompleted(new ItemUsageCompletedEvent
            {
                OwnerId = ObjectId,
                IsLocalPlayer = true,
                Item = item
            });
        }
        
        [ObserversRpc]
        private void RaiseItemUsageCancelledObserverRpc(ItemInstanceData itemData)
        {
            if (!IsOwner)
                return;
            
            var item = DeserializeItem(itemData);
            
            InventoryEvents.RaiseItemUsageCancelled(new ItemUsageCancelledEvent
            {
                OwnerId = ObjectId,
                IsLocalPlayer = true,
                Item = item
            });
        }
        
        [ObserversRpc]
        private void RaiseItemResourceChangedObserverRpc(string instanceId, float newValue, float maxValue)
        {
            if (!IsOwner)
                return;
            
            // Calculate old value from cache if available
            float oldValue = 0f;
            if (runtimeItemCache.TryGetValue(instanceId, out ItemInstance cachedItem))
            {
                oldValue = cachedItem.CurrentResource;
            }
            
            InventoryEvents.RaiseItemResourceChanged(new ItemResourceChangedEvent
            {
                OwnerId = ObjectId,
                IsLocalPlayer = true,
                InstanceId = instanceId,
                OldValue = oldValue,
                NewValue = newValue,
                MaxValue = maxValue
            });
        }
        
        // ===== PUBLIC GETTERS FOR UI =====
        
        /// <summary>
        /// Get current item being used (if any).
        /// </summary>
        public ItemInstance GetCurrentUsageItem()
        {
            return currentUsageItemInstance;
        }
        
        /// <summary>
        /// Get remaining usage time (for progress bar).
        /// </summary>
        public float GetUsageTimeRemaining()
        {
            return usageTimeRemaining;
        }
        
        /// <summary>
        /// Is player currently using an item?
        /// </summary>
        public bool IsUsingItem()
        {
            return isUsingItem;
        }
        
        /// <summary>
        /// Get usage progress (0.0 - 1.0 for UI).
        /// </summary>
        public float GetUsageProgress()
        {
            if (!isUsingItem || currentUsageItemInstance == null)
                return 0f;
            
            float totalDuration = currentUsageItemInstance.Definition.UsageDuration;
            if (totalDuration <= 0f)
                return 1f;
            
            return 1f - (usageTimeRemaining / totalDuration);
        }
    }
}