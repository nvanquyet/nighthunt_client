using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NightHunt.Gameplay.Input
{
    /// <summary>
    /// Controller for individual action map
    /// </summary>
    public class InputActionMapController
    {
        private readonly InputActionMap actionMap;
        private bool isEnabled = false;

        public string Name => actionMap?.name ?? "Unknown";
        public bool IsEnabled => isEnabled;

        public InputActionMapController(InputActionMap map)
        {
            actionMap = map ?? throw new ArgumentNullException(nameof(map));
        }

        /// <summary>
        /// Enable action map
        /// </summary>
        public void Enable()
        {
            if (isEnabled) return;

            actionMap?.Enable();
            isEnabled = true;
        }

        /// <summary>
        /// Disable action map
        /// </summary>
        public void Disable()
        {
            if (!isEnabled) return;

            actionMap?.Disable();
            isEnabled = false;
        }

        /// <summary>
        /// Get action by name
        /// </summary>
        public InputAction GetAction(string actionName)
        {
            return actionMap?.FindAction(actionName);
        }
    }
}

