using UnityEngine.InputSystem;

namespace NightHunt.Gameplay.Input.Core
{
    /// <summary>
    /// Base interface for all input handlers
    /// Ensures consistent lifecycle management
    /// </summary>
    public interface IInputHandler
    {
        /// <summary>
        /// Enable input handling (only call when IsOwner)
        /// </summary>
        void EnableInput();

        /// <summary>
        /// Disable input handling
        /// </summary>
        void DisableInput();

        /// <summary>
        /// Check if input is currently enabled
        /// </summary>
        bool IsInputEnabled { get; }

        /// <summary>
        /// Get the action map this handler manages
        /// </summary>
        InputActionMap GetActionMap();
    }
}