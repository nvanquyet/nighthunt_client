using UnityEngine;
using NightHunt.StatSystem.Core.Types;
using NightHunt.StatSystem.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Interfaces;

namespace NightHunt.GameplaySystems.Aim
{
    /// <summary>
    /// Top-down aim resolver for a single local player.
    ///
    /// Gun / Melee mode (default):
    ///   Casts a ray from the main camera through the mouse position onto a
    ///   horizontal ground plane.  The hit point is clamped so the aim range
    ///   never exceeds the player's VisionRange stat and never falls below
    ///   MinAimRadius (prevents "aim at feet" artefacts).
    ///
    /// Throwable mode:
    ///   Activated by SetThrowableAim(joystick).  The joystick vector is scaled
    ///   by VisionRange and added to the player's position directly.  Call
    ///   SetThrowableAim(Vector2.zero) to return to mouse-aim.
    ///
    /// WIRING:
    ///   Attach to the local player GameObject.
    ///   Assign _playerStatSystemMB (PlayerStatSystem on the same object).
    ///   _camera defaults to Camera.main if left null.
    /// </summary>
    public class AimSystem : MonoBehaviour, IAimSystem
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────────────────────────────

        [Header("References")]
        [Tooltip("Root transform used as the aim origin (usually the player root).")]
        [SerializeField] private Transform _playerRoot;

        [Tooltip("Camera used for screen-to-world raycasts. Defaults to Camera.main.")]
        [SerializeField] private UnityEngine.Camera _camera;

        [Tooltip("Implements IPlayerStatSystem – used to read VisionRange.")]
        [SerializeField] private MonoBehaviour _playerStatSystemMB;

        [Header("Ground Plane")]
        [Tooltip("World-space Y coordinate of the aiming plane.")]
        [SerializeField] private float _groundHeight = 0f;

        [Tooltip("Layer mask for optional physics-based ground raycast. Leave at 0 to use the infinite plane fallback.")]
        [SerializeField] private LayerMask _groundLayerMask = 0;

        [Header("Clamp Settings")]
        [Tooltip("Minimum distance from the player that the aim point can be placed.")]
        [SerializeField] private float _minAimRadius = 0.5f;

        [Tooltip("Fallback VisionRange used when the stat system is unavailable.")]
        [SerializeField] private float _fallbackVisionRange = 15f;

        [Header("World Cursor")]
        [Tooltip("Optional world-space Transform (flat disc, decal, or ring) that is repositioned to FinalAimPos every frame. "
               + "Mirrors PR\u2019s AimTargetVisual pattern. Assign a prefab instance in the scene.")]
        [SerializeField] private Transform _worldAimCursor;

        // ─────────────────────────────────────────────────────────────────────
        //  Runtime State
        // ─────────────────────────────────────────────────────────────────────

        private IPlayerStatSystem _playerStats;

        // Throwable override
        private bool    _isThrowableMode;
        private Vector2 _throwableJoystick;

        // ─────────────────────────────────────────────────────────────────────
        //  IAimSystem Implementation
        // ─────────────────────────────────────────────────────────────────────

        public Vector3 FinalAimDir   { get; private set; }
        public Vector3 FinalAimPos   { get; private set; }
        public Vector3 AimWorldPoint { get; private set; }
        public bool    IsThrowableMode => _isThrowableMode;

        // ─────────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _playerStats = _playerStatSystemMB as IPlayerStatSystem;

            if (_camera == null)
                _camera = UnityEngine.Camera.main;

            if (_playerRoot == null)
                _playerRoot = transform;
        }

        private void Update()
        {
            if (_isThrowableMode)
                ResolveThrowableAim();
            else
                ResolveMouseAim();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  IAimSystem — Throwable Override
        // ─────────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public void SetThrowableAim(Vector2 joystickInput)
        {
            if (joystickInput.sqrMagnitude < 0.001f)
            {
                _isThrowableMode   = false;
                _throwableJoystick = Vector2.zero;
            }
            else
            {
                _isThrowableMode   = true;
                _throwableJoystick = joystickInput;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Aim Resolution — Mouse
        // ─────────────────────────────────────────────────────────────────────

        private void ResolveMouseAim()
        {
            if (_camera == null) return;

            Vector3 rawHit = GetMouseGroundPoint();
            AimWorldPoint  = rawHit;

            ApplyToTarget(rawHit);
        }

        /// <summary>
        /// Casts from the camera through the mouse cursor onto the ground plane.
        /// Falls back to the infinite horizontal plane if no physics hit.
        /// </summary>
        private Vector3 GetMouseGroundPoint()
        {
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);

            // Optional physics raycast (only if a layer mask is set)
            if (_groundLayerMask.value != 0)
            {
                if (Physics.Raycast(ray, out RaycastHit hit, 200f, _groundLayerMask))
                    return hit.point;
            }

            // Infinite ground plane fallback
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, _groundHeight, 0f));
            if (groundPlane.Raycast(ray, out float dist))
                return ray.GetPoint(dist);

            return _playerRoot.position; // last resort
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Aim Resolution — Throwable
        // ─────────────────────────────────────────────────────────────────────

        private void ResolveThrowableAim()
        {
            float visionRange = GetVisionRange();

            // Joystick already normalised (or close to it) by caller
            Vector3 dir = new Vector3(_throwableJoystick.x, 0f, _throwableJoystick.y);
            float   mag = dir.magnitude;

            if (mag > 1f) dir /= mag; // normalise if over-extended

            Vector3 rawHit = _playerRoot.position + dir * visionRange;
            AimWorldPoint  = rawHit;

            ApplyToTarget(rawHit);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Shared Clamp + Output
        // ─────────────────────────────────────────────────────────────────────

        private void ApplyToTarget(Vector3 worldPoint)
        {
            Vector3 origin    = _playerRoot.position;
            Vector3 toTarget  = worldPoint - origin;
            toTarget.y        = 0f; // enforce horizontal plane

            float dist        = toTarget.magnitude;
            float visionRange = GetVisionRange();

            // Clamp to [minRadius, visionRange]
            dist = Mathf.Clamp(dist, _minAimRadius, visionRange);

            FinalAimDir = dist > 0.001f ? toTarget.normalized : transform.forward;
            FinalAimDir = new Vector3(FinalAimDir.x, 0f, FinalAimDir.z).normalized;

            FinalAimPos  = origin + FinalAimDir * dist;
            FinalAimPos  = new Vector3(FinalAimPos.x, origin.y, FinalAimPos.z);

            // Reposition the world cursor (mirrors PR’s AimTargetVisual moving to AimFinalPos).
            if (_worldAimCursor != null)
                _worldAimCursor.position = FinalAimPos;
        }

        private float GetVisionRange()
        {
            if (_playerStats != null)
                return _playerStats.GetStat(PlayerStatType.VisionRange);

            return _fallbackVisionRange;
        }

#if UNITY_EDITOR
        // ─────────────────────────────────────────────────────────────────────
        //  Debug Gizmos
        // ─────────────────────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

            Vector3 origin = _playerRoot != null ? _playerRoot.position : transform.position;

            // Vision range circle
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
            DrawWireCircle(origin, GetVisionRange(), 32);

            // Aim line
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(origin, FinalAimPos);
            Gizmos.DrawSphere(FinalAimPos, 0.15f);

            // Min-radius circle
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.4f);
            DrawWireCircle(origin, _minAimRadius, 16);
        }

        private static void DrawWireCircle(Vector3 center, float radius, int segments)
        {
            float step = 360f / segments;
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * step * Mathf.Deg2Rad;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
#endif
    }
}
