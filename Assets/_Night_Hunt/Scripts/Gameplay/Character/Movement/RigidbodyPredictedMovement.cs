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
        // maxFallSpeed moved to MovementSettings SO (field: maxFallSpeed).

        [Header("Ground Check Tuning")]
        [Tooltip(
            "Tỉ lệ thu nhỏ sphere check ground so với capsule.radius.\n" +
            "< 1.0 = sphere nhỏ hơn capsule → không chạm tường khi đứng sát.\n" +
            "Khuyến nghị: 0.70–0.85. Nếu miss ground trên địa hình gồ ghề, tăng lên.")]
        [SerializeField] [Range(0.4f, 1.0f)] private float groundCheckRadiusFactor = 0.75f;

        [Header("Physics Tuning")]
        // Keep drag at 0 when using MovePosition — direct position control does NOT need
        // velocity damping, and a non-zero linearDamping fights PhysX's internally
        // computed velocity (from position delta), causing client/server divergence.
        [SerializeField] private float drag = 0f;
        [SerializeField] private float angularDrag = 0.05f;

        [Header("[DIAG] Bật để debug mystery move")]
        [Tooltip("Log chi tiết IsGrounded mỗi 0.5 giây: groundLayer, CheckSphere radius, hịt gì.")]
        [SerializeField] private bool diagGrounding = false;
        [Tooltip("Log chi tiết ApplyMovement mỗi tick khi không có horizontal input.")]
        [SerializeField] private bool diagApply = false;

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
        private Vector3 _groundNormal = Vector3.up;
        private Vector3 _lastAppliedMovement;

        // Physics query buffers — INSTANCE (not static) to avoid inter-player data corruption.
        private readonly Collider[]  _overlapBuffer  = new Collider[16];
        private readonly RaycastHit[] _raycastBuffer  = new RaycastHit[8];

        private bool _isNonOwnerKinematic;

        // ── Editor gizmo cache ────────────────────────────────────────────────
        // Populated by IsGrounded() every tick so OnDrawGizmos always shows the
        // exact same values that were used for the most recent ground check.
        private Vector3  _dbg_bottomSphereCenter;
        private float    _dbg_checkRadius;         // new (fixed) radius
        private float    _dbg_legacyCheckRadius;   // old radius (capsule.radius + groundCheckDistance)
        private Collider _dbg_groundHitCollider;   // last surface hit (null = airborne)

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

            // ── ALWAYS KINEMATIC ──────────────────────────────────────────────────
            //
            // Root cause của oscillation/drift bug:
            //   Non-kinematic Rigidbody bị PhysX depenetration khi capsule overlap
            //   với wall/doorframe geometry. PhysX đẩy capsule theo wall normal (có
            //   XZ component) → character drift/oscillate dù không có input.
            //
            // Tại sao kinematic là ĐÚNG cho character controller:
            //   - MovePosition trên kinematic = exact, deterministic, no forces
            //   - PhysX KHÔNG apply depenetration/contact/friction forces vào kinematic body
            //   - Client và server produce IDENTICAL positions → reconcile không fire
            //   - IsBlockedHorizontally (CapsuleCast) phát hiện wall trước MovePosition
            //     → character không bị embedded vào wall ngay từ đầu
            //
            // Non-owner vẫn được phân biệt bởi _isNonOwnerKinematic flag để skip
            // ApplyMovement (non-owners interpolate toward server-reconciled position).
            _isNonOwnerKinematic = !base.Owner.IsLocalClient && !IsServerStarted;

            _rigidbody.isKinematic  = true;  // ALWAYS — owner + server + non-owner
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate; // Sub-step visual smoothing

            if (enableDebugLogs)
                Debug.Log($"[RigidbodyPredictedMovement] isOwner={base.Owner.IsLocalClient} " +
                          $"isServer={IsServerStarted} → kinematic=true nonOwnerMode={_isNonOwnerKinematic}");
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

            // ── Bottom sphere center ──────────────────────────────────────────────
            // Dịch xuống thêm groundCheckDistance để bù lại radius bị thu nhỏ,
            // đảm bảo vẫn detect mặt đất ngay khi vừa đáp.
            Vector3 bottomSphereCenter = capsuleWorldCenter
                                         - Vector3.up * (_capsule.height * 0.5f - _capsule.radius)
                                         - Vector3.up * groundCheckDistance;

            // ── Check radius (FIX) ────────────────────────────────────────────────
            // Nhỏ hơn capsule.radius → sphere không phình sang ngang → không chạm tường.
            // groundCheckRadiusFactor ∈ [0.4, 1.0], khuyến nghị 0.70–0.85.
            float checkRadius = _capsule.radius * groundCheckRadiusFactor;

            // ── Cache cho Gizmos (editor only) ───────────────────────────────────
            _dbg_bottomSphereCenter = bottomSphereCenter;
            _dbg_checkRadius        = checkRadius;
            _dbg_legacyCheckRadius  = _capsule.radius + groundCheckDistance; // giá trị cũ để so sánh

            // ── Grounded detection: OverlapSphereNonAlloc + self-exclusion ────────
            //
            // Tại sao KHÔNG dùng CheckSphere:
            //   CheckSphere với groundLayer=-1 (ALL LAYERS) sẽ detect CHÍNH capsule
            //   của player → grounded=true dù đang nhảy!
            //
            // Tại sao KHÔNG đổi groundLayer (bỏ Player layer):
            //   Ta muốn player có thể đứng TRÊN player khác.
            //
            // Giải pháp: OverlapSphereNonAlloc → lặp kết quả → skip own colliders.
            //   - groundLayer=-1 (ALL LAYERS) → vẫn detect player khác ✓
            //   - Own capsule bị bỏ qua → không false-positive ✓
            int hitCount = Physics.OverlapSphereNonAlloc(
                bottomSphereCenter,
                checkRadius,
                _overlapBuffer,
                groundLayer,
                QueryTriggerInteraction.Ignore
            );

            _isGroundedCached        = false;
            _dbg_groundHitCollider   = null;

            for (int i = 0; i < hitCount; i++)
            {
                // Bỏ qua bất kỳ collider nào thuộc hierarchy của chính mình.
                // IsChildOf(root) cover cả chính root lẫn các child objects.
                if (_overlapBuffer[i].transform.IsChildOf(transform.root))
                    continue;

                _isGroundedCached      = true;
                _dbg_groundHitCollider = _overlapBuffer[i];
                break;
            }

            // ── [DIAG] GROUNDING INFO ────────────────────────────────────────
            if (diagGrounding)
            {
                Debug.Log(
                    $"[DIAG][GROUND] grounded={_isGroundedCached} " +
                    $"groundLayerMask={groundLayer.value} overlaps={hitCount} " +
                    $"surface='{(_dbg_groundHitCollider != null ? _dbg_groundHitCollider.name : "none")}' " +
                    $"checkRadius={checkRadius:F3} (factor={groundCheckRadiusFactor:F2}) " +
                    $"legacyRadius={_dbg_legacyCheckRadius:F3} " +
                    $"sphereBot={bottomSphereCenter:F2}");
            }

            // ── Ground normal: RaycastAll + self-exclusion ────────────────────────
            // RaycastAll vì Raycast() chỉ trả about 1 hit — nếu hit own capsule trước
            // sẽ miss ground. RaycastAll cho phép filter self rồi lấy ground normal.
            if (_isGroundedCached)
            {
                Vector3 rayOrigin = bottomSphereCenter + Vector3.up * 0.05f;
                float rayDist = checkRadius + groundCheckDistance + 0.1f;
                int rayCount = Physics.RaycastNonAlloc(
                    rayOrigin, Vector3.down, _raycastBuffer, rayDist, groundLayer, QueryTriggerInteraction.Ignore);

                float bestDist = float.MaxValue;
                RaycastHit bestHit = default;
                bool bestFound = false;
                for (int ri = 0; ri < rayCount; ri++)
                {
                    RaycastHit h = _raycastBuffer[ri];
                    if (h.collider.transform.IsChildOf(transform.root)) continue;
                    if (h.distance < bestDist) { bestDist = h.distance; bestHit = h; bestFound = true; }
                }

                if (bestFound)
                {
                    _groundNormal = bestHit.normal;

                    if (diagGrounding)
                    {
                        float angle = Vector3.Angle(Vector3.up, _groundNormal);
                        if (angle > 0.5f)
                            Debug.Log(
                                $"[DIAG][SLOPE] slopeAngle={angle:F1}° " +
                                $"normal=({_groundNormal.x:F3},{_groundNormal.y:F3},{_groundNormal.z:F3}) " +
                                $"surface='{bestHit.collider.name}' layer={LayerMask.LayerToName(bestHit.collider.gameObject.layer)}");
                    }
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

            // XZ tách biệt để xử lý wall-cast và slope projection.
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
                // bề mặt thay vì đâm vào sườn dốc.
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
            //     → normal có component XZ → trượt dù không có input!
            //   • Airborne:  tích tụ gravity mỗi tick (âm = fall, dương = jump)
            // bool groundedIdle = _isGroundedCached && horizontal.sqrMagnitude <= 0.0001f && movement.y <= 0f;
            // float vertDisp = groundedIdle ? 0f : movement.y * dt;

            bool groundedIdle = _isGroundedCached && movement.y <= 0f;
            float vertDisp = groundedIdle ? 0f : movement.y * dt;

            if (diagApply)
            {
                float sa = Vector3.Angle(Vector3.up, _groundNormal);
                Debug.Log($"[DIAG][APPLY] grounded={_isGroundedCached} idle={groundedIdle} " +
                          $"h=({horizontal.x:F3},{horizontal.z:F3}) vertDisp={vertDisp:F4} " +
                          $"step={stepUpY:F3} slope={sa:F1}°");
            }

            _lastAppliedMovement = new Vector3(horizontal.x, movement.y, horizontal.z);

            _rigidbody.MovePosition(_rigidbody.position
                + horizontal   * dt
                + Vector3.up   * vertDisp
                + Vector3.up   * stepUpY);

            _rigidbody.linearVelocity  = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;

            // Always freeze only rotation to prevent capsule tumbling.
            // Never freeze position — that blocks MovePosition on non-kinematic RB.
            _rigidbody.constraints = RigidbodyConstraints.FreezeRotationX
                                   | RigidbodyConstraints.FreezeRotationZ;

            if (enableDebugLogs && (horizontal.magnitude > 0.001f || !_isGroundedCached || stepUpY > 0.001f))
                Debug.Log($"[Move] grounded={_isGroundedCached} vv={_verticalVelocity:F2} " +
                          $"h=({horizontal.x:F2},{horizontal.z:F2}) step={stepUpY:F3}");
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
            _rigidbody.position        = transform.position;
            _rigidbody.rotation        = transform.rotation;
            if (!_isNonOwnerKinematic)
            {
                _rigidbody.linearVelocity  = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }
            // _verticalVelocity intentionally NOT reset here —
            // base class already sets it from server snapshot before calling this.
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

#if UNITY_EDITOR
            DrawGroundCheckGizmos();
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only: visualise the ground-check sphere and compare it with the
        /// old (over-sized) sphere that was causing false wall hits.
        ///
        /// Colour legend:
        ///   Green  solid   = new sphere, grounded
        ///   Red    solid   = new sphere, airborne
        ///   Yellow dashed  = old sphere (capsule.radius + groundCheckDistance) — what we removed
        ///   White  label   = live stats printed next to the character
        /// </summary>
        private void DrawGroundCheckGizmos()
        {
            // ── New check sphere ──────────────────────────────────────────────────
            Gizmos.color = _isGroundedCached
                ? new Color(0.1f, 0.9f, 0.2f, 0.25f)   // green fill (grounded)
                : new Color(0.9f, 0.1f, 0.1f, 0.20f);  // red fill (airborne)
            Gizmos.DrawSphere(_dbg_bottomSphereCenter, _dbg_checkRadius);

            Gizmos.color = _isGroundedCached
                ? new Color(0.1f, 0.9f, 0.2f, 0.85f)
                : new Color(0.9f, 0.1f, 0.1f, 0.85f);
            Gizmos.DrawWireSphere(_dbg_bottomSphereCenter, _dbg_checkRadius);

            // ── Old (legacy) check sphere — shown in yellow so you can see how much
            //    bigger it was and verify it no longer intersects the wall.
            Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.15f);
            Gizmos.DrawSphere(_dbg_bottomSphereCenter, _dbg_legacyCheckRadius);
            Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.60f);
            Gizmos.DrawWireSphere(_dbg_bottomSphereCenter, _dbg_legacyCheckRadius);

            // ── Hit surface dot ───────────────────────────────────────────────────
            if (_dbg_groundHitCollider != null)
            {
                Gizmos.color = new Color(0.1f, 1f, 0.4f, 1f);
                // Draw a small marker at the collider's closest point to the sphere center.
                Vector3 closest = _dbg_groundHitCollider.ClosestPoint(_dbg_bottomSphereCenter);
                Gizmos.DrawSphere(closest, 0.04f);
                Gizmos.DrawLine(_dbg_bottomSphereCenter, closest);
            }

            // ── Ground normal arrow ───────────────────────────────────────────────
            if (_isGroundedCached)
            {
                Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
                Vector3 normalBase = _dbg_bottomSphereCenter;
                Gizmos.DrawRay(normalBase, _groundNormal * 0.5f);
            }

            // ── Handles labels (scene view only, shows live numbers) ─────────────
            UnityEditor.Handles.color = Color.white;
            Vector3 labelPos = transform.position + Vector3.up * (_capsule.height + 0.3f);

            string groundedStr = _isGroundedCached
                ? $"<color=#22ee44>GROUNDED</color>  ({_dbg_groundHitCollider?.name ?? "?"})"
                : "<color=#ee4444>AIRBORNE</color>";

            string slopeStr = _isGroundedCached
                ? $"slope {Vector3.Angle(Vector3.up, _groundNormal):F1}°"
                : "";

            UnityEditor.Handles.Label(labelPos,
                $"── Ground Check ──\n" +
                $"{groundedStr}\n" +
                $"new  radius : {_dbg_checkRadius:F3} m  (factor {groundCheckRadiusFactor:F2})\n" +
                $"old  radius : {_dbg_legacyCheckRadius:F3} m  (legacy)\n" +
                $"sphere.y    : {_dbg_bottomSphereCenter.y:F3}\n" +
                $"{slopeStr}");
        }
#endif

        protected override void OnGUI()
        {
            base.OnGUI();

            if (!IsOwner || !Application.isPlaying || !enableDebugLogs) return;

            // Additional Rigidbody-specific debug info
            GUI.color = Color.yellow;
            GUILayout.BeginArea(new Rect(520, 10, 300, 200));

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

#if UNITY_EDITOR
            // ── Ground check live readout (editor only) ───────────────────────
            GUILayout.Space(6);
            GUI.color = _isGroundedCached ? Color.green : Color.red;
            GUILayout.Label("=== GROUND CHECK ===");
            GUI.color = Color.white;
            GUILayout.Label($"Grounded : {_isGroundedCached}");
            GUILayout.Label($"Surface  : {(_dbg_groundHitCollider != null ? _dbg_groundHitCollider.name : "—")}");
            GUILayout.Label($"New R    : {_dbg_checkRadius:F3}  (×{groundCheckRadiusFactor:F2})");
            GUILayout.Label($"Old R    : {_dbg_legacyCheckRadius:F3}  (legacy)");
            GUILayout.Label($"Slope    : {Vector3.Angle(Vector3.up, _groundNormal):F1}°");
#endif

            GUILayout.EndArea();
        }

        #endregion
    }
}