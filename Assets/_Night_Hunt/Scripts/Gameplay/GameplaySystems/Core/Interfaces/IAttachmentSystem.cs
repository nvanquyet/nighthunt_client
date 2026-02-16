using System;
using System.Collections.Generic;
using GameplaySystems.Core.Data;
using GameplaySystems.Inventory;

namespace GameplaySystems.Core.Interfaces
{
    /// <summary>
    /// Interface for attachment management system
    /// Manages attachments on items (scopes, grips, lights, pouches)
    /// Implemented by AttachmentSystem (NetworkBehaviour)
    /// </summary>
    public interface IAttachmentSystem
    {
        #region Getters
        
        /// <summary>
        /// Get attachment in specific slot on item
        /// Returns null if slot empty
        /// </summary>
        ItemInstance GetAttachment(string parentInstanceID, int slotIndex);
        
        /// <summary>
        /// Get all attachments on item
        /// Array length = AttachmentSlots.Length from item definition
        /// null = empty slot
        /// </summary>
        ItemInstance[] GetAllAttachments(string parentInstanceID);
        
        /// <summary>
        /// Check if attachment slot is occupied
        /// </summary>
        bool IsSlotOccupied(string parentInstanceID, int slotIndex);
        
        /// <summary>
        /// Check if attachment can be attached to item
        /// Validates slot types compatibility
        /// </summary>
        bool CanAttach(string attachmentInstanceID, string parentInstanceID, int slotIndex);
        
        /// <summary>
        /// Get attachment slot type at index
        /// </summary>
        AttachmentSlotType GetSlotType(string parentInstanceID, int slotIndex);
        
        #endregion
        
        #region Attach/Detach
        
        /// <summary>
        /// Attach item to parent item's slot
        /// Removes attachment from inventory
        /// Auto-swaps if slot occupied
        /// Server-side only
        /// </summary>
        void AttachItem(string attachmentInstanceID, string parentInstanceID, int slotIndex);
        
        /// <summary>
        /// Detach attachment back to inventory
        /// Server-side only
        /// </summary>
        void DetachItem(string parentInstanceID, int slotIndex);
        
        /// <summary>
        /// Swap attachments between two items
        /// Both items must have compatible slots
        /// </summary>
        void SwapAttachments(string parentID1, int slotIndex1, string parentID2, int slotIndex2);
        
        /// <summary>
        /// Detach all attachments from item
        /// Useful when unequipping item
        /// </summary>
        void DetachAllFromItem(string parentInstanceID);
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Event fired when attachment attached
        /// Parameters: (parentInstanceID, slotIndex, attachment)
        /// </summary>
        event Action<string, int, ItemInstance> OnAttachmentAttached;
        
        /// <summary>
        /// Event fired when attachment detached
        /// Parameters: (parentInstanceID, slotIndex, attachment)
        /// </summary>
        event Action<string, int, ItemInstance> OnAttachmentDetached;
        
        #endregion
    }
}