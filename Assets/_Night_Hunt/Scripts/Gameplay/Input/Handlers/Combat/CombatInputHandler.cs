using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Gameplay.Input.Handlers.Movement;

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
    ///     Mouse Move (while held)    → UpdateAimDirection() cập nhật _aimDirection
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
        private bool    _inputEnabled = false;
        // ── Camera ref (PC raycast) ───────────────────────────────────────────────
        private UnityEngine.Camera _playerCamera;

        // ── MOBA feedback refs (optional) ─────────────────────────────────────────
        private NightHunt.GameplaySystems.UI.Combat.RangeIndicator _rangeIndicator;
        private NightHunt.GameplaySystems.UI.Combat.FireButton      _fireButton;

        // ── Combat system refs ────────────────────────────────────────────────────
        private MovementInputHandler _movementInputHandler;
        private NightHunt.GameplaySystems.Core.Interfaces.IWeaponSystem _weaponSystem;
        /// <summary>Local player transform — origin cho ground-plane aim raycast.</summary>
        private Transform _playerTransform;
        /// <summary>Camera lock state trước khi fire — restored khi EndFire.</summary>
        private bool _prevCameraLockBeforeFire;

        // ── Camera freeze refs ────────────────────────────────────────────────────
        private NightHunt.Gameplay.Camera.CameraStateManager _cameraStateManager;
        /// <summary>Camera state trước khi fire — restored khi EndFire.</summary>
        private NightHunt.Gameplay.Camera.CameraState _prevCameraStateBeforeFire;

        // ── Mobile aim override ───────────────────────────────────────────────────
        /// <summary>
        /// True khi mobile FireButton đang drag.
        /// Khi true, _aimDirection được set bởi FireButton.OnDrag thay vì mouse raycast.
        /// </summary>
        private bool    _mobileAimActive;
        private Vector3 _mobileAimDirection;

        // ── Events ────────────────────────────────────────────────────────────────
        public event System.Action       OnFire;
        public event System.Action       OnFireStop;
        public event System.Action       OnAimStart;
        public event System.Action       OnAimStop;
        public event System.Action       OnReload;
        public event System.Action<int>  OnWeaponSlotChanged;
        public event System.Action       OnThrowGrenade;

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
            // NHƯNG GetAimDirection() chỉ expose giá trị này khi _isFiring = true.
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
                _switchWeaponAction  = _combatActionMap.FindAction("SwitchWeapon");
                // MousePosition action mới thêm vào .inputactions
                _mousePositionAction = _combatActionMap.FindAction("MousePosition");
            }
            else
            {
                Debug.LogError("[CombatInputHandler] 'Combat' action map không tìm thấy!");
            }
        }

        // ── IInputHandler Implementation ──────────────────────────────────────────

        public void EnableInput()
        {
            if (_inputEnabled) return;
            if (_combatActionMap == null) InitializeActions();
            if (_combatActionMap == null)
            {
                Debug.LogError("[CombatInputHandler] CombatActionMap is null — không thể EnableInput!");
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
        /// Luôn cập nhật _aimDirection nội bộ mỗi frame.
        ///
        /// PC:     Ground-plane raycast từ mouse position.
        /// Mobile: Direction được set bởi FireButton.OnDrag via SetMobileAimDirection().
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

                // Đọc vị trí chuột từ MousePosition action nếu có, fallback về legacy Input
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
                    Vector3 dir       = hitPoint - groundOrigin;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.001f)
                        _aimDirection = dir.normalized;
                }
            }

            // Luôn push aim direction vào WeaponSystem để đạn bay đúng hướng.
            // WeaponSystem cần direction ngay cả khi chưa bắn (preview trajectory, v.v.)
            _weaponSystem?.SetAimDirection(_aimDirection);
        }

        // ── Fire Callbacks ────────────────────────────────────────────────────────

        private void OnFirePerformed(InputAction.CallbackContext ctx)
        {
            BeginFire(); // Mouse Down
        }
        
        private void OnFireCanceled(InputAction.CallbackContext ctx)
        {
            EndFire(); // Mouse Up
        }

        // ── Core Fire Logic ───────────────────────────────────────────────────────

        /// <summary>
        /// Bắt đầu bắn — gọi từ Mouse Down (PC) hoặc PointerDown (Mobile/Button).
        ///
        /// FLOW:
        ///   1. Freeze camera → disable CinemachineInputAxisController
        ///   2. Force STRAFE → character face cursor khi WASD
        ///   3. Từ thời điểm này: GetAimDirection() ≠ zero → GatherInput() dùng cursor aim
        /// </summary>
        private void BeginFire()
        {
            if (_isFiring) return;
            _isFiring = true;

            // ── Bước 1: Freeze camera tại góc hiện tại ────────────────────────────
            // Lưu state để restore khi EndFire.
            // ForceState(Locked) → disable CinemachineInputAxisController → camera đứng yên.
            // Camera đứng yên + character xoay theo cursor = MOBA aim feel.
            if (_cameraStateManager != null)
            {
                _prevCameraStateBeforeFire = _cameraStateManager.CurrentState;
                _cameraStateManager.ForceState(NightHunt.Gameplay.Camera.CameraState.Locked);
            }

            // ── Bước 2: Force STRAFE mode trong movement ──────────────────────────
            // _cameraLocked = true → SimulateMovement dùng STRAFE:
            //   character model xoay nhìn theo _aimYaw (cursor direction)
            //   WASD di chuyển relative với góc camera đang bị freeze
            if (_movementInputHandler != null)
            {
                _prevCameraLockBeforeFire = _movementInputHandler.IsCameraLocked();
                _movementInputHandler.SetCameraLockOverride(active: true, forcedValue: true);
            }

            // ── Bước 3: Kể từ đây GetAimDirection() ≠ zero ───────────────────────
            // _isFiring = true → GetAimDirection() expose _aimDirection
            // GatherInput() Priority 1 sẽ resolve → _aimYaw = cursor direction
            // → character xoay nhìn theo cursor

            OnFire?.Invoke();
            _rangeIndicator?.Show();
        }

        /// <summary>
        /// Dừng bắn — gọi từ Mouse Up (PC) hoặc PointerUp (Mobile/Button).
        ///
        /// FLOW:
        ///   1. GetAimDirection() trả zero → GatherInput() fallback về camera yaw
        ///   2. Restore movement lock state
        ///   3. Restore camera state (re-enable CinemachineInputAxisController nếu trước đó Free)
        /// </summary>
        private void EndFire()
        {
            if (!_isFiring) return;
            _isFiring = false;

            // ── Bước 1: _isFiring = false → GetAimDirection() trả zero ───────────
            // GatherInput() Priority 1 fail → fallback về camera yaw
            // → character không còn bị kéo nhìn theo cursor

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

            // Reset mobile aim nếu còn active
            _mobileAimActive    = false;
            _mobileAimDirection = Vector3.zero;

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

        private void SwitchToWeaponSlot(int slot)
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
        /// Gọi SimulateFire(true) = PointerDown → BeginFire()
        /// Gọi SimulateFire(false) = PointerUp  → EndFire()
        /// </summary>
        public void SimulateFire(bool start)
        {
            if (!_inputEnabled) return;
            if (start) BeginFire(); else EndFire();
        }

        /// <summary>
        /// Override aim direction từ mobile drag (FireButton.OnDrag).
        ///
        /// Gọi với active=true + dir = drag world direction khi đang drag.
        /// Gọi với active=false khi finger lift để revert về mouse raycast.
        ///
        /// NOTE: _mobileAimActive = true cũng khiến GetAimDirection() expose direction
        /// ngay cả khi _isFiring = false — điều này cho phép preview aim trước khi bắn.
        /// </summary>
        public void SetMobileAimDirection(Vector3 dir, bool active = true)
        {
            _mobileAimActive    = active && dir.sqrMagnitude > 0.001f;
            _mobileAimDirection = active ? dir.normalized : Vector3.zero;
        }

        /// <summary>
        /// Bind tất cả refs cần thiết cho fire flow.
        /// Gọi một lần sau khi local player spawn (trong NetworkPlayer.EnableInput).
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
            _movementInputHandler = movementInputHandler;
            _weaponSystem         = weaponSystem;
            _playerTransform      = playerTransform;
            _cameraStateManager   = cameraStateManager;

            if (_cameraStateManager == null)
                Debug.LogWarning("[CombatInputHandler] CameraStateManager not bound — " +
                                 "camera will NOT freeze during fire.");
        }
    }
}