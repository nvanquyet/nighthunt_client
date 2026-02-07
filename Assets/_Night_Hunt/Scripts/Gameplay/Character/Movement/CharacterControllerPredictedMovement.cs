using UnityEngine;

namespace NightHunt.Gameplay.Character
{
    /// <summary>
    /// CharacterController-based predicted movement implementation.
    /// 
    /// ADVANTAGES:
    /// - Simple and reliable
    /// - Built-in collision detection
    /// - No physics simulation overhead
    /// - Good for most third-person games
    /// 
    /// DISADVANTAGES:
    /// - Limited physics interaction
    /// - Cannot be affected by external forces easily
    /// - No realistic physics behavior
    /// 
    /// USE WHEN:
    /// - Building standard third-person movement
    /// - Don't need complex physics interactions
    /// - Want predictable, responsive controls
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class CharacterControllerPredictedMovement : BaseCharacterPredictedMovement
    {
        private CharacterController _controller;

        #region INITIALIZATION

        protected override void InitializePhysicsComponents()
        {
            _controller = GetComponent<CharacterController>();

            if (_controller == null)
            {
                Debug.LogError("[CharacterControllerPredictedMovement] CharacterController component NOT FOUND!");
            }
            else
            {
                if (enableDebugLogs)
                    Debug.Log($"[CharacterControllerPredictedMovement] CharacterController OK - height={_controller.height}, radius={_controller.radius}");
            }
        }

        protected override string GetPhysicsComponentName()
        {
            return _controller != null ? $"CharacterController (enabled={_controller.enabled})" : "NULL!";
        }

        #endregion

        #region PHYSICS IMPLEMENTATION

        protected override bool IsGrounded()
        {
            return _controller != null && _controller.isGrounded;
        }

        protected override void ApplyMovement(Vector3 movement, float dt)
        {
            if (_controller == null || !_controller.enabled)
                return;

            // CharacterController uses Move() with deltaTime
            CollisionFlags flags = _controller.Move(movement * dt);

            if (enableDebugLogs && movement.sqrMagnitude > 0.01f)
            {
                Debug.Log($"[CharacterControllerPredictedMovement] Move: {movement * dt}, Flags: {flags}");
            }
        }

        protected override Vector3 GetCurrentVelocity()
        {
            // CharacterController doesn't store velocity, we calculate it from movement
            // Return the velocity we're tracking
            return _velocity;
        }

        protected override void ResetPhysicsState()
        {
            // Reset vertical velocity when grounded during reconciliation
            if (IsGrounded() && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }
        }

        #endregion

        #region ADDITIONAL FEATURES

        /// <summary>
        /// Get CharacterController-specific info
        /// </summary>
        public CharacterController GetController() => _controller;

        /// <summary>
        /// Check if controller is valid and enabled
        /// </summary>
        public bool IsControllerValid() => _controller != null && _controller.enabled;

        #endregion

        #region DEBUG

        protected override void OnGUI()
        {
            base.OnGUI();

            if (!IsOwner || !Application.isPlaying || !enableDebugLogs) return;

            // Additional CharacterController-specific debug info
            GUI.color = Color.cyan;
            GUILayout.BeginArea(new Rect(520, 10, 300, 100));

            GUILayout.Label("=== CHARACTERCONTROLLER INFO ===");
            if (_controller != null)
            {
                GUILayout.Label($"Height: {_controller.height:F2}");
                GUILayout.Label($"Radius: {_controller.radius:F2}");
                GUILayout.Label($"Step Offset: {_controller.stepOffset:F2}");
                GUILayout.Label($"Slope Limit: {_controller.slopeLimit:F1}°");
            }
            else
            {
                GUILayout.Label("Controller: NULL");
            }

            GUILayout.EndArea();
        }

        #endregion

    }
}