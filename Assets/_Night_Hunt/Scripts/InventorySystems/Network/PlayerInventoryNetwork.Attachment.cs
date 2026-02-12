// CONTINUATION OF PlayerInventoryNetwork.cs
// Part 4: Attachment System

using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Structs;

namespace NightHunt.Inventory.Network
{
    public partial class PlayerInventoryNetwork
    {
        // ===== ATTACHMENT OPERATIONS =====
        
        public bool TryAttachToItem(string attachmentInstanceId, string hostInstanceId, AttachmentSlotType slotType)
        {
            if (!IsOwner)
            {
                LogWarning("TryAttachToItem: Not owner!");
                return false;
            }
            
            AttachToItemServerRpc(attachmentInstanceId, hostInstanceId, slotType);
            return true;
        }
        
        [ServerRpc]
        private void AttachToItemServerRpc(string attachmentInstanceId, string hostInstanceId, AttachmentSlotType slotType, NetworkConnection sender = null)
        {
            // Get attachment and host items
            var attachment = GetItemFromAnywhere(attachmentInstanceId);
            var hostItem = GetItemFromAnywhere(hostInstanceId);
            
            if (attachment == null)
            {
                SendOperationFailedObserverRpc("AttachToItem", "Attachment not found", sender);
                return;
            }
            
            if (hostItem == null)
            {
                SendOperationFailedObserverRpc("AttachToItem", "Host item not found", sender);
                return;
            }
            
            // Validate attachment
            if (!validator.ValidateAttachment(attachment, hostItem, slotType))
            {
                SendOperationFailedObserverRpc("AttachToItem", "Invalid attachment", sender);
                return;
            }
            
            // Remove attachment from inventory
            RemoveItemFromInventory(attachmentInstanceId);
            
            // Attach to host item
            hostItem.AddAttachment(attachment);
            
            // Update host item in its location
            UpdateItemInAnyLocation(hostItem);
            
            // Update stats (attachment modifiers now apply to host item)
            if (hostItem.IsEquipped)
            {
                ApplyItemStatModifiers(hostItem);
            }
            
            Log($"Attached {attachment.Definition.DisplayName} to {hostItem.Definition.DisplayName} at slot {slotType}");
            
            // Raise event
            RaiseAttachmentEventObserverRpc(true, attachment.Serialize(), hostItem.Serialize(), slotType);
        }
        
        public bool TryDetachFromItem(string hostInstanceId, AttachmentSlotType slotType, out ItemInstance detachedAttachment)
        {
            detachedAttachment = null;
            
            if (!IsOwner)
            {
                LogWarning("TryDetachFromItem: Not owner!");
                return false;
            }
            
            DetachFromItemServerRpc(hostInstanceId, slotType);
            return true;
        }
        
        [ServerRpc]
        private void DetachFromItemServerRpc(string hostInstanceId, AttachmentSlotType slotType, NetworkConnection sender = null)
        {
            var hostItem = GetItemFromAnywhere(hostInstanceId);
            
            if (hostItem == null)
            {
                SendOperationFailedObserverRpc("DetachFromItem", "Host item not found", sender);
                return;
            }
            
            // Check if attachment exists
            if (!hostItem.HasAttachment(slotType))
            {
                SendOperationFailedObserverRpc("DetachFromItem", "No attachment in this slot", sender);
                return;
            }
            
            // Detach attachment
            hostItem.RemoveAttachment(slotType, out ItemInstance detached);
            
            if (detached == null)
            {
                SendOperationFailedObserverRpc("DetachFromItem", "Failed to detach", sender);
                return;
            }
            
            // Return detached attachment to inventory
            detached.IsEquipped = false;
            detached.InventoryIndex = FindFirstAvailableIndex();
            AddItemToInventory(detached);
            
            // Update host item
            UpdateItemInAnyLocation(hostItem);
            
            // Update stats
            if (hostItem.IsEquipped)
            {
                RemoveItemStatModifiers(hostItem);
                ApplyItemStatModifiers(hostItem); // Re-apply without detached attachment
            }
            
            Log($"Detached {detached.Definition.DisplayName} from {hostItem.Definition.DisplayName}");
            
            // Raise event
            RaiseAttachmentEventObserverRpc(false, detached.Serialize(), hostItem.Serialize(), slotType);
        }
        
        public bool TrySwapAttachments(string hostInstanceId1, string hostInstanceId2, AttachmentSlotType slotType)
        {
            if (!IsOwner)
            {
                LogWarning("TrySwapAttachments: Not owner!");
                return false;
            }
            
            SwapAttachmentsServerRpc(hostInstanceId1, hostInstanceId2, slotType);
            return true;
        }
        
