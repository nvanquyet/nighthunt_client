using System;
using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;

namespace NightHunt.Inventory.Core.Events
{
    /// <summary>
    /// Context data for drag and drop operations.
    /// </summary>
    [Serializable]
    public struct DragContext
    {
        public SlotLocationType SourceLocation;
        public int SourceIndex;
        public SlotLocationType TargetLocation;
        public int TargetIndex;
        public ItemInstance ItemInstance;
    }
    
    /// <summary>
    /// Events for drag and drop system.
    /// </summary>
    public static class DragDropEvents
    {
        // === Drag Events ===
        
        /// <summary>Fired when drag operation begins</summary>
        public static event Action<DragContext> OnBeginDrag;
        
        /// <summary>Fired during drag (for ghost visual update)</summary>
        public static event Action<Vector2> OnDragging; // screen position
        
        /// <summary>Fired when item is dropped</summary>
        public static event Action<DragContext> OnDrop;
        
        /// <summary>Fired when drag operation ends</summary>
        public static event Action OnEndDrag;
        
        /// <summary>Fired when drag is cancelled (ESC or right-click)</summary>
        public static event Action OnDragCancelled;
        
        // === Invocation Methods ===
        
        public static void InvokeBeginDrag(DragContext context) => OnBeginDrag?.Invoke(context);
        public static void InvokeDragging(Vector2 position) => OnDragging?.Invoke(position);
        public static void InvokeDrop(DragContext context) => OnDrop?.Invoke(context);
        public static void InvokeEndDrag() => OnEndDrag?.Invoke();
        public static void InvokeDragCancelled() => OnDragCancelled?.Invoke();
    }
}