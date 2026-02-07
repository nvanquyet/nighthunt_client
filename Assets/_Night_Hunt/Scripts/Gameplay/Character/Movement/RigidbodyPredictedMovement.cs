using UnityEngine;

namespace NightHunt.Gameplay.Character
{
    /// <summary>
    /// Rigidbody-based predicted movement implementation.
    /// 
    /// ADVANTAGES:
    /// - Full physics simulation
    /// - Can be affected by external forces (explosions, wind, etc.)
    /// - Realistic physics interactions
    /// - Better for physics-heavy games
    /// 
    /// DISADVANTAGES:
    /// - More complex to tune
    /// - Can feel "floaty" if not configured properly
    /// - Higher performance cost
    /// - Requires careful prediction tuning
    /// 
    /// USE WHEN:
    /// - Need realistic physics interactions
    /// - Want characters affected by forces
    /// - Building physics-based gameplay (ragdoll, knockback, etc.)
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public sealed class RigidbodyPredictedMovement : BaseCharacterPredictedMovement
    {
        [Header("Rigidbody Settings")]
        [SerializeField] private float groundCheckDistance = 0.1f;
        [SerializeField] private LayerMask groundLayer = ~0;
        [SerializeField] private float maxFallSpeed = -40f;

        [Header("Physics Tuning")]
        [SerializeField] private float drag = 5f;
        [SerializeField] private float angularDrag = 0.05f;

        private Rigidbody _rigidbody;
        private CapsuleCollider _capsule;
        private bool _isGroundedCached;

        #region INITIALIZATION

        protected override void InitializePhysicsComponents()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _capsule = GetComponent<CapsuleCollider>();

            if (_rigidbody == null)
            {
                Debug.LogError("[RigidbodyPredictedMovement] Rigidbody component NOT FOUND!");
                return;
            }

            if (_capsule == null)
            {
                Debug.LogError("[RigidbodyPredictedMovement] CapsuleCollider component NOT FOUND!");
                return;
            }

            // Configure Rigidbody for character movement
            // _rigidbody.isKinematic = false;
            // _rigidbody.useGravity = true; // We handle gravity manually
            // _rigidbody.interpolation = RigidbodyInterpolation.None; // Prediction handles smoothing
            // _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            // _rigidbody.constraints = RigidbodyConstraints.FreezeRotation; // Prevent physics rotation
            // _rigidbody.linearDamping = drag;
            // _rigidbody.angularDamping = angularDrag;

            _rigidbody.useGravity = true; // We handle gravity manually
            if (enableDebugLogs)
                Debug.Log($"[RigidbodyPredictedMovement] Rigidbody configured - mass={_rigidbody.mass}");
        }

        protected override string GetPhysicsComponentName()
        {
            return _rigidbody != null ? $"Rigidbody (mass={_rigidbody.mass}, kinematic={_rigidbody.isKinematic})" : "NULL!";
        }

        #endregion

        #region PHYSICS IMPLEMENTATION

        protected override bool IsGrounded()
        {
            if (_capsule == null) return false;

            // Raycast from capsule bottom
            Vector3 origin = transform.position + Vector3.up * (_capsule.height * 0.5f);
            float distance = _capsule.height * 0.5f + groundCheckDistance;

            _isGroundedCached = Physics.Raycast(origin, Vector3.down, distance, groundLayer, QueryTriggerInteraction.Ignore);

            return _isGroundedCached;
        }

