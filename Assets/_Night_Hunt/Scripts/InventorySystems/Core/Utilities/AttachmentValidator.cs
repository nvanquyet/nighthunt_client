using System;
using System.Linq;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;

namespace NightHunt.Inventory.Core.Utilities
{
    public static class AttachmentValidator
    {
        /// <summary>
        /// Checks if attachment can be attached to target.
        /// </summary>
        public static bool CanAttach(ItemInstance attachment, ItemInstance target)
        {
            if (attachment == null || target == null)
                return false;
            
            // Check attachment is Attachment type
            if (attachment.Definition.ItemType != ItemType.Attachment)
                return false;
            
            // Check target accepts this attachment type
            if (!target.Definition.AttachmentSlots.Contains(attachment.Definition.AttachmentType))
                return false;
            
            // Check slot not already occupied (if want to prevent swap, check here)
            // For now, we allow swap, so this check is optional
            
            return true;
        }
        
        /// <summary>
        /// Gets available attachment slot types on target.
        /// </summary>
        public static AttachmentSlotType[] GetAvailableSlots(ItemInstance target)
        {
            if (target == null)
                return Array.Empty<AttachmentSlotType>();
            
            return target.Definition.AttachmentSlots;
        }
    }
    
    /// <summary>
    /// Result of attachment validation.
    /// </summary>
    public struct AttachmentValidationResult
    {
        public bool IsValid;
        public string FailReason;
        
        public static AttachmentValidationResult Success()
        {
            return new AttachmentValidationResult { IsValid = true };
        }
        
        public static AttachmentValidationResult Fail(string reason)
        {
            return new AttachmentValidationResult
            {
                IsValid = false,
                FailReason = reason
            };
        }
    }
}