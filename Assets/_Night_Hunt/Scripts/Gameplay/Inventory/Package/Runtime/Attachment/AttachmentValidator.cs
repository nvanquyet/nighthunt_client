using System.Linq;
using NightHunt.Inventory.Core;

namespace NightHunt.Inventory.Attachment
{
    /// <summary>
    /// Validates attachment compatibility and slot availability.
    /// </summary>
    public static class AttachmentValidator
    {
        /// <summary>
        /// Check if attachment can be attached to target item.
        /// Attachment compatibility is defined in the TARGET item (weapon/equipment).
        /// </summary>
        public static bool CanAttach(ItemInstance attachment, ItemInstance target)
        {
            // Check if attachment type is valid for target
            if (!target.Definition.AttachmentSlots.Contains(attachment.Definition.AttachmentType))
                return false;
            
            // Check if slot is already occupied
            var existingAttachment = target.AttachedItems.Find(a => 
                a.Definition.AttachmentType == attachment.Definition.AttachmentType);
            
            return existingAttachment == null; // Slot must be empty
        }
        
        /// <summary>
        /// Validate attachment drop onto target slot.
        /// </summary>
        public static AttachmentValidationResult ValidateAttachment(
            ItemInstance attachment, 
            ItemInstance target, 
            AttachmentSlotType targetSlotType)
        {
            if (attachment.Definition.AttachmentType != targetSlotType)
            {
                return AttachmentValidationResult.Fail("Wrong attachment type for this slot");
            }
            
            if (!CanAttach(attachment, target))
            {
                return AttachmentValidationResult.Fail("Slot already occupied");
            }
            
            return AttachmentValidationResult.Success();
        }
    }
    
    /// <summary>
    /// Result of attachment validation.
    /// </summary>
    public struct AttachmentValidationResult
    {
        public bool IsValid;
        public string FailReason;
        
        public static AttachmentValidationResult Success() => new AttachmentValidationResult { IsValid = true };
        public static AttachmentValidationResult Fail(string reason) => new AttachmentValidationResult { IsValid = false, FailReason = reason };
    }
}
