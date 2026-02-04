using System.Linq;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;

namespace NightHunt.Inventory.Domain.Attachment
{
    /// <summary>
    /// Validates attachment operations.
    /// </summary>
    public static class AttachmentValidator
    {
        /// <summary>
        /// Checks if an attachment can be attached to a target item.
        /// Attachment compatibility is defined in the TARGET item (weapon/equipment).
        /// </summary>
        public static bool CanAttach(ItemInstance attachment, ItemInstance target)
        {
            if (attachment == null || target == null) return false;

            // Validate attachment type
            if (attachment.Definition.ItemType != ItemType.Attachment)
                return false;

            // Check if attachment type is in target's allowed slots
            if (!target.Definition.AttachmentSlots.Contains(attachment.Definition.AttachmentType))
                return false;

            // Check if slot is already occupied
            var existingAttachment = target.AttachedItems.Find(a =>
                a.Definition.AttachmentType == attachment.Definition.AttachmentType);

            return existingAttachment == null; // Slot must be empty
        }

        /// <summary>
        /// Validates attachment drop onto target slot.
        /// </summary>
        public static AttachmentValidationResult ValidateAttachment(
            ItemInstance attachment,
            ItemInstance target,
            AttachmentSlotType targetSlotType)
        {
            if (attachment == null)
                return AttachmentValidationResult.Fail("Attachment is null");

            if (target == null)
                return AttachmentValidationResult.Fail("Target is null");

            // Check attachment type matches slot
            if (attachment.Definition.AttachmentType != targetSlotType)
            {
                return AttachmentValidationResult.Fail(
                    $"Wrong attachment type for this slot (need {targetSlotType})");
            }

            // Check if can attach
            if (!CanAttach(attachment, target))
            {
                return AttachmentValidationResult.Fail("Slot already occupied or incompatible");
            }

            return AttachmentValidationResult.Success();
        }
    }
}