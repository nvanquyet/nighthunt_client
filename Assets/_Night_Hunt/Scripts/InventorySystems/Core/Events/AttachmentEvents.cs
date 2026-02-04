using System;
using NightHunt.Inventory.Core.Data;

namespace NightHunt.Inventory.Core.Events
{
    /// <summary>
    /// Events for attachment system.
    /// </summary>
    public static class AttachmentEvents
    {
        public static event Action<ItemInstance, ItemInstance> OnAttachmentAdded; // attachment, parent
        public static event Action<ItemInstance, ItemInstance> OnAttachmentRemoved; // attachment, parent
        
        public static void InvokeAttachmentAdded(ItemInstance attachment, ItemInstance parent) => OnAttachmentAdded?.Invoke(attachment, parent);
        public static void InvokeAttachmentRemoved(ItemInstance attachment, ItemInstance parent) => OnAttachmentRemoved?.Invoke(attachment, parent);
    }
}