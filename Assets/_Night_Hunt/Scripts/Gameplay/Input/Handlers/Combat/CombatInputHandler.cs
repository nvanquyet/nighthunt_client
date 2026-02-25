using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using NightHunt.Gameplay.Input.Core;

namespace NightHunt.Gameplay.Input.Handlers.Combat
{
    /// <summary>
    /// Handles ONLY combat input (Fire, Aim, Reload, WeaponSlots, Grenade).
    /// <para>Action map "Combat" được <see cref="InputLayerManager"/> bật/tắt theo context.
    /// Handler chỉ subscribe/unsubscribe callbacks.</para>
    ///
    /// <b>Quan trọng:</b> Khi <see cref="InputState.InventoryOpen"/> hoặc bất kỳ UI context
    /// nào được active, InputLayerManager sẽ disable ActionMap "Combat" → các action
    /// KHÔNG bao giờ fire, kể cả click chuột trái.
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
        private InputAction _switchWeaponAction; // scroll wheel switch

        // ── Delegate fields (để unsubscribe đúng, tránh lambda leak) ─────────────
        private System.Action<InputAction.CallbackContext> _onSlot1;
        private System.Action<InputAction.CallbackContext> _onSlot2;
        private System.Action<InputAction.CallbackContext> _onSlot3;

        // ── State ─────────────────────────────────────────────────────────────────
        private bool _isFiring;
        private bool _isAiming;
        private bool _isReloading;
        private int  _currentWeaponSlot = 0;
        private UnityEngine.Camera _playerCamera;
        private Vector3 _aimDirection;
        private bool _inputEnabled = false;

        // ── Events ────────────────────────────────────────────────────────────────
        public event System.Action          OnFire;
        public event System.Action          OnFireStop;
        public event System.Action          OnAimStart;
        public event System.Action          OnAimStop;
        public event System.Action          OnReload;
        public event System.Action<int>     OnWeaponSlotChanged;
        public event System.Action          OnThrowGrenade;

        // ── IInputHandler ─────────────────────────────────────────────────────────
        public bool IsInputEnabled  => _inputEnabled;
        public InputActionMap GetActionMap() => _combatActionMap;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _playerCamera = UnityEngine.Camera.main;
            // Tạo delegate references cho weapon slots để unsubscribe đúng
            _onSlot1 = _ => SwitchToWeaponSlot(0);
            _onSlot2 = _ => SwitchToWeaponSlot(1);
            _onSlot3 = _ => SwitchToWeaponSlot(2);
            InitializeActions();
        }

        private void OnEnable()
        {
            InputLayerManager.Instance?.RegisterHandler(this);
        }

        private void OnDisable()
        {
            DisableInput();
            InputLayerManager.Instance?.UnregisterHandler(this);
        }

        private void Update()
        {
            if (!_inputEnabled) return;
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
            if (_combatActionMap == null) return;

            _inputEnabled = true;

            if (_fireAction != null)
            {
                _fireAction.performed += OnFirePerformed;
                _fireAction.canceled  += OnFireCanceled;
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

            // Reset transient state
            _isFiring   = false;
            _isAiming   = false;
            _isReloading = false;

            Debug.Log("[CombatInputHandler] Input disabled");
        }

        // ── Aim Direction ─────────────────────────────────────────────────────────

        private void UpdateAimDirection()
        {
            if (_playerCamera == null) return;

            Ray ray = _playerCamera.ScreenPointToRay(UnityEngine.Input.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, transform.position);

            if (groundPlane.Raycast(ray, out float distance))
            {
                Vector3 hitPoint = ray.GetPoint(distance);
                _aimDirection = (hitPoint - transform.position).normalized;
                _aimDirection.y = 0f;
            }
        }

        // ── Callbacks ─────────────────────────────────────────────────────────────

        private void OnFirePerformed(InputAction.CallbackContext ctx)
        {
            // Nếu pointer đang ở trên UI element → bỏ qua
            // (phòng thủ thứ 2 – phòng thủ chính là Combat ActionMap đã bị disable)
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            _isFiring = true;
            OnFire?.Invoke();
        }

        private void OnFireCanceled(InputAction.CallbackContext ctx)
        {
            _isFiring = false;
            OnFireStop?.Invoke();
        }

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
                SwitchToWeaponSlot((_currentWeaponSlot + 2) % 3); // wrap back
        }

        private void OnThrowGrenadePerformed(InputAction.CallbackContext ctx) => OnThrowGrenade?.Invoke();

        // ── Public API ────────────────────────────────────────────────────────────

        public bool IsFiring()          => _isFiring;
        public bool IsAiming()          => _isAiming;
        public bool IsReloading()       => _isReloading;
        public Vector3 GetAimDirection() => _aimDirection;
        public int GetCurrentWeaponSlot() => _currentWeaponSlot;
        public void SetReloading(bool v)  => _isReloading = v;
    }
}
