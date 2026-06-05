using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using NightHunt.Config;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Gameplay.StatSystem.Core.Interfaces;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.Input.Handlers.Combat;
using NightHunt.Gameplay.Input.Core;
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
    public class ThrowableAimController : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────────────────────────────

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
        private IInventorySystem     _inventorySystem;

        // ─────────────────────────────────────────────────────────────────────
        //  Aim state
        // ─────────────────────────────────────────────────────────────────────

        private bool   _inAimMode;
        private bool   _inDeployMode;       // separate from throwable aim mode
        private string _activeItemInstanceId;
        private bool   _deployAwaitingServerUse;
        private bool   _deployReleaseQueued;
        private bool   _deployPointerWasHeld;
        private bool   _suppressNextDeployRelease;
        private bool   _deployConfirmInProgress;

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
            AimDirection = Vector3.zero;
        }

        public static void ClearExternalAimTarget()
        {
            AimWorldTarget = Vector3.zero;
            AimDirection = Vector3.zero;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Properties
        // ─────────────────────────────────────────────────────────────────────

        private bool IsMobile =>
            PlatformInputDetector.Instance != null
                ? PlatformInputDetector.Instance.IsMobile
                : Application.isMobilePlatform;

        /// <summary>True while the controller is in throwable aim mode.</summary>
        public bool IsInAimMode => _inAimMode;

        /// <summary>True while the controller is waiting for deploy placement confirm/cancel.</summary>
        public bool IsInDeployMode => _inDeployMode;

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

        private void Start()
        {
        #if !UNITY_SERVER
            var uiInput = FindFirstObjectByType<NightHunt.Gameplay.Input.Handlers.UI.UIInputHandler>(FindObjectsInactive.Include);
            if (uiInput != null)
            {
                uiInput.OnCancelPressed += HandleCancelInput;
            }
        #endif
        }

        private void OnDestroy()
        {
        #if !UNITY_SERVER
            if (_itemUseSystem != null)
            {
                _itemUseSystem.OnItemUseStarted -= HandleItemUseStarted;
                _itemUseSystem.OnItemUseCompleted -= HandleItemUseEnded;
                _itemUseSystem.OnItemUseCancelled -= HandleItemUseEnded;
            }

            var uiInput = FindFirstObjectByType<NightHunt.Gameplay.Input.Handlers.UI.UIInputHandler>(FindObjectsInactive.Include);
            if (uiInput != null)
                uiInput.OnCancelPressed -= HandleCancelInput;
        #endif
        }

        private void HandleCancelInput()
        {
            if (_inAimMode) CancelAim();
            if (_inDeployMode) CancelDeploy();
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

                // Removed manual Escape check (handled by HandleCancelInput via UIInputHandler)
                if (UnityEngine.Input.GetMouseButtonDown(1))
                {
                    CancelAim();
                }
                return;
            }

            // ── Deployable Mobile: track any active world touch → update aim ─────
            if (_inDeployMode && IsMobile)
                UpdateMobileDeployAim();

            // ── Deployable PC placement ───────────────────────────────────────────
            if (_inDeployMode)
            {
                if (PrimaryPointerReleasedThisFrame())
                {
                    if (_deployConfirmInProgress)
                        return;

                    if (_suppressNextDeployRelease)
                    {
                        IgnoreDeployRelease(IsMobile ? "mobileInitialUiReleaseSuppressed" : "initialUiReleaseSuppressed");
                        return;
                    }

                    if (IsPrimaryPointerOverUI())
                    {
                        IgnoreDeployRelease(IsMobile ? "mobilePointerUpOverUI" : "pointerUpOverUI");
                        return;
                    }

                    TryConfirmDeployRelease("pointerUp");
                    return;
                }

                // Removed manual Escape check
                if (UnityEngine.Input.GetMouseButtonDown(1))
                {
                    CancelDeploy();
                    return;
                }

                bool pointerHeld = IsPrimaryPointerHeld();
                // ...
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
            CombatInputHandler   combatInputHandler  = null,
            IInventorySystem     inventorySystem     = null)
        {
            _statSystem          = statSystem;
            _itemSelectionSystem = itemSelectionSystem;
            _playerTransform     = playerTransform;
            _aimSystem           = aimSystem;
            _combatInputHandler  = combatInputHandler;
            _inventorySystem     = inventorySystem;

            if (_itemUseSystem != null)
            {
                _itemUseSystem.OnItemUseStarted -= HandleItemUseStarted;
                _itemUseSystem.OnItemUseCompleted -= HandleItemUseEnded;
                _itemUseSystem.OnItemUseCancelled -= HandleItemUseEnded;
            }

            _itemUseSystem = itemUseSystem;

            if (_itemUseSystem != null)
            {
                _itemUseSystem.OnItemUseStarted += HandleItemUseStarted;
                _itemUseSystem.OnItemUseCompleted += HandleItemUseEnded;
                _itemUseSystem.OnItemUseCancelled += HandleItemUseEnded;
            }

            if (_rangeIndicator != null)
                _rangeIndicator.SetFollowTarget(playerTransform);

            if (_cam == null)
                _cam = Camera.main;

            ClearExternalAimTarget();
            Debug.Log($"[NH_FLOW][01][ThrowableAimController.Initialize] controller={name} stat={(statSystem != null ? "ok" : "null")} selection={(itemSelectionSystem != null ? "ok" : "null")} player={(playerTransform != null ? playerTransform.name : "null")} aim={(aimSystem != null ? "ok" : "null")} itemUse={(itemUseSystem != null ? "ok" : "null")} combat={(combatInputHandler != null ? "ok" : "null")} inventory={(inventorySystem != null ? "ok" : "null")}");
        }

        private void HandleItemUseStarted(ItemInstance item)
        {
            if (item == null) return;
            var def = ItemDatabase.GetDefinition(item.DefinitionID);
            if (def == null) return;

            if (def.Type == ItemType.Deployable)
            {
                bool releaseQueued = _deployReleaseQueued;
                bool pointerWasHeld = _deployPointerWasHeld;
                // Preserve the current aim position so a queued release targets the right spot.
                Vector3 savedAimTarget = AimWorldTarget;
                if (_inDeployMode) ResetDeployState();
                if (_inAimMode)    ResetAimState();

                _activeItemInstanceId = item.InstanceID;
                _inDeployMode = true;
                _deployAwaitingServerUse = _itemUseSystem?.IsDeploying != true;
                _deployReleaseQueued = releaseQueued;
                _deployPointerWasHeld = pointerWasHeld || IsPrimaryPointerHeld();
                // Only suppress the release if the pointer is still held; if the finger has
                // already lifted off the button we do not want to swallow the very next tap.
                _suppressNextDeployRelease = IsPrimaryPointerHeld();
                _deployConfirmInProgress = false;
                IsDeployingPC = !IsMobile;
                _combatInputHandler?.SetCameraLockOverride(active: true, forcedValue: true);
                _aimSystem?.SetCursorVisible(true);
                // Restore the aim position (cleared by ResetDeployState) and seed the cursor.
                if (savedAimTarget.sqrMagnitude > 0.001f)
                {
                    AimWorldTarget = savedAimTarget;
                    MoveCursor(AimWorldTarget);
                }
                else if (IsMobile && _playerTransform != null)
                {
                    AimWorldTarget = FlattenTargetForPlayer(_playerTransform.position + _playerTransform.forward * 3f);
                    AimDirection   = _playerTransform.forward;
                    MoveCursor(AimWorldTarget);
                }
                LogDeploy($"[01][ServerUseActive] item={item.InstanceID} mouseHeld={UnityEngine.Input.GetMouseButton(0)} deployHandlerActive={_itemUseSystem?.IsDeploying.ToString() ?? "null"}");

                Debug.Log($"[ThrowableAimController] HandleItemUseStarted: deployable '{item.InstanceID}' → deploy mode");
                if (_deployReleaseQueued && _itemUseSystem?.IsDeploying == true)
                    TryConfirmDeployRelease("queuedAfterServerUse");
            }
            else if (def.Type == ItemType.Throwable)
            {
                if (_inAimMode)    ResetAimState();
                if (_inDeployMode) ResetDeployState();

                _activeItemInstanceId = item.InstanceID;
                _inAimMode = true;
                IsAimingPC = !IsMobile;
                _combatInputHandler?.SetCameraLockOverride(active: true, forcedValue: true);

                float range = GetThrowRange();
                if (_rangeIndicator != null)
                    _rangeIndicator.ShowWithRange(range);
                if (IsMobile)
                    _aimSystem?.SetCursorVisible(true);

                Debug.Log($"[ThrowableAimController] HandleItemUseStarted: throwable '{item.InstanceID}' mobile={IsMobile} range={range:F2}");
                if (!IsMobile)
                    UpdatePCRaycast();
            }
        }

        private void HandleItemUseEnded(ItemInstance item)
        {
            if (_inAimMode) ResetAimState();
            if (_inDeployMode) ResetDeployState();
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
        public bool TryBeginAim(string instanceID)
        {
            if (_itemSelectionSystem == null || string.IsNullOrEmpty(instanceID))
            {
                Debug.LogWarning($"[ITEM_FLOW] [00][Aim.Begin.Reject] selection={(_itemSelectionSystem != null ? "ok" : "null")} instance='{instanceID ?? "null"}'");
                Debug.LogWarning($"[NH_FLOW][06][ItemAim.BeginRejected] reason=selection-or-instance selection={(_itemSelectionSystem != null ? "ok" : "null")} instance='{instanceID ?? "null"}' {DescribeAimFlowState()}");
                return false;
            }

            var item = _inventorySystem?.GetItemByInstanceID(instanceID);
            if (item == null)
            {
                var bridge = SpectateManager.Instance?.GetCurrentPlayer()?.GamePlaySystemBridge;
                item = bridge?.GetItemByInstanceID(instanceID);
            }
            if (item == null)
            {
                Debug.LogWarning($"[ITEM_FLOW] [00][Aim.Begin.Reject] item '{instanceID}' not found via bound inventory/current player bridge.");
                Debug.LogWarning($"[NH_FLOW][06][ItemAim.BeginRejected] reason=item-not-found instance='{instanceID}' {DescribeAimFlowState()}");
                return false;
            }

            var  def          = ItemDatabase.GetDefinition(item.DefinitionID);
            if (def == null)
            {
                Debug.LogWarning($"[ITEM_FLOW] [00][Aim.Begin.Reject] definition '{item.DefinitionID}' not found for item '{instanceID}'.");
                Debug.LogWarning($"[NH_FLOW][06][ItemAim.BeginRejected] reason=def-not-found instance='{instanceID}' def='{item.DefinitionID}' {DescribeAimFlowState()}");
                return false;
            }

            bool isThrowable  = def.Type == ItemType.Throwable;
            bool isDeployable = def.Type == ItemType.Deployable;

            // ── Deployable: enter deploy mode ──────────────────────────────────
            if (isDeployable)
            {
                if (_inDeployMode) CancelDeploy();
                if (_inAimMode)    CancelAim();

                _activeItemInstanceId = instanceID;
                _inDeployMode = true;
                _deployAwaitingServerUse = true;
                _deployReleaseQueued = false;
                _deployPointerWasHeld = IsPrimaryPointerHeld();
                _suppressNextDeployRelease = true;
                _deployConfirmInProgress = false;
                IsDeployingPC = !IsMobile;
                _combatInputHandler?.SetCameraLockOverride(active: true, forcedValue: true);
                _aimSystem?.SetCursorVisible(true);

                // Mobile: seed the aim target in front of the player so the cursor and
                // placement preview are immediately visible at a reasonable position.
                if (IsMobile && _playerTransform != null)
                {
                    AimWorldTarget = FlattenTargetForPlayer(_playerTransform.position + _playerTransform.forward * 3f);
                    AimDirection   = _playerTransform.forward;
                    MoveCursor(AimWorldTarget);
                }

                Debug.Log($"[ThrowableAimController] TryBeginAim: deployable '{instanceID}' → deploy mode");
                Debug.Log($"[NH_FLOW][06][ItemAim.BeginDeployAim] item={instanceID} def={def.ItemID} mobile={IsMobile} mouseHeld={UnityEngine.Input.GetMouseButton(0)} {DescribeAimFlowState()}");
                LogDeploy($"[00][BeginDeployAim] item={instanceID} def={def.ItemID} mobile={IsMobile} mouseDown={UnityEngine.Input.GetMouseButton(0)}");
                _itemSelectionSystem.RequestSelectItem(instanceID);
                _itemSelectionSystem.RequestUseSelectedItem();
                return true;
            }

            // ── Consumable: direct use ─────────────────────────────────────────
            if (!isThrowable)
            {
                Debug.Log($"[ThrowableAimController] TryBeginAim: consumable '{instanceID}' → direct use");
                Debug.Log($"[NH_FLOW][06][ItemAim.UseConsumableDirect] item={instanceID} def={def.ItemID} type={def.Type} {DescribeAimFlowState()}");
                _itemSelectionSystem.RequestSelectItem(instanceID);
                _itemSelectionSystem.RequestUseSelectedItem();
                return true;
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

            Debug.Log($"[ThrowableAimController] TryBeginAim: throwable '{instanceID}' mobile={IsMobile} range={range:F2}");
            Debug.Log($"[NH_FLOW][06][ItemAim.BeginThrowableAim] item={instanceID} def={def.ItemID} mobile={IsMobile} range={range:F2} {DescribeAimFlowState()}");
            LogThrowable($"TryBeginAim start item={instanceID} mobile={IsMobile} range={range:F2} cursor={AimWorldTarget:F2}");
            _itemSelectionSystem.RequestSelectItem(instanceID);
            _itemSelectionSystem.RequestUseSelectedItem();
            if (!IsMobile)
                UpdatePCRaycast();
            return true;
        }

        /// <summary>
        /// Called by the selection button IDragHandler during a mobile drag.
        /// </summary>
        public void OnMobileDrag(Vector2 joystickDir)
        {
            if (!_inAimMode && !_inDeployMode) return;

            float   range    = GetThrowRange();
            Vector3 worldDir = Joystick01ToWorldDir(joystickDir, range);

            if (_playerTransform != null)
            {
                if (_inDeployMode && joystickDir.magnitude >= _dragThreshold)
                    _suppressNextDeployRelease = false;

                AimWorldTarget = FlattenTargetForPlayer(_playerTransform.position + worldDir);
                AimDirection   = worldDir.sqrMagnitude > 0.001f ? worldDir.normalized : Vector3.forward;
                MoveCursor(AimWorldTarget);
                RotatePlayerTowards(AimDirection);

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
            Debug.Log($"[NH_FLOW][17][ItemAim.MobileDragEnd] magnitude={joystickMagnitude:F2} {DescribeAimFlowState()}");
            if (!_inAimMode && !_inDeployMode) return;

            if (_inDeployMode)
            {
                bool overUi = IsPrimaryPointerOverUI();
                if (overUi)
                {
                    IgnoreDeployRelease($"mobileDragEndOverUI magnitude={joystickMagnitude:F2}");
                    return;
                }

                if (joystickMagnitude < _dragThreshold)
                {
                    IgnoreDeployRelease($"mobileDragEndIgnored magnitude={joystickMagnitude:F2} overUI={overUi}");
                    return;
                }

                _suppressNextDeployRelease = false;
                TryConfirmDeployRelease($"mobileDragEnd magnitude={joystickMagnitude:F2} overUI={overUi}");
                return;
            }

            if (joystickMagnitude >= _dragThreshold)
            {
                if (_inAimMode) ConfirmAim();
            }
            else
            {
                if (_inAimMode) CancelAim();
            }
        }

        public void ConfirmDeployFromFireButtonRelease(float joystickMagnitude)
        {
            Debug.Log($"[NH_FLOW][20][ItemAim.FireButtonDeployRelease] magnitude={joystickMagnitude:F2} {DescribeAimFlowState()}");
            if (!_inDeployMode) return;

            _suppressNextDeployRelease = false;
            TryConfirmDeployRelease($"fireButtonRelease magnitude={joystickMagnitude:F2}");
        }

        /// <summary>
        /// Defensive version of <see cref="OnMobileDragEnd"/> — no-op if aim mode is inactive.
        /// </summary>
        public void OnMobileDragEndIfStillActive(float joystickMagnitude)
        {
            Debug.Log($"[NH_FLOW][17][ItemAim.MobileDragEndIfActive] magnitude={joystickMagnitude:F2} {DescribeAimFlowState()}");
            if (!_inAimMode && !_inDeployMode) return;
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

            Vector3 hit    = FlattenTargetForPlayer(ray.GetPoint(dist));
            float   range  = GetThrowRange();
            Vector3 offset = hit - _playerTransform.position;
            offset.y = 0f;

            if (offset.sqrMagnitude > range * range)
                hit = FlattenTargetForPlayer(_playerTransform.position + offset.normalized * range);

            AimWorldTarget = hit;
            Vector3 aimOffset = hit - _playerTransform.position;
            aimOffset.y = 0f;
            AimDirection = aimOffset.sqrMagnitude > 0.001f
                ? aimOffset.normalized
                : Vector3.forward;
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
            Debug.Log($"[NH_FLOW][38][ItemAim.ConfirmThrow] item={_activeItemInstanceId ?? "null"} target={throwTarget:F2} {DescribeAimFlowState()}");
            ResetAimState();
            _itemUseSystem?.RequestExecuteThrow(throwTarget);
        }

        private Vector3 ResolveConfirmedThrowTarget()
        {
            if (_aimSystem != null)
            {
                Vector3 aimGround = _aimSystem.FinalAimGroundPos;
                if (aimGround.sqrMagnitude > 0.0001f)
                    return FlattenTargetForPlayer(aimGround);
            }
            return FlattenTargetForPlayer(AimWorldTarget);
        }

        /// <summary>
        /// Cancel throwable aim mode and abort the in-progress item use on the server.
        /// Also serves as the shared Cancel HUD button handler.
        /// </summary>
        public void CancelAim()
        {
            Debug.Log($"[NH_FLOW][42][ItemAim.CancelAim] {DescribeAimFlowState()}");
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
            ClearExternalAimTarget();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Confirm / Cancel — Deployable
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Confirm deployable placement at the current aim cursor position (PC left-click).
        /// </summary>
        private void TryConfirmDeployRelease(string source)
        {
            if (!_inDeployMode) return;

            if (_deployConfirmInProgress)
            {
                Debug.Log($"[NH_FLOW][39][ItemAim.DeployReleaseIgnored] source={source} reason=confirm-in-progress {DescribeAimFlowState()}");
                LogDeploy($"[02][ReleaseIgnored] source={source} item={_activeItemInstanceId ?? "null"} reason=confirmInProgress");
                return;
            }

            if (_deployAwaitingServerUse && _itemUseSystem?.IsDeploying != true)
            {
                _deployReleaseQueued = true;
                _deployPointerWasHeld = false;
                Debug.Log($"[NH_FLOW][39][ItemAim.DeployReleaseQueued] source={source} {DescribeAimFlowState()}");
                LogDeploy($"[02][ReleaseQueued] source={source} item={_activeItemInstanceId ?? "null"} using={_itemUseSystem?.IsUsingItem.ToString() ?? "null"} deploying={_itemUseSystem?.IsDeploying.ToString() ?? "null"}");
                return;
            }

            _deployReleaseQueued = false;
            _deployPointerWasHeld = false;
            ConfirmDeploy(source);
        }

        private void IgnoreDeployRelease(string source)
        {
            _deployPointerWasHeld = false;
            _deployReleaseQueued = false;
            _suppressNextDeployRelease = false;
            Debug.Log($"[NH_FLOW][39][ItemAim.DeployReleaseIgnored] source={source} {DescribeAimFlowState()}");
            LogDeploy($"[02][ReleaseIgnored] source={source} item={_activeItemInstanceId ?? "null"} using={_itemUseSystem?.IsUsingItem.ToString() ?? "null"} deploying={_itemUseSystem?.IsDeploying.ToString() ?? "null"}");
        }

        private void ConfirmDeploy(string source = "direct")
        {
            if (!_inDeployMode) return;
            if (_deployAwaitingServerUse && _itemUseSystem?.IsDeploying != true)
            {
                _deployReleaseQueued = true;
                Debug.Log($"[NH_FLOW][39][ItemAim.ConfirmDeployQueued] source={source} {DescribeAimFlowState()}");
                LogDeploy($"ConfirmDeploy queued: source={source} waiting for TargetBeginDeployable item={_activeItemInstanceId ?? "null"} using={_itemUseSystem?.IsUsingItem.ToString() ?? "null"} deploying={_itemUseSystem?.IsDeploying.ToString() ?? "null"}");
                return;
            }
            _deployAwaitingServerUse = false;
            LogDeploy($"[03][ReleaseConfirm] source={source} item={_activeItemInstanceId ?? "null"}");
            Debug.Log($"[NH_FLOW][39][ItemAim.ConfirmDeploy] source={source} {DescribeAimFlowState()}");
            bool confirmed = _itemUseSystem?.TryConfirmDeploy() ?? false;
            if (confirmed)
            {
                _deployConfirmInProgress = true;
                _deployReleaseQueued = false;
                _deployPointerWasHeld = false;
                _suppressNextDeployRelease = false;
                LogDeploy($"[04][DeployUseStarted] item={_activeItemInstanceId ?? "null"} waitingForUseComplete");
                Debug.Log($"[NH_FLOW][41][ItemAim.ConfirmDeployAccepted] source={source} {DescribeAimFlowState()}");
            }
            else
            {
                Debug.Log($"[NH_FLOW][41][ItemAim.ConfirmDeployRejected] source={source} {DescribeAimFlowState()}");
                LogDeploy($"ConfirmDeploy pending/rejected item={_activeItemInstanceId ?? "null"} using={_itemUseSystem?.IsUsingItem.ToString() ?? "null"}");
            }
        }

        /// <summary>
        /// Cancel deploy mode and abort the in-progress placement on the server.
        /// </summary>
        public void CancelDeploy()
        {
            Debug.Log($"[NH_FLOW][42][ItemAim.CancelDeploy] {DescribeAimFlowState()}");
            if (_inDeployMode)
                ResetDeployState();
            _itemUseSystem?.RequestCancelUse();
        }

        private void ResetDeployState()
        {
            _inDeployMode = false;
            IsDeployingPC = false;
            _activeItemInstanceId = null;
            _deployAwaitingServerUse = false;
            _deployReleaseQueued = false;
            _deployPointerWasHeld = false;
            _suppressNextDeployRelease = false;
            _deployConfirmInProgress = false;

            HideCursor();
            if (IsMobile)
                _aimSystem?.SetCursorVisible(false);
            _combatInputHandler?.SetFireMobileJoystick(Vector2.zero, false);
            _combatInputHandler?.SetCameraLockOverride(active: false, forcedValue: false);
            ClearExternalAimTarget();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        private string DescribeAimFlowState()
        {
            var selected = _itemSelectionSystem?.SelectedItem;
            var selectedDef = selected != null ? ItemDatabase.GetDefinition(selected.DefinitionID) : null;
            var current = _itemUseSystem?.CurrentItem;
            var currentDef = current != null ? ItemDatabase.GetDefinition(current.DefinitionID) : null;
            return $"inAim={_inAimMode} inDeploy={_inDeployMode} mobile={IsMobile} active='{_activeItemInstanceId ?? "null"}' " +
                   $"awaitServerUse={_deployAwaitingServerUse} queuedRelease={_deployReleaseQueued} suppressRelease={_suppressNextDeployRelease} confirmInProgress={_deployConfirmInProgress} " +
                   $"selected='{selected?.InstanceID ?? "null"}' selectedDef={selectedDef?.ItemID ?? "null"} selectedType={selectedDef?.Type.ToString() ?? "null"} " +
                   $"using={_itemUseSystem?.IsUsingItem.ToString() ?? "null"} deploying={_itemUseSystem?.IsDeploying.ToString() ?? "null"} current='{current?.InstanceID ?? "null"}' currentDef={currentDef?.ItemID ?? "null"} currentType={currentDef?.Type.ToString() ?? "null"} " +
                   $"aimTarget={AimWorldTarget:F2} aimDir={AimDirection:F2}";
        }

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

            var item = _inventorySystem?.GetItemByInstanceID(_activeItemInstanceId);
            if (item == null)
            {
                var bridge = SpectateManager.Instance?.GetCurrentPlayer()?.GamePlaySystemBridge;
                item = bridge?.GetItemByInstanceID(_activeItemInstanceId);
            }
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
            _aimCursor.position = FlattenTargetForPlayer(worldPos) + Vector3.up * 0.1f;
        }

        private Vector3 FlattenTargetForPlayer(Vector3 worldPos)
        {
            float y = _playerTransform != null ? _playerTransform.position.y : worldPos.y;
            return new Vector3(worldPos.x, y, worldPos.z);
        }

        private void RotatePlayerTowards(Vector3 worldDirection)
        {
            if (_playerTransform == null)
                return;

            Vector3 flatDir = new Vector3(worldDirection.x, 0f, worldDirection.z);
            if (flatDir.sqrMagnitude <= 0.001f)
                return;

            _playerTransform.rotation = Quaternion.LookRotation(flatDir.normalized, Vector3.up);
        }

        private void HideCursor()
        {
            if (_aimCursor != null)
                _aimCursor.gameObject.SetActive(false);
        }

        private static bool IsPrimaryPointerHeld()
        {
            if (UnityEngine.Input.GetMouseButton(0))
                return true;

            for (int i = 0; i < UnityEngine.Input.touchCount; i++)
            {
                TouchPhase phase = UnityEngine.Input.GetTouch(i).phase;
                if (phase == TouchPhase.Began ||
                    phase == TouchPhase.Moved ||
                    phase == TouchPhase.Stationary)
                    return true;
            }

            return false;
        }

        private static bool PrimaryPointerReleasedThisFrame()
        {
            if (UnityEngine.Input.GetMouseButtonUp(0))
                return true;

            for (int i = 0; i < UnityEngine.Input.touchCount; i++)
            {
                TouchPhase phase = UnityEngine.Input.GetTouch(i).phase;
                if (phase == TouchPhase.Ended || phase == TouchPhase.Canceled)
                    return true;
            }

            return false;
        }

        private static bool IsPrimaryPointerOverUI()
        {
            if (EventSystem.current == null)
                return false;

            if (IsPointerOverBlockingUI(UnityEngine.Input.mousePosition))
                return true;

            for (int i = 0; i < UnityEngine.Input.touchCount; i++)
            {
                var touch = UnityEngine.Input.GetTouch(i);
                if (IsPointerOverBlockingUI(touch.position))
                    return true;
            }

            return false;
        }

        private static bool IsPointerOverBlockingUI(Vector2 screenPosition)
        {
            if (EventSystem.current == null)
                return false;

            var pointer = new PointerEventData(EventSystem.current)
            {
                position = screenPosition
            };

            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, results);
            for (int i = 0; i < results.Count; i++)
            {
                if (IsBlockingUiTarget(results[i].gameObject))
                    return true;
            }

            return false;
        }

        private static bool IsBlockingUiTarget(GameObject go)
        {
            if (go == null)
                return false;

            if (IsNonBlockingWorldInputUi(go))
                return false;

            if (go.GetComponentInParent<Selectable>() != null)
                return true;

            if (go.GetComponentInParent<ActionButton>() != null)
                return true;

            if (go.GetComponentInParent<ItemFilterButton>() != null)
                return true;

            if (go.GetComponentInParent<SelectableItemButton>() != null)
                return true;

            if (go.GetComponentInParent<WeaponSlotButton>() != null)
                return true;

            return false;
        }

        private static bool IsNonBlockingWorldInputUi(GameObject go)
        {
            if (go.GetComponentInParent<NightHunt.UI.Mobile.MobileCameraDragArea>() != null)
                return true;

            for (Transform t = go.transform; t != null; t = t.parent)
            {
                string n = t.name;
                if (!string.IsNullOrEmpty(n) &&
                    n.IndexOf("CameraDragArea", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
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

        // ─────────────────────────────────────────────────────────────────────
        //  Mobile deploy aim tracking
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// On mobile in deploy mode, project any active non-UI touch to the ground
        /// plane and update <see cref="AimWorldTarget"/> so the cursor / placement
        /// preview follows the finger even without going through the drag-on-button path.
        /// </summary>
        private void UpdateMobileDeployAim()
        {
            if (_cam == null || _playerTransform == null) return;

            for (int i = 0; i < UnityEngine.Input.touchCount; i++)
            {
                var touch = UnityEngine.Input.GetTouch(i);
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    continue;

                // Skip only blocking controls; camera drag/fullscreen world-input
                // surfaces must still update the deploy target.
                if (IsPointerOverBlockingUI(touch.position))
                    continue;

                Ray   ray   = _cam.ScreenPointToRay(touch.position);
                Plane plane = new Plane(Vector3.up, _playerTransform.position);
                if (!plane.Raycast(ray, out float dist))
                    continue;

                Vector3 hit    = ray.GetPoint(dist);
                float   range  = GetThrowRange();
                Vector3 offset = hit - _playerTransform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude > range * range)
                    hit = _playerTransform.position + offset.normalized * range;

                AimWorldTarget = FlattenTargetForPlayer(hit);
                Vector3 aimOffset = hit - _playerTransform.position;
                aimOffset.y = 0f;
                AimDirection = aimOffset.sqrMagnitude > 0.001f ? aimOffset.normalized : Vector3.forward;
                MoveCursor(AimWorldTarget);
                RotatePlayerTowards(AimDirection);

                // Feed AimSystem so _worldAimCursor is positioned via ResolveThrowableAim.
                _aimSystem?.SetThrowableAim(new Vector2(AimDirection.x, AimDirection.z));
                break; // process the first valid world touch only
            }
        }
    }
}
