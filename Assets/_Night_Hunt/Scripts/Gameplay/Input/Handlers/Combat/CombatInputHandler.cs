using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections.Generic;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Gameplay.Input.Handlers.Movement;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.GameplaySystems.UI.Combat;   // ThrowableAimController.IsAimingPC guard
using NightHunt.Diagnostics;

namespace NightHunt.Gameplay.Input.Handlers.Combat
{
    /// <summary>
    /// Handles ONLY combat input (Fire, Aim, Reload, WeaponSlots, Grenade).
    ///
    /// FIRE FLOW (PC + Mobile both use BeginFire/EndFire):
    ///
    ///   PC:
    ///     Mouse Down (LMB performed) → BeginFire()
    ///       → Freeze camera (CameraStateManager.ForceState Locked)
    ///       → Force STRAFE (MovementInputHandler.SetCameraLockOverride true)
    ///     Mouse Move (while held)    → UpdateAimDirection() updates _aimDirection
    ///       → GetAimDirection() returns _aimDirection (≠ zero because _isFiring = true)
    ///       → GatherInput() uses it as _aimYaw → character rotates to face cursor
    ///     Mouse Up (LMB canceled)    → EndFire()
    ///       → Restore camera state
    ///       → Restore movement lock state
    ///       → GetAimDirection() returns zero → _aimYaw falls back to camera yaw
    ///
    ///   Mobile (FireButton):
    ///     PointerDown  → SimulateFire(true)  → BeginFire() — same flow
    ///     OnDrag       → SetMobileAimDirection(worldDir) → _aimDirection = drag dir
    ///     PointerUp    → SimulateFire(false) → EndFire()  — same flow
    ///
    /// KEY RULE:
    ///   GetAimDirection() returns Vector3.zero when NOT firing.
    ///   This ensures GatherInput() does not use the cursor direction when not shooting.
    /// </summary>
    public class CombatInputHandler : MonoBehaviour, IInputHandler
    {
        // ── Cached actions ────────────────────────────────────────────────────────
        private InputActionMap _combatActionMap;
        private InputAction _fireAction;
        private InputAction _aimAction;
        private InputAction _reloadAction;
        private InputAction _weaponSlot1Action;
        private InputAction _weaponSlot2Action;
        private InputAction _weaponSlot3Action;
        private InputAction _throwGrenadeAction;
        private InputAction _consumablePanelAction;  // optional — maps to "ConsumablePanel" action if defined
        private InputAction _deployablePanelAction;  // maps to "UseAbility" action (key 5 / Alt)
        private InputAction _switchWeaponAction;
        private InputAction _mousePositionAction; // Track mouse position via Input System

        // ── Delegate fields (stored to allow correct unsubscription, avoids lambda leak) ─────
        private System.Action<InputAction.CallbackContext> _onSlot1;
        private System.Action<InputAction.CallbackContext> _onSlot2;
        private System.Action<InputAction.CallbackContext> _onSlot3;
        // Delegate that bridges OnWeaponSlotChanged(int) → weaponSystem.SelectWeapon(WeaponSlotType).
        // Stored as a field so it can be unsubscribed cleanly on rebind.
        private System.Action<int> _weaponSlotSelectionHandler;

        // ── State ─────────────────────────────────────────────────────────────────
        private bool    _isFiring;
        private bool    _isAiming;
        private bool    _isReloading;
        private bool    _pendingSingleShotOnRelease;
        private int     _currentWeaponSlot = 0;
        private Vector3 _aimDirection;          // internal — always tracks cursor
        private Vector3 _lastGroundHitPoint;    // last cursor-to-ground hit (PC) — passed to RequestExecuteThrow
        private bool    _inputEnabled = false;
        private bool    _uiConsumedThisPress;   // set by UI buttons to block the concurrent LMB/touch fire event
        private bool    _suppressNextFireCanceled;
        // ── Camera ref (PC raycast) ───────────────────────────────────────────────
        private UnityEngine.Camera _playerCamera;

        // ── MOBA feedback refs (optional) ─────────────────────────────────────────
        private NightHunt.GameplaySystems.UI.Combat.RangeIndicator _rangeIndicator;
        private NightHunt.GameplaySystems.UI.Combat.FireButton      _fireButton;
        private NightHunt.GameplaySystems.UI.Combat.ThrowableAimController _throwableAimController;

        // ── Combat system refs ────────────────────────────────────────────────────
        private MovementInputHandler _movementInputHandler;
        private NightHunt.GameplaySystems.Core.Interfaces.IWeaponSystem _weaponSystem;
        private NightHunt.GameplaySystems.Core.Interfaces.IAimSystem _aimSystem;
        private IItemUseSystem _itemUseSystem;
        private NightHunt.GameplaySystems.Core.Interfaces.IItemSelectionSystem _itemSelectionSystem;
        /// <summary>Local player transform — origin cho ground-plane aim raycast.</summary>
        private Transform _playerTransform;
        /// <summary>Camera lock state before firing — restored when EndFire is called.</summary>
        private bool _prevCameraLockBeforeFire;

        // ── Camera freeze refs ────────────────────────────────────────────────────
        private NightHunt.Gameplay.Camera.CameraStateManager _cameraStateManager;
        /// <summary>Camera state before firing — restored when EndFire is called.</summary>
        private NightHunt.Gameplay.Camera.CameraState _prevCameraStateBeforeFire;

        // ── Mobile aim override ───────────────────────────────────────────────────
        /// <summary>
        /// True when the mobile FireButton is being dragged.
        /// When true, _aimDirection is set by FireButton.OnDrag instead of mouse raycast.
        /// </summary>
        private bool    _mobileAimActive;
        private Vector3 _mobileAimDirection;   // normalised — used for character rotation
        private Vector2 _mobileJoystick01;     // raw [0,1] with magnitude — used for cursor placement
        private bool    _mobileFirePressHadDrag;
        private float   _mobileFirePressReleaseMagnitude;

        // ── Events ────────────────────────────────────────────────────────────────
        public event System.Action       OnFire;
        public event System.Action       OnFireStop;
        public event System.Action       OnAimStart;
        public event System.Action       OnAimStop;
        public event System.Action       OnReload;
        public event System.Action<int>  OnWeaponSlotChanged;
        public event System.Action       OnThrowGrenade;
        /// <summary>Fired when the ConsumablePanel shortcut key is pressed.</summary>
        public event System.Action       OnConsumablePanel;
        /// <summary>Fired when the deployable shortcut key is pressed.</summary>
        public event System.Action       OnDeployablePanel;

        // ── IInputHandler ─────────────────────────────────────────────────────────
        public bool IsInputEnabled => _inputEnabled;

        public InputActionMap GetActionMap()
        {
            if (_combatActionMap == null && InputLayerManager.Instance != null)
                _combatActionMap = InputLayerManager.Instance.CombatMap;
            return _combatActionMap;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            // Camera.main may be null here (player not yet spawned).
            // UpdateAimDirection() refreshes every frame — do not rely on this cache.
            _playerCamera = UnityEngine.Camera.main;

            _onSlot1 = _ => SwitchToWeaponSlot(0);
            _onSlot2 = _ => SwitchToWeaponSlot(1);
            _onSlot3 = _ => SwitchToWeaponSlot(2);

            InitializeActions();
        }

