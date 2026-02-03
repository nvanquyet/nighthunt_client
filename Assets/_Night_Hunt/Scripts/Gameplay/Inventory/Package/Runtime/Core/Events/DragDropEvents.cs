using System;
using UnityEngine;
using NightHunt.Inventory.Core;

namespace NightHunt.Inventory.Events
{
    /// <summary>
    /// Events for drag & drop operations.
    /// </summary>
    public static class DragDropEvents
    {
        public static event Action<DragContext> OnBeginDrag;
        public static event Action<Vector2> OnDragging;
        public static event Action<DragContext> OnDrop;
        public static event Action OnEndDrag;
        public static event Action OnDragCancelled;
        
        public static void FireBeginDrag(DragContext context) => OnBeginDrag?.Invoke(context);
        public static void FireDragging(Vector2 position) => OnDragging?.Invoke(position);
        public static void FireDrop(DragContext context) => OnDrop?.Invoke(context);
        public static void FireEndDrag() => OnEndDrag?.Invoke();
        public static void FireDragCancelled() => OnDragCancelled?.Invoke();
    }
    
    /// <summary>
    /// Context for drag & drop operations.
    /// </summary>
    [Serializable]
    public struct DragContext
    {
        public SlotLocationType SourceLocation;
        public int SourceIndex;
        public SlotLocationType TargetLocation;
        public int TargetIndex;
        
        [System.NonSerialized]
        public ItemInstance ItemInstance;  // NOT serialized - contains ItemDefinition with Sprite
    }
}
