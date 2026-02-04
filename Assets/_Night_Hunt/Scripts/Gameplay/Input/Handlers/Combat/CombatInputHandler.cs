using UnityEngine;
using UnityEngine.InputSystem;
using NightHunt.Gameplay.Input.Core;

namespace NightHunt.Gameplay.Input.Handlers.Combat
{
    /// <summary>
    /// Handles ONLY combat input (Attack, Aim, Reload, Weapon Switch)
    /// Separated from movement and interaction for clean architecture
    /// Components read input values from this handler via InputManager
    /// </summary>
    public class CombatInputHandler : MonoBehaviour, IInputHandler
    {
        private InputActionMap combatActionMap;
        private InputAction fireAction;
        private InputAction aimAction;
        private InputAction reloadAction;
        private InputAction weaponSlot1Action;
        private InputAction weaponSlot2Action;
        private InputAction weaponSlot3Action;
        private InputAction throwGrenadeAction;

        // Current state
        private bool isFiring;
        private bool isAiming;
        private bool isReloading;
        private int currentWeaponSlot = 0;

        // Camera for aim direction
        private UnityEngine.Camera playerCamera;
        private Vector3 aimDirection;

        private bool inputEnabled = false;

        // Events
        public event System.Action OnFire;
        public event System.Action OnFireStop;
        public event System.Action OnAimStart;
        public event System.Action OnAimStop;
        public event System.Action OnReload;
        public event System.Action<int> OnWeaponSlotChanged;
        public event System.Action OnThrowGrenade;

        #region Lifecycle

        private void Awake()
        {
            InitializeActions();
            playerCamera = UnityEngine.Camera.main;
        }

        private void OnEnable()
        {
            RegisterWithManager();
        }

        private void OnDisable()
        {
            DisableInput();
            UnregisterFromManager();
        }

        private void Update()
        {
            if (!inputEnabled) return;

            UpdateAimDirection();
        }

        #endregion

        #region Initialization

        private void InitializeActions()
        {
            if (InputLayerManager.Instance == null)
            {
                Debug.LogError("[CombatInputHandler] InputLayerManager.Instance is null!");
                return;
            }

            combatActionMap = InputLayerManager.Instance.CombatMap;

            if (combatActionMap != null)
            {
                fireAction = combatActionMap.FindAction("Fire");
                aimAction = combatActionMap.FindAction("AimDownSights");
                reloadAction = combatActionMap.FindAction("Reload");
                weaponSlot1Action = combatActionMap.FindAction("WeaponSlot1");
                weaponSlot2Action = combatActionMap.FindAction("WeaponSlot2");
                weaponSlot3Action = combatActionMap.FindAction("WeaponSlot3");
                throwGrenadeAction = combatActionMap.FindAction("ThrowGrenade");
            }
            else
            {
                Debug.LogError("[CombatInputHandler] 'Combat' action map not found!");
            }
        }

        private void RegisterWithManager()
        {
            InputLayerManager.Instance?.RegisterHandler(this);
        }

        private void UnregisterFromManager()
        {
            InputLayerManager.Instance?.UnregisterHandler(this);
        }

        #endregion

        #region IInputHandler Implementation

        public bool IsInputEnabled => inputEnabled;

        public InputActionMap GetActionMap() => combatActionMap;

        public void EnableInput()
        {
            if (inputEnabled) return;

            inputEnabled = true;

            // Subscribe to events
            if (fireAction != null)
            {
                fireAction.performed += OnFirePerformed;
                fireAction.canceled += OnFireCanceled;
            }

            if (aimAction != null)
            {
                aimAction.performed += OnAimPerformed;
                aimAction.canceled += OnAimCanceled;
            }

            if (reloadAction != null)
            {
                reloadAction.performed += OnReloadPerformed;
            }

            if (weaponSlot1Action != null)
                weaponSlot1Action.performed += ctx => SwitchToWeaponSlot(0);

            if (weaponSlot2Action != null)
                weaponSlot2Action.performed += ctx => SwitchToWeaponSlot(1);

            if (weaponSlot3Action != null)
                weaponSlot3Action.performed += ctx => SwitchToWeaponSlot(2);

            if (throwGrenadeAction != null)
                throwGrenadeAction.performed += OnThrowGrenadePerformed;

            Debug.Log("[CombatInputHandler] Input enabled");
        }

        public void DisableInput()
        {
            if (!inputEnabled) return;

            inputEnabled = false;

            // Unsubscribe
            if (fireAction != null)
            {
                fireAction.performed -= OnFirePerformed;
                fireAction.canceled -= OnFireCanceled;
            }

            if (aimAction != null)
            {
                aimAction.performed -= OnAimPerformed;
                aimAction.canceled -= OnAimCanceled;
            }

            if (reloadAction != null)
            {
                reloadAction.performed -= OnReloadPerformed;
            }

            if (weaponSlot1Action != null)
                weaponSlot1Action.performed -= ctx => SwitchToWeaponSlot(0);

            if (weaponSlot2Action != null)
                weaponSlot2Action.performed -= ctx => SwitchToWeaponSlot(1);

            if (weaponSlot3Action != null)
                weaponSlot3Action.performed -= ctx => SwitchToWeaponSlot(2);

            if (throwGrenadeAction != null)
                throwGrenadeAction.performed -= OnThrowGrenadePerformed;

            // Reset state
            isFiring = false;
            isAiming = false;
            isReloading = false;

            Debug.Log("[CombatInputHandler] Input disabled");
        }

        #endregion

        #region Aim Direction

        private void UpdateAimDirection()
        {
            if (playerCamera == null) return;

            // For top-down: aim at mouse world position
            Ray ray = playerCamera.ScreenPointToRay(UnityEngine.Input.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, transform.position);

            if (groundPlane.Raycast(ray, out float distance))
            {
                Vector3 hitPoint = ray.GetPoint(distance);
                aimDirection = (hitPoint - transform.position).normalized;
                aimDirection.y = 0; // Keep horizontal
            }
        }

        #endregion

        #region Input Event Handlers

        private void OnFirePerformed(InputAction.CallbackContext ctx)
        {
            isFiring = true;
            OnFire?.Invoke();
        }

        private void OnFireCanceled(InputAction.CallbackContext ctx)
        {
            isFiring = false;
            OnFireStop?.Invoke();
        }

        private void OnAimPerformed(InputAction.CallbackContext ctx)
        {
            isAiming = true;
            OnAimStart?.Invoke();
        }

        private void OnAimCanceled(InputAction.CallbackContext ctx)
        {
            isAiming = false;
            OnAimStop?.Invoke();
        }

        private void OnReloadPerformed(InputAction.CallbackContext ctx)
        {
            if (!isReloading)
            {
                isReloading = true;
                OnReload?.Invoke();
            }
        }

        private void SwitchToWeaponSlot(int slot)
        {
            if (currentWeaponSlot != slot)
            {
                currentWeaponSlot = slot;
                OnWeaponSlotChanged?.Invoke(slot);
            }
        }

        private void OnThrowGrenadePerformed(InputAction.CallbackContext ctx)
        {
            OnThrowGrenade?.Invoke();
        }

        #endregion

        #region Public API

        public bool IsFiring() => isFiring;
        public bool IsAiming() => isAiming;
        public bool IsReloading() => isReloading;
        public Vector3 GetAimDirection() => aimDirection;
        public int GetCurrentWeaponSlot() => currentWeaponSlot;

        public void SetReloading(bool reloading) => isReloading = reloading;

        #endregion
    }
}