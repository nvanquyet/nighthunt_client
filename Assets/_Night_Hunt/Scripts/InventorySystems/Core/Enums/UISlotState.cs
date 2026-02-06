namespace NightHunt.Inventory.Core.Enums
{
    /// <summary>
    /// Defines UI states for inventory slot components.
    /// Used for visual feedback and state management across all slot UIs.
    /// </summary>
    public enum UISlotState
    {
        /// <summary>No item in slot, default empty state</summary>
        Empty,
        
        /// <summary>Slot contains an item, normal occupied state</summary>
        Occupied,
        
        /// <summary>Mouse pointer is hovering over the slot</summary>
        Hover,
        
        /// <summary>Slot is currently selected (first click or hotkey press)</summary>
        Selected,
        
        /// <summary>Slot was selected but is now deselected</summary>
        Unselected
    }
}