        protected override void ApplyMovement(Vector3 movement, float dt)
        {
            if (_rigidbody == null) return;

            // METHOD 1: Direct velocity assignment (more responsive, less physics-y)
            // _rigidbody.velocity = movement;

            // METHOD 2: MovePosition (kinematic-like, but with physics)
            // Vector3 targetPosition = _rigidbody.position + movement * dt;
            // _rigidbody.MovePosition(targetPosition);

            // METHOD 3: AddForce (most physics-realistic, but can feel floaty)
            // Vector3 desiredVelocity = movement;
            // Vector3 velocityChange = desiredVelocity - _rigidbody.velocity;
            // _rigidbody.AddForce(velocityChange, ForceMode.VelocityChange);

            // RECOMMENDED: Hybrid approach - direct velocity for horizontal, addForce for vertical
            Vector3 currentVelocity = _rigidbody.linearVelocity;

            // Set horizontal velocity directly for responsive movement
            currentVelocity.x = movement.x;
            currentVelocity.z = movement.z;

            // Apply vertical movement (gravity)
            currentVelocity.y = Mathf.Max(movement.y, maxFallSpeed);

            _rigidbody.linearVelocity = currentVelocity;

            if (enableDebugLogs && movement.sqrMagnitude > 0.01f)
            {
                Debug.Log($"[RigidbodyPredictedMovement] Velocity set to: {_rigidbody.linearVelocity}");
            }
        }

        protected override Vector3 GetCurrentVelocity()
        {
            // Rigidbody stores its own velocity
            return _rigidbody != null ? _rigidbody.linearVelocity : Vector3.zero;
        }

        protected override void ResetPhysicsState()
        {
            if (_rigidbody == null) return;

            // Reset velocities during reconciliation
            if (IsGrounded() && _rigidbody.linearVelocity.y < 0f)
            {
                Vector3 vel = _rigidbody.linearVelocity;
                vel.y = -2f;
                _rigidbody.linearVelocity = vel;
            }

            // Reset angular velocity
            _rigidbody.angularVelocity = Vector3.zero;
        }

        #endregion

        #region ADDITIONAL FEATURES

        /// <summary>
        /// Get Rigidbody component
        /// </summary>
        public Rigidbody GetRigidbody() => _rigidbody;

        /// <summary>
        /// Apply external force to character (explosions, knockback, etc.)
        /// </summary>
        public void ApplyExternalForce(Vector3 force, ForceMode mode = ForceMode.Impulse)
        {
            if (_rigidbody == null) return;
            _rigidbody.AddForce(force, mode);
        }

        /// <summary>
        /// Check if rigidbody is valid
        /// </summary>
        public bool IsRigidbodyValid() => _rigidbody != null && !_rigidbody.isKinematic;

        #endregion

        #region DEBUG

        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();

            if (!Application.isPlaying || _capsule == null) return;

            // Draw ground check raycast
            Vector3 origin = transform.position + Vector3.up * (_capsule.radius + 0.01f);
            float distance = _capsule.radius + groundCheckDistance;

            Gizmos.color = _isGroundedCached ? Color.green : Color.red;
            Gizmos.DrawRay(origin, Vector3.down * distance);
            Gizmos.DrawWireSphere(origin + Vector3.down * distance, 0.1f);
        }

        protected override void OnGUI()
        {
            base.OnGUI();

            if (!IsOwner || !Application.isPlaying || !enableDebugLogs) return;

            // Additional Rigidbody-specific debug info
            GUI.color = Color.yellow;
            GUILayout.BeginArea(new Rect(520, 10, 300, 140));

            GUILayout.Label("=== RIGIDBODY INFO ===");
            if (_rigidbody != null)
            {
                GUILayout.Label($"Mass: {_rigidbody.mass:F2}");
                GUILayout.Label($"Drag: {_rigidbody.linearDamping:F2}");
                GUILayout.Label($"Velocity: {_rigidbody.linearVelocity.magnitude:F2}");
                GUILayout.Label($"Angular Vel: {_rigidbody.angularVelocity.magnitude:F2}");
                GUILayout.Label($"Kinematic: {_rigidbody.isKinematic}");
            }
            else
            {
                GUILayout.Label("Rigidbody: NULL");
            }

            if (_capsule != null)
            {
                GUILayout.Label($"Capsule Height: {_capsule.height:F2}");
                GUILayout.Label($"Capsule Radius: {_capsule.radius:F2}");
            }

            GUILayout.EndArea();
        }

        #endregion
        
    }
}