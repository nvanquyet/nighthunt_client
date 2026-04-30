using System.Collections.Generic;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Configs;

namespace NightHunt.Gameplay.Character.Combat
{
    /// <summary>
    /// Scene-level singleton that maintains a live registry of all <see cref="IBulletTarget"/>
    /// objects and resolves the best acquisition candidate on each shot.
    ///
    /// ──────────────────────────────────────────────────────────────────────────
    /// WHY — The Problem with Pure Raycasting on Multi-Level Maps
    /// ──────────────────────────────────────────────────────────────────────────
    /// A top-down / 45° camera maps one screen pixel to an infinite 3-D ray. On
    /// a flat map, projecting that ray onto Y = 0 is sufficient. On a multi-level
    /// map it is ambiguous: the same cursor position can refer to the Ground floor,
    /// a Platform, or a Roof at three different Y values — and targets standing under
    /// a roof cannot be reached by a downward camera ray at all.
    ///
    /// This registry sidesteps the ambiguity by tracking each target's real 3-D
    /// position and comparing it to the fire direction using a cone angle (3-D).
    /// The AimSystem direction can remain purely horizontal; the angle tolerance
    /// naturally absorbs elevation differences.
    ///
    /// ──────────────────────────────────────────────────────────────────────────
    /// QUERY ALGORITHM (FindBestTarget)
    /// ──────────────────────────────────────────────────────────────────────────
    ///   For every registered IBulletTarget:
    ///     1. Skip if not IsAcquirable.
    ///     2. Skip if 3-D distance > maxRange.
    ///     3. Skip if 3-D angle(fireDir, toTarget) > config.MaxAcquireAngleDegrees.
    ///     4. Skip if line-of-sight blocked (optional, config.RequireLineOfSight).
    ///   Sort survivors by:
    ///     1. config.PriorityOrder  (Character before Beacon before …)
    ///     2. Tiebreaker  (best angle / closest / best score, config.TiebreakerMode)
    ///   Return the winner, or null → caller falls back to Physics.Raycast.
    ///
    /// ──────────────────────────────────────────────────────────────────────────
    /// SETUP
    /// ──────────────────────────────────────────────────────────────────────────
    ///   Place ONE BulletTargetRegistry component anywhere in the match scene.
    ///   BulletTargetMarker (or any IBulletTarget implementor) calls
    ///   BulletTargetRegistry.Register / Unregister from OnEnable / OnDisable.
    /// </summary>
    public sealed class BulletTargetRegistry : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────

