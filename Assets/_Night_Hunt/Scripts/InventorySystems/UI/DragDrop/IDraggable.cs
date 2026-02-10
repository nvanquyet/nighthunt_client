using NightHunt.Inventory.Core.Data;

namespace NightHunt.Inventory.UI.DragDrop
{
    /// <summary>
    /// Interface for draggable items.
    /// Defines contract for drag & drop system.
    /// </summary>
    public interface IDraggable
    {
        /// <summary>
        /// Get item being dragged.
        /// </summary>
        ItemInstance GetDraggedItem();
        
        /// <summary>
        /// Called when drag starts.
        /// </summary>
        void OnDragStart();
        
        /// <summary>
        /// Called when drag ends.
        /// </summary>
        void OnDragEnd();
        
        /// <summary>
        /// Check if item can be dragged.
        /// </summary>
        bool CanDrag();
    }
}
