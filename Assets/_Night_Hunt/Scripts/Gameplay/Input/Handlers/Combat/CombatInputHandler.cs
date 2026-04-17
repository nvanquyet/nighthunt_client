using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Gameplay.Input.Handlers.Movement;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.GameplaySystems.UI.Combat;   // ItemAimController.IsAimingPC guard

namespace NightHunt.Gameplay.Input.Handlers.Combat
{
    /// <summary>
    /// Handles ONLY combat input (Fire, Aim, Reload, WeaponSlots, Grenade).
    ///
    /// FIRE FLOW (PC + Mobile đều dùng chung BeginFire/EndFire):
    ///
    ///   PC:
    ///     Mouse Down (LMB performed) → BeginFire()
    ///       → Freeze camera (CameraStateManager.ForceState Locked)
    ///       → Force STRAFE (MovementInputHandler.SetCameraLockOverride true)
    ///     Mouse Move (while held)    → UpdateAimDirection() update _aimDirection
    ///       → GetAimDirection() trả về _aimDirection (≠ zero vì _isFiring = true)
    ///       → GatherInput() dùng làm _aimYaw → character xoay nhìn theo cursor
    ///     Mouse Up (LMB canceled)    → EndFire()
    ///       → Restore camera state
    ///       → Restore movement lock state
    ///       → GetAimDirection() trả zero → _aimYaw fallback về camera yaw
    ///
    ///   Mobile (FireButton):
    ///     PointerDown  → SimulateFire(true)  → BeginFire() — same flow
    ///     OnDrag       → SetMobileAimDirection(worldDir) → _aimDirection = drag dir
    ///     PointerUp    → SimulateFire(false) → EndFire()  — same flow
    ///
    /// KEY RULE:
    ///   GetAimDirection() trả về Vector3.zero khi KHÔNG fire.
    ///   Điều này đảm bảo GatherInput() không dùng cursor direction khi chưa bắn.
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
        private InputAction _switchWeaponAction;
        private InputAction _mousePositionAction; // Track mouse position via Input System

        // ── Delegate fields (để unsubscribe đúng, tránh lambda leak) ─────────────
        private System.Action<InputAction.CallbackContext> _onSlot1;
        private System.Action<InputAction.CallbackContext> _onSlot2;
        private System.Action<InputAction.CallbackContext> _onSlot3;

        // ── State ─────────────────────────────────────────────────────────────────
        private bool    _isFiring;
        private bool    _isAiming;
        private bool    _isReloading;
        private int     _currentWeaponSlot = 0;
        private Vector3 _aimDirection;          // internal — luôn track cursor
        private Vector3 _lastGroundHitPoint;    // last cursor-to-ground hit (PC) — passed to RequestExecuteThrow
        private bool    _inputEnabled = false;
        private bool    _uiConsumedThisPress;   // set by WeaponSlotButton etc. to block the concurrent LMB fire event
        // ── Camera ref (PC raycast) ───────────────────────────────────────────────
        private UnityEngine.Camera _playerCamera;

        // ── MOBA feedback refs (optional) ─────────────────────────────────────────
        private NightHunt.GameplaySystems.UI.Combat.RangeIndicator _rangeIndicator;
        private NightHunt.GameplaySystems.UI.Combat.FireButton      _fireButton;

        // ── Combat system refs ────────────────────────────────────────────────────
        private MovementInputHandler _movementInputHandler;
        private NightHunt.GameplaySystems.Core.Interfaces.IWeaponSystem _weaponSystem;        private NightHunt.GameplaySystems.Core.Interfaces.IAimSystem _aimSystem;
        private IItemUseSystem _itemUseSystem;
        private NightHunt.GameplaySystems.Core.Interfaces.IItemSelectionSystem _itemSelectionSystem;        /// <summary>Local player transform — origin cho ground-plane aim raycast.</summary>
        private Transform _playerTransform;
        /// <summary>Camera lock state before fire — restored khi EndFire.</summary>
        private bool _prevCameraLockBeforeFire;

        // ── Camera freeze refs ────────────────────────────────────────────────────
        private NightHunt.Gameplay.Camera.CameraStateManager _cameraStateManager;
        /// <summary>Camera state before fire — restored khi EndFire.</summary>
        private NightHunt.Gameplay.Camera.CameraState _prevCameraStateBeforeFire;