        public static BulletTargetRegistry Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[BulletTargetRegistry] Duplicate instance destroyed.");
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                _registered.Clear();
                Instance = null;
            }
        }

        // ── Registry ──────────────────────────────────────────────────────────

        private static readonly HashSet<IBulletTarget> _registered = new HashSet<IBulletTarget>();

        /// <summary>Register a target. Called automatically by <see cref="BulletTargetMarker.OnEnable"/>.</summary>
        public static void Register(IBulletTarget target)
        {
            if (target != null) _registered.Add(target);
        }

        /// <summary>Unregister a target. Called automatically by <see cref="BulletTargetMarker.OnDisable"/>.</summary>
        public static void Unregister(IBulletTarget target)
        {
            _registered.Remove(target);
        }

        /// <summary>Number of currently registered targets (diagnostic / debug).</summary>
        public static int RegisteredCount => _registered.Count;

        // ── Query ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Result returned by <see cref="FindBestTarget"/>.
        /// </summary>
        public readonly struct AcquisitionResult
        {
            /// <summary>Winning candidate, or null if no target was acquired.</summary>
            public readonly IBulletTarget Target;

            /// <summary>
            /// World-space point to use as the bullet endpoint / damage origin.
            /// Only meaningful when <see cref="Target"/> is not null.
            /// </summary>
            public readonly Vector3 HitPoint;

            /// <summary>Angle (degrees) between the fire direction and the direction to the target.</summary>
            public readonly float AngleDeg;

            /// <summary>3-D distance from the muzzle to the target's acquire point.</summary>
            public readonly float Distance;

            public bool HasTarget => Target != null;

            public AcquisitionResult(IBulletTarget target, Vector3 hitPoint, float angleDeg, float distance)
            {
                Target   = target;
                HitPoint = hitPoint;
                AngleDeg = angleDeg;
                Distance = distance;
            }

            public static readonly AcquisitionResult None = default;
        }

        // Pre-allocated list — reused per query to avoid per-shot allocations.
        private static readonly List<AcquisitionResult> _candidates = new List<AcquisitionResult>(32);

        /// <summary>
        /// Find the best acquirable target from <paramref name="muzzleOrigin"/> in
        /// <paramref name="fireDirection"/> within <paramref name="maxRange"/>.
        ///
        /// The shooter's own <see cref="IBulletTarget"/> (if any) should be excluded by the
        /// layer-mask or by temporarily unregistering → pass <paramref name="shooterTarget"/>
        /// to skip it without unregistering.
        /// </summary>
        /// <param name="muzzleOrigin">Fire origin (muzzle tip, world space).</param>
        /// <param name="fireDirection">Normalised fire direction (may be horizontal / Y=0 from AimSystem).</param>
        /// <param name="maxRange">Maximum 3-D distance to consider.</param>
        /// <param name="config">Acquisition parameters. Required.</param>
        /// <param name="shooterTarget">Optional. This target is skipped (self-hit prevention).</param>
        public static AcquisitionResult FindBestTarget(
            Vector3           muzzleOrigin,
            Vector3           fireDirection,
            float             maxRange,
            BulletTargetConfig config,
            IBulletTarget     shooterTarget = null)
        {
            if (config == null)
            {
                Debug.LogError("[BulletTargetRegistry] BulletTargetConfig is null — returning no target.");
                return AcquisitionResult.None;
            }

            _candidates.Clear();

            Vector3 normFireDir = fireDirection.sqrMagnitude > 0.001f
                ? fireDirection.normalized
                : Vector3.forward;

            float maxAngleSq = config.MaxAcquireAngleDegrees; // avoid sqrt in hot loop where possible

            foreach (IBulletTarget t in _registered)
            {
                // ── Guards ────────────────────────────────────────────────────

                if (t == null)             continue; // stale entry
                if (!t.IsAcquirable)       continue;
                if (ReferenceEquals(t, shooterTarget)) continue;

                // ── Distance check (3-D) ──────────────────────────────────────

                Vector3 acquirePoint = ResolveAcquirePoint(t, config);
                Vector3 toTarget = acquirePoint - muzzleOrigin;
                float   dist     = toTarget.magnitude;
                if (dist < 0.01f || dist > maxRange) continue;

                // ── Cone angle check (3-D — handles elevation naturally) ───────

                float angle = Vector3.Angle(normFireDir, toTarget / dist); // /dist = normalised
                if (angle > maxAngleSq) continue;

                // ── Line-of-sight check ───────────────────────────────────────

                if (config.RequireLineOfSight && IsLoSBlocked(muzzleOrigin, acquirePoint, dist, config))
                    continue;

                _candidates.Add(new AcquisitionResult(t, acquirePoint, angle, dist));
            }

            if (_candidates.Count == 0) return AcquisitionResult.None;

            // ── Sort ──────────────────────────────────────────────────────────

            _candidates.Sort((a, b) =>
            {
                int priA = config.GetPriorityIndex(a.Target.TargetType);
                int priB = config.GetPriorityIndex(b.Target.TargetType);

                if (priA != priB) return priA.CompareTo(priB);  // lower index = higher priority

                // Tiebreaker
                switch (config.TiebreakerMode)
                {
                    case TiebreakerMode.Closest:
                        return a.Distance.CompareTo(b.Distance);

                    case TiebreakerMode.BestScore:
                        float scoreA = a.AngleDeg * a.Distance;
                        float scoreB = b.AngleDeg * b.Distance;
                        return scoreA.CompareTo(scoreB);

                    default: // BestAngle
                        return a.AngleDeg.CompareTo(b.AngleDeg);
                }
            });

            AcquisitionResult winner = _candidates[0];

            // ── Debug logging ─────────────────────────────────────────────────

            if (config.LogAcquisitionResult)
            {
                Debug.Log($"[BulletTargetRegistry] Acquired: {winner.Target.TargetType} " +
                          $"| angle={winner.AngleDeg:F1}° | dist={winner.Distance:F1}m " +
                          $"| candidates={_candidates.Count}");
            }

            return winner;
        }

        // ── Line-of-Sight Helper ──────────────────────────────────────────────

        private static bool IsLoSBlocked(Vector3 from, Vector3 to, float dist, BulletTargetConfig config)
        {
            Vector3 dir = (to - from).normalized;

            if (config.UseSphereCastForLoS)
            {
                return Physics.SphereCast(from, config.LoSSphereRadius, dir, out _,
                    dist - 0.05f, config.LoSBlockLayers, QueryTriggerInteraction.Ignore);
            }

            return Physics.Raycast(from, dir, dist - 0.05f,
                config.LoSBlockLayers, QueryTriggerInteraction.Ignore);
        }

        private static Vector3 ResolveAcquirePoint(IBulletTarget target, BulletTargetConfig config)
        {
            Vector3 point = target.AcquirePoint;
            if (config == null || Mathf.Approximately(config.TargetCentreYOffset, 0f))
                return point;

            return point + Vector3.up * config.TargetCentreYOffset;
        }

        // ── Debug Gizmos ──────────────────────────────────────────────────────

#if UNITY_EDITOR
        [Header("Editor Debug")]
        [SerializeField] private bool _drawAllRegistered = false;

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || !_drawAllRegistered) return;

            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.5f);
            foreach (IBulletTarget t in _registered)
            {
                if (t == null) continue;
                Gizmos.DrawWireSphere(t.AcquirePoint, t.AcquireRadius);
            }
        }
#endif
    }
}
