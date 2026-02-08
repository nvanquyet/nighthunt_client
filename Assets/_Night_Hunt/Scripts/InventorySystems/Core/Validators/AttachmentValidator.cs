using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using System.Collections.Generic;
using System.Linq;

namespace NightHunt.Inventory.Core.Validators
{
    /// <summary>
    /// Validates attachment operations.
    /// Handles: Attachment compatibility, slot availability, restrictions.
    /// </summary>
    public static class AttachmentValidator
    {
        // === Basic Validation ===
        
        /// <summary>
        /// Check if attachment can be attached to parent.
        /// </summary>
        public static bool CanAttach(ItemInstance parent, ItemInstance attachment)
        {
            if (parent == null || attachment == null)
                return false;
            
            if (parent.Definition == null || attachment.Definition == null)
                return false;
            
            return parent.CanAcceptAttachment(attachment);
        }
        
        /// <summary>
        /// Validate attachment with detailed reason.
        /// </summary>
        public static AttachmentValidationResult ValidateAttachment(ItemInstance parent, ItemInstance attachment)
        {
            var result = new AttachmentValidationResult();
            
            // Null checks
            if (parent == null)
            {
                result.IsValid = false;
                result.Reason = "Parent item is null";
                return result;
            }
            
            if (attachment == null)
            {
                result.IsValid = false;
                result.Reason = "Attachment is null";
                return result;
            }
            
            if (parent.Definition == null)
            {
                result.IsValid = false;
                result.Reason = "Parent item has no definition";
                return result;
            }
            
            if (attachment.Definition == null)
            {
                result.IsValid = false;
                result.Reason = "Attachment has no definition";
                return result;
            }
            
            // Check if parent has this attachment slot type
            AttachmentSlotType attachmentType = attachment.Definition.AttachmentType;
            if (!parent.Definition.HasAttachmentSlot(attachmentType))
            {
                result.IsValid = false;
                result.Reason = $"Parent does not have {attachmentType} slot";
                result.MissingSlotType = attachmentType;
                return result;
            }
            
            // Check if slot already occupied
            if (parent.HasAttachment(attachmentType))
            {
                result.IsValid = false;
                result.Reason = $"{attachmentType} slot already occupied";
                result.OccupiedSlot = attachmentType;
                result.ExistingAttachment = parent.GetAttachment(attachmentType);
                return result;
            }
            
            // Check compatibility rules (if any)
            var compatibilityCheck = CheckCompatibilityRules(parent, attachment);
            if (!compatibilityCheck.isCompatible)
            {
                result.IsValid = false;
                result.Reason = compatibilityCheck.reason;
                return result;
            }
            
            result.IsValid = true;
            result.Reason = "Can attach";
            result.TargetSlot = attachmentType;
            return result;
        }
        
        // === Slot Validation ===
        
        /// <summary>
        /// Check if parent has specific attachment slot available.
        /// </summary>
        public static bool HasSlotAvailable(ItemInstance parent, AttachmentSlotType slotType)
        {
            if (parent == null || parent.Definition == null)
                return false;
            
            // Check if parent has this slot type
            if (!parent.Definition.HasAttachmentSlot(slotType))
                return false;
            
            // Check if slot is not occupied
            return !parent.HasAttachment(slotType);
        }
        
        /// <summary>
        /// Get all available attachment slots on parent.
        /// </summary>
        public static AttachmentSlotType[] GetAvailableSlots(ItemInstance parent)
        {
            if (parent == null || parent.Definition == null)
                return new AttachmentSlotType[0];
            
            // Get all slots
            var allSlots = parent.Definition.AttachmentSlots;
            
            // Filter out occupied slots
            return allSlots.Where(slot => !parent.HasAttachment(slot)).ToArray();
        }
        
        /// <summary>
        /// Get all occupied attachment slots on parent.
        /// </summary>
        public static AttachmentSlotType[] GetOccupiedSlots(ItemInstance parent)
        {
            if (parent == null || parent.Definition == null)
                return new AttachmentSlotType[0];
            
            return parent.AttachedItems
                .Where(a => a != null && a.Definition != null)
                .Select(a => a.Definition.AttachmentType)
                .ToArray();
        }
        
        /// <summary>
        /// Count available attachment slots.
        /// </summary>
        public static int CountAvailableSlots(ItemInstance parent)
        {
            return GetAvailableSlots(parent).Length;
        }
        
        /// <summary>
        /// Count total attachment slots.
        /// </summary>
        public static int CountTotalSlots(ItemInstance parent)
        {
            if (parent == null || parent.Definition == null)
                return 0;
            
            return parent.Definition.AttachmentSlots.Length;
        }
        
        // === Compatibility Rules ===
        
