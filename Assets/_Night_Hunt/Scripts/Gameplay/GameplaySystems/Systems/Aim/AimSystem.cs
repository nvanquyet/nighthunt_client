using UnityEngine;
using NightHunt.Core;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.Diagnostics;

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
    ///   Place this component anywhere in the scene (not on the player prefab).
    ///   Call Initialize(playerRoot, statSystem) from NetworkPlayer after the
    ///   local player spawns — it auto-wires itself to the correct player.
    ///   DO NOT assign _playerStatSystemMB in the Inspector; that field has been
    ///   removed because PlayerStatSystem is a NetworkBehaviour spawned at runtime
    ///   and cannot be pre-assigned to a scene object.
    /// </summary>
    public class AimSystem : MonoBehaviour, IAimSystem
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────────────────────────────

        [Header("References")]
        [Tooltip("Root transform used as the aim origin (usually the player root). " +
                 "Leave null — Initialize() assigns it from the spawned player.")]
        [SerializeField] private Transform _playerRoot;

        [Tooltip("Camera used for screen-to-world raycasts. Defaults to Camera.main.")]
        [SerializeField] private UnityEngine.Camera _camera;

        [Header("Ground Plane")]
        [Tooltip("World-space Y coordinate of the aiming plane.")]
        [SerializeField] private float _groundHeight = 0f;

        [Tooltip("Layer mask for optional physics-based ground raycast. 0 or Everything falls back to NightHuntLayers.MaskGroundAim.")]
        [SerializeField] private LayerMask _groundLayerMask = 0;

        [Header("Clamp Settings")]
        [Tooltip("Minimum distance from the player that the aim point can be placed.")]
        [SerializeField] private float _minAimRadius = 0.5f;

        [Tooltip("Fallback VisionRange used when the stat system is unavailable.")]
        [SerializeField] private float _fallbackVisionRange = 15f;

        [Header("World Cursor")]
        [Tooltip("Optional world-space Transform (flat disc, decal, or ring) that is repositioned to FinalAimPos every frame.")]
        [SerializeField] private Transform _worldAimCursor;

        [Tooltip("Y-lift above _groundHeight applied to the visual cursor to avoid z-fighting with the floor mesh. " +
                 "Does NOT affect FinalAimPos (gameplay logic uses the unlifted value).")]
        [SerializeField] private float _cursorYOffset = 0.02f;

        public enum CursorFacingAxis { X, Y, Z }
        [Tooltip("Which local axis of the cursor mesh is rotated to face toward the player.\n" +
                 "Y  → mesh forward on XZ plane (default for 3D arrow/sprite standing upright)\n" +
                 "X  → rotates around X axis (rare)\n" +
                 "Z  → mesh lying flat with Z pointing toward player")]
        [SerializeField] private CursorFacingAxis _cursorFacingAxis = CursorFacingAxis.Y;

        // ─────────────────────────────────────────────────────────────────────
        //  Runtime State
        // ─────────────────────────────────────────────────────────────────────

        private IPlayerStatSystem _playerStats;

        // Cursor facing — capture initial X/Z euler so we only ever change Y at runtime
        private Vector3 _cursorInitialEuler;

        // Throwable override
        private bool    _isThrowableMode;
        private Vector2 _throwableJoystick;
        private bool    _cursorVisible;

        // ─────────────────────────────────────────────────────────────────────
        //  IAimSystem Implementation
        // ─────────────────────────────────────────────────────────────────────

        public Vector3 FinalAimDir   { get; private set; }
        public Vector3 FinalAimPos   { get; private set; }
        public Vector3 FinalAimGroundPos { get; private set; }
        public Vector3 AimWorldPoint { get; private set; }
        public bool    IsThrowableMode => _isThrowableMode;

        // ─────────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
#if UNITY_SERVER
            // DS build: AimSystem is client-only (camera raycast / world cursor).
            // Disable immediately so no Update() logic runs on the headless server.
            enabled = false;
            return;
