using UnityEngine;
using UnityEngine.EventSystems;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.Input.Handlers.Combat;
using NightHunt.Gameplay.Spectator;

namespace NightHunt.GameplaySystems.UI.Combat
{
    /// <summary>
    /// Orchestrates the aim flow for throwable quick-slot items.
    ///
    /// <para><b>PC Mode:</b>
    ///   Entering aim mode shows the <see cref="RangeIndicator"/> ring and an optional
    ///   world-space cursor at the current mouse ground position.
    ///   Left-click → confirm throw.  Right-click or Escape → cancel.</para>
    ///
    /// <para><b>Mobile Mode (joystick drag style, similar to LoL Wild Rift):</b>
    ///   Drag begins at the quick-slot button's screen centre.  Dragging in any direction
    ///   computes a world-space aim direction.  Releasing beyond the threshold confirms the
    ///   throw; releasing inside the threshold cancels it.</para>
    ///
    /// <para>After confirmation the static properties
    ///   <see cref="AimWorldTarget"/> and <see cref="AimDirection"/>
    ///   are set so <c>ThrowableHandler</c> can read them.</para>
    ///
    /// Inspector / runtime setup:
    ///   Assign <see cref="RangeIndicator"/> and (optionally) <c>_aimCursor</c> world transform.
    ///   Call <see cref="Initialize"/> when the local player spawns.
    /// </summary>
    public class ItemAimController : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────────────────────────────

        [Header("Platform Override")]
        [Tooltip("Force mobile-style drag joystick even in the editor for testing.")]
        [SerializeField] private bool _forceMobileMode = false;

        [Header("World Indicators")]
        [Tooltip("RangeIndicator GO in the scene — shows VisionRange ring while aiming a throwable.")]
        [SerializeField] private RangeIndicator _rangeIndicator;

        [Tooltip("Optional world-space dot / crosshair shown at the aim target position.")]
        [SerializeField] private Transform _aimCursor;

        [Header("Mobile Drag Threshold")]
        [Tooltip("Minimum joystick magnitude [0–1] for a drag-release to be treated as a confirm throw.")]
        [SerializeField] private float _dragThreshold = 0.25f;

        // ─────────────────────────────────────────────────────────────────────
        //  Runtime refs
        // ─────────────────────────────────────────────────────────────────────

        private IPlayerStatSystem    _statSystem;
        private IItemSelectionSystem _itemSelectionSystem;
        private Transform            _playerTransform;
        private Camera               _cam;
        private IAimSystem           _aimSystem;
        private IItemUseSystem       _itemUseSystem;
        private CombatInputHandler   _combatInputHandler;

        // ─────────────────────────────────────────────────────────────────────
        //  Aim state
        // ─────────────────────────────────────────────────────────────────────

        private bool   _inAimMode;
        private bool   _inDeployMode;       // separate from throwable aim mode
        private string _activeItemInstanceId;

        // ─────────────────────────────────────────────────────────────────────
        //  Static output (read by ThrowableHandler)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Last confirmed world-space throw target position.</summary>
        public static Vector3 AimWorldTarget { get; private set; }

        /// <summary>Normalised world-space aim direction from the player.</summary>
        public static Vector3 AimDirection   { get; private set; }

        /// <summary>True while the controller is in throwable aim mode.</summary>
        public static bool IsAimingPC    { get; private set; }

        /// <summary>True while waiting for the player to confirm a deployable placement.</summary>
        public static bool IsDeployingPC { get; private set; }

        /// <summary>
        /// Called by <see cref="CombatInputHandler"/> during the fire-hold throwable path.
        /// Keeps the visual aim cursor in sync with the already-clamped ground hit point.
        /// No-op when TryBeginAim is active (IsAimingPC=true).
        /// </summary>
        public static void SetExternalAimTarget(Vector3 worldPos)
        {
            if (IsAimingPC) return;
            AimWorldTarget = worldPos;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Properties
        // ─────────────────────────────────────────────────────────────────────

        private bool IsMobile => _forceMobileMode || Application.isMobilePlatform;

        /// <summary>True while the controller is in throwable aim mode.</summary>
        public bool IsInAimMode => _inAimMode;

        // ─────────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
#if UNITY_SERVER
            enabled = false;
            return;
#endif
            _cam = Camera.main;
            HideCursor();
        }