        private void Start()
        {
            if (_combatActionMap == null)
            {
                Debug.LogWarning("[CombatInputHandler] Start(): retrying InitializeActions");
                InitializeActions();
            }
            if (InputLayerManager.Instance != null &&
                !InputLayerManager.Instance.IsLayerActive(InputLayer.None) &&
                _combatActionMap != null)
            {
                InputLayerManager.Instance.RegisterHandler(this);
            }
        }

        private void OnEnable()
        {
            GameActionBus.OnWeaponSlotRequested += HandleWeaponSlotRequested;
            if (InputLayerManager.Instance != null)
                InputLayerManager.Instance.RegisterHandler(this);
        }

        private void OnDisable()
        {
            GameActionBus.OnWeaponSlotRequested -= HandleWeaponSlotRequested;
            DisableInput();
            InputLayerManager.Instance?.UnregisterHandler(this);
        }

        private void Update()
        {
            // UpdateAimDirection always runs so _aimDirection always equals the current cursor position.
            // BUT GetAimDirection() only exposes this value when _isFiring = true.
            // → Not firing: GatherInput() receives no aim direction → _aimYaw falls back to camera yaw.
            // → Firing:     GatherInput() receives aim direction → character rotates to face cursor.
            UpdateAimDirection();
        }

        // ── Initialization ────────────────────────────────────────────────────────

        private void InitializeActions()
        {
            if (InputLayerManager.Instance == null)
            {
                Debug.LogError("[CombatInputHandler] InputLayerManager.Instance is null!");
                return;
            }

            _combatActionMap = InputLayerManager.Instance.CombatMap;

            if (_combatActionMap != null)
            {
                _fireAction          = _combatActionMap.FindAction("Fire");
                _aimAction           = _combatActionMap.FindAction("AimDownSights");
                _reloadAction        = _combatActionMap.FindAction("Reload");
                _weaponSlot1Action   = _combatActionMap.FindAction("WeaponSlot1");
                _weaponSlot2Action   = _combatActionMap.FindAction("WeaponSlot2");
                _weaponSlot3Action   = _combatActionMap.FindAction("WeaponSlot3");
                _throwGrenadeAction  = _combatActionMap.FindAction("ThrowGrenade");
                _consumablePanelAction = _combatActionMap.FindAction("ConsumablePanel");
                _deployablePanelAction = _combatActionMap.FindAction("UseAbility");
                _switchWeaponAction  = _combatActionMap.FindAction("SwitchWeapon");
                // MousePosition action added to .inputactions
                _mousePositionAction = _combatActionMap.FindAction("MousePosition");
            }
            else
            {
                Debug.LogError("[CombatInputHandler] 'Combat' action map not found!");
            }
        }

        // ── IInputHandler Implementation ──────────────────────────────────────────

        public void EnableInput()
        {
            if (_inputEnabled) return;
            if (_combatActionMap == null) InitializeActions();
            if (_combatActionMap == null)
            {
                Debug.LogError("[CombatInputHandler] CombatActionMap is null — cannot EnableInput!");
                return;
            }

            _inputEnabled = true;

            // Fire: performed = mouse down, canceled = mouse up
            // Press(behavior=2) in .inputactions ensures both events fire correctly.
            if (_fireAction != null)
            {
                _fireAction.performed += OnFirePerformed; // Mouse Down → BeginFire
                _fireAction.canceled  += OnFireCanceled;  // Mouse Up   → EndFire
            }
            if (_aimAction != null)
            {
                _aimAction.performed += OnAimPerformed;
                _aimAction.canceled  += OnAimCanceled;
            }
            if (_reloadAction       != null) _reloadAction.performed       += OnReloadPerformed;
            if (_weaponSlot1Action  != null) _weaponSlot1Action.performed  += _onSlot1;
            if (_weaponSlot2Action  != null) _weaponSlot2Action.performed  += _onSlot2;
            if (_weaponSlot3Action  != null) _weaponSlot3Action.performed  += _onSlot3;
            if (_throwGrenadeAction != null) _throwGrenadeAction.performed += OnThrowGrenadePerformed;
            if (_consumablePanelAction != null) _consumablePanelAction.performed += OnConsumablePanelPerformed;
            if (_deployablePanelAction != null) _deployablePanelAction.performed += OnDeployablePanelPerformed;
            if (_switchWeaponAction != null) _switchWeaponAction.performed += OnSwitchWeaponPerformed;
            // MousePosition: no event subscription needed — polling in UpdateAimDirection() is sufficient.
            // The action only needs to be enabled (handled by InputLayerManager).

            Debug.Log("[CombatInputHandler] Input enabled");
        }

        public void DisableInput()
        {
            if (!_inputEnabled) return;
            _inputEnabled = false;

            if (_fireAction != null)
            {
                _fireAction.performed -= OnFirePerformed;
                _fireAction.canceled  -= OnFireCanceled;
            }
            if (_aimAction != null)
            {
                _aimAction.performed -= OnAimPerformed;
                _aimAction.canceled  -= OnAimCanceled;
            }
            if (_reloadAction       != null) _reloadAction.performed       -= OnReloadPerformed;
            if (_weaponSlot1Action  != null) _weaponSlot1Action.performed  -= _onSlot1;
            if (_weaponSlot2Action  != null) _weaponSlot2Action.performed  -= _onSlot2;
            if (_weaponSlot3Action  != null) _weaponSlot3Action.performed  -= _onSlot3;
            if (_throwGrenadeAction != null) _throwGrenadeAction.performed -= OnThrowGrenadePerformed;
            if (_consumablePanelAction != null) _consumablePanelAction.performed -= OnConsumablePanelPerformed;
            if (_deployablePanelAction != null) _deployablePanelAction.performed -= OnDeployablePanelPerformed;
            if (_switchWeaponAction != null) _switchWeaponAction.performed -= OnSwitchWeaponPerformed;

            // If firing when disabled (e.g., opening inventory), force EndFire to restore state.
            if (_isFiring) EndFire();

            _isFiring    = false;
            _isAiming    = false;
            _isReloading = false;
            _pendingSingleShotOnRelease = false;

            Debug.Log("[CombatInputHandler] Input disabled");
        }

        // ── Aim Direction (Internal Tracking) ─────────────────────────────────────