#endif
            // NOTE: IPlayerStatSystem is NOT resolved here. AimSystem is a scene object;
            // PlayerStatSystem is a NetworkBehaviour on the player prefab spawned at runtime.
            // _playerStats is set via Initialize(playerRoot, statSystem) called by NetworkPlayer.

            if (_camera == null)
                _camera = UnityEngine.Camera.main;

            if (_playerRoot == null)
                _playerRoot = transform;

            // Save the mesh's designed tilt (X, Z) so we only rotate Y at runtime.
            if (_worldAimCursor != null)
            {
                _cursorInitialEuler = _worldAimCursor.eulerAngles;
                _worldAimCursor.gameObject.SetActive(false);
                _cursorVisible = false;
            }
        }

        /// <inheritdoc/>
        public void Initialize(Transform playerRoot, IPlayerStatSystem statSystem)
        {
            _playerRoot  = playerRoot;

            // Prefer the explicitly passed stat system.
            // Fallback: auto-find on the player's hierarchy (handles edge cases where
            // NetworkPlayer passes null because ComponentResolver failed).
            _playerStats = statSystem
                ?? playerRoot?.GetComponentInChildren<IPlayerStatSystem>()
                ?? playerRoot?.GetComponent<IPlayerStatSystem>();

            if (_playerStats == null)
                Debug.LogWarning("[AimSystem] IPlayerStatSystem not found — VisionRange will use fallback value.");
            else
                Debug.Log($"[AimSystem] Bound to stat system: {_playerStats.GetType().Name}");

            if (_camera == null)
                _camera = UnityEngine.Camera.main;

            SetCursorVisible(false);
        }

        /// <inheritdoc/>
        public void SetCursorVisible(bool visible)
        {
            bool changed = _cursorVisible != visible ||
                           (_worldAimCursor != null && _worldAimCursor.gameObject.activeSelf != visible);

            if (_worldAimCursor != null)
                _worldAimCursor.gameObject.SetActive(visible);

            _cursorVisible = visible;

            if (changed)
            {
                PhaseTestLog.Log(
                    PhaseTestLogCategory.Input,
                    "AimCursorVisibility",
                    $"visible={visible} cursor={(_worldAimCursor != null ? _worldAimCursor.name : "null")} throwableMode={_isThrowableMode}",
                    this);
            }
        }

        /// <inheritdoc/>
        public float GetVisionRange()
        {
            if (_playerStats == null) return _fallbackVisionRange;
            float range = _playerStats.GetStat(PlayerStatType.VisionRange);
            // GetStat returns 0 when _statCache not yet populated (client waiting for sync).
            return range > 0f ? range : _fallbackVisionRange;
        }

        private void Update()
        {
            if (_playerRoot == null) return; // guard: _playerRoot destroyed (player despawned)
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

            LayerMask groundMask = ResolveGroundLayerMask();
            if (groundMask.value != 0 &&
                Physics.Raycast(ray, out RaycastHit hit, 200f, groundMask, QueryTriggerInteraction.Ignore))
                return hit.point;

            // Infinite ground plane fallback
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, _groundHeight, 0f));
            if (groundPlane.Raycast(ray, out float dist))
                return ray.GetPoint(dist);

            return _playerRoot != null ? _playerRoot.position : Vector3.zero; // last resort
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
            if (_playerRoot == null) return;
            Vector3 origin    = _playerRoot.position;
            Vector3 toTarget  = worldPoint - origin;
            toTarget.y        = 0f; // enforce horizontal plane

            float dist        = toTarget.magnitude;
            float visionRange = GetVisionRange();

            // Clamp to [minRadius, visionRange]
            dist = Mathf.Clamp(dist, _minAimRadius, visionRange);

            FinalAimDir = dist > 0.001f ? toTarget.normalized : transform.forward;
            FinalAimDir = new Vector3(FinalAimDir.x, 0f, FinalAimDir.z).normalized;

            FinalAimPos = origin + FinalAimDir * dist;
            FinalAimPos = new Vector3(FinalAimPos.x, origin.y, FinalAimPos.z);

            // Keep the visual cursor on the configured flat aim plane.
            // Physics projection can hit vehicle/wall tops and make AimTargetMesh jump upward.
            FinalAimGroundPos = new Vector3(FinalAimPos.x, _groundHeight, FinalAimPos.z);

            // Reposition the world cursor and rotate it to face the player.
            // Only the configured axis angle is changed; the other two keep their designer-set values.
            if (_worldAimCursor != null)
            {
                // FinalAimPos.y intentionally stays at origin.y for weapon/ability game logic.
                Vector3 cursorPos = FinalAimGroundPos + Vector3.up * _cursorYOffset;
                _worldAimCursor.position = cursorPos;
                if (FinalAimDir.sqrMagnitude > 0.001f)
                {
                    // angle in degrees that points FROM cursor TOWARD player on the horizontal plane
                    float yaw = Mathf.Atan2(-FinalAimDir.x, -FinalAimDir.z) * Mathf.Rad2Deg;
                    Vector3 e = _cursorInitialEuler;
                    switch (_cursorFacingAxis)
                    {
                        case CursorFacingAxis.Y: e.y = yaw; break;
                        case CursorFacingAxis.X: e.x = yaw; break;
                        case CursorFacingAxis.Z: e.z = yaw; break;
                    }
                    _worldAimCursor.rotation = Quaternion.Euler(e);
                }
            }
        }

        private Vector3 ProjectAimPointToGround(Vector3 target)
        {
            LayerMask groundMask = ResolveGroundLayerMask();
            if (groundMask.value != 0)
            {
                Vector3 rayOrigin = target + Vector3.up * 8f;
                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 32f, groundMask, QueryTriggerInteraction.Ignore))
                    return hit.point;
            }

            return new Vector3(target.x, _groundHeight, target.z);
        }

        private LayerMask ResolveGroundLayerMask()
        {
            int value = _groundLayerMask.value;
            if (value == 0 || value == ~0)
                return NightHuntLayers.MaskGroundAim;

            int nonGroundMask = LayerMask.GetMask(
                NightHuntLayers.Player,
                NightHuntLayers.PlayerHitBox,
                NightHuntLayers.Projectile,
                NightHuntLayers.Throwable,
                NightHuntLayers.Interactable,
                NightHuntLayers.Items,
                NightHuntLayers.Zone);

            int sanitized = value & ~nonGroundMask;
            if (sanitized == 0)
                sanitized = NightHuntLayers.MaskGroundAim.value;

            LayerMask mask = default;
            mask.value = sanitized;
            return mask;
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
