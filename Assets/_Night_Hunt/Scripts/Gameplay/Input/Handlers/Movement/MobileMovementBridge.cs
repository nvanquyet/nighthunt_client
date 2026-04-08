using UnityEngine;

namespace NightHunt.Gameplay.Input.Handlers.Movement
{
    /// <summary>
    /// Scene-level bridge that feeds a Joystick Pack joystick into the local
    /// player's <see cref="MovementInputHandler"/>.
    ///
    /// Hierarchy:
    ///   [MoveJoystick] (FixedJoystick + MobileMovementBridge)
    ///     └── Background (Image)
    ///           └── Handle   (Image)
    ///
    /// Inspector:
    ///   • <c>_joystick</c> — assign the FixedJoystick on the same GO (auto-wired by DemoSceneGenerator).
    ///
    /// HUD Wiring:
    ///   Call <see cref="BindHandler"/> from GameHUD / UIRootController once the local
    ///   player spawns and its MovementInputHandler is known.
    ///   Call <see cref="UnbindHandler"/> when the local player despawns.
    ///
    /// PC behaviour:
    ///   On non-touch platforms the joystick input is always zero, so the bridge
    ///   has no effect — WASD continues to drive movement via InputSystem.
    /// </summary>
    public class MobileMovementBridge : MonoBehaviour
    {
        [Header("Joystick (same GO — FixedJoystick / DynamicJoystick)")]
        [SerializeField] private Joystick _joystick;

        private MovementInputHandler _handler;

        // ─────────────────────────────────────────────────────────────────────
        //  Unity
        // ─────────────────────────────────────────────────────────────────────

        private void Update()
        {
            if (_handler == null || _joystick == null) return;
            _handler.SetMobileMove(_joystick.Direction);
        }

        private void OnDisable()
        {
            _handler?.SetMobileMove(Vector2.zero);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Bind to the local player's MovementInputHandler.
        /// Call from the HUD orchestrator (GameHUD.OnLocalPlayerSpawned) after the player spawns.
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
