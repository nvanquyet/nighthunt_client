using UnityEngine;
using NightHunt.Utilities;

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
        // Keep drag at 0 when using MovePosition — direct position control does NOT need
        // velocity damping, and a non-zero linearDamping fights PhysX's internally
        // computed velocity (from position delta), causing client/server divergence.
        [SerializeField] private float drag = 0f;
        [SerializeField] private float angularDrag = 0.05f;

        [Header("Wall Prevention")]
        // Extra distance beyond the capsule radius checked ahead of movement each tick.
        // Increase if tunneling happens at high speed; decrease if movement feels sluggish near walls.
        [SerializeField] private float wallCheckSkinWidth = 0.08f;

        private Rigidbody _rigidbody;
        private CapsuleCollider _capsule;
        private bool _isGroundedCached;

        // Stores the movement vector last passed to ApplyMovement so GetCurrentVelocity()
        // returns the *intended* velocity, not the physics-derived linearVelocity.
        // Using linearVelocity after MovePosition is unreliable: wall collisions produce
        // depenetration responses (potentially with positive Y) that would fly the player up.
        private Vector3 _lastAppliedMovement;

        // Target for non-owner kinematic interpolation (set by Reconcile in base class).
        // We shadow the base _targetPosition/_targetRotation via FixedUpdate MovePosition
        // so the kinematic Rigidbody's interpolation buffer is kept current.
        private bool _isNonOwnerKinematic;

        #region INITIALIZATION

        protected override void InitializePhysicsComponents()
        {
            _rigidbody = ComponentResolver.Find<Rigidbody>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] Rigidbody not found")
        .Resolve();
            _capsule = ComponentResolver.Find<CapsuleCollider>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] CapsuleCollider not found")
        .Resolve();

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

            // ── Gravity ──────────────────────────────────────────────────────────
            // We manage vertical velocity manually inside SimulateMovement via
            // _verticalVelocity + Physics.gravity.y accumulation.
            // Leaving useGravity = true lets Unity ALSO apply gravity every
            // FixedUpdate, causing double-gravity and jitter especially at sprint.
            _rigidbody.useGravity = false;

            // ── Visual smoothing ─────────────────────────────────────────────────
            // Physics runs at 50 Hz (tickRate); render runs at 60-120+ Hz.
            // Interpolate makes Unity sub-step the visual position between
            // FixedUpdate calls so movement appears smooth at any frame rate.
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            // ── Collision detection ──────────────────────────────────────────────
            // ContinuousDynamic prevents tunneling at sprint / high velocity.
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // ── Rotation ────────────────────────────────────────────────────────
            // FreezeRotationX/Z prevents the capsule from tipping on contact.
            // We deliberately do NOT freeze Y (yaw) — MoveRotation controls yaw
            // every tick and requires the Y axis to be free in the physics solver.
            // (FreezeRotation = freeze all three axes; that also blocks MoveRotation
            // which works through angular velocity that the solver would then zero.)
            // Angular velocity is zeroed manually in ApplyMovement every tick so
            // physics forces can never accumulate an unintended yaw spin.
            _rigidbody.constraints = RigidbodyConstraints.FreezeRotationX
                                   | RigidbodyConstraints.FreezeRotationZ;

            // ── Damping ──────────────────────────────────────────────────────────
            // Keep low drag so we control speed fully via velocity assignment.
            _rigidbody.linearDamping  = drag;
            _rigidbody.angularDamping = angularDrag;

            if (enableDebugLogs)
                Debug.Log($"[RigidbodyPredictedMovement] Rigidbody configured - mass={_rigidbody.mass}, interpolation={_rigidbody.interpolation}, gravity={_rigidbody.useGravity}");

            // Ownership-based kinematic setup runs in OnStartNetwork once IsOwner is known.
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            if (_rigidbody == null) return;

            // ── Owner / Server: active physics simulation ────────────────────────
            // ── Non-owner observer: kinematic so that our FixedUpdate MovePosition
            //    drives the visual position without the physics engine fighting it.
            //
            // Non-kinematic Rigidbody + direct transform.position writes (Lerp in
            // Update) cause the physics engine to override the result every step,
            // producing visible jitter for remote players seen by the local client.
            _isNonOwnerKinematic = !base.Owner.IsLocalClient && !IsServerStarted;
            _rigidbody.isKinematic = _isNonOwnerKinematic;

            if (_isNonOwnerKinematic)
            {
                // Kinematic objects must use Interpolate to get sub-FixedUpdate
                // visual smoothing when MovePosition is called each physics step.
                _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                if (enableDebugLogs)
                    Debug.Log("[RigidbodyPredictedMovement] Non-owner → kinematic mode");
            }
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

            // ── Correct bottom-sphere ground check ───────────────────────────────
            // Previous code cast from the TOP of the capsule (transform.position +
            // up * height/2) downward by height/2+offset, which only reached the
            // centre of the capsule — never the feet.  This caused IsGrounded() to
            // return false on nearly every tick, making _verticalVelocity oscillate
            // between accumulated gravity and the -2f reset, producing a visible
            // micro-bounce/jitter especially during sprint.
            //
            // Fix: CheckSphere at the centre of the bottom hemisphere of the capsule
            // (transform.position + capsule.center - up * (height/2 - radius)).
            // A sphere of the same radius plus a small skin detects the ground
            // reliably without false positives from side-walls.
            Vector3 capsuleWorldCenter = transform.position + _capsule.center;
            Vector3 bottomSphereCenter = capsuleWorldCenter
                                         - Vector3.up * (_capsule.height * 0.5f - _capsule.radius);

            _isGroundedCached = Physics.CheckSphere(
                bottomSphereCenter,
                _capsule.radius + groundCheckDistance,
                groundLayer,
                QueryTriggerInteraction.Ignore
            );

            return _isGroundedCached;
        }

        protected override void ApplyMovement(Vector3 movement, float dt)
        {
            if (_rigidbody == null || _isNonOwnerKinematic) return;

            // ── Sync rigidbody rotation ────────────────────────────────────────────
            // SimulateMovement already wrote transform.rotation via RotateTowards.
            // MoveRotation feeds the Rigidbody interpolation buffer so the visual
            // rotation is sub-stepped smoothly between 50 Hz ticks at any FPS.
            // This works because we only freeze X/Z in the constraints, leaving
            // yaw (Y) free for the physics solver to accept the MoveRotation target.
            _rigidbody.MoveRotation(transform.rotation);
            // Zero any residual angular velocity so physics forces (collisions, etc.)
            // cannot introduce an unintended spin between ticks.
            _rigidbody.angularVelocity = Vector3.zero;

            // ── Look-ahead wall check ─────────────────────────────────────────────
            // Before committing the position delta to MovePosition, cast the capsule
            // in the horizontal movement direction over the intended displacement.
            // If it would hit a wall, zero the horizontal component so the player
            // stops cleanly against the surface instead of generating a depenetration
            // response (which can have a positive Y component → player flies up).
            //
            // Vertical movement (gravity / jump) is always preserved so the player
            // continues to fall / land correctly even while hugging a wall.
            Vector3 horizontal = new Vector3(movement.x, 0f, movement.z);
            Vector3 vertical   = new Vector3(0f, movement.y, 0f);

            if (horizontal.sqrMagnitude > 0.0001f)
            {
                float   moveDistance = horizontal.magnitude * dt;
                Vector3 moveDir      = horizontal.normalized;

                if (IsBlockedHorizontally(moveDir, moveDistance))
                {
                    // Wall ahead — cancel horizontal, keep gravity/vertical.
                    horizontal = Vector3.zero;
                    if (enableDebugLogs)
                        Debug.Log($"[RigidbodyPredictedMovement] Wall look-ahead blocked in dir={moveDir}");
                }
            }

            // Cache the resolved (possibly wall-blocked) movement so GetCurrentVelocity()
            // returns it deterministically — no collision-induced linearVelocity noise.
            // _lastAppliedMovement carries the full intended Y (including stick-down) so
            // reconcile snapshots always receive the authoritative vertical velocity.
            Vector3 resolvedMovement = horizontal + vertical;
            _lastAppliedMovement = resolvedMovement;

            // NOTE: Do NOT suppress physicsMovement.y here.
            // Previous iteration zeroed Y when grounded to avoid PhysX floor-push jitter,
            // but that causes the character to "float" above terrain after rolling (capsule
            // drifts up a few millimetres, grounded check still true, y=0 → stuck at height).
            // With the corrected gravity model (constant -stickDown, no accumulation),
            // the downward force is tiny (stickDown=0.3 → 6mm/tick); PhysX contact
            // resolution + Rigidbody interpolation keeps it visually stable.
            Vector3 physicsMovement = resolvedMovement;

            // Owner / Server: drive movement via MovePosition.
            //
            // Why MovePosition instead of linearVelocity assignment?
            //   • MovePosition commits an exact world-space displacement each physics
            //     step and feeds the Rigidbody's interpolation buffer, so the visual
            //     position is sub-stepped smoothly between 50 Hz ticks at any FPS.
            //   • Directly setting linearVelocity leaves the actual move to the
            //     physics integrator; any sub-tick timing difference between the
            //     FishNet tick and FixedUpdate produces a 1-frame offset that
            //     manifests as jitter at sprint speed.
            //   • Because we call MovePosition once per tick (50 Hz) the displacement
            //     per call is resolvedMovement * dt  (same as velocity * fixedDeltaTime).
            Vector3 newPosition = _rigidbody.position + physicsMovement * dt;
            _rigidbody.MovePosition(newPosition);
            // Zero residual velocity so PhysX does not apply it again between ticks.
            // MovePosition commits the exact intended displacement; any leftover
            // linearVelocity from contact responses (especially positive-Y depenetration)
            // would fly the character upward in the next physics step.
            _rigidbody.linearVelocity = Vector3.zero;

            if (enableDebugLogs && resolvedMovement.sqrMagnitude > 0.01f)
                Debug.Log($"[RigidbodyPredictedMovement] MovePosition delta={(physicsMovement * dt).magnitude:F4}");
        }

        /// <summary>
        /// CapsuleCast in the horizontal direction to check whether the player
        /// would hit a wall within the intended displacement this tick.
        /// Uses wallCheckSkinWidth as extra look-ahead beyond the move distance
        /// to catch high-speed face-into-wall spam reliably.
        /// </summary>
        private bool IsBlockedHorizontally(Vector3 direction, float moveDistance)
        {
            if (_capsule == null) return false;

            // Build the two sphere centres of the capsule in world space.
            Vector3 worldCenter = transform.position + _capsule.center;
            float   innerHalf   = Mathf.Max(0f, _capsule.height * 0.5f - _capsule.radius);
            Vector3 sphereTop   = worldCenter + Vector3.up * innerHalf;
            Vector3 sphereBot   = worldCenter - Vector3.up * innerHalf;

            // Cast slightly shorter than the full radius so the capsule
            // can already be flush against a wall without false-positives.
            float castRadius = _capsule.radius * 0.99f;

            return Physics.CapsuleCast(
                sphereBot,
                sphereTop,
                castRadius,
                direction,
                moveDistance + wallCheckSkinWidth,
                groundLayer,
                QueryTriggerInteraction.Ignore
            );
        }

        protected override Vector3 GetCurrentVelocity()
        {
            // Return the INTENDED movement vector from the last ApplyMovement call,
            // NOT _rigidbody.linearVelocity.
            //
            // Why: after MovePosition hits a wall, PhysX resolves the collision and may
            // produce a depenetration velocity with a positive Y component. If we return
            // linearVelocity, Reconcile packs that upward spike into data.Velocity.y,
            // which the client then restores as _verticalVelocity → player flies up.
            // The cached intended movement always carries the correct _verticalVelocity
            // that SimulateMovement computed, so Reconcile restores it faithfully.
            return _lastAppliedMovement;
        }

        protected override void ResetPhysicsState()
        {
            if (_rigidbody == null) return;

            // ── Sync Rigidbody transform to current transform ────────────────────
            _rigidbody.position = transform.position;
            _rigidbody.rotation = transform.rotation;

            if (!_isNonOwnerKinematic)
            {
                // Zero both velocities.  We use MovePosition for displacement, not
                // linearVelocity; any non-zero value here would be applied by PhysX
                // as an extra unwanted impulse on the first post-reconcile FixedUpdate.
                _rigidbody.linearVelocity  = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }

            // NOTE: Do NOT reset _verticalVelocity here.
            // When called from Reconcile, the base class has ALREADY set
            // _verticalVelocity = data.Velocity.y (the server-authoritative fall speed).
            // Clamping it to -2f here would wipe that value every reconcile tick,
            // causing the player to perpetually restart falling from near-zero speed
            // rather than continuing at the correct velocity → float / bounce near walls.
            // Initialization sets _verticalVelocity = -2f explicitly in OnStartNetwork,
            // so no special handling is needed here.
        }

        /// <summary>
        /// Override base Teleport: set Rigidbody position/rotation DIRECTLY (bypasses physics
        /// interpolation that would otherwise delay the move by one frame), then zero velocities.
        /// The server's next reconcile tick broadcasts the corrected state to all clients.
        /// </summary>
        public override void Teleport(Vector3 position, Quaternion rotation)
        {
            transform.position = position;
            transform.rotation = rotation;
            _velocity          = Vector3.zero;
            _verticalVelocity  = -2f;

            if (_rigidbody != null)
            {
                _rigidbody.position           = position;
                _rigidbody.rotation           = rotation;
                _rigidbody.linearVelocity  = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }

            if (enableDebugLogs)
                Debug.Log($"[RigidbodyPredictedMovement] Teleport → pos={position}, rot={rotation.eulerAngles}");
        }

        #endregion

        #region ROLL WALL CHECK

        protected override bool IsRollBlocked(Vector3 direction, float stepDistance)
            => IsBlockedHorizontally(direction, stepDistance);

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

        // ── Non-owner kinematic interpolation ─────────────────────────────────
        // MovePosition on a kinematic Rigidbody must be called from FixedUpdate
        // (not Update) so that Rigidbody.interpolation can sub-step between physics
        // frames and the visual position is smooth at any render frame rate.
        //
        // We override Update() from the base class to suppress its direct
        // transform.position Lerp for the kinematic path; the base Update still
        // runs for the non-Rigidbody fallback (server / non-spawned).
        protected override void Update()
        {
            if (_isNonOwnerKinematic) return; // FixedUpdate handles this path
            base.Update();
        }

        private void FixedUpdate()
        {
            if (!_isNonOwnerKinematic || _rigidbody == null) return;

            // Smooth kinematic follow towards server-authoritative target.
            // _targetPosition / _targetRotation are set by Reconcile() in base class
            // each time a server snapshot arrives (50 Hz).
            Vector3    newPos = Vector3.Lerp(
                _rigidbody.position, _targetPosition,
                Time.fixedDeltaTime * interpolationSpeed);

            Quaternion newRot = Quaternion.Slerp(
                _rigidbody.rotation, _targetRotation,
                Time.fixedDeltaTime * interpolationSpeed);

            _rigidbody.MovePosition(newPos);
            _rigidbody.MoveRotation(newRot);
        }

        #region DEBUG

        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();

            if (!Application.isPlaying || _capsule == null) return;

            // Draw the corrected ground-check sphere at the capsule bottom
            Vector3 capsuleWorldCenter = transform.position + _capsule.center;
            Vector3 bottomSphereCenter = capsuleWorldCenter
                                         - Vector3.up * (_capsule.height * 0.5f - _capsule.radius);

            Gizmos.color = _isGroundedCached ? Color.green : Color.red;
            Gizmos.DrawWireSphere(bottomSphereCenter, _capsule.radius + groundCheckDistance);
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
