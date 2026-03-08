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
            // Track intended movement as velocity so GetCurrentVelocity() returns a value
            // that carries the correct _verticalVelocity into the reconcile snapshot.
            // Without this, data.Velocity.y is always 0 and every reconcile wipes fall speed.
            _velocity = movement;

            if (enableDebugLogs && movement.sqrMagnitude > 0.01f)
            {
                Debug.Log($"[CharacterControllerPredictedMovement] Move: {movement * dt}, Flags: {flags}");
            }
        }

        protected override bool IsRollBlocked(Vector3 direction, float stepDistance)
        {
            if (_controller == null) return false;
            // CapsuleCast in the roll direction to detect walls before committing the step.
            float height = _controller.height;
            float radius = _controller.radius;
            Vector3 center = transform.position + _controller.center;
            Vector3 p1 = center + Vector3.up * (height * 0.5f - radius);
            Vector3 p2 = center - Vector3.up * (height * 0.5f - radius);
            return Physics.CapsuleCast(p2, p1, radius * 0.9f, direction, stepDistance,
                ~0, QueryTriggerInteraction.Ignore);
        }

        protected override Vector3 GetCurrentVelocity()
        {
            // CharacterController doesn't store velocity, we calculate it from movement
            // Return the velocity we're tracking
            return _velocity;
        }

        protected override void ResetPhysicsState()
        {
            // Sync the CharacterController's internal capsule to the current transform.
            // FishNet reconcile (and spawn) sets transform.position directly; without this
            // toggle the CC's cached position is stale and fights the change on the next Move().
            if (_controller != null && _controller.enabled)
            {
                _controller.enabled = false;
                _controller.enabled = true;
            }

            // NOTE: Do NOT reset _verticalVelocity here.
            // Reconcile has already written _verticalVelocity = data.Velocity.y before
            // calling ResetPhysicsState. Clamping it here wipes the server-authoritative
            // fall/launch speed every reconcile tick, causing stutter mid-jump/fall.
        }

        /// <summary>
        /// Override base Teleport to disable CharacterController BEFORE setting the
        /// position so it cannot resist the transform write.
        /// </summary>
        public override void Teleport(Vector3 position, Quaternion rotation)
        {
            if (_controller != null) _controller.enabled = false;

            transform.position = position;
            transform.rotation = rotation;
            _velocity         = Vector3.zero;
            _verticalVelocity = -2f;

            if (_controller != null) _controller.enabled = true;

            if (enableDebugLogs)
                Debug.Log($"[CharacterControllerPredictedMovement] Teleport → pos={position}, rot={rotation.eulerAngles}");
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