        /// <summary>
        /// Check custom compatibility rules.
        /// Can be extended for game-specific rules.
        /// </summary>
        private static (bool isCompatible, string reason) CheckCompatibilityRules(ItemInstance parent, ItemInstance attachment)
        {
            // Example rule: Scopes can only attach to weapons
            if (attachment.Definition.AttachmentType == AttachmentSlotType.Scope)
            {
                if (parent.Definition.ItemType != ItemType.Weapon)
                {
                    return (false, "Scopes can only be attached to weapons");
                }
            }
            
            // Example rule: Flashlights can only attach to helmets or weapons
            if (attachment.Definition.AttachmentType == AttachmentSlotType.Flashlight)
            {
                bool validParent = parent.Definition.ItemType == ItemType.Weapon ||
                                   parent.Definition.EquipmentSlot == EquipmentSlotType.Helmet;
                
                if (!validParent)
                {
                    return (false, "Flashlights can only attach to helmets or weapons");
                }
            }
            
            // Add more custom rules here as needed
            
            return (true, "Compatible");
        }
        
        // === Swap Validation ===
        
        /// <summary>
        /// Validate swapping one attachment for another.
        /// </summary>
        public static AttachmentSwapValidationResult ValidateSwap(
            ItemInstance parent,
            ItemInstance newAttachment,
            out ItemInstance oldAttachment)
        {
            oldAttachment = null;
            var result = new AttachmentSwapValidationResult();
            
            // Validate new attachment
            var attachValidation = ValidateAttachment(parent, newAttachment);
            
            // If slot is occupied, that's okay for swap - we'll replace it
            if (!attachValidation.IsValid)
            {
                // Check if the only issue is occupied slot
                if (attachValidation.OccupiedSlot != default(AttachmentSlotType))
                {
                    // Slot is occupied - valid for swap
                    oldAttachment = attachValidation.ExistingAttachment;
                    result.IsValid = true;
                    result.Reason = "Can swap";
                    result.OldAttachment = oldAttachment;
                    result.NewAttachment = newAttachment;
                    result.SlotType = attachValidation.OccupiedSlot;
                    return result;
                }
                else
                {
                    // Other validation error
                    result.IsValid = false;
                    result.Reason = attachValidation.Reason;
                    return result;
                }
            }
            
            // Slot is empty - not a swap, just an attach
            result.IsValid = true;
            result.Reason = "Slot empty - will attach (not swap)";
            result.NewAttachment = newAttachment;
            result.SlotType = attachValidation.TargetSlot;
            return result;
        }
        
        // === Detach Validation ===
        
        /// <summary>
        /// Validate detaching attachment.
        /// </summary>
        public static (bool canDetach, string reason) ValidateDetach(ItemInstance parent, AttachmentSlotType slotType)
        {
            if (parent == null || parent.Definition == null)
                return (false, "Parent item is null or invalid");
            
            if (!parent.Definition.HasAttachmentSlot(slotType))
                return (false, $"Parent does not have {slotType} slot");
            
            if (!parent.HasAttachment(slotType))
                return (false, $"No attachment in {slotType} slot");
            
            return (true, "Can detach");
        }
        
        // === Bulk Operations ===
        
        /// <summary>
        /// Validate detaching all attachments.
        /// </summary>
        public static (bool canDetachAll, List<ItemInstance> attachments, string reason) ValidateDetachAll(ItemInstance parent)
        {
            if (parent == null || parent.Definition == null)
                return (false, null, "Parent item is null or invalid");
            
            if (parent.AttachedItems.Count == 0)
                return (false, null, "No attachments to detach");
            
            var attachments = new List<ItemInstance>(parent.AttachedItems);
            return (true, attachments, $"Can detach {attachments.Count} attachments");
        }
        
        // === Helper Methods ===
        
        /// <summary>
        /// Get attachment by slot type.
        /// </summary>
        public static ItemInstance GetAttachment(ItemInstance parent, AttachmentSlotType slotType)
        {
            return parent?.GetAttachment(slotType);
        }
        
        /// <summary>
        /// Check if parent has any attachments.
        /// </summary>
        public static bool HasAnyAttachments(ItemInstance parent)
        {
            if (parent == null)
                return false;
            
            return parent.AttachedItems != null && parent.AttachedItems.Count > 0;
        }
        
        /// <summary>
        /// Get count of attached items.
        /// </summary>
        public static int GetAttachmentCount(ItemInstance parent)
        {
            if (parent == null || parent.AttachedItems == null)
                return 0;
            
            return parent.AttachedItems.Count;
        }
    }
    
    // === Result Structures ===
    
    /// <summary>
    /// Result structure for attachment validation.
    /// </summary>
    public struct AttachmentValidationResult
    {
        public bool IsValid;
        public string Reason;
        public AttachmentSlotType TargetSlot;
        public AttachmentSlotType MissingSlotType;
        public AttachmentSlotType OccupiedSlot;
        public ItemInstance ExistingAttachment;
    }
    
    /// <summary>
    /// Result structure for attachment swap validation.
    /// </summary>
    public struct AttachmentSwapValidationResult
    {
        public bool IsValid;
        public string Reason;
        public ItemInstance OldAttachment;
        public ItemInstance NewAttachment;
        public AttachmentSlotType SlotType;
    }
}