        /// <summary>
        /// Always updates _aimDirection internally each frame.
        ///
        /// PC:     Ground-plane raycast from mouse position.
        /// Mobile: Direction set by FireButton.OnDrag via SetMobileAimDirection().
        ///
        /// IMPORTANT: This method only updates internal state.
        /// GetAimDirection() is the public API — it only exposes the direction while firing.
        /// </summary>
        private void UpdateAimDirection()
        {
            if (_mobileAimActive)
            {
                // Mobile: direction set by FireButton.OnDrag
                _aimDirection = _mobileAimDirection;
            }
            else
            {
                // PC: always refresh Camera.main to avoid stale reference (HOST spawn issue)
                var freshCam = UnityEngine.Camera.main;
                if (freshCam != null) _playerCamera = freshCam;
                if (_playerCamera == null) return;

                Vector3 groundOrigin = _playerTransform != null
                    ? _playerTransform.position
                    : transform.position;

                // Read mouse position from MousePosition action if available, fallback to legacy Input
                Vector2 mouseScreenPos;
                if (_mousePositionAction != null && _mousePositionAction.enabled)
                    mouseScreenPos = _mousePositionAction.ReadValue<Vector2>();
                else
                    mouseScreenPos = UnityEngine.Input.mousePosition;

                Ray   ray    = _playerCamera.ScreenPointToRay(mouseScreenPos);
                Plane ground = new Plane(Vector3.up, groundOrigin);

                if (ground.Raycast(ray, out float distance))
                {
                    Vector3 hitPoint = FlattenAimTarget(ray.GetPoint(distance));

                    // When a throwable/deployable is armed via the FilterPanel path
                    // (not via ThrowableAimController.TryBeginAim), clamp the hit to the
                    // throwable vision range so the actual throw target matches the
                    // visual ring indicator. Also update ThrowableAimController's static
                    // AimWorldTarget so any aim cursor stays in sync.
                    if (_itemUseSystem != null && _itemUseSystem.IsUsingItem && !ThrowableAimController.IsAimingPC)
                    {
                        Vector3 offset = hitPoint - groundOrigin;
                        offset.y = 0f;
                        float   maxRange = _aimSystem?.GetVisionRange() ?? 15f;
                        if (offset.sqrMagnitude > maxRange * maxRange)
                            hitPoint = FlattenAimTarget(groundOrigin + offset.normalized * maxRange);
                        ThrowableAimController.SetExternalAimTarget(hitPoint);
                    }

                    _lastGroundHitPoint = hitPoint;
                    Vector3 dir       = hitPoint - groundOrigin;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.001f)
                        _aimDirection = dir.normalized;
                }
            }

            // Always push aim direction to WeaponSystem so projectiles fly in the right direction.
            // WeaponSystem needs the direction even before firing (preview trajectory, etc.).
            _weaponSystem?.SetAimDirection(_aimDirection);

            // Mobile MOBA cursor sync:
            // Uses _mobileJoystick01 (retains magnitude [0,1]) instead of _aimDirection (normalized).
            // -> Cursor placed at player + joystickDir * joystickMagnitude * visionRange.
            // -> Joystick at 50% places cursor at 50% of range — like Mobile Legends.
            if (_mobileAimActive && _mobileJoystick01.sqrMagnitude > 0.001f)
                _aimSystem?.SetThrowableAim(_mobileJoystick01);
        }

        // ── Fire Callbacks ────────────────────────────────────────────────────────

