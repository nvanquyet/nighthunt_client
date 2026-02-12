using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;

namespace NightHunt.Inventory.Core.Structs
{
    /// <summary>
    /// Event fired when attachment is detached.
    /// </summary>
    public struct AttachmentDetachedEvent
    {
        public int OwnerId;
        public bool IsLocalPlayer;
        public ItemInstance Attachment;
        public ItemInstance HostItem;
        public AttachmentSlotType SlotType;
    }
}