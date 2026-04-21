using System;
using System.Collections.Generic;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.Core.Interfaces
{
    /// <summary>
    /// Contract for the server-authoritative attachment management system.
    ///
    /// RESPONSIBILITIES:
    ///   - Manages attachment sockets on items (scopes, grips, lights, pouches, plates, etc.).
    ///   - Validates attachment compatibility between item and socket type.
    ///   - Drives stat recalculation after every attach / detach operation.
    ///
    /// NETWORK ARCHITECTURE:
    ///   - Server-authoritative — all mutations happen on the server.
    ///   - Attachment state is stored inside <see cref="ItemInstance.AttachedItems"/> and
    ///     replicated automatically via the parent item entry in InventorySystem's SyncList.
    ///   - The owning client can send requests via [ServerRpc(RequireOwnership = true)].
    ///     All public API methods route to the server automatically when called from the owner.
    /// </summary>
    public interface IAttachmentSystem
    {
        #region Getters

        /// <summary>
        /// Returns the attachment instance in <paramref name="slotIndex"/> on
        /// <paramref name="parentInstanceID"/>, or null when the slot is empty.
        /// </summary>
        ItemInstance GetAttachment(string parentInstanceID, int slotIndex);

        /// <summary>
        /// Returns an array of all attachments on the given item.
        /// Array length equals <c>AttachmentSlots.Length</c> from the item definition;
        /// entries are null when a slot is empty.
        /// </summary>
        ItemInstance[] GetAllAttachments(string parentInstanceID);

        /// <summary>Returns true when the given attachment slot contains an item.</summary>
        bool IsSlotOccupied(string parentInstanceID, int slotIndex);

        /// <summary>
        /// Returns true when the attachment in <paramref name="attachmentInstanceID"/>
        /// can be placed into <paramref name="slotIndex"/> on <paramref name="parentInstanceID"/>.
        /// Validates slot index bounds and slot-type compatibility.
        /// </summary>
        bool CanAttach(string attachmentInstanceID, string parentInstanceID, int slotIndex);

        /// <summary>Returns the <see cref="AttachmentSlotType"/> at the given slot index on the parent item.</summary>
        AttachmentSlotType GetSlotType(string parentInstanceID, int slotIndex);

        #endregion

        #region Attach / Detach

        /// <summary>
        /// Attach the item in <paramref name="attachmentInstanceID"/> to
        /// <paramref name="slotIndex"/> on <paramref name="parentInstanceID"/>.
        /// Removes the attachment from the inventory grid (keeps it in ItemDatabase for stat lookup).
        /// If the target slot is already occupied the existing attachment is detached first.
        /// Owning client routes to the server automatically.
        /// </summary>
        void AttachItem(string attachmentInstanceID, string parentInstanceID, int slotIndex);

        /// <summary>
        /// Detach the attachment at <paramref name="slotIndex"/> on
        /// <paramref name="parentInstanceID"/> and return it to the inventory grid.
        /// Owning client routes to the server automatically.
        /// </summary>
        void DetachItem(string parentInstanceID, int slotIndex);

        /// <summary>
        /// Swap the attachments between two item slots.
        /// Both slot types must be cross-compatible before the swap is performed.
        /// Owning client routes to the server automatically.
        /// </summary>
        void SwapAttachments(string parentID1, int slotIndex1, string parentID2, int slotIndex2);

        /// <summary>
        /// Detach all attachments from <paramref name="parentInstanceID"/> and return them
        /// to the inventory grid (follows InventoryConfig.ReturnAttachmentsToInventoryOnDrop).
        /// Owning client routes to the server automatically.
        /// </summary>
        void DetachAllFromItem(string parentInstanceID);

        #endregion

        #region Events

        /// <summary>
        /// Fired after an attachment is successfully placed onto a parent item.
        /// Parameters: (parentInstanceID, slotIndex, attachmentInstance).
        /// </summary>
        event Action<string, int, ItemInstance> OnAttachmentAttached;

        /// <summary>
        /// Fired after an attachment is removed from a parent item.
        /// Parameters: (parentInstanceID, slotIndex, attachmentInstance).
        /// </summary>
        event Action<string, int, ItemInstance> OnAttachmentDetached;

        #endregion
    }
}