        [ServerRpc]
        private void SwapAttachmentsServerRpc(string hostInstanceId1, string hostInstanceId2, AttachmentSlotType slotType, NetworkConnection sender = null)
        {
            var host1 = GetItemFromAnywhere(hostInstanceId1);
            var host2 = GetItemFromAnywhere(hostInstanceId2);
            
            if (host1 == null || host2 == null)
            {
                SendOperationFailedObserverRpc("SwapAttachments", "One or both host items not found", sender);
                return;
            }
            
            // Get attachments from both hosts
            var attachment1 = host1.GetAttachment(slotType);
            var attachment2 = host2.GetAttachment(slotType);
            
            if (attachment1 == null && attachment2 == null)
            {
                SendOperationFailedObserverRpc("SwapAttachments", "Both slots are empty", sender);
                return;
            }
            
            // Validate both hosts can accept the opposite attachment
            if (attachment2 != null)
            {
                if (!host1.CanAcceptAttachment(attachment2))
                {
                    SendOperationFailedObserverRpc("SwapAttachments", "Host 1 cannot accept attachment from Host 2", sender);
                    return;
                }
            }
            
            if (attachment1 != null)
            {
                if (!host2.CanAcceptAttachment(attachment1))
                {
                    SendOperationFailedObserverRpc("SwapAttachments", "Host 2 cannot accept attachment from Host 1", sender);
                    return;
                }
            }
            
            // Perform swap
            if (attachment1 != null)
            {
                host1.RemoveAttachment(slotType, out _);
            }
            
            if (attachment2 != null)
            {
                host2.RemoveAttachment(slotType, out _);
            }
            
            if (attachment1 != null)
            {
                host2.AddAttachment(attachment1);
            }
            
            if (attachment2 != null)
            {
                host1.AddAttachment(attachment2);
            }
            
            // Update both host items
            UpdateItemInAnyLocation(host1);
            UpdateItemInAnyLocation(host2);
            
            // Update stats for equipped items
            if (host1.IsEquipped)
            {
                RemoveItemStatModifiers(host1);
                ApplyItemStatModifiers(host1);
            }
            
            if (host2.IsEquipped)
            {
                RemoveItemStatModifiers(host2);
                ApplyItemStatModifiers(host2);
            }
            
            Log($"Swapped attachments at slot {slotType} between {host1.Definition.DisplayName} and {host2.Definition.DisplayName}");
        }
        
        // ===== HELPER: UPDATE ITEM IN ANY LOCATION =====
        
        private void UpdateItemInAnyLocation(ItemInstance item)
        {
            if (item == null)
                return;
            
            // Try inventory
            for (int i = 0; i < inventoryList.Count; i++)
            {
                if (inventoryList[i].InstanceId == item.InstanceId)
                {
                    inventoryList[i] = item.Serialize();
                    return;
                }
            }
            
            // Try equipment
            foreach (var key in equipmentSlots.Keys)
            {
                if (equipmentSlots[key].InstanceId == item.InstanceId)
                {
                    equipmentSlots[key] = item.Serialize();
                    return;
                }
            }
            
            // Try weapons
            for (int i = 0; i < weaponSlots.Count; i++)
            {
                if (weaponSlots[i].InstanceId == item.InstanceId)
                {
                    weaponSlots[i] = item.Serialize();
                    return;
                }
            }
            
            // Try quickslots
            for (int i = 0; i < quickSlots.Count; i++)
            {
                if (quickSlots[i].InstanceId == item.InstanceId)
                {
                    quickSlots[i] = item.Serialize();
                    return;
                }
            }
            
            LogWarning($"UpdateItemInAnyLocation: Item {item.InstanceId} not found in any location");
        }
        
        [ObserversRpc]
        private void RaiseAttachmentEventObserverRpc(bool isAttach, ItemInstanceData attachmentData, ItemInstanceData hostData, AttachmentSlotType slotType)
        {
            if (!IsOwner)
                return;
            
            var attachment = DeserializeItem(attachmentData);
            var host = DeserializeItem(hostData);
            
            if (isAttach)
            {
                InventoryEvents.RaiseAttachmentAttached(new AttachmentAttachedEvent
                {
                    OwnerId = ObjectId,
                    IsLocalPlayer = true,
                    Attachment = attachment,
                    HostItem = host,
                    SlotType = slotType
                });
            }
            else
            {
                InventoryEvents.RaiseAttachmentDetached(new AttachmentDetachedEvent
                {
                    OwnerId = ObjectId,
                    IsLocalPlayer = true,
                    Attachment = attachment,
                    HostItem = host,
                    SlotType = slotType
                });
            }
        }
    }
}