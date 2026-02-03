using NightHunt.InteractionSystem.Items.Data;

namespace NightHunt.InteractionSystem.Core.Interfaces
{
    /// <summary>
    /// Interface for attachment slots on equipment.
    /// </summary>
    public interface IAttachmentSlot
    {
        /// <summary>
        /// Get the slot type (e.g., Scope, Barrel, Flashlight, Plate).
        /// </summary>
        AttachmentSlotType GetSlotType();

        /// <summary>
        /// Check if an attachment can be attached to this slot.
        /// </summary>
        bool CanAttach(AttachmentData attachment);

        /// <summary>
        /// Attach an attachment to this slot.
        /// </summary>
        bool Attach(AttachmentData attachment);

        /// <summary>
        /// Remove the attachment from this slot.
        /// </summary>
        bool Detach();

        /// <summary>
        /// Get the currently attached attachment.
        /// </summary>
        AttachmentData GetAttachedAttachment();

        /// <summary>
        /// Check if the slot is occupied.
        /// </summary>
        bool IsOccupied();
    }

    /// <summary>
    /// Types of attachment slots available.
    /// </summary>
    public enum AttachmentSlotType
    {
        // Weapon attachments
        Scope,
        Barrel,
        Magazine,
        Grip,
        Stock,
        
        // Helmet attachments
        NightVision,
        Flashlight,
        Camera,
        
        // Armor attachments
        Plate,
        Pouch1,
        Pouch2,
        Pouch3,
        Hydration,
        
        // Backpack attachments
        BackpackPouch1,
        BackpackPouch2,
        BackpackHydration,
        
        // Universal
        Tactical,
        Utility,
        None
    }
}
