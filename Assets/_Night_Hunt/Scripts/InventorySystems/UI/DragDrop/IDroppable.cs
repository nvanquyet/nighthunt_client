using NightHunt.Inventory.Core.Data;

namespace NightHunt.Inventory.UI.DragDrop
{
    /// <summary>
    /// Interface for droppable slots.
    /// Defines contract for drag & drop system.
    /// </summary>
    public interface IDroppable
    {
        /// <summary>
        /// Check if item can be dropped here.
        /// </summary>
        bool CanAcceptDrop(ItemInstance item);
        
        /// <summary>
        /// Called when item is dropped here.
        /// </summary>
        void OnDrop(ItemInstance item);
        
        /// <summary>
        /// Called when item is hovering over this slot.
        /// </summary>
        void OnDragHover(ItemInstance item);
        
        /// <summary>
        /// Called when item stops hovering over this slot.
        /// </summary>
        void OnDragExit();
    }
}
