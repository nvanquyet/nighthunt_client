using UnityEngine;

namespace NightHunt.Gameplay.Input.Handlers.Movement
{
    /// <summary>
    /// Lifecycle bridge that binds the local player's <see cref="MovementInputHandler"/>
    /// to the mobile HUD.
    ///
    /// The joystick reference and per-frame input feeding have been moved to
    /// <see cref="NightHunt.UI.Mobile.MobileHUDPanel"/>, which owns the joystick
    /// and calls <see cref="MovementInputHandler.SetMobileMove"/> in its Update loop.
    ///
    /// HUD Wiring:
    ///   Call <see cref="BindHandler"/> from <see cref="NightHunt.UI.Mobile.MobileHUDPanel.Bind"/>
    ///   once the local player spawns and its MovementInputHandler is known.
    ///   Call <see cref="UnbindHandler"/> when the local player despawns.
    /// </summary>
    public class MobileMovementBridge : MonoBehaviour
    {
        private MovementInputHandler _handler;

        // ─────────────────────────────────────────────────────────────────────
        //  Unity
        // ─────────────────────────────────────────────────────────────────────

        private void OnDisable()
        {
            _handler?.SetMobileMove(Vector2.zero);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Bind to the local player's MovementInputHandler.
        /// Called by <see cref="NightHunt.UI.Mobile.MobileHUDPanel.Bind"/> after the player spawns.
        /// </summary>
        public void BindHandler(MovementInputHandler handler)
        {
            _handler = handler;
        }

        /// <summary>
        /// Clear the binding. Call when the local player despawns.
        /// </summary>
        public void UnbindHandler()
        {
            _handler?.SetMobileMove(Vector2.zero);
            _handler = null;
        }
    }
}
