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
        [SerializeField, Tooltip("Maximum surface angle (degrees) considered ground. Surfaces steeper than this are treated as walls.")]
        private float maxGroundAngle = 60f;
        [SerializeField] private LayerMask groundLayer = ~0;
#pragma warning disable CS0414
        [SerializeField] private float maxFallSpeed = -40f;
#pragma warning restore CS0414

        [Header("Physics Tuning")]
        // Keep drag at 0 when using MovePosition — direct position control does NOT need
        // velocity damping, and a non-zero linearDamping fights PhysX's internally
        // computed velocity (from position delta), causing client/server divergence.
        [SerializeField] private float drag = 0f;
        [SerializeField] private float angularDrag = 0.05f;

        [Header("Wall Prevention")]
        [SerializeField] private float wallCheckSkinWidth = 0.08f;
        [SerializeField, Tooltip("Layer mask for wall/obstacle detection. Must NOT include the player's own layer.")]
        private LayerMask wallLayerMask = ~0;

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

        // Throttle ground-check diagnostic to once per 50 calls (~1 s at 50 Hz).
        private int _groundDiagCounter;

        #region INITIALIZATION

        protected override void InitializePhysicsComponents()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _capsule   = GetComponent<CapsuleCollider>();

            if (_rigidbody == null) { Debug.LogError("[RigidbodyPredictedMovement] Rigidbody not found."); return; }
            if (_capsule   == null) { Debug.LogError("[RigidbodyPredictedMovement] CapsuleCollider not found."); return; }

            _rigidbody.useGravity             = false;                              // Gravity managed manually via _verticalVelocity
            _rigidbody.interpolation          = RigidbodyInterpolation.Interpolate; // Sub-steps visual position between 50 Hz ticks
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rigidbody.constraints            = RigidbodyConstraints.FreezeRotationX
                                              | RigidbodyConstraints.FreezeRotationZ;
            _rigidbody.linearDamping          = drag;
            _rigidbody.angularDamping         = angularDrag;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            if (_rigidbody == null) return;

            // Owner/Server: active simulation. Non-owner: kinematic, driven via FixedUpdate MovePosition.
            _isNonOwnerKinematic   = !base.Owner.IsLocalClient && !IsServerStarted;
            _rigidbody.isKinematic = _isNonOwnerKinematic;

            if (_isNonOwnerKinematic)
                _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
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

            // SphereCast from just above the capsule's bottom sphere centre.
            // groundLayer excludes the player's own layer — no self-hit filtering needed.
            Vector3 capsuleWorldCenter = transform.position + _capsule.center;
            Vector3 bottomSphereCenter = capsuleWorldCenter - Vector3.up * (_capsule.height * 0.5f - _capsule.radius);
            Vector3 castOrigin         = bottomSphereCenter + Vector3.up * 0.01f;
            float   castRadius         = Mathf.Max(0.001f, _capsule.radius * 0.99f);
            float   castDistance       = Mathf.Max(0f, groundCheckDistance + 0.01f);

            bool found = Physics.SphereCast(castOrigin, castRadius, Vector3.down, out RaycastHit hit,
                castDistance, groundLayer, QueryTriggerInteraction.Ignore);

            if (!found)
            {
                _isGroundedCached = false;
                SetGroundInfo(false, Vector3.up);
                return false;
            }

            // Reject steep surfaces (walls/ledges) based on maxGroundAngle.
            float slopeAngle  = Vector3.Angle(hit.normal, Vector3.up);
            _isGroundedCached = slopeAngle <= Mathf.Clamp(maxGroundAngle, 0f, 89f);
            SetGroundInfo(_isGroundedCached, hit.normal);

            if (diagnoseMysteryMove && IsOwner)
            {
                if (++_groundDiagCounter >= 50)
                {
                    _groundDiagCounter = 0;
                    Debug.Log($"[DIAG][GND] hit='{hit.collider.name}' " +
                              $"layer={hit.collider.gameObject.layer}({LayerMask.LayerToName(hit.collider.gameObject.layer)}) " +
                              $"slope={slopeAngle:F1}deg normal=({hit.normal.x:F2},{hit.normal.y:F2},{hit.normal.z:F2}) " +
                              $"valid={_isGroundedCached}");
                }
            }

            return _isGroundedCached;
        }

        protected override void ApplyMovement(Vector3 movement, float dt)
        {
            if (_rigidbody == null || _isNonOwnerKinematic) return;

            // Log any incoming horizontal velocity from external sources (before we clear it).
            if (diagnoseMysteryMove && IsOwner && IsSpawned)
            {
                Vector3 v = _rigidbody.linearVelocity;
                if (v.x * v.x + v.z * v.z > 0.01f)
                    Debug.LogWarning($"[DIAG][VEL_IN] Incoming rb.linearVelocity=({v.x:F3},{v.y:F3},{v.z:F3}) " +
                                     $"rb.pos={_rigidbody.position:F2} tr.pos={transform.position:F2}");
            }

            // ROOT DRIFT FIX: zero velocity BEFORE MovePosition, not just after.
            // PhysX contact responses from the previous FixedUpdate can leave residual
            // velocity that would be integrated and "lock in" lateral drift every tick.
            _rigidbody.linearVelocity  = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;

            // Push rotation into the Rigidbody interpolation buffer (sub-steps between ticks).
            _rigidbody.MoveRotation(transform.rotation);

            // Split into horizontal/vertical so wall check gates horizontal only.
            // Vertical (gravity/jump) is always preserved — player must still fall near walls.
            Vector3 horizontal = new Vector3(movement.x, 0f, movement.z);
            Vector3 vertical   = new Vector3(0f, movement.y, 0f);

            if (horizontal.sqrMagnitude > 0.0001f)
            {
                float   moveDistance = horizontal.magnitude * dt;
                Vector3 moveDir      = horizontal.normalized;

                if (IsBlockedHorizontally(moveDir, moveDistance, out Vector3 wallNormal))
                {
                    // During roll: hard stop (no slide — roll direction is committed).
                    // During walk/run: project onto wall plane and slide if clear.
                    Vector3 slideDir = Vector3.zero;
                    if (!_isRolling)
                        slideDir = Vector3.ProjectOnPlane(moveDir, wallNormal).normalized;

                    if (slideDir.sqrMagnitude > 0.001f &&
                        !IsBlockedHorizontally(slideDir, moveDistance, out _))
                    {
                        horizontal = slideDir * horizontal.magnitude;
                    }
                    else
                    {
                        horizontal = Vector3.zero;
                    }

                    if (enableDebugLogs)
                        Debug.Log($"[RigidbodyPredictedMovement] Wall hit: rolling={_isRolling} normal={wallNormal} slide={slideDir:F2}");
                }
                else if (GroundSlopeAngle > 0.5f)
                {
                    // Project horizontal onto the ground plane for smooth slope traversal.
                    horizontal = Vector3.ProjectOnPlane(horizontal, GroundNormal).normalized * moveDistance / dt;
                }
            }

            Vector3 resolvedMovement = horizontal + vertical;
            _lastAppliedMovement = resolvedMovement;

            _rigidbody.MovePosition(_rigidbody.position + resolvedMovement * dt);
            _rigidbody.linearVelocity = Vector3.zero; // Suppress contact impulses from this step.

            if (enableDebugLogs && resolvedMovement.sqrMagnitude > 0.01f)
                Debug.Log($"[RigidbodyPredictedMovement] MovePosition delta={(resolvedMovement * dt).magnitude:F4}");
        }

        private bool IsBlockedHorizontally(Vector3 direction, float moveDistance, out Vector3 wallNormal)
        {
            wallNormal = Vector3.zero;
            if (_capsule == null) return false;

            Vector3 worldCenter = transform.position + _capsule.center;
            float   innerHalf   = Mathf.Max(0f, _capsule.height * 0.5f - _capsule.radius);
            Vector3 sphereTop   = worldCenter + Vector3.up * innerHalf;
            Vector3 sphereBot   = worldCenter - Vector3.up * innerHalf;

            bool hit = Physics.CapsuleCast(sphereBot, sphereTop, _capsule.radius * 0.99f,
                direction, out RaycastHit hitInfo, moveDistance + wallCheckSkinWidth,
                wallLayerMask, QueryTriggerInteraction.Ignore);

            if (hit) wallNormal = hitInfo.normal;
            return hit;
        }

        protected override Vector3 GetCurrentVelocity()
        {
            // Return intended movement, not _rigidbody.linearVelocity:
            // PhysX contact responses can corrupt linearVelocity.y → player flies on wall hits.
            return _lastAppliedMovement;
        }

        protected override void ResetPhysicsState()
        {
            if (_rigidbody == null) return;

            _rigidbody.position = transform.position;
            _rigidbody.rotation = transform.rotation;

            if (!_isNonOwnerKinematic && !_rigidbody.isKinematic)
            {
                _rigidbody.linearVelocity  = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }
            // _verticalVelocity is NOT reset here — Reconcile already set it to data.Velocity.y.
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
            => IsBlockedHorizontally(direction, stepDistance, out _);

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

        // Non-owner: suppress base Update() Lerp; kinematic path uses FixedUpdate MovePosition.
        protected override void Update()
        {
            if (_isNonOwnerKinematic) return;
            base.Update();
        }

        private void FixedUpdate()
        {
            // Non-owner kinematic: drive toward reconciled target.
            if (_isNonOwnerKinematic && _rigidbody != null)
            {
                _rigidbody.MovePosition(Vector3.Lerp(_rigidbody.position, _targetPosition, Time.fixedDeltaTime * interpolationSpeed));
                _rigidbody.MoveRotation(Quaternion.Slerp(_rigidbody.rotation, _targetRotation, Time.fixedDeltaTime * interpolationSpeed));
                return;
            }

            // Owner / server: monitor for unexpected horizontal velocity between ticks.
            if (!_isNonOwnerKinematic && _rigidbody != null && diagnoseMysteryMove && IsOwner && IsSpawned)
            {
                Vector3 v = _rigidbody.linearVelocity;
                if (v.x * v.x + v.z * v.z > 0.01f)
                    Debug.LogWarning($"[DIAG][FU] FixedUpdate horizontal velocity detected: " +
                                     $"({v.x:F3},{v.y:F3},{v.z:F3}) rb.pos={_rigidbody.position:F2}");
            }
        }

        #region DEBUG

        private void OnCollisionEnter(Collision col)
        {
            if (!diagnoseMysteryMove || !IsOwner || !IsSpawned) return;
            Debug.LogWarning($"[DIAG][COL_ENTER] object='{col.gameObject.name}' " +
                             $"layer={col.gameObject.layer}({LayerMask.LayerToName(col.gameObject.layer)}) " +
                             $"relVel=({col.relativeVelocity.x:F2},{col.relativeVelocity.y:F2},{col.relativeVelocity.z:F2}) " +
                             $"impulse=({col.impulse.x:F2},{col.impulse.y:F2},{col.impulse.z:F2})");
        }

        private void OnCollisionStay(Collision col)
        {
            if (!diagnoseMysteryMove || !IsOwner || !IsSpawned || _rigidbody == null) return;
            Vector3 v = _rigidbody.linearVelocity;
            if (v.x * v.x + v.z * v.z > 0.01f)
                Debug.Log($"[DIAG][COL_STAY] object='{col.gameObject.name}' " +
                          $"layer={col.gameObject.layer}({LayerMask.LayerToName(col.gameObject.layer)}) " +
                          $"rb.vel=({v.x:F3},{v.y:F3},{v.z:F3})");
        }

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