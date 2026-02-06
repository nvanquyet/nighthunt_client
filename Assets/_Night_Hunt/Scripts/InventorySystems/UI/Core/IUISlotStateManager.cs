using NightHunt.Inventory.Core.Enums;

namespace NightHunt.Inventory.UI.Core
{
    /// <summary>
    /// Interface for UI slot components that manage visual states.
    /// Ensures consistent state management across all slot UIs.
    /// </summary>
    public interface IUISlotStateManager
    {
        /// <summary>
        /// Sets the current state of the slot and updates visual feedback.
        /// </summary>
        /// <param name="state">The state to set</param>
        void SetState(UISlotState state);
        
        /// <summary>
        /// Gets the current state of the slot.
        /// </summary>
        /// <returns>The current UI slot state</returns>
        UISlotState GetCurrentState();
        
        /// <summary>
        /// Called when pointer enters the slot area.
        /// </summary>
        void OnPointerEnter();
        
        /// <summary>
        /// Called when pointer exits the slot area.
        /// </summary>
        void OnPointerExit();
        
        /// <summary>
        /// Called when slot is selected (first click or hotkey).
        /// </summary>
        void OnSelect();
        
        /// <summary>
        /// Called when slot is deselected.
        /// </summary>
        void OnUnselect();
    }
}