        private void Update()
        {
            // ── Throwable PC aim ──────────────────────────────────────────────────
            if (_inAimMode && !IsMobile)
            {
                UpdatePCRaycast();

                if (UnityEngine.Input.GetMouseButtonDown(0) &&
                    (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
                {
                    ConfirmAim();
                    return;
                }

                if (UnityEngine.Input.GetMouseButtonDown(1) ||
                    UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                {
                    CancelAim();
                }
                return;
            }

            // ── Deployable PC placement ───────────────────────────────────────────
            // Release-to-place: PointerDown on item button starts deploy mode (preview visible).
            // Dragging moves the preview. Releasing the mouse button (PointerUp) confirms placement.
            // Right-click or Escape cancels at any time.
            if (_inDeployMode && !IsMobile)
            {
                // Confirm on mouse RELEASE (not press) — this allows drag-to-place:
                // hold LMB down → drag to position → release → place.
                if (UnityEngine.Input.GetMouseButtonUp(0) &&
                    (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
                {
                    ConfirmDeploy();
                    return;
                }

                if (UnityEngine.Input.GetMouseButtonDown(1) ||
                    UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                {
                    CancelDeploy();
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API – initialization
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Bind to the local player's systems. Call when the local player spawns / changes.
        /// </summary>
        public void Initialize(
            IPlayerStatSystem    statSystem,
            IItemSelectionSystem itemSelectionSystem,
            Transform            playerTransform,
            IAimSystem           aimSystem           = null,
            IItemUseSystem       itemUseSystem       = null,
            CombatInputHandler   combatInputHandler  = null)
        {
            _statSystem          = statSystem;
            _itemSelectionSystem = itemSelectionSystem;
            _playerTransform     = playerTransform;
            _aimSystem           = aimSystem;
            _itemUseSystem       = itemUseSystem;
            _combatInputHandler  = combatInputHandler;

            if (_rangeIndicator != null)
                _rangeIndicator.SetFollowTarget(playerTransform);

            if (_cam == null)
                _cam = Camera.main;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API – called by selection buttons
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Called by the selection button when an item is pressed.
        ///
        /// Deployable  → enters deploy-aim mode (click-to-place).
        /// Throwable   → enters throwable aim mode (joystick/mouse).
        /// Consumable  → direct use (no aiming needed).
        /// </summary>
        public void TryBeginAim(string instanceID)
        {
            if (_itemSelectionSystem == null || string.IsNullOrEmpty(instanceID)) return;

            var bridge = SpectateManager.Instance?.GetCurrentPlayer()?.GamePlaySystemBridge;
            var item   = bridge?.GetItemByInstanceID(instanceID);
            if (item == null) return;

            var  def          = ItemDatabase.GetDefinition(item.DefinitionID);
            bool isThrowable  = def != null && def.Type == ItemType.Throwable;
            bool isDeployable = def != null && def.Type == ItemType.Deployable;

            // ── Deployable: enter deploy mode ──────────────────────────────────
            if (isDeployable)
            {
                if (_inDeployMode) CancelDeploy();
                if (_inAimMode)    CancelAim();

                _activeItemInstanceId = instanceID;
                _inDeployMode = true;
                IsDeployingPC = !IsMobile;
                _combatInputHandler?.SetCameraLockOverride(active: true, forcedValue: true);
                _aimSystem?.SetCursorVisible(true);

                Debug.Log($"[ItemAimController] TryBeginAim: deployable '{instanceID}' → deploy mode");
                _itemSelectionSystem.RequestSelectItem(instanceID);
                _itemSelectionSystem.RequestUseSelectedItem();
                return;
            }

            // ── Consumable: direct use ─────────────────────────────────────────
            if (!isThrowable)
            {
                Debug.Log($"[ItemAimController] TryBeginAim: consumable '{instanceID}' → direct use");
                _itemSelectionSystem.RequestSelectItem(instanceID);
                _itemSelectionSystem.RequestUseSelectedItem();
                return;
            }

            // ── Throwable: enter throwable aim mode ────────────────────────────
            if (_inAimMode)    CancelAim();
            if (_inDeployMode) CancelDeploy();

            _activeItemInstanceId = instanceID;
            _inAimMode = true;
            IsAimingPC = !IsMobile;
            _combatInputHandler?.SetCameraLockOverride(active: true, forcedValue: true);

            float range = GetThrowRange();
            if (_rangeIndicator != null)
                _rangeIndicator.ShowWithRange(range);
            if (IsMobile)
                _aimSystem?.SetCursorVisible(true);

            Debug.Log($"[ItemAimController] TryBeginAim: throwable '{instanceID}' mobile={IsMobile} range={range:F2}");
            LogThrowable($"TryBeginAim start item={instanceID} mobile={IsMobile} range={range:F2} cursor={AimWorldTarget:F2}");
            _itemSelectionSystem.RequestSelectItem(instanceID);
            _itemSelectionSystem.RequestUseSelectedItem();
            if (!IsMobile)
                UpdatePCRaycast();
        }

        /// <summary>
        /// Called by the selection button IDragHandler during a mobile drag.
        /// </summary>
        public void OnMobileDrag(Vector2 joystickDir)
        {
            if (!_inAimMode || !IsMobile) return;

            float   range    = GetThrowRange();
            Vector3 worldDir = Joystick01ToWorldDir(joystickDir, range);

            if (_playerTransform != null)
            {
                AimWorldTarget = _playerTransform.position + worldDir;
                AimDirection   = worldDir.magnitude > 0.001f ? worldDir.normalized : Vector3.forward;
                MoveCursor(AimWorldTarget);

                Vector2 camRelJoystick = joystickDir;
                if (_cam != null)
                {
                    Vector3 cr = _cam.transform.right;   cr.y = 0f; cr.Normalize();
                    Vector3 cf = _cam.transform.forward; cf.y = 0f; cf.Normalize();
                    Vector3 w  = cr * joystickDir.x + cf * joystickDir.y;
                    camRelJoystick = new Vector2(w.x, w.z);
                }
                _aimSystem?.SetThrowableAim(camRelJoystick);
                _combatInputHandler?.SetFireMobileJoystick(camRelJoystick, active: true);
            }
        }

        /// <summary>
        /// Called by the selection button IEndDragHandler when the finger lifts.
        /// </summary>
        public void OnMobileDragEnd(float joystickMagnitude)
        {
            if (!_inAimMode || !IsMobile) return;

            if (joystickMagnitude >= _dragThreshold)
                ConfirmAim();
            else
                CancelAim();
        }

        /// <summary>
        /// Defensive version of <see cref="OnMobileDragEnd"/> — no-op if aim mode is inactive.
        /// </summary>
        public void OnMobileDragEndIfStillActive(float joystickMagnitude)
        {
            if (!_inAimMode) return;
            OnMobileDragEnd(joystickMagnitude);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PC raycast
        // ─────────────────────────────────────────────────────────────────────

        private void UpdatePCRaycast()
        {
            if (_cam == null || _playerTransform == null) return;

            Ray   ray   = _cam.ScreenPointToRay(UnityEngine.Input.mousePosition);
            Plane plane = new Plane(Vector3.up, _playerTransform.position);

            if (!plane.Raycast(ray, out float dist)) return;

            Vector3 hit    = ray.GetPoint(dist);
            float   range  = GetThrowRange();
            Vector3 offset = hit - _playerTransform.position;

            if (offset.magnitude > range)
                hit = _playerTransform.position + offset.normalized * range;

            AimWorldTarget = hit;
            AimDirection   = (hit - _playerTransform.position).normalized;
            MoveCursor(hit);

            Vector3 flatDir = new Vector3(AimDirection.x, 0f, AimDirection.z);
            if (flatDir.sqrMagnitude > 0.001f)
                _playerTransform.rotation = Quaternion.LookRotation(flatDir.normalized, Vector3.up);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Confirm / Cancel — Throwable
        // ─────────────────────────────────────────────────────────────────────

        private void ConfirmAim()
        {
            if (!_inAimMode) return;
            Vector3 throwTarget = ResolveConfirmedThrowTarget();
            LogThrowable($"ConfirmAim item={_activeItemInstanceId ?? "null"} target={throwTarget:F2}");
            ResetAimState();
            _itemUseSystem?.RequestExecuteThrow(throwTarget);
        }

        private Vector3 ResolveConfirmedThrowTarget()
        {
            if (_aimSystem != null)
            {
                Vector3 aimGround = _aimSystem.FinalAimGroundPos;
                if (aimGround.sqrMagnitude > 0.0001f)
                    return aimGround;
            }
            return AimWorldTarget;
        }

        /// <summary>
        /// Cancel throwable aim mode and abort the in-progress item use on the server.
        /// Also serves as the shared Cancel HUD button handler.
        /// </summary>
        public void CancelAim()
        {
            if (_inAimMode)
                ResetAimState();

            _itemUseSystem?.RequestCancelUse();
        }

        private void ResetAimState()
        {
            _inAimMode  = false;
            IsAimingPC  = false;
            _activeItemInstanceId = null;

            if (_rangeIndicator != null) _rangeIndicator.Hide();
            HideCursor();
            _aimSystem?.SetThrowableAim(Vector2.zero);
            if (IsMobile)
                _aimSystem?.SetCursorVisible(false);
            _combatInputHandler?.SetFireMobileJoystick(Vector2.zero, false);
            _combatInputHandler?.SetCameraLockOverride(active: false, forcedValue: false);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Confirm / Cancel — Deployable
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Confirm deployable placement at the current aim cursor position (PC left-click).
        /// </summary>
        private void ConfirmDeploy()
        {
            if (!_inDeployMode) return;
            LogDeploy($"ConfirmDeploy item={_activeItemInstanceId ?? "null"}");
            ResetDeployState();
            _itemUseSystem?.TryConfirmDeploy();
        }

        /// <summary>
        /// Cancel deploy mode and abort the in-progress placement on the server.
        /// </summary>
        public void CancelDeploy()
        {
            if (_inDeployMode)
                ResetDeployState();
            _itemUseSystem?.RequestCancelUse();
        }

        private void ResetDeployState()
        {
            _inDeployMode = false;
            IsDeployingPC = false;
            _activeItemInstanceId = null;

            HideCursor();
            if (IsMobile)
                _aimSystem?.SetCursorVisible(false);
            _combatInputHandler?.SetCameraLockOverride(active: false, forcedValue: false);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        private float GetVisionRange()
        {
            if (_statSystem == null) return 10f;
            float v = _statSystem.GetStat(PlayerStatType.VisionRange);
            return v > 0f ? v : 10f;
        }

        /// <summary>
        /// Effective aim clamp radius for the active throwable item.
        /// Uses physics-derived max throw distance, clamped to VisionRange.
        /// </summary>
        private float GetThrowRange()
        {
            if (string.IsNullOrEmpty(_activeItemInstanceId))
                return GetVisionRange();

            var bridge = SpectateManager.Instance?.GetCurrentPlayer()?.GamePlaySystemBridge;
            var item   = bridge?.GetItemByInstanceID(_activeItemInstanceId);
            if (item == null) return GetVisionRange();

            var def = ItemDatabase.GetDefinition(item.DefinitionID) as ThrowableDefinition;
            if (def == null) return GetVisionRange();

            float throwRange  = def.GetMaxThrowDistance();
            float visionRange = GetVisionRange();
            return throwRange > 0.1f ? Mathf.Min(throwRange, visionRange) : visionRange;
        }

        private Vector3 Joystick01ToWorldDir(Vector2 joystick01, float range)
        {
            if (_cam == null) return Vector3.zero;
            Vector3 camRight   = _cam.transform.right;   camRight.y   = 0; camRight.Normalize();
            Vector3 camForward = _cam.transform.forward; camForward.y = 0; camForward.Normalize();
            return (camRight * joystick01.x + camForward * joystick01.y) * range;
        }

        private void MoveCursor(Vector3 worldPos)
        {
            if (_aimCursor == null) return;
            _aimCursor.gameObject.SetActive(true);
            _aimCursor.position = worldPos + Vector3.up * 0.1f;
        }

        private void HideCursor()
        {
            if (_aimCursor != null)
                _aimCursor.gameObject.SetActive(false);
        }

        private static bool ThrowableDebugEnabled()
        {
            var cfg = NightHuntDebugConfig.Instance;
            return cfg != null && cfg.EnableThrowableDebugLogs;
        }

        private static bool DeployDebugEnabled()
        {
            var cfg = NightHuntDebugConfig.Instance;
            return cfg != null && cfg.EnableDeployableDebugLogs;
        }

        private static void LogThrowable(string message)
        {
            if (ThrowableDebugEnabled())
                Debug.Log($"[THROW_FLOW] {message}");
        }

        private static void LogDeploy(string message)
        {
            if (DeployDebugEnabled())
                Debug.Log($"[DEPLOY_FLOW] {message}");
        }
    }
}
