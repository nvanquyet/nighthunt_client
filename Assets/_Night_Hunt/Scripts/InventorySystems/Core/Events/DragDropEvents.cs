using System;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.UI.Slots;

namespace NightHunt.Inventory.Core.Events
{
    /// <summary>
    /// Global events for drag & drop system.
    /// Used for UI state management and coordination.
    /// </summary>
    public static class DragDropEvents
    {
        // === Drag Operations ===
        /// <summary>Fired when drag starts. Args: (item, sourceSlot)</summary>
        public static event Action<ItemInstance, ItemSlotUI> OnDragStarted;
        
        /// <summary>Fired when drag ends. Args: (item, sourceSlot, targetSlot)</summary>
        public static event Action<ItemInstance, ItemSlotUI, ItemSlotUI> OnDragEnded;
        
        /// <summary>Fired when drag is cancelled. Args: (item, sourceSlot)</summary>
        public static event Action<ItemInstance, ItemSlotUI> OnDragCancelled;
        
        // === Drop Operations ===
        /// <summary>Fired when drop is validated. Args: (item, targetSlot)</summary>
        public static event Action<ItemInstance, ItemSlotUI> OnDropValidated;
        
        /// <summary>Fired when drop is rejected. Args: (item, targetSlot, reason)</summary>
        public static event Action<ItemInstance, ItemSlotUI, string> OnDropRejected;
        
        // === Hover Operations ===
        /// <summary>Fired when item hovers over slot. Args: (item, slot)</summary>
        public static event Action<ItemInstance, ItemSlotUI> OnDragHover;
        
        /// <summary>Fired when item stops hovering over slot. Args: (slot)</summary>
        public static event Action<ItemSlotUI> OnDragHoverExit;
        
        // === Invoke Methods ===
        public static void InvokeDragStarted(ItemInstance item, ItemSlotUI sourceSlot) 
            => OnDragStarted?.Invoke(item, sourceSlot);
        
        public static void InvokeDragEnded(ItemInstance item, ItemSlotUI sourceSlot, ItemSlotUI targetSlot) 
            => OnDragEnded?.Invoke(item, sourceSlot, targetSlot);
        
        public static void InvokeDragCancelled(ItemInstance item, ItemSlotUI sourceSlot) 
            => OnDragCancelled?.Invoke(item, sourceSlot);
        
        public static void InvokeDropValidated(ItemInstance item, ItemSlotUI targetSlot) 
            => OnDropValidated?.Invoke(item, targetSlot);
        
        public static void InvokeDropRejected(ItemInstance item, ItemSlotUI targetSlot, string reason) 
            => OnDropRejected?.Invoke(item, targetSlot, reason);
        
        public static void InvokeDragHover(ItemInstance item, ItemSlotUI slot) 
            => OnDragHover?.Invoke(item, slot);
        
        public static void InvokeDragHoverExit(ItemSlotUI slot) 
            => OnDragHoverExit?.Invoke(slot);
    }
}
