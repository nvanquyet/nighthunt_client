using NightHunt.Inventory.Core.Enums;

namespace NightHunt.Inventory.Core.Interfaces
{
    /// <summary>
    /// Interface for UI slot components that handle state management.
    /// Provides basic state setting and retrieval functionality.
    /// </summary>
    public interface IUISlotStateHandler
    {
        /// <summary>
        /// Sets the current state of the slot.
        /// </summary>
        /// <param name="state">The state to set</param>
        void SetState(UISlotState state);
        
        /// <summary>
        /// Gets the current state of the slot.
        /// </summary>
        /// <returns>The current UI slot state</returns>
        UISlotState GetCurrentState();
    }
}