        // ── Mobile aim override ───────────────────────────────────────────────────
        /// <summary>
        /// True khi mobile FireButton đang drag.
        /// Khi true, _aimDirection set bởi FireButton.OnDrag instead of mouse raycast.
        /// </summary>
        private bool    _mobileAimActive;
        private Vector3 _mobileAimDirection;   // normalised — dùng cho character rotation
        private Vector2 _mobileJoystick01;     // raw [0,1] với magnitude — dùng cho cursor placement

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
            // Camera.main có thể null ở đây (player chưa spawn).
            // UpdateAimDirection() refresh mỗi frame — không rely vào cache này.
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
            if (InputLayerManager.Instance != null)
                InputLayerManager.Instance.RegisterHandler(this);
        }

        private void OnDisable()
        {
            DisableInput();
            InputLayerManager.Instance?.UnregisterHandler(this);
        }

        private void Update()
        {
            // UpdateAimDirection luôn chạy để _aimDirection luôn = vị trí cursor hiện tại.
            // NHƯNG GetAimDirection() chỉ expose value này khi _isFiring = true.
            // → Khi không bắn: GatherInput() không nhận aim direction → _aimYaw fallback về camera yaw.
            // → Khi bắn: GatherInput() nhận aim direction → character xoay nhìn theo cursor.
            UpdateAimDirection();
        }

        // ── Initialization ────────────────────────────────────────────────────────

        private void InitializeActions()
        {
            if (InputLayerManager.Instance == null)
            {
                Debug.LogError("[CombatInputHandler] InputLayerManager.Instance là null!");
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
                _switchWeaponAction  = _combatActionMap.FindAction("SwitchWeapon");
                // MousePosition action mới thêm vào .inputactions
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
            // Press(behavior=2) trong .inputactions đảm bảo cả 2 event đều fire đúng.
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
            if (_switchWeaponAction != null) _switchWeaponAction.performed += OnSwitchWeaponPerformed;
            // MousePosition: không cần subscribe event — polling trong UpdateAimDirection() đủ.
            // Action chỉ cần được enable (handled by InputLayerManager).

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
            if (_switchWeaponAction != null) _switchWeaponAction.performed -= OnSwitchWeaponPerformed;

            // Nếu đang fire khi bị disable (ví dụ mở inventory), force EndFire để restore state.
            if (_isFiring) EndFire();

            _isFiring    = false;
            _isAiming    = false;
            _isReloading = false;

            Debug.Log("[CombatInputHandler] Input disabled");
        }

        // ── Aim Direction (Internal Tracking) ─────────────────────────────────────

        /// <summary>
        /// Luôn update _aimDirection nội bộ mỗi frame.
        ///
        /// PC:     Ground-plane raycast từ mouse position.
        /// Mobile: Direction set bởi FireButton.OnDrag via SetMobileAimDirection().
        ///
        /// QUAN TRỌNG: Hàm này chỉ update internal state.
        /// GetAimDirection() mới là public API — nó chỉ expose direction khi đang fire.
        /// </summary>
        private void UpdateAimDirection()
        {
            if (_mobileAimActive)
            {
                // Mobile: direction set bởi FireButton.OnDrag
                _aimDirection = _mobileAimDirection;
            }
            else
            {
                // PC: luôn refresh Camera.main để tránh stale reference (HOST spawn issue)
                var freshCam = UnityEngine.Camera.main;
                if (freshCam != null) _playerCamera = freshCam;
                if (_playerCamera == null) return;

                Vector3 groundOrigin = _playerTransform != null
                    ? _playerTransform.position
                    : transform.position;

                // Read vị trí chuột từ MousePosition action nếu có, fallback về legacy Input
                Vector2 mouseScreenPos;
                if (_mousePositionAction != null && _mousePositionAction.enabled)
                    mouseScreenPos = _mousePositionAction.ReadValue<Vector2>();
                else
                    mouseScreenPos = UnityEngine.Input.mousePosition;

                Ray   ray    = _playerCamera.ScreenPointToRay(mouseScreenPos);
                Plane ground = new Plane(Vector3.up, groundOrigin);

                if (ground.Raycast(ray, out float distance))
                {
                    Vector3 hitPoint  = ray.GetPoint(distance);

                    // When a throwable/deployable is armed via the FilterPanel path
                    // (not via ItemAimController.TryBeginAim), clamp the hit to the
                    // throwable vision range so the actual throw target matches the
                    // visual ring indicator. Also update ItemAimController's static
                    // AimWorldTarget so any aim cursor stays in sync.
                    if (_itemUseSystem != null && _itemUseSystem.IsUsingItem && !ItemAimController.IsAimingPC)
                    {
                        Vector3 offset   = hitPoint - groundOrigin;
                        float   maxRange = _aimSystem?.GetVisionRange() ?? 15f;
                        if (offset.sqrMagnitude > maxRange * maxRange)
                            hitPoint = groundOrigin + offset.normalized * maxRange;
                        ItemAimController.SetExternalAimTarget(hitPoint);
                    }

                    _lastGroundHitPoint = hitPoint;
                    Vector3 dir       = hitPoint - groundOrigin;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.001f)
                        _aimDirection = dir.normalized;
                }
            }

            // Luôn push aim direction vào WeaponSystem để đạn bay đúng hướng.
            // WeaponSystem cần direction ngay cả khi chưa bắn (preview trajectory, v.v.)
            _weaponSystem?.SetAimDirection(_aimDirection);

            // Mobile MOBA cursor sync:
            // Uses _mobileJoystick01 (giữ nguyên magnitude [0,1]) instead of _aimDirection (normalized).
            // -> Cursor đặt tại player + joystickDir * joystickMagnitude * visionRange.
            // -> joystick 50% ra đưa cursor đến 50% range — giống Mobile Legends.
            if (_mobileAimActive && _mobileJoystick01.sqrMagnitude > 0.001f)
                _aimSystem?.SetThrowableAim(_mobileJoystick01);
        }

        // ── Fire Callbacks ────────────────────────────────────────────────────────

        private void OnFirePerformed(InputAction.CallbackContext ctx)
        {
            // Skip if the pointer is currently over a UI element (e.g. FireButton, item selector).
            // Those buttons call SimulateFire() directly via EventSystem — the Input System
            // action must NOT also trigger BeginFire() for the same press.
            if (IsPointerOverAnyUI()) return;

            // Skip if a UI button consumed this press via NotifyUIConsumedPress().
            if (_uiConsumedThisPress) { _uiConsumedThisPress = false; return; }
            BeginFire(); // Mouse Down
        }

        private void OnFireCanceled(InputAction.CallbackContext ctx)
        {
            _uiConsumedThisPress = false;  // clear in case LMB-up fires after UI ate the down
            EndFire(); // Mouse Up
        }

        // ── Core Fire Logic ───────────────────────────────────────────────────────

        /// <summary>
        /// Bắt đầu bắn — called from Mouse Down (PC) hoặc PointerDown (Mobile/Button).
        ///
        /// FLOW:
        ///   1. Freeze camera → disable CinemachineInputAxisController
        ///   2. Force STRAFE → character face cursor khi WASD
        ///   3. Từ thời điểm này: GetAimDirection() ≠ zero → GatherInput() dùng cursor aim
        /// </summary>
        private void BeginFire()
        {
            if (_isFiring) return;

            // Throwable/deployable already armed: re-enter aim state so EndFire executes throw on release.
            if (_itemUseSystem != null && _itemUseSystem.IsUsingItem)
            {
                _isFiring = true;
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
                if (_rangeIndicator != null)
                    _rangeIndicator.ShowWithRange(_aimSystem?.GetVisionRange() ?? 15f);
                if (Application.isMobilePlatform)
                    _aimSystem?.SetCursorVisible(true);
                return;
            }

            // BUG 1 FIX: Weapon always fires first — even when a throwable is selected in the
            // HUD. To throw, players press the throwable HUD button (TryBeginAim) and then LMB.
            if (_weaponSystem != null && _weaponSystem.GetActiveWeaponSlot() != null)
            {
                _isFiring = true;

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

                OnFire?.Invoke();
                if (_rangeIndicator != null)
                    _rangeIndicator.ShowWithRange(_aimSystem?.GetVisionRange() ?? 15f);
                if (Application.isMobilePlatform)
                    _aimSystem?.SetCursorVisible(true);
                return;
            }

            // No weapon active: use selected item (throwable / consumable) on fire press.
            if (_itemSelectionSystem != null && _itemSelectionSystem.HasSelection)
            {
                var selectedItem = _itemSelectionSystem.SelectedItem;
                var def = selectedItem != null ? ItemDatabase.GetDefinition(selectedItem.DefinitionID) : null;
                bool isThrowableOrDeployable = def != null &&
                    (def.Type == ItemType.Throwable || def.Type == ItemType.Deployable);

                _itemSelectionSystem.UseSelectedItem();

                if (isThrowableOrDeployable)
                {
                    // Enter aim state — range indicator visible, camera locked, strafe on.
                    // Fire NOT invoked (weapon holstered). Throw executes on EndFire.
                    _isFiring = true;
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
                    if (_rangeIndicator != null)
                        _rangeIndicator.ShowWithRange(_aimSystem?.GetVisionRange() ?? 15f);
                    if (Application.isMobilePlatform)
                        _aimSystem?.SetCursorVisible(true);
                }
                // Consumable: UseSelectedItem() handles use immediately; no aim state needed.
                return;
            }
        }

        /// <summary>
        /// Stop bắn — called from Mouse Up (PC) hoặc PointerUp (Mobile/Button).
        ///
        /// FLOW:
        ///   1. GetAimDirection() trả zero → GatherInput() fallback về camera yaw
        ///   2. Restore movement lock state
        ///   3. Restore camera state (re-enable CinemachineInputAxisController nếu trước đó Free)
        /// </summary>
        private void EndFire()
        {
            // Execute throw on fire-release when a throwable is armed and in aim state.
            // BUG 2 FIX: pass the current cursor ground position so the server receives the
            // correct world target (ItemAimController.AimWorldTarget is client-only).
            if (_isFiring && _itemUseSystem != null && _itemUseSystem.IsUsingItem && !ItemAimController.IsAimingPC)
                _itemUseSystem.RequestExecuteThrow(_lastGroundHitPoint);

            if (!_isFiring) return;
            _isFiring = false;

            // ── Bước 1: _isFiring = false → GetAimDirection() trả zero ───────────
            // GatherInput() Priority 1 fail → fallback về camera yaw
            // → character no longer bị kéo nhìn theo cursor

            _rangeIndicator?.Hide();

            // ── Bước 2: Restore movement lock state ───────────────────────────────
            _movementInputHandler?.SetCameraLockOverride(
                active: false,
                forcedValue: _prevCameraLockBeforeFire);

            // ── Bước 3: Restore camera state ──────────────────────────────────────
            // Nếu trước đó Free → re-enable CinemachineInputAxisController → camera xoay tự do lại
            // Nếu trước đó Locked → giữ nguyên Locked
            if (_cameraStateManager != null)
                _cameraStateManager.ForceState(_prevCameraStateBeforeFire);

            // Reset mobile aim and throwable cursor (activated by SetFireMobileJoystick)
            _mobileAimActive    = false;
            _mobileAimDirection = Vector3.zero;
            _mobileJoystick01   = Vector2.zero;
            _aimSystem?.SetThrowableAim(Vector2.zero);  // exit throwable mode if joystick activated it

            // Mobile: hide world aim cursor on fire-stop.
            if (Application.isMobilePlatform)
                _aimSystem?.SetCursorVisible(false);

            OnFireStop?.Invoke();
        }

        // ── Other Input Callbacks ─────────────────────────────────────────────────

        private void OnAimPerformed(InputAction.CallbackContext ctx)  { _isAiming = true;  OnAimStart?.Invoke(); }
        private void OnAimCanceled(InputAction.CallbackContext ctx)   { _isAiming = false; OnAimStop?.Invoke();  }

        private void OnReloadPerformed(InputAction.CallbackContext ctx)
        {
            if (!_isReloading)
            {
                _isReloading = true;
                OnReload?.Invoke();
            }
        }

        public void SwitchToWeaponSlot(int slot)
        {
            if (_currentWeaponSlot != slot)
            {
                _currentWeaponSlot = slot;
                OnWeaponSlotChanged?.Invoke(slot);
            }
        }

        private void OnSwitchWeaponPerformed(InputAction.CallbackContext ctx)
        {
            float scroll = ctx.ReadValue<float>();
            if (scroll > 0f)
                SwitchToWeaponSlot((_currentWeaponSlot + 1) % 3);
            else if (scroll < 0f)
                SwitchToWeaponSlot((_currentWeaponSlot + 2) % 3);
        }

        private void OnThrowGrenadePerformed(InputAction.CallbackContext ctx) => OnThrowGrenade?.Invoke();
        private void OnConsumablePanelPerformed(InputAction.CallbackContext ctx) => OnConsumablePanel?.Invoke();

        // ── Public API ────────────────────────────────────────────────────────────

        public bool IsFiring()            => _isFiring;
        public bool IsAiming()            => _isAiming;
        public bool IsReloading()         => _isReloading;
        public int  GetCurrentWeaponSlot() => _currentWeaponSlot;
        public void SetReloading(bool v)  => _isReloading = v;

        /// <summary>
        /// Trả về aim direction CHỈ KHI đang fire (LMB held hoặc mobile button held).
        ///
        /// Trả về Vector3.zero khi không fire.
        /// → GatherInput() Priority 1 sẽ fail → _aimYaw fallback về camera yaw
        /// → Character không bị kéo nhìn theo cursor khi chưa bắn.
        ///
        /// Đây là KEY RULE của toàn bộ aim system.
        /// </summary>
        public Vector3 GetAimDirection()
        {
            // Chỉ expose direction khi đang fire hoặc mobile aim active
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
        /// Simulate fire từ on-screen button (mobile).
        /// Call SimulateFire(true) = PointerDown → BeginFire()
        /// Call SimulateFire(false) = PointerUp  → EndFire()
        /// </summary>
        public void SimulateFire(bool start)
        {
            if (!_inputEnabled) return;
            if (start)
            {
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

        /// <summary>
        /// Override aim direction từ mobile drag (FireButton.OnDrag).
        ///
        /// Call với active=true + dir = drag world direction khi đang drag.
        /// Call với active=false khi finger lift để revert về mouse raycast.
        ///
        /// NOTE: _mobileAimActive = true cũng khiến GetAimDirection() expose direction
        /// ngay cả khi _isFiring = false — điều này cho phép preview aim before bắn.
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
        public void NotifyUIConsumedPress() => _uiConsumedThisPress = true;

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

        /// <summary>
        /// Called by the FireButton virtual joystick while dragging.
        /// <paramref name="joystick01"/> is a camera-relative XZ vector with magnitude [0,1]:
        ///   • character rotates toward the direction
        ///   • AimSystem cursor placed at magnitude × VisionRange (not always at the edge)
        /// Call with active=false on finger lift to restore normal mouse aim.
        /// </summary>
        public void SetFireMobileJoystick(Vector2 joystick01, bool active)
        {
            if (active && joystick01.sqrMagnitude > 0.001f)
            {
                _mobileAimActive    = true;
                // Store raw joystick01 WITH magnitude so cursor placement is proportional.
                // (joystickMagnitude 0.5 → cursor at 50% of VisionRange, not at the edge)
                _mobileJoystick01   = joystick01;
                _mobileAimDirection = new Vector3(joystick01.x, 0f, joystick01.y).normalized; // rotation only
                _aimSystem?.SetThrowableAim(joystick01);   // immediate update; also refreshed in UpdateAimDirection

                Debug.Log($"[FireButton] SetFireMobileJoystick active — joystick01={joystick01:F2} mag={joystick01.magnitude:F2}  " +
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

        /// <summary>
        /// Bind tất cả refs cần thiết cho fire flow.
        /// Call một lần after local player spawn (trong NetworkPlayer.EnableInput).
        ///
        /// Params:
        ///   movementInputHandler  — để force STRAFE khi bắn
        ///   weaponSystem          — để push aim direction vào weapon
        ///   playerTransform       — origin cho ground-plane aim raycast
        ///   cameraStateManager    — để freeze/unfreeze camera khi bắn
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
            }

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
            }

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
        /// Expose MovementInputHandler.SetCameraLockOverride so ItemAimController can
        /// force STRAFE mode while the player is in throwable aim mode (mirrors BeginFire).
        /// </summary>
        public void SetCameraLockOverride(bool active, bool forcedValue)
        {
            _movementInputHandler?.SetCameraLockOverride(active, forcedValue);
        }
    }
}
