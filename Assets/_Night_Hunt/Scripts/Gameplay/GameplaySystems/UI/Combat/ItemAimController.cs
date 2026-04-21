using UnityEngine;
using UnityEngine.EventSystems;
using NightHunt.GameplaySystems.Core.Interfaces;
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
        [Tooltip("Minimum joystick magnitude [0\u20131] for a drag-release to be treated as a confirm throw.")]
        [SerializeField] private float _dragThreshold = 0.25f;

        // ─────────────────────────────────────────────────────────────────────
        //  Runtime refs
        // ─────────────────────────────────────────────────────────────────────

        private IPlayerStatSystem   _statSystem;
        private IItemSelectionSystem _itemSelectionSystem;
        private Transform           _playerTransform;
        private Camera              _cam;
        private IAimSystem          _aimSystem;
        private IItemUseSystem      _itemUseSystem;
        private CombatInputHandler _combatInputHandler;

        // ─────────────────────────────────────────────────────────────────────
        //  Aim state
        // ─────────────────────────────────────────────────────────────────────

        private bool    _inAimMode;
        private string  _activeItemInstanceId;

        // ─────────────────────────────────────────────────────────────────────
        //  Static output (read by ThrowableHandler)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Last confirmed world-space throw target position.</summary>
        public static Vector3 AimWorldTarget { get; private set; }

        /// <summary>Normalised world-space aim direction from the player.</summary>
        public static Vector3 AimDirection   { get; private set; }

        /// <summary>True while the controller is in aim mode (for ThrowableHandler / input guards).</summary>
        public static bool    IsAimingPC     { get; private set; }

        /// <summary>
        /// Called by <see cref="CombatInputHandler"/> during the fire-hold throwable path
        /// (armed via FilterPanel, not via TryBeginAim). Keeps the visual aim cursor in sync
        /// with the already-clamped ground hit point so both paths show the same target.
        /// No-op when TryBeginAim is active (IsAimingPC=true) since that path owns its raycast.
        /// </summary>
        public static void SetExternalAimTarget(Vector3 worldPos)
        {
            if (IsAimingPC) return;  // TryBeginAim path is authoritative
            AimWorldTarget = worldPos;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Properties
        // ─────────────────────────────────────────────────────────────────────

        private bool IsMobile => _forceMobileMode || Application.isMobilePlatform;

        /// <summary>
        /// True while the controller is in aim mode (waiting for confirm/cancel).
        /// Used by item-selection buttons to decide whether to start a hold-timer or joystick.
        /// </summary>
        public bool IsInAimMode => _inAimMode;

        // ─────────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
#if UNITY_SERVER
            // DS build: ItemAimController is client-only UI logic.
            // Disable immediately so Update() never runs on the headless server.
            enabled = false;
            return;
#endif
            _cam = Camera.main;
            HideCursor();
        }

        private void Update()
        {
            if (!_inAimMode || IsMobile) return;

            UpdatePCRaycast();

            // Confirm on left-click (NOT over a UI element so item button taps work).
            if (UnityEngine.Input.GetMouseButtonDown(0) &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                ConfirmAim();
                return;
            }

            // Cancel on right-click or Escape.
            if (UnityEngine.Input.GetMouseButtonDown(1) ||
                UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                CancelAim();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API – initialization
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Bind to the local player's systems.  Call when the local player spawns /
        /// changes (mirrors the pattern in <c>UIRootController</c>).
        /// </summary>
        public void Initialize(
            IPlayerStatSystem  statSystem,
            IItemSelectionSystem itemSelectionSystem,
            Transform          playerTransform,
            IAimSystem         aimSystem            = null,
            IItemUseSystem     itemUseSystem        = null,
            CombatInputHandler combatInputHandler   = null)
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
        /// If the item in <paramref name="slotIndex"/> is a <c>Throwable</c>, enters aim mode.
        /// Otherwise falls back to direct item selection/use.
        /// </summary>
        public void TryBeginAim(string instanceID)
        {
            if (_itemSelectionSystem == null || string.IsNullOrEmpty(instanceID)) return;

            var bridge = SpectateManager.Instance?.GetCurrentPlayer()?.GamePlaySystemBridge;
            var item = bridge?.GetItemByInstanceID(instanceID);
            if (item == null) return;

            var def = ItemDatabase.GetDefinition(item.DefinitionID);
            bool isThrowable = def != null && def.Type == ItemType.Throwable;

            if (!isThrowable)
            {
                // Direct use – no aiming needed. Use ServerRpc so it works on any connection.
                Debug.Log($"[ItemAimController] TryBeginAim: non-throwable '{instanceID}' → RequestSelectItem + RequestUseSelectedItem");
                _itemSelectionSystem.RequestSelectItem(instanceID);
                _itemSelectionSystem.RequestUseSelectedItem();
                return;
            }

            // ── Enter aim mode ──────────────────────────────────────────────
            if (_inAimMode) CancelAim();   // cancel previous aim if any

            _activeItemInstanceId = instanceID;
            _inAimMode  = true;
            IsAimingPC  = !IsMobile;
            // Force STRAFE mode so the character rotates to face the throw direction
            // while the player is in aim mode (mirrors BeginFire behaviour).
            _combatInputHandler?.SetCameraLockOverride(active: true, forcedValue: true);
            float range = GetThrowRange();
            if (_rangeIndicator != null)
                _rangeIndicator.ShowWithRange(range);
            // FIX: Show the AimSystem world cursor on mobile when entering throwable aim.
            // On PC the cursor is always visible (AimSystem.Initialize enables it);
            // on mobile it is only shown by BeginFire/EndFire — we must do the same here.
            if (IsMobile)
                _aimSystem?.SetCursorVisible(true);
            // Select + arm the item via ServerRpc — safe to call on any connection.
            // RequestSelectItem selects without arming; RequestUseSelectedItem arms (BeginThrowable).
            // Both are no-ops if the item is already in that state.
            Debug.Log($"[ItemAimController] TryBeginAim: RequestSelectItem + RequestUseSelectedItem for '{instanceID}'");
            _itemSelectionSystem.RequestSelectItem(instanceID);
            _itemSelectionSystem.RequestUseSelectedItem();
            if (!IsMobile)
            {
                // PC: immediately do a raycast so the ring is ready on first frame.
                UpdatePCRaycast();
            }
        }

        /// <summary>
        /// Called by the selection button IDragHandler during a mobile drag.
        /// <paramref name="joystickDir"/> is <see cref="VariableJoystick.Direction"/> — already
        /// normalised [0,1] joystick space; no further screen-space conversion needed.
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

                // FIX: Convert raw joystick → camera-relative world XZ before passing to AimSystem.
                // AimSystem.ResolveThrowableAim maps input as (x→worldX, y→worldZ) with NO camera rotation.
                // CombatInputHandler.SetFireMobileJoystick (via FireButton) already sends camera-relative;
                // ItemAimController must match that convention.
                Vector2 camRelJoystick = joystickDir;  // fallback = raw (camera == null edge case)
                if (_cam != null)
                {
                    Vector3 cr = _cam.transform.right;   cr.y = 0f; cr.Normalize();
                    Vector3 cf = _cam.transform.forward; cf.y = 0f; cf.Normalize();
                    Vector3 w  = cr * joystickDir.x + cf * joystickDir.y;
                    camRelJoystick = new Vector2(w.x, w.z);
                }
                _aimSystem?.SetThrowableAim(camRelJoystick);

                // Drive character rotation to face throw direction, same as FireButton mobile drag.
                // FIX: pass camRelJoystick (camera-relative world XZ) — NOT raw joystickDir.
                // SetFireMobileJoystick does new Vector3(x, 0, y) = world direction; raw joystick
                // Y is screen-up which without camera rotation maps inverted when camera faces -Z.
                _combatInputHandler?.SetFireMobileJoystick(camRelJoystick, active: true);
            }
        }

        /// <summary>
        /// Called by the selection button IEndDragHandler when the finger lifts.
        /// <paramref name="joystickMagnitude"/> is the final <see cref="VariableJoystick.Direction"/>
        /// magnitude [0,1] at the moment of release.
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
        /// Defensive version of <see cref="OnMobileDragEnd"/> — only executes if aim mode
        /// is still active. Called from OnEndDrag to prevent double-resolve when
        /// OnPointerUp has already handled the event first.
        /// </summary>
        public void OnMobileDragEndIfStillActive(float joystickMagnitude)
        {
            // _inAimMode is reset to false if OnPointerUp already called OnMobileDragEnd first.
            // This guard prevents a double Confirm/Cancel.
            if (!_inAimMode) return;
            OnMobileDragEnd(joystickMagnitude);
        }        // ─────────────────────────────────────────────────────────────────────
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

            // Clamp to VisionRange radius.
            if (offset.magnitude > range)
                hit = _playerTransform.position + offset.normalized * range;

            AimWorldTarget = hit;
            AimDirection   = (hit - _playerTransform.position).normalized;
            MoveCursor(hit);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Confirm / Cancel
        // ─────────────────────────────────────────────────────────────────────

        private void ConfirmAim()
        {
            if (!_inAimMode) return;
            Vector3 throwTarget = AimWorldTarget;  // capture before ResetAimState clears _inAimMode
            ResetAimState();

            // Selection was already started in TryBeginAim (server started BeginThrowable).
            // We only need to request the actual throw execution now.
            if (_itemUseSystem != null)
                _itemUseSystem.RequestExecuteThrow(throwTarget);
        }

        /// <summary>
        /// Cancel aim mode and abort the in-progress item use on the server.
        /// Called by PC right-click/Escape, mobile under-threshold release, or the
        /// shared Cancel HUD button.
        /// </summary>
        public void CancelAim()
        {
            // If currently aiming a throwable, clear aim visuals and controls.
            if (_inAimMode)
                ResetAimState();

            // Always request server-side cancel so shared Cancel button works for both:
            // - Throwable hold/aim state
            // - Consumable channeling state
            // RequestCancelUse is a no-op when no item is active.
            _itemUseSystem?.RequestCancelUse();
        }

        private void ResetAimState()
        {
            _inAimMode  = false;
            IsAimingPC  = false;
            _activeItemInstanceId = null;

            if (_rangeIndicator != null) _rangeIndicator.Hide();
            HideCursor();
            // Exit throwable mode in AimSystem so it reverts to normal mouse aim.
            _aimSystem?.SetThrowableAim(Vector2.zero);
            // FIX: Hide AimSystem world cursor on mobile (was shown in TryBeginAim).
            // On PC the cursor is always visible — do not hide it here.
            if (IsMobile)
                _aimSystem?.SetCursorVisible(false);
            // Clear mobile aim drive and restore movement lock state.
            _combatInputHandler?.SetFireMobileJoystick(Vector2.zero, false);
            _combatInputHandler?.SetCameraLockOverride(active: false, forcedValue: false);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        private float GetVisionRange()
        {
            if (_statSystem == null) return 10f;                 // safe fallback
            float v = _statSystem.GetStat(PlayerStatType.VisionRange);
            return v > 0f ? v : 10f;
        }

        /// <summary>
        /// Returns the effective aim clamp radius for the item currently in <see cref="_activeSlot"/>.
        /// For throwables: physics-derived max throw distance from <see cref="ThrowableDefinition.GetMaxThrowDistance()">,
        /// so the aim ring exactly matches what the ballistic arc can reach.
        /// Falls back to VisionRange for non-throwables or when no item is loaded.
        /// </summary>
        private float GetThrowRange()
        {
            if (string.IsNullOrEmpty(_activeItemInstanceId))
                return GetVisionRange();

            var bridge = SpectateManager.Instance?.GetCurrentPlayer()?.GamePlaySystemBridge;
            var item = bridge?.GetItemByInstanceID(_activeItemInstanceId);
            if (item == null) return GetVisionRange();

            var def = ItemDatabase.GetDefinition(item.DefinitionID) as NightHunt.GameplaySystems.Core.Data.ThrowableDefinition;
            if (def == null) return GetVisionRange();

            float throwRange = def.GetMaxThrowDistance();
            float visionRange = GetVisionRange();
            // Clamp to VisionRange: the aim ring must not extend beyond what is visible
            // (prevents a second larger ring appearing outside the FOW visibility circle).
            return throwRange > 0.1f ? Mathf.Min(throwRange, visionRange) : visionRange;
        }

        /// <summary>
        /// Convert [0,1] joystick value to camera-relative world XZ offset scaled by range.
        /// </summary>
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
    }
}
