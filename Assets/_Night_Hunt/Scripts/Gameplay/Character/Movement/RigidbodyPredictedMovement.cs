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

        // groundDetectionLayer: used ONLY in IsGrounded() CheckSphere.
        // MUST include the layer of every surface the player can stand on (terrain, floor, platforms).
        // Defaults to Everything (~0) — do NOT restrict unless you have a deliberate reason.
        // Wrong value = IsGrounded() always false → gravity accumulates on server → player falls through map.
        [SerializeField] private LayerMask groundDetectionLayer = ~0;

        // groundLayer: used for wall-blocking, step-climbing and slope detection.
        // Can be restricted to specific obstacle layers if needed.
        [SerializeField] private LayerMask groundLayer = ~0;
        // maxFallSpeed moved to MovementSettings SO (field: maxFallSpeed).

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

        [Header("Slope & Step Settings")]
        // Matches CharacterController's stepOffset: max height (metres) the character can step over.
        // Set to 0 to disable step-climbing entirely.
        [SerializeField] private float maxStepHeight = 0.35f;
        // Slopes steeper than this angle (degrees) are treated as walls — character is pushed off
        // rather than walking up them. Matches CharacterController's slopeLimit.
        [SerializeField] private float maxSlopeAngle = 46f;

        private Rigidbody _rigidbody;
        private CapsuleCollider _capsule;
        private bool _isGroundedCached;

        // Surface normal of the ground below the character, updated every IsGrounded() call.
        // Used to project horizontal movement onto slopes and determine whether a surface is walkable.
        private Vector3 _groundNormal = Vector3.up;

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

            // ── useGravity = false (bắt buộc) ───────────────────────────────────
            // Code tự tính gravity qua _verticalVelocity trong SimulateMovement.
            // Nếu bật useGravity = true → gravity bị cộng ĐÔI (Unity + code) → jitter, bay lên.
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
            _rigidbody.linearDamping = drag;
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

            Vector3 capsuleWorldCenter = transform.position + _capsule.center;
            Vector3 bottomSphereCenter = capsuleWorldCenter
                                         - Vector3.up * (_capsule.height * 0.5f - _capsule.radius);

            // ── Grounded detection: CheckSphere (overlap test) ────────────────────
            // CheckSphere is used deliberately over SphereCast here.
            // PhysX SphereCast returns NO HIT when the origin sphere is already
            // touching/overlapping a collider — which happens every tick when using
            // MovePosition + linearVelocity=0 (capsule is placed exactly on the floor).
            // CheckSphere does NOT have this limitation: it returns true for any overlap
            // regardless of the initial position, so grounded detection is rock-solid.
            _isGroundedCached = Physics.CheckSphere(
                bottomSphereCenter,
                _capsule.radius + groundCheckDistance,
                groundDetectionLayer,   // uses dedicated layer mask — never blocks grounded detection
                QueryTriggerInteraction.Ignore
            );

            // ── Ground normal: separate Raycast for slope projection ──────────────
            // Done as a Raycast (not SphereCast) so we get the exact surface normal
            // without the SphereCast origin-overlap false-negative problem.
            // Only run when grounded to avoid wasting casts in the air.
            if (_isGroundedCached)
            {
                // Ray origin lifted slightly above the sphere center so it starts
                // outside any floor geometry the capsule may be touching.
                Vector3 rayOrigin = bottomSphereCenter + Vector3.up * 0.05f;
                if (Physics.Raycast(
                        rayOrigin,
                        Vector3.down,
                        out RaycastHit normalHit,
                        _capsule.radius + groundCheckDistance + 0.1f,
                        groundDetectionLayer,   // same dedicated mask as CheckSphere above
                        QueryTriggerInteraction.Ignore))
                {
                    _groundNormal = normalHit.normal;
                }
                else
                {
                    _groundNormal = Vector3.up;
                }
            }
            else
            {
                _groundNormal = Vector3.up;
            }

            return _isGroundedCached;
        }

        protected override void ApplyMovement(Vector3 movement, float dt)
        {
            if (_rigidbody == null || _isNonOwnerKinematic) return;

            _rigidbody.MoveRotation(transform.rotation);
            _rigidbody.angularVelocity = Vector3.zero;

            // XZ tách biệt để handle wall-cast và slope projection.
            // Y = _verticalVelocity từ SimulateMovement (gravity / jump / grounded-stick).
            // useGravity = false → Unity KHÔNG thêm gravity tự động.
            // Toàn bộ gravity do SimulateMovement tính và truyền vào movement.y ở đây.
            Vector3 horizontal = new Vector3(movement.x, 0f, movement.z);
            float   stepUpY    = 0f;

            if (horizontal.sqrMagnitude > 0.0001f)
            {
                float moveDistance = horizontal.magnitude * dt;

                // ── Slope projection ─────────────────────────────────────────────
                // Project hướng di chuyển lên mặt phẳng slope để character đi theo
                // bề mặt instead of đâm vào sườn dốc.
                float slopeAngle = Vector3.Angle(Vector3.up, _groundNormal);
                if (_isGroundedCached && slopeAngle > 1f)
                {
                    Vector3 projected = Vector3.ProjectOnPlane(horizontal, _groundNormal);
                    if (projected.sqrMagnitude > 0.0001f)
                        horizontal = projected.normalized * horizontal.magnitude;
                    moveDistance = horizontal.magnitude * dt;

                    if (enableDebugLogs)
                        Debug.Log($"[Move] Slope {slopeAngle:F1}° | projected dir={horizontal.normalized:F2}");
                }

                // Wall-cast chỉ dùng hướng XZ (bỏ slope-Y để tránh false positive).
                Vector3 moveDir = new Vector3(horizontal.x, 0f, horizontal.z).normalized;

                // ── Wall look-ahead ──────────────────────────────────────────────
                if (IsBlockedHorizontally(moveDir, moveDistance, out RaycastHit wallHit))
                {
                    stepUpY = ComputeStepUp(moveDir);
                    if (stepUpY <= 0f)
                    {
                        horizontal = Vector3.zero;
                        if (enableDebugLogs)
                            Debug.Log($"[Move] Blocked by '{wallHit.collider.name}' (no step-up)");
                    }
                    else
                    {
                        if (enableDebugLogs)
                            Debug.Log($"[Move] StepUp {stepUpY:F3}m qua '{wallHit.collider.name}'");
                    }
                }
            }

            // ── Vertical ─────────────────────────────────────────────────────────
            // movement.y = _verticalVelocity do SimulateMovement tính:
            //   • Grounded + đang di chuyển:  dùng stick-down để bám mặt dốc
            //   • Grounded + idle:            vertDisp = 0 — KHÔNG đẩy xuống
            //     → đẩy thẳng xuống khi idle trên slope → PhysX depenetrate theo normal
            //     → normal có component XZ → trượt dù not available input!
            //   • Airborne:  tích tụ gravity mỗi tick (âm = fall, dương = jump)
            bool groundedIdle = _isGroundedCached && horizontal.sqrMagnitude <= 0.0001f && movement.y <= 0f;
            float vertDisp = groundedIdle ? 0f : movement.y * dt;

            // _lastAppliedMovement lưu velocity (không phải displacement) để
            // GetCurrentVelocity() và Reconcile restore _verticalVelocity đúng.
            _lastAppliedMovement = new Vector3(horizontal.x, movement.y, horizontal.z);

            _rigidbody.MovePosition(_rigidbody.position
                + horizontal            * dt          // XZ + slope Y follow
                + Vector3.up           * vertDisp     // vertical (gravity / jump)
                + Vector3.up           * stepUpY);    // step-up nếu có

            // Zero linearVelocity sau MovePosition:
            // - Prediction drive position bằng MovePosition, KHÔNG dùng velocity
            // - Nếu để velocity != 0: PhysX apply thêm 1 impulse frame sau → diverge client/server
            _rigidbody.linearVelocity = Vector3.zero;

            if (enableDebugLogs && (horizontal.magnitude > 0.001f || !_isGroundedCached || stepUpY > 0.001f))
                Debug.Log($"[Move] grounded={_isGroundedCached} | vertVel={_verticalVelocity:F2} vertDisp={vertDisp:F4} " +
                          $"| h=({horizontal.x:F2},{horizontal.z:F2}) step={stepUpY:F3}");
        }

        // Overload used by IsRollBlocked — no hit-info output needed.
        private bool IsBlockedHorizontally(Vector3 direction, float moveDistance)
            => IsBlockedHorizontally(direction, moveDistance, out _);

        /// <summary>
        /// CapsuleCast in the horizontal direction to check whether the player
        /// would hit a wall within the intended displacement this tick.
        /// <paramref name="hit"/> is populated when a hit is found (same semantics as Physics.CapsuleCast).
        /// Uses wallCheckSkinWidth as extra look-ahead beyond the move distance.
        /// </summary>
        private bool IsBlockedHorizontally(Vector3 direction, float moveDistance, out RaycastHit hit)
        {
            hit = default;
            if (_capsule == null) return false;

            Vector3 worldCenter = transform.position + _capsule.center;
            float innerHalf = Mathf.Max(0f, _capsule.height * 0.5f - _capsule.radius);
            Vector3 sphereTop = worldCenter + Vector3.up * innerHalf;
            Vector3 sphereBot = worldCenter - Vector3.up * innerHalf;
            float castRadius = _capsule.radius * 0.99f;

            if (!Physics.CapsuleCast(
                    sphereBot, sphereTop, castRadius,
                    direction, out hit,
                    moveDistance + wallCheckSkinWidth,
                    groundLayer,
                    QueryTriggerInteraction.Ignore))
                return false;

            // ── KEY FIX: Bỏ qua nếu surface là walkable ground/slope ──────────────
            // Normal có Y cao = mặt nằm ngang = ground, không phải wall.
            // Nếu angle < maxSlopeAngle thì đây là slope/ground edge, không block.
            float hitSurfaceAngle = Vector3.Angle(Vector3.up, hit.normal);
            if (hitSurfaceAngle < maxSlopeAngle)
            {
                if (enableDebugLogs)
                    Debug.Log($"[Move] Wall-cast bỏ qua '{hit.collider.name}' — slope {hitSurfaceAngle:F1}° (walkable)");
                hit = default;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks whether the obstacle directly ahead is a steppable ledge
        /// (height ≤ maxStepHeight) that the character can climb over, matching
        /// the behaviour of CharacterController's built-in stepOffset.
        ///
        /// Returns the Y offset (metres) to lift the character this tick,
        /// or 0 if the obstacle is too tall / not a step / not grounded.
        ///
        /// The returned offset is applied directly to the MovePosition target and
        /// is intentionally NOT stored in _lastAppliedMovement, so the gravity
        /// accumulator (_verticalVelocity) is not disrupted by step events.
        /// </summary>
        private float ComputeStepUp(Vector3 direction)
        {
            if (!_isGroundedCached || _capsule == null || maxStepHeight <= 0f)
            {
                if (enableDebugLogs)
                    Debug.Log($"[RB_MOVE][STEP_SKIP] grounded={_isGroundedCached} capsule={_capsule != null} maxStep={maxStepHeight}");
                return 0f;
            }

            Vector3 footBase = transform.position + _capsule.center - Vector3.up * (_capsule.height * 0.5f);
            float probeForward = _capsule.radius + wallCheckSkinWidth + 0.02f;
            Vector3 probeOrigin = footBase
                                   + Vector3.up * (maxStepHeight + _capsule.radius)
                                   + direction * probeForward;

            if (!Physics.SphereCast(
                    probeOrigin,
                    _capsule.radius * 0.5f,
                    Vector3.down,
                    out RaycastHit stepHit,
                    maxStepHeight + _capsule.radius,
                    groundLayer,
                    QueryTriggerInteraction.Ignore))
            {
                if (enableDebugLogs)
                    Debug.Log($"[RB_MOVE][STEP_NO_SURFACE] probe origin={probeOrigin} dir=down — no surface found ahead");
                return 0f;
            }

            float stepUp = stepHit.point.y - footBase.y;
            float surfaceAngle = Vector3.Angle(Vector3.up, stepHit.normal);

            if (stepUp <= 0f || stepUp > maxStepHeight)
            {
                if (enableDebugLogs)
                    Debug.Log($"[RB_MOVE][STEP_HEIGHT_FAIL] stepUp={stepUp:F3} maxStepHeight={maxStepHeight:F3} " +
                              $"surface='{stepHit.collider.name}'");
                return 0f;
            }

            if (surfaceAngle > maxSlopeAngle)
            {
                if (enableDebugLogs)
                    Debug.Log($"[RB_MOVE][STEP_SLOPE_FAIL] surface angle={surfaceAngle:F1}° > maxSlopeAngle={maxSlopeAngle:F1}° " +
                              $"surface='{stepHit.collider.name}'");
                return 0f;
            }

            float innerHalf = Mathf.Max(0f, _capsule.height * 0.5f - _capsule.radius);
            Vector3 liftedCenter = transform.position + _capsule.center + Vector3.up * stepUp;
            bool blocked = Physics.CheckCapsule(
                liftedCenter - Vector3.up * innerHalf,
                liftedCenter + Vector3.up * innerHalf,
                _capsule.radius * 0.95f,
                groundLayer,
                QueryTriggerInteraction.Ignore);

            if (blocked)
            {
                if (enableDebugLogs)
                    Debug.Log($"[RB_MOVE][STEP_CLEARANCE_FAIL] no room at lifted pos={liftedCenter} stepUp={stepUp:F3}");
                return 0f;
            }

            return stepUp;
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
                _rigidbody.linearVelocity = Vector3.zero;
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
            _velocity = Vector3.zero;
            _verticalVelocity = 0f; // SimulateMovement sets correct value on first tick post-teleport.

            if (_rigidbody != null)
            {
                _rigidbody.position = position;
                _rigidbody.rotation = rotation;
                _rigidbody.linearVelocity = Vector3.zero;
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
            Vector3 newPos = Vector3.Lerp(
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

        #region PHYSICS DIAGNOSTICS

        // ── Collision callbacks ───────────────────────────────────────────────
        // These run on the PHYSICS thread and fire when PhysX actually detects contact.
        // If OnCollisionEnter never fires when standing on terrain, the Physics Layer
        // Collision Matrix is blocking Player ↔ Ground — fix in Edit > Project Settings > Physics.

        private void OnCollisionEnter(Collision collision)
        {
            if (!enableDebugLogs) return;
            int layer = collision.collider.gameObject.layer;
            Debug.Log($"[RB][Collision ENTER] '{collision.collider.name}' " +
                      $"layer={layer} ({LayerMask.LayerToName(layer)}) " +
                      $"contacts={collision.contactCount} " +
                      $"relVel={collision.relativeVelocity.magnitude:F2} " +
                      $"pos={transform.position}");
        }

        private void OnCollisionStay(Collision collision)
        {
            // Log only occasionally to avoid log spam (once per ~3 s at 50Hz).
            if (!enableDebugLogs || Time.frameCount % 150 != 0) return;
            int layer = collision.collider.gameObject.layer;
            Debug.Log($"[RB][Collision STAY] '{collision.collider.name}' " +
                      $"layer={layer} ({LayerMask.LayerToName(layer)}) " +
                      $"grounded={_isGroundedCached} vertVel={_verticalVelocity:F3} pos={transform.position}");
        }

        // ── OnValidate: warn about misconfigured layer masks ──────────────────
        private void OnValidate()
        {
            if (groundDetectionLayer.value == 0)
                Debug.LogWarning("[RigidbodyPredictedMovement] groundDetectionLayer is set to 'Nothing' — " +
                                 "IsGrounded() will ALWAYS return false! Set it to 'Everything' or include " +
                                 "the layer(s) your terrain/floor uses.", this);
        }

        #endregion

    }
}