        private void OnFirePerformed(InputAction.CallbackContext ctx)
        {
            bool overUI = IsPointerOverAnyUIByRaycast();
            bool uiConsumed = _uiConsumedThisPress;
            Debug.Log($"[NH_FLOW][10][Fire.Performed] overUI={overUI} uiConsumed={uiConsumed} {DescribeCombatFlowState()} raycast={DescribePointerRaycastTargets()}");

            // A UI ActionButton already handled this press through EventSystem.
            if (uiConsumed)
            {
                _uiConsumedThisPress = false;
                Debug.Log($"[NH_FLOW][10][Fire.Performed.Blocked] reason=ui-consumed {DescribeCombatFlowState()}");
                PhaseTestLog.Log(
                    PhaseTestLogCategory.Input,
                    "FirePerformedConsumedByUI",
                    $"raycast={DescribePointerRaycastTargets()} activeWeapon={_weaponSystem?.GetActiveWeaponSlot()?.ToString() ?? "none"}",
                    this);
                return;
            }

            // Skip if the pointer is currently over a UI element (e.g. FireButton, item selector).
            // Those buttons call SimulateFire() directly via EventSystem — the Input System
            // action must NOT also trigger BeginFire() for the same press.
            if (overUI)
            {
                if (TryRouteFirePressToCombatUi())
                {
                    Debug.Log($"[NH_FLOW][10][Fire.Performed.RoutedToUI] {DescribeCombatFlowState()}");
                    return;
                }

                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableThrowableDebugLogs)
                    Debug.Log($"[CombatInputHandler] Fire BLOCKED by UI overlay (IsPointerOverUI=true). " +
                              $"Raycast={DescribePointerRaycastTargets()}");
                Debug.Log($"[NH_FLOW][10][Fire.Performed.Blocked] reason=pointer-over-ui {DescribeCombatFlowState()} raycast={DescribePointerRaycastTargets()}");
                PhaseTestLog.Log(
                    PhaseTestLogCategory.Input,
                    "FireBlockedByUI",
                    $"raycast={DescribePointerRaycastTargets()} itemUsing={_itemUseSystem?.IsUsingItem ?? false} activeWeapon={_weaponSystem?.GetActiveWeaponSlot()?.ToString() ?? "none"}",
                    this);
                return;
            }
            BeginFire(); // Mouse Down
        }

        private void OnFireCanceled(InputAction.CallbackContext ctx)
        {
            Debug.Log($"[NH_FLOW][18][Fire.Canceled] suppress={_suppressNextFireCanceled} {DescribeCombatFlowState()}");
            if (_suppressNextFireCanceled)
            {
                _uiConsumedThisPress = false;
                _suppressNextFireCanceled = false;
                Debug.Log($"[NH_FLOW][18][Fire.Canceled.Blocked] reason=ui-consumed {DescribeCombatFlowState()}");
                PhaseTestLog.Log(
                    PhaseTestLogCategory.Input,
                    "FireCanceledConsumedByUI",
                    $"activeWeapon={_weaponSystem?.GetActiveWeaponSlot()?.ToString() ?? "none"}",
                    this);
                return;
            }

            _uiConsumedThisPress = false;  // clear in case LMB-up fires after UI ate the down
            EndFire(); // Mouse Up
        }

        // ── Core Fire Logic ───────────────────────────────────────────────────────

        /// <summary>
        /// Begin firing — called from Mouse Down (PC) or PointerDown (Mobile/Button).
        ///
        /// FLOW:
        ///   1. Freeze camera → disable CinemachineInputAxisController
        ///   2. Force STRAFE → character faces cursor while pressing WASD
        ///   3. From this point: GetAimDirection() ≠ zero → GatherInput() uses cursor aim
        /// </summary>
        private void BeginFire()
        {
            Debug.Log($"[NH_FLOW][11][BeginFire.Enter] {DescribeCombatFlowState()}");
            if (_isFiring)
            {
                Debug.Log($"[NH_FLOW][11][BeginFire.Ignored] reason=already-firing {DescribeCombatFlowState()}");
                return;
            }

            if (_itemUseSystem != null && _itemUseSystem.IsDeploying)
            {
                Debug.Log($"[NH_FLOW][11][BeginFire.Blocked] reason=deploy-preview-active {DescribeCombatFlowState()}");
                LogDeploy("BeginFire ignored while deploy preview is active; placement confirms on pointer release.");
                PhaseTestLog.Log(
                    PhaseTestLogCategory.Input,
                    "FireBlocked",
                    $"reason=deploy-preview-active item={_itemUseSystem.CurrentItem?.InstanceID ?? "null"} activeWeapon={_weaponSystem?.GetActiveWeaponSlot()?.ToString() ?? "none"}",
                    this);
                return;
            }

            // Active item use has priority over weapon fire. Item selection holsters the
            // weapon on the server, but this local guard prevents firing during the sync gap.
            if (IsCurrentItemThrowable())
            {
                _isFiring = true;
                _pendingSingleShotOnRelease = false;
                if (_cameraStateManager != null)
                {
                    _prevCameraStateBeforeFire = _cameraStateManager.CurrentState;
                    _cameraStateManager.ForceState(NightHunt.Gameplay.Camera.CameraState.Locked);
                }
                if (_movementInputHandler != null)
                {
                    _prevCameraLockBeforeFire = _movementInputHandler.IsCameraLocked();
                    _movementInputHandler.SetCameraLockOverride(active: true, forcedValue: true);
                }
                PrepareImmediateFireAim("BeginThrowable");
                if (_rangeIndicator != null)
                    _rangeIndicator.ShowWithRange(_aimSystem?.GetVisionRange() ?? 15f);
                _aimSystem?.SetCursorVisible(true);
                LogThrowable($"BeginFire.ThrowAim currentItem={_itemUseSystem?.CurrentItem?.InstanceID ?? "null"} activeWeapon={_weaponSystem?.GetActiveWeaponSlot()?.ToString() ?? "none"} aim={_aimDirection:F2} target={GetCurrentThrowAimTarget():F2}");
                PhaseTestLog.Log(
                    PhaseTestLogCategory.Input,
                    "BeginThrowableAim",
                    $"item={_itemUseSystem?.CurrentItem?.InstanceID ?? "null"} activeWeapon={_weaponSystem?.GetActiveWeaponSlot()?.ToString() ?? "none"} aim={_aimDirection:F2} target={GetCurrentThrowAimTarget():F2} cameraState={_prevCameraStateBeforeFire} cameraLockedPrev={_prevCameraLockBeforeFire}",
                    this);
                return;
            }

            if (_itemUseSystem != null && _itemUseSystem.IsUsingItem)
            {
                Debug.Log($"[ITEM_FLOW] [10][BeginFire.ItemInUse] block weapon fire currentItem={_itemUseSystem.CurrentItem?.InstanceID ?? "null"} activeWeapon={_weaponSystem?.GetActiveWeaponSlot()?.ToString() ?? "none"}");
                Debug.Log($"[NH_FLOW][11][BeginFire.Blocked] reason=item-in-use {DescribeCombatFlowState()}");
                PhaseTestLog.Log(
                    PhaseTestLogCategory.Input,
                    "FireBlocked",
                    $"reason=item-in-use item={_itemUseSystem.CurrentItem?.InstanceID ?? "null"} activeWeapon={_weaponSystem?.GetActiveWeaponSlot()?.ToString() ?? "none"}",
                    this);
                return;
            }

            if (TryUseSelectedItemFromFire())
                return;

            // Active weapon fire.
            if (_weaponSystem != null && _weaponSystem.GetActiveWeaponSlot() != null)
            {
                _isFiring = true;
                FireMode mode = _weaponSystem.GetCurrentFireMode();
                Debug.Log($"[NH_FLOW][12][BeginWeaponFire] mode={mode} {DescribeCombatFlowState()}");

                // ── Freeze camera at current yaw ──────────────────────────────────
                if (_cameraStateManager != null)
                {
                    _prevCameraStateBeforeFire = _cameraStateManager.CurrentState;
                    _cameraStateManager.ForceState(NightHunt.Gameplay.Camera.CameraState.Locked);
                }

                // ── Force STRAFE so the character rotates to face the cursor ──────
                if (_movementInputHandler != null)
                {
                    _prevCameraLockBeforeFire = _movementInputHandler.IsCameraLocked();
                    _movementInputHandler.SetCameraLockOverride(active: true, forcedValue: true);
                }

                PrepareImmediateFireAim("BeginWeapon");

                if (mode == FireMode.Auto)
                {
                    OnFire?.Invoke();
                    _pendingSingleShotOnRelease = false;
                }
                else
                {
                    _pendingSingleShotOnRelease = true;
                }
                if (_rangeIndicator != null)
                    _rangeIndicator.ShowWithRange(_aimSystem?.GetVisionRange() ?? 15f);
                _aimSystem?.SetCursorVisible(true);
                PhaseTestLog.Log(
                    PhaseTestLogCategory.Input,
                    "BeginWeaponFire",
                    $"slot={_weaponSystem.GetActiveWeaponSlot()?.ToString() ?? "none"} mode={mode} aim={_aimDirection:F2} target={_lastGroundHitPoint:F2} cameraState={_prevCameraStateBeforeFire} cameraLockedPrev={_prevCameraLockBeforeFire} pendingSingle={_pendingSingleShotOnRelease}",
                    this);
                return;
            }

            // No weapon, no armed throwable — use selected item on fire press.
            // For throwables this arms the item only; the next deliberate fire press/release confirms the throw.
            if (!TryUseSelectedItemFromFire())
                Debug.Log($"[NH_FLOW][11][BeginFire.Ignored] reason=no-weapon-no-selected-item {DescribeCombatFlowState()}");
        }

        /// <summary>
        /// Stop firing — called from Mouse Up (PC) or PointerUp (Mobile/Button).
        ///
        /// FLOW:
        ///   1. GetAimDirection() returns zero → GatherInput() falls back to camera yaw
        ///   2. Restore movement lock state
        ///   3. Restore camera state (re-enable CinemachineInputAxisController if previously Free)
        /// </summary>
        // Uses selected item intent before weapon fire so HUD/keyboard item selections can be confirmed by Fire.
        private bool TryUseSelectedItemFromFire()
        {
            if (_itemSelectionSystem == null || !_itemSelectionSystem.HasSelection)
                return false;

            var selectedItem = _itemSelectionSystem.SelectedItem;
            var def = selectedItem != null ? ItemDatabase.GetDefinition(selectedItem.DefinitionID) : null;

            Debug.Log($"[ITEM_FLOW] [10][BeginFire.UseSelected] selected={selectedItem?.InstanceID ?? "null"} def={def?.ItemID ?? "null"} activeWeapon={_weaponSystem?.GetActiveWeaponSlot()?.ToString() ?? "none"}");
            Debug.Log($"[NH_FLOW][13][UseSelectedFromFire] selected={selectedItem?.InstanceID ?? "null"} def={def?.ItemID ?? "null"} type={def?.Type.ToString() ?? "null"} {DescribeCombatFlowState()}");
            _itemSelectionSystem.RequestUseSelectedItem();
            PhaseTestLog.Log(
                PhaseTestLogCategory.Input,
                "UseSelectedItemFromFire",
                $"selected={selectedItem?.InstanceID ?? "null"} def={def?.ItemID ?? "null"} type={def?.Type.ToString() ?? "null"} activeWeapon={_weaponSystem?.GetActiveWeaponSlot()?.ToString() ?? "none"} aim={_aimDirection:F2} target={_lastGroundHitPoint:F2}",
                this);

            if (def != null && def.Type == ItemType.Throwable)
            {
                if (_rangeIndicator != null)
                    _rangeIndicator.ShowWithRange(_aimSystem?.GetVisionRange() ?? 15f);
                _aimSystem?.SetCursorVisible(true);
            }

            return true;
        }

        private void EndFire()
        {
            Debug.Log($"[NH_FLOW][19][EndFire.Enter] {DescribeCombatFlowState()}");
            if (_itemUseSystem != null && _itemUseSystem.IsDeploying)
            {
                float joystickMagnitude = GetMobileFireReleaseJoystickMagnitude();
                Debug.Log($"[NH_FLOW][20][EndFire.DeployConfirm] joystickMagnitude={joystickMagnitude:F2} current={_mobileJoystick01.magnitude:F2} captured={_mobileFirePressReleaseMagnitude:F2} hadDrag={_mobileFirePressHadDrag} {DescribeCombatFlowState()}");
                ResolveThrowableAimController()?.ConfirmDeployFromFireButtonRelease(joystickMagnitude);
                SetFireMobileJoystick(Vector2.zero, false);
                ClearMobileFirePressReleaseState();
                _rangeIndicator?.Hide();
                PhaseTestLog.Log(
                    PhaseTestLogCategory.Input,
                    "EndDeployFireButton",
                    $"joystickMagnitude={joystickMagnitude:F2} item={_itemUseSystem.CurrentItem?.InstanceID ?? "null"}",
                    this);
                return;
            }

            if (!_isFiring && !_pendingSingleShotOnRelease)
            {
                Debug.Log($"[NH_FLOW][19][EndFire.Ignored] reason=not-firing-no-single-shot {DescribeCombatFlowState()}");
                ClearMobileFirePressReleaseState();
                PhaseTestLog.Log(
                    PhaseTestLogCategory.Input,
                    "EndFireIgnored",
                    $"reason=not-firing activeWeapon={_weaponSystem?.GetActiveWeaponSlot()?.ToString() ?? "none"}",
                    this);
                return;
            }

            // Confirm an armed throwable on release. This intentionally wins over a stale
            // active weapon slot while weapon holster replication is still catching up.
            bool hasActiveWeapon = _weaponSystem != null && _weaponSystem.GetActiveWeaponSlot() != null;
            if (_isFiring && IsCurrentItemThrowable() && !ThrowableAimController.IsAimingPC)
            {
                Vector3 throwTarget = GetCurrentThrowAimTarget();
                Debug.Log($"[NH_FLOW][21][EndFire.ThrowConfirm] target={throwTarget:F2} {DescribeCombatFlowState()}");
                LogThrowable($"EndFire.ThrowConfirm target={throwTarget:F2} currentItem={_itemUseSystem?.CurrentItem?.InstanceID ?? "null"} activeWeapon={hasActiveWeapon}");
                PhaseTestLog.Log(
                    PhaseTestLogCategory.Input,
                    "EndThrowableConfirm",
                    $"target={throwTarget:F2} item={_itemUseSystem?.CurrentItem?.InstanceID ?? "null"} activeWeapon={hasActiveWeapon}",
                    this);
                _itemUseSystem?.RequestExecuteThrow(throwTarget);
            }

            if (_pendingSingleShotOnRelease)
            {
                PrepareImmediateFireAim("ReleaseSingleShot");
                Debug.Log($"[NH_FLOW][22][EndFire.SingleShot] {DescribeCombatFlowState()}");
                PhaseTestLog.Log(
                    PhaseTestLogCategory.Input,
                    "ReleaseSingleShot",
                    $"aim={_aimDirection:F2} activeWeapon={_weaponSystem?.GetActiveWeaponSlot()?.ToString() ?? "none"}",
                    this);
                OnFire?.Invoke();
            }

            _isFiring = false;
            _pendingSingleShotOnRelease = false;

            // ── Step 1: _isFiring = false → GetAimDirection() returns zero ───────
            // GatherInput() Priority 1 fails → falls back to camera yaw
            // → Character no longer forced to look toward cursor

            _rangeIndicator?.Hide();

            // ── Step 2: Restore movement lock state ─────────────────────────────────
            _movementInputHandler?.SetCameraLockOverride(
                active: false,
                forcedValue: _prevCameraLockBeforeFire);

            // ── Step 3: Restore camera state ─────────────────────────────────
            // Previously Free → re-enable CinemachineInputAxisController → camera rotates freely again
            // Previously Locked → keep Locked
            if (_cameraStateManager != null)
                _cameraStateManager.ForceState(_prevCameraStateBeforeFire);

            // Reset mobile aim and throwable cursor (activated by SetFireMobileJoystick)
            _mobileAimActive    = false;
            _mobileAimDirection = Vector3.zero;
            _mobileJoystick01   = Vector2.zero;
            ClearMobileFirePressReleaseState();
            _aimSystem?.SetThrowableAim(Vector2.zero);  // exit throwable mode if joystick activated it

            if (!ThrowableAimController.IsAimingPC && !ThrowableAimController.IsDeployingPC)
                _aimSystem?.SetCursorVisible(true);
            if (!ThrowableAimController.IsAimingPC && !ThrowableAimController.IsDeployingPC)
                ThrowableAimController.ClearExternalAimTarget();

            OnFireStop?.Invoke();
            Debug.Log($"[NH_FLOW][23][EndFire.Exit] {DescribeCombatFlowState()}");
            PhaseTestLog.Log(
                PhaseTestLogCategory.Input,
                "EndFire",
                $"activeWeapon={_weaponSystem?.GetActiveWeaponSlot()?.ToString() ?? "none"} restoredCamera={_prevCameraStateBeforeFire} restoredLock={_prevCameraLockBeforeFire} aim={_aimDirection:F2}",
                this);
        }

        private ThrowableAimController ResolveThrowableAimController()
        {
            if (_throwableAimController != null)
                return _throwableAimController;

#if UNITY_2023_1_OR_NEWER
            _throwableAimController = FindFirstObjectByType<ThrowableAimController>(FindObjectsInactive.Include);
#else
            _throwableAimController = FindObjectOfType<ThrowableAimController>(true);
#endif
            return _throwableAimController;
        }

        private float GetMobileFireReleaseJoystickMagnitude()
        {
            return _mobileFirePressHadDrag
                ? _mobileFirePressReleaseMagnitude
                : _mobileJoystick01.magnitude;
        }

        private void ClearMobileFirePressReleaseState()
        {
            _mobileFirePressHadDrag = false;
            _mobileFirePressReleaseMagnitude = 0f;
        }

        // ── Other Input Callbacks ─────────────────────────────────────────────────

        private void OnAimPerformed(InputAction.CallbackContext ctx)  { _isAiming = true;  OnAimStart?.Invoke(); }
        private void OnAimCanceled(InputAction.CallbackContext ctx)   { _isAiming = false; OnAimStop?.Invoke();  }

        private void OnReloadPerformed(InputAction.CallbackContext ctx)
        {
            Debug.Log($"[NH_FLOW][30][Reload.Performed] isReloading={_isReloading} {DescribeCombatFlowState()}");
            if (!_isReloading)
            {
                PhaseTestLog.Log(
                    PhaseTestLogCategory.Input,
                    "ReloadPressed",
                    $"activeWeapon={_weaponSystem?.GetActiveWeaponSlot()?.ToString() ?? "none"} isUsingItem={_itemUseSystem?.IsUsingItem ?? false}",
                    this);
                OnReload?.Invoke();
            }
        }

        public void SwitchToWeaponSlot(int slot)
        {
            Debug.Log($"[NH_FLOW][31][WeaponSlot.Request] slot={slot} {DescribeCombatFlowState()}");
            CancelItemIntentForWeaponSlot(slot);

            if (_currentWeaponSlot != slot || !IsWeaponSlotActive(slot))
            {
                _currentWeaponSlot = slot;
                PhaseTestLog.Log(
                    PhaseTestLogCategory.Input,
                    "WeaponSlotPressed",
                    $"slotIndex={slot} itemUsing={_itemUseSystem?.IsUsingItem ?? false}",
                    this);
                OnWeaponSlotChanged?.Invoke(slot);
            }
        }

        private void OnSwitchWeaponPerformed(InputAction.CallbackContext ctx)
        {
            // Mouse wheel is reserved for camera zoom. Weapon switching is handled
            // only by explicit weapon slot input/buttons.
        }

        private void OnThrowGrenadePerformed(InputAction.CallbackContext ctx) => OnThrowGrenade?.Invoke();
        private void OnConsumablePanelPerformed(InputAction.CallbackContext ctx) => OnConsumablePanel?.Invoke();
        private void OnDeployablePanelPerformed(InputAction.CallbackContext ctx) => OnDeployablePanel?.Invoke();
        private void HandleWeaponSlotRequested(int zeroBasedIndex) => SwitchToWeaponSlot(zeroBasedIndex);

        // ── Public API ────────────────────────────────────────────────────────────

        public bool IsFiring()            => _isFiring;
        public bool IsAiming()            => _isAiming;
        public bool IsReloading()         => _isReloading;
        public int  GetCurrentWeaponSlot() => _currentWeaponSlot;
        public void SetReloading(bool v)  => _isReloading = v;

        public void SimulateWeaponSlotPressed(WeaponSlotType slotType, bool isDoubleClick)
        {
            if (!_inputEnabled)
            {
                Debug.Log($"[NH_FLOW][31][WeaponSlot.Blocked] slot={slotType} reason=input-disabled {DescribeCombatFlowState()}");
                return;
            }

            int slot = SlotTypeToIndex(slotType);
            bool isActiveSlot = IsWeaponSlotActive(slot);
            CancelItemIntentForWeaponSlot(slot);

            if (isDoubleClick)
            {
                if (isActiveSlot)
                    SimulateReload();
                else
                    SwitchToWeaponSlot(slot);
                return;
            }

            if (isActiveSlot)
            {
                Debug.Log($"[NH_FLOW][31][WeaponSlot.Holster] slot={slot} type={slotType} {DescribeCombatFlowState()}");
                _weaponSystem?.HolsterWeapon();
                _currentWeaponSlot = -1;
                PhaseTestLog.Log(
                    PhaseTestLogCategory.Input,
                    "WeaponSlotHolster",
                    $"slotIndex={slot} slotType={slotType}",
                    this);
                return;
            }

            SwitchToWeaponSlot(slot);
        }

        /// <summary>
        /// Returns the aim direction ONLY while firing (LMB held or mobile button held).
        ///
        /// Returns Vector3.zero when not firing.
        /// → GatherInput() Priority 1 fails → _aimYaw falls back to camera yaw
        /// → Character is not forced to look toward cursor when not shooting.
        ///
        /// This is the KEY RULE of the entire aim system.
        /// </summary>
        public Vector3 GetAimDirection()
        {
            // Only expose direction when firing or when mobile aim is active
            if (!_isFiring && !_mobileAimActive)
                return Vector3.zero;

            return _aimDirection;
        }

        /// <summary>
        /// Bind optional MOBA visual feedback components.
        /// </summary>
        public void BindAttackIndicators(
            NightHunt.GameplaySystems.UI.Combat.RangeIndicator rangeIndicator,
            NightHunt.GameplaySystems.UI.Combat.FireButton fireButton = null)
        {
            _rangeIndicator = rangeIndicator;
            _fireButton     = fireButton;
        }

        /// <summary>
        /// Simulate fire from an on-screen button (mobile).
        /// Call SimulateFire(true) = PointerDown → BeginFire()
        /// Call SimulateFire(false) = PointerUp  → EndFire()
        /// </summary>
        public void SimulateFire(bool start)
        {
            Debug.Log($"[NH_FLOW][09][SimulateFire] start={start} {DescribeCombatFlowState()}");
            if (!_inputEnabled)
            {
                Debug.Log($"[NH_FLOW][09][SimulateFire.Blocked] reason=input-disabled start={start} {DescribeCombatFlowState()}");
                if (!start)
                    ClearMobileFirePressReleaseState();
                return;
            }
            if (start)
            {
                ClearMobileFirePressReleaseState();
                // Immediately activate mobile aim so AimSystem stops chasing the mouse
                // the moment the button is pressed (before the first OnDrag fires).
                // Direction is the current _aimDirection (where the cursor already is);
                // it will be overridden by SetFireMobileJoystick on the first drag frame.
                _mobileAimActive    = true;
                _mobileAimDirection = _aimDirection.sqrMagnitude > 0.001f
                    ? _aimDirection
                    : transform.forward;
                // Use a small initial magnitude so cursor appears near the player, not at the edge.
                _mobileJoystick01   = new Vector2(_mobileAimDirection.x, _mobileAimDirection.z) * 0.15f;
                _aimSystem?.SetThrowableAim(_mobileJoystick01);

                Debug.Log($"[FireButton] SimulateFire(true) — mobileAimDir={_mobileAimDirection:F2}  joystick01={_mobileJoystick01:F2}  " +
                          $"aimPos={_aimSystem?.FinalAimPos:F2}  mobileAimActive={_mobileAimActive}");

                BeginFire();
            }
            else
            {
                EndFire();
            }
        }

        public void SimulateReload()
        {
            Debug.Log($"[NH_FLOW][30][SimulateReload] {DescribeCombatFlowState()}");
            if (!_inputEnabled || _isReloading)
            {
                Debug.Log($"[CombatInputHandler] SimulateReload blocked: inputEnabled={_inputEnabled} isReloading={_isReloading}");
                Debug.Log($"[NH_FLOW][30][SimulateReload.Blocked] reason=input-disabled-or-reloading {DescribeCombatFlowState()}");
                return;
            }

            Debug.Log("[CombatInputHandler] SimulateReload -> OnReload");
            OnReload?.Invoke();
        }

        private void CancelItemIntentForWeaponSlot(int slot)
        {
            bool hasItemIntent = _itemSelectionSystem != null && _itemSelectionSystem.HasSelection;
            bool isUsingItem = _itemUseSystem != null && _itemUseSystem.IsUsingItem;
            if (!isUsingItem && !hasItemIntent)
                return;

            Debug.Log($"[CombatInputHandler] Weapon slot {slot}: cancelling item intent before switching. isUsing={isUsingItem} hasSelection={hasItemIntent}");
            Debug.Log($"[NH_FLOW][31][WeaponSlot.CancelItemIntent] slot={slot} isUsing={isUsingItem} hasSelection={hasItemIntent} {DescribeCombatFlowState()}");
            _itemSelectionSystem?.RequestCancelSelection();
        }

        private bool IsWeaponSlotActive(int slot)
        {
            var activeSlot = _weaponSystem?.GetActiveWeaponSlot();
            return activeSlot.HasValue && SlotTypeToIndex(activeSlot.Value) == slot;
        }

        private static int SlotTypeToIndex(WeaponSlotType slotType) => slotType switch
        {
            WeaponSlotType.Primary => 0,
            WeaponSlotType.Secondary => 1,
            WeaponSlotType.Melee => 2,
            WeaponSlotType.Slot3 => 3,
            WeaponSlotType.Slot4 => 4,
            _ => 0
        };

        /// <summary>
        /// Override aim direction from mobile drag (FireButton.OnDrag).
        ///
        /// Call with active=true + dir = drag world direction while dragging.
        /// Call with active=false on finger lift to revert to mouse raycast.
        ///
        /// NOTE: _mobileAimActive = true also makes GetAimDirection() expose the direction
        /// even when _isFiring = false — this allows aim preview before shooting.
        /// </summary>
        public void SetMobileAimDirection(Vector3 dir, bool active = true)
        {
            _mobileAimActive    = active && dir.sqrMagnitude > 0.001f;
            _mobileAimDirection = active ? dir.normalized : Vector3.zero;
        }

        /// <summary>
        /// Called by UI buttons (WeaponSlotButton etc.) on PointerDown.
        /// Prevents the concurrent Input System LMB-performed event from triggering BeginFire.
        /// Clears automatically on the next fire event.
        /// </summary>
        public void NotifyUIConsumedPress()
        {
            _uiConsumedThisPress = true;
            _suppressNextFireCanceled = true;
            Debug.Log($"[NH_FLOW][08][UIConsumedPress] {DescribeCombatFlowState()}");
        }

        /// <summary>
        /// Returns true if the mouse cursor or any active touch is currently over a UI element.
        /// Used to prevent aim direction from snapping to UI panel screen positions.
        /// </summary>
        private static bool IsPointerOverAnyUI()
        {
            if (EventSystem.current == null) return false;
            if (EventSystem.current.IsPointerOverGameObject()) return true;
            for (int i = 0; i < UnityEngine.Input.touchCount; i++)
                if (EventSystem.current.IsPointerOverGameObject(UnityEngine.Input.GetTouch(i).fingerId))
                    return true;
            return false;
        }

        private void PrepareImmediateFireAim(string reason)
        {
            Vector3 flatAim = _aimDirection;
            flatAim.y = 0f;
            if (flatAim.sqrMagnitude <= 0.001f)
            {
                Transform source = _playerTransform != null ? _playerTransform : transform;
                flatAim = source.forward;
                flatAim.y = 0f;
            }

            if (flatAim.sqrMagnitude <= 0.001f)
                return;

            flatAim.Normalize();
            _weaponSystem?.SetAimDirection(flatAim);

            Transform player = _playerTransform != null ? _playerTransform : transform;
            player.rotation = Quaternion.LookRotation(flatAim, Vector3.up);
            Debug.Log($"[WEAPON_FLOW] [01][InputAimReady] reason={reason} aim={flatAim:F2} activeWeapon={_weaponSystem?.GetActiveWeaponSlot()?.ToString() ?? "none"}");
            PhaseTestLog.Log(
                PhaseTestLogCategory.Input,
                "InputAimReady",
                $"reason={reason} aim={flatAim:F2} activeWeapon={_weaponSystem?.GetActiveWeaponSlot()?.ToString() ?? "none"} player={player.position:F2}",
                this);
        }

        private Vector3 GetCurrentThrowAimTarget()
        {
            if (_aimSystem != null)
            {
                Vector3 aimGround = _aimSystem.FinalAimGroundPos;
                if (aimGround.sqrMagnitude > 0.0001f)
                    return FlattenAimTarget(aimGround);
            }

            return FlattenAimTarget(_lastGroundHitPoint);
        }

        private string DescribeCombatFlowState()
        {
            var layers = InputLayerManager.Instance;
            var selectedItem = _itemSelectionSystem?.SelectedItem;
            var selectedDef = selectedItem != null ? ItemDatabase.GetDefinition(selectedItem.DefinitionID) : null;
            var currentItem = _itemUseSystem?.CurrentItem;
            var currentDef = currentItem != null ? ItemDatabase.GetDefinition(currentItem.DefinitionID) : null;
            return $"inputEnabled={_inputEnabled} inputState={layers?.CurrentState.ToString() ?? "null"} layers={(layers != null ? layers.ActiveLayers.ToString() : "null")} " +
                   $"isFiring={_isFiring} pendingSingle={_pendingSingleShotOnRelease} mobileAim={_mobileAimActive} " +
                   $"activeWeapon={_weaponSystem?.GetActiveWeaponSlot()?.ToString() ?? "none"} " +
                   $"hasSelection={_itemSelectionSystem?.HasSelection.ToString() ?? "null"} selected={selectedItem?.InstanceID ?? "null"} selectedDef={selectedDef?.ItemID ?? "null"} selectedType={selectedDef?.Type.ToString() ?? "null"} " +
                   $"itemUsing={_itemUseSystem?.IsUsingItem.ToString() ?? "null"} deploying={_itemUseSystem?.IsDeploying.ToString() ?? "null"} currentItem={currentItem?.InstanceID ?? "null"} currentDef={currentDef?.ItemID ?? "null"} currentType={currentDef?.Type.ToString() ?? "null"} " +
                   $"aim={_aimDirection:F2} ground={_lastGroundHitPoint:F2}";
        }

        private Vector3 FlattenAimTarget(Vector3 worldPos)
        {
            Vector3 origin = _playerTransform != null
                ? _playerTransform.position
                : transform.position;
            return new Vector3(worldPos.x, origin.y, worldPos.z);
        }

        private bool IsCurrentItemThrowable()
        {
            if (_itemUseSystem == null || !_itemUseSystem.IsUsingItem)
                return false;

            var item = _itemUseSystem.CurrentItem;
            var def = item != null ? ItemDatabase.GetDefinition(item.DefinitionID) : null;
            return def != null && def.Type == ItemType.Throwable;
        }

        private static bool IsPointerOverAnyUIByRaycast()
        {
            if (EventSystem.current == null)
                return false;

            var pointer = BuildCurrentPointerEventData();
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, results);
            return HasBlockingUiTarget(results);
        }

        private bool TryRouteFirePressToCombatUi()
        {
            if (EventSystem.current == null)
                return false;

            var pointer = BuildCurrentPointerEventData();
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, results);

            for (int i = 0; i < results.Count; i++)
            {
                var go = results[i].gameObject;
                if (go == null)
                    continue;

                var itemFilter = go.GetComponentInParent<ItemFilterButton>();
                if (itemFilter != null)
                {
                    Debug.Log($"[CombatInputHandler] Fire over UI routed to ItemFilterButton target={go.name} root={itemFilter.name}");
                    itemFilter.OnPointerDown(pointer);
                    _uiConsumedThisPress = true;
                    return true;
                }

                var selectable = go.GetComponentInParent<SelectableItemButton>();
                if (selectable != null)
                {
                    Debug.Log($"[CombatInputHandler] Fire over UI routed to SelectableItemButton target={go.name} root={selectable.name}");
                    selectable.OnPointerDown(pointer);
                    _uiConsumedThisPress = true;
                    return true;
                }
            }

            return false;
        }

        private static PointerEventData BuildCurrentPointerEventData()
        {
            return new PointerEventData(EventSystem.current)
            {
                position = Mouse.current != null
                    ? Mouse.current.position.ReadValue()
                    : (Vector2)UnityEngine.Input.mousePosition
            };
        }

        private static string DescribePointerRaycastTargets()
        {
            if (EventSystem.current == null)
                return "EventSystem=null";

            var pointer = BuildCurrentPointerEventData();

            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, results);
            if (results.Count == 0)
                return "none";

            int count = Mathf.Min(results.Count, 5);
            var parts = new string[count];
            for (int i = 0; i < count; i++)
                parts[i] = results[i].gameObject != null ? results[i].gameObject.name : "null";
            return string.Join(" > ", parts);
        }

        private static bool HasBlockingUiTarget(List<RaycastResult> results)
        {
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

        /// <summary>
        /// Called by the FireButton virtual joystick while dragging.
        /// <paramref name="joystick01"/> is a camera-relative XZ vector with magnitude [0,1]:
        ///   • character rotates toward the direction
        ///   • AimSystem cursor placed at magnitude × VisionRange (not always at the edge)
        /// Call with active=false on finger lift to restore normal mouse aim.
        /// </summary>
        public void SetFireMobileJoystick(Vector2 joystick01, bool active)
        {
            if (active)
            {
                _mobileFirePressHadDrag = true;
                _mobileFirePressReleaseMagnitude = joystick01.magnitude;
            }

            if (active && joystick01.sqrMagnitude > 0.001f)
            {
                _mobileAimActive    = true;
                // Store raw joystick01 WITH magnitude so cursor placement is proportional.
                // (joystickMagnitude 0.5 → cursor at 50% of VisionRange, not at the edge)
                _mobileJoystick01   = joystick01;
                _mobileAimDirection = new Vector3(joystick01.x, 0f, joystick01.y).normalized; // rotation only
                _aimSystem?.SetThrowableAim(joystick01);   // immediate update; also refreshed in UpdateAimDirection
                ApplyMobileAimImmediately();

                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableThrowableDebugLogs)
                    Debug.Log($"[FireButton] SetFireMobileJoystick active - joystick01={joystick01:F2} mag={joystick01.magnitude:F2}  " +
                              $"aimPos={_aimSystem?.FinalAimPos:F2}  dir={_mobileAimDirection:F2}");
            }
            else
            {
                _mobileAimActive    = false;
                _mobileAimDirection = Vector3.zero;
                _mobileJoystick01   = Vector2.zero;
                _aimSystem?.SetThrowableAim(Vector2.zero); // revert AimSystem to mouse aim
            }
        }

        private void ApplyMobileAimImmediately()
        {
            if (_mobileAimDirection.sqrMagnitude <= 0.001f)
                return;

            _aimDirection = _mobileAimDirection;
            _weaponSystem?.SetAimDirection(_aimDirection);

            Transform player = _playerTransform != null ? _playerTransform : transform;
            Vector3 flatAim = _aimDirection;
            flatAim.y = 0f;
            if (flatAim.sqrMagnitude <= 0.001f)
                return;

            player.rotation = Quaternion.LookRotation(flatAim.normalized, Vector3.up);
        }

        /// <summary>
        /// Bind all refs required for the fire flow.
        /// Call once after local player spawns (inside NetworkPlayer.EnableInput).
        ///
        /// Params:
        ///   movementInputHandler  — to force STRAFE while firing
        ///   weaponSystem          — to push aim direction into the weapon
        ///   playerTransform       — origin for ground-plane aim raycast
        ///   cameraStateManager    — to freeze/unfreeze the camera while firing
        /// </summary>
        public void BindCombatSystems(
            MovementInputHandler movementInputHandler,
            NightHunt.GameplaySystems.Core.Interfaces.IWeaponSystem weaponSystem,
            Transform playerTransform = null,
            NightHunt.Gameplay.Camera.CameraStateManager cameraStateManager = null)
        {
            // Unsubscribe old weapon system before replacing (safe rebind).
            if (_weaponSystem != null)
            {
                OnFire     -= _weaponSystem.StartFire;
                OnFireStop -= _weaponSystem.StopFire;
                OnReload   -= _weaponSystem.RequestReload;
                _weaponSystem.OnReloadStateChanged -= SetReloading;
            }
            // Unsubscribe slot selection handler from previous weapon system.
            if (_weaponSlotSelectionHandler != null)
                OnWeaponSlotChanged -= _weaponSlotSelectionHandler;

            _movementInputHandler = movementInputHandler;
            _weaponSystem         = weaponSystem;
            _playerTransform      = playerTransform;
            _cameraStateManager   = cameraStateManager;

            // Wire combat events → weapon system so button / keyboard fire actually shoots.
            if (_weaponSystem != null)
            {
                OnFire     += _weaponSystem.StartFire;
                OnFireStop += _weaponSystem.StopFire;
                OnReload   += _weaponSystem.RequestReload;
                _weaponSystem.OnReloadStateChanged += SetReloading;
            }

            // Wire hotkeys 1/2/3 → WeaponSystem.SelectWeapon(slot).
            // Without this, SwitchToWeaponSlot() fires OnWeaponSlotChanged(int) but nothing calls SelectWeapon.
            _weaponSlotSelectionHandler = slot =>
            {
                var slotType = slot switch
                {
                    0 => NightHunt.GameplaySystems.Core.Data.WeaponSlotType.Primary,
                    1 => NightHunt.GameplaySystems.Core.Data.WeaponSlotType.Secondary,
                    2 => NightHunt.GameplaySystems.Core.Data.WeaponSlotType.Melee,
                    3 => NightHunt.GameplaySystems.Core.Data.WeaponSlotType.Slot3,
                    4 => NightHunt.GameplaySystems.Core.Data.WeaponSlotType.Slot4,
                    _ => NightHunt.GameplaySystems.Core.Data.WeaponSlotType.Primary
                };
                _weaponSystem?.SelectWeapon(slotType);
            };
            OnWeaponSlotChanged += _weaponSlotSelectionHandler;

            if (_cameraStateManager == null)
                Debug.LogWarning("[CombatInputHandler] CameraStateManager not bound — " +
                                 "camera will NOT freeze during fire.");
        }

        /// <summary>
        /// Optional: bind AimSystem so the mobile-aim world cursor stays in sync with
        /// fire drag direction. Call after BindCombatSystems when the local player spawns.
        /// </summary>
        public void BindAimSystem(NightHunt.GameplaySystems.Core.Interfaces.IAimSystem aimSystem)
        {
            _aimSystem = aimSystem;
        }

        /// <summary>
        /// Bind ItemUseSystem so fire input during throwable mode calls ExecuteThrow instead of shooting.
        /// Call after BindCombatSystems when the local player spawns.
        /// </summary>
        public void BindItemUseSystem(IItemUseSystem itemUseSystem)
        {
            _itemUseSystem = itemUseSystem;
        }

        /// <summary>
        /// Bind the item selection system so BeginFire can call UseSelectedItem
        /// when a consumable or throwable is selected.
        /// </summary>
        public void BindItemSelectionSystem(NightHunt.GameplaySystems.Core.Interfaces.IItemSelectionSystem itemSelectionSystem)
        {
            _itemSelectionSystem = itemSelectionSystem;
        }

        /// <summary>
        /// Expose MovementInputHandler.SetCameraLockOverride so ThrowableAimController can
        /// force STRAFE mode while the player is in throwable aim mode (mirrors BeginFire).
        /// </summary>
        public void SetCameraLockOverride(bool active, bool forcedValue)
        {
            _movementInputHandler?.SetCameraLockOverride(active, forcedValue);
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
