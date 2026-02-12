using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;

namespace NightHunt.Inventory.Core.Structs
{
    /// <summary>
    /// Event fired when attachment is attached.
    /// </summary>
    public struct AttachmentAttachedEvent
    {
        public int OwnerId;
        public bool IsLocalPlayer;
        public ItemInstance Attachment;
        public ItemInstance HostItem;
        public AttachmentSlotType SlotType;
    }
    
    
}