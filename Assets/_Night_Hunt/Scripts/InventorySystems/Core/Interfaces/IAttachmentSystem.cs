using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;

namespace NightHunt.Inventory.Interfaces
{
    /// <summary>
    /// Public API for AttachmentSystem.
    /// Handles attachment of items to parent items (e.g., scope on weapon, flashlight on helmet).
    /// </summary>
    public interface IAttachmentSystem
    {
        // === Query ===
        ItemInstance[] GetAttachments(ItemInstance parentItem);
        ItemInstance GetAttachment(ItemInstance parentItem, AttachmentSlotType slotType);
        bool HasAttachment(ItemInstance parentItem, AttachmentSlotType slotType);
        bool CanAttach(ItemInstance parentItem, ItemInstance attachment);
        
        // === Attach/Detach ===
        OperationResult AttachItem(ItemInstance parentItem, ItemInstance attachment);
        OperationResult DetachItem(ItemInstance parentItem, AttachmentSlotType slotType, out ItemInstance detachedItem);
        OperationResult SwapAttachment(ItemInstance parentItem, ItemInstance newAttachment, out ItemInstance oldAttachment);
        
        // === Utility ===
        void DetachAll(ItemInstance parentItem);
        AttachmentSlotType[] GetAvailableSlots(ItemInstance parentItem);
    }
}