using System;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;

namespace NightHunt.Inventory.Core.Events
{
    /// <summary>
    /// Global events for AttachmentSystem.
    /// Example: Flashlight attached to helmet → VisionRadius increased
    ///          Grip attached to weapon → Recoil reduced
    /// </summary>
    public static class AttachmentEvents
    {
        // === Attach/Detach ===
        /// <summary>Fired when attachment is attached. Args: (parentItem, attachment, slotType)</summary>
        public static event Action<ItemInstance, ItemInstance, AttachmentSlotType> OnAttachmentAttached;
        
        /// <summary>Fired when attachment is detached. Args: (parentItem, attachment, slotType)</summary>
        public static event Action<ItemInstance, ItemInstance, AttachmentSlotType> OnAttachmentDetached;
        
        /// <summary>Fired when attachment is swapped. Args: (parentItem, oldAttachment, newAttachment, slotType)</summary>
        public static event Action<ItemInstance, ItemInstance, ItemInstance, AttachmentSlotType> OnAttachmentSwapped;
        
        // === State Changes ===
        /// <summary>Fired when all attachments are removed from parent. Args: (parentItem)</summary>
        public static event Action<ItemInstance> OnAllAttachmentsDetached;
        
        // === Validation ===
        /// <summary>Fired when attachment operation fails. Args: (result, parentItem, attachment, errorMessage)</summary>
        public static event Action<OperationResult, ItemInstance, ItemInstance, string> OnAttachmentOperationFailed;
        
        // === Invoke Methods ===
        public static void InvokeAttachmentAttached(ItemInstance parent, ItemInstance attachment, AttachmentSlotType slotType) 
            => OnAttachmentAttached?.Invoke(parent, attachment, slotType);
        
        public static void InvokeAttachmentDetached(ItemInstance parent, ItemInstance attachment, AttachmentSlotType slotType) 
            => OnAttachmentDetached?.Invoke(parent, attachment, slotType);
        
        public static void InvokeAttachmentSwapped(ItemInstance parent, ItemInstance oldAttachment, ItemInstance newAttachment, AttachmentSlotType slotType) 
            => OnAttachmentSwapped?.Invoke(parent, oldAttachment, newAttachment, slotType);
        
        public static void InvokeAllAttachmentsDetached(ItemInstance parent) 
            => OnAllAttachmentsDetached?.Invoke(parent);
        
        public static void InvokeAttachmentOperationFailed(OperationResult result, ItemInstance parent, ItemInstance attachment, string message) 
            => OnAttachmentOperationFailed?.Invoke(result, parent, attachment, message);
    }
}