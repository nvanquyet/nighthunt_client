using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Gameplay.Input.Handlers.Camera;
using NightHunt.Gameplay.Input.Handlers.Movement;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.UI.Combat;
using NightHunt.GameplaySystems.Weapon;

namespace NightHunt.UI.Mobile
{
    /// <summary>
    /// MobileHUDPanel — single container for all on-screen mobile input controls.
    ///
    /// Replaces the previously scattered approach where standalone components like
    /// <c>MobileInventoryButton</c> each handled their own platform detection.
    ///
    /// RESPONSIBILITIES:
    ///   • Detect platform (or force mobile via Inspector flag) once in Awake.
    ///   • Show/hide the entire <see cref="_mobileRoot"/> in one call.
    ///   • Bind the inventory toggle button → <see cref="InputManager.InventoryHandler.SimulateToggle"/>.
    ///   • Bind the pinch-zoom bridge → <see cref="MobilePinchZoomBridge"/>.
    ///
    /// CHILD LAYOUT inside _mobileRoot:
    ///   MovementJoystick  (OnScreenStick, controlPath = "&lt;Gamepad&gt;/leftStick")
    ///   FireButton        (OnScreenButton — handled by CombatHUDPanel / FireButton script)
    ///   SprintButton      (OnScreenButton, controlPath = "&lt;Gamepad&gt;/leftShoulder")
    ///   CrouchButton      (OnScreenButton, controlPath = "&lt;Gamepad&gt;/rightShoulder")
    ///   JumpButton        (OnScreenButton, controlPath = "&lt;Gamepad&gt;/buttonSouth")
    ///   RollButton        (OnScreenButton, controlPath = "&lt;Gamepad&gt;/buttonWest")
    ///   ReloadButton      (OnScreenButton)
    ///   InventoryButton   → wired to _inventoryButton below
    ///   CameraDragArea    → MobileCameraDragArea component handles aim/look
    ///
    /// AIM DIRECTION (top-down game):
    ///   Camera drag area rotates the top-down view camera.
    ///   The weapon system reads the camera forward projected onto the XZ plane as
    ///   the aim direction — no separate joystick needed for aiming.
    ///   If tap-to-aim is preferred: tap position is raycasted against the ground
    ///   plane and the character faces the world hit point each frame.
    ///
    /// SETUP:
    ///   Attach to any child of HUDCanvas.
    ///   Wire _mobileRoot to the parent GO containing all mobile controls.
    ///   Wire _inventoryButton to the mobile inventory icon button.
    ///   Wire _pinchZoomBridge to the MobilePinchZoomBridge component (optional).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MobileHUDPanel : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Platform")]
        [Tooltip("Treat this panel as mobile even in the Editor — use for in-Editor testing.")]
        [SerializeField] private bool _forceMobileMode;

        [Tooltip("Root GameObject that contains all mobile-only controls. " +
                 "Shown on mobile / hidden on desktop.")]
        [SerializeField] private GameObject _mobileRoot;

        [Header("Controls (Mobile Only)")]
        [Tooltip("Fire Button script handling drag/aim and shoot.")]
        [SerializeField] private FireButton _fireButton;

        [Tooltip("Bridge that feeds the movement joystick into MovementInputHandler.")]
        [SerializeField] private MobileMovementBridge _movementBridge;

        [Tooltip("Movement joystick (FixedJoystick / DynamicJoystick) injected into MovementInputHandler each frame.")]
        [SerializeField] private Joystick _joystick;

        [Tooltip("Inventory Button. Mirrors the Tab key on desktop.")]
        [SerializeField] private Button _inventoryButton;

        [Tooltip("Sprint button. OnScreenButton controlPath = \"<Gamepad>/leftShoulder\".")]
        [SerializeField] private Button _sprintButton;

        [Tooltip("Crouch button. OnScreenButton controlPath = \"<Gamepad>/rightShoulder\".")]
        [SerializeField] private Button _crouchButton;

        [Tooltip("Jump button. OnScreenButton controlPath = \"<Gamepad>/buttonSouth\".")]
        [SerializeField] private Button _jumpButton;

        [Tooltip("Roll button. OnScreenButton controlPath = \"<Gamepad>/buttonWest\".")]
        [SerializeField] private Button _rollButton;

        [Tooltip("Reload button. OnScreenButton controlPath = \"<Gamepad>/buttonWest\".")]
        [SerializeField] private Button _reloadButton;

        [Header("Roll Cooldown Override")]
        [Tooltip("Same MovementSettings SO used by the gameplay character.")]
        [SerializeField] private NightHunt.Gameplay.Character.Movement.MovementSettings _movementSettings;
        
        [Tooltip("Filled Image on top of _rollButton used as radial cooldown indicator.")]
        [SerializeField] private Image _rollCooldownImage;

        [Header("Camera / Aim")]
        [Tooltip("MobileCameraDragArea handles looking/aiming.")]
        [SerializeField] private MobileCameraDragArea _cameraDragArea;

        [Tooltip("MobilePinchZoomBridge handles two-finger zoom on mobile. " +
                 "Leave null if the camera does not support zoom.")]
        [SerializeField] private MobilePinchZoomBridge _pinchZoomBridge;

        // ── Runtime ───────────────────────────────────────────────────────────

        /// <summary>True when the panel is treating the current runtime as mobile.</summary>
        public bool IsMobile => _forceMobileMode || PlatformManager.IsMobile;

        private float _rollCooldownRemaining;
        private UnityEngine.InputSystem.InputAction _rollAction;
        private InputManager _boundInput;
        private IWeaponSystem _boundWeaponSystem;
        private bool _lastReloadButtonShow;
        private bool _lastReloadButtonInteractable;
        private bool _reloadButtonMissingWarned;
        private float _nextReloadButtonRefreshTime;

        private float RollCooldownDuration =>
            (_movementSettings != null && _movementSettings.enableRoll)
                ? _movementSettings.rollDuration
                : 0.35f;

        // ── Unity Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            ApplyPlatformVisibility();
            ResolveMissingButtonReferences();

            if (_inventoryButton != null)
                _inventoryButton.onClick.AddListener(OnInventoryButtonClicked);

            WireActionButtons();
        }

        private void OnEnable()
        {
            ApplyPlatformVisibility();
            _rollCooldownRemaining = 0f;
            RefreshRollButton();
            if (IsMobile) SubscribeRollCooldown();
        }

        private void OnDisable()
        {
            UnsubscribeRollCooldown();
        }

        private void Update()
        {
            if (!IsMobile) return;
            TickRollCooldown();
            if (_joystick != null)
                _boundInput?.MovementHandler?.SetMobileMove(_joystick.Direction);

            if (Time.unscaledTime >= _nextReloadButtonRefreshTime)
            {
                _nextReloadButtonRefreshTime = Time.unscaledTime + 0.2f;
                RefreshReloadButtonVisibility();
            }
        }

        private void OnDestroy()
        {
            if (_inventoryButton != null)
                _inventoryButton.onClick.RemoveListener(OnInventoryButtonClicked);

            UnwireActionButtons();
            _pinchZoomBridge?.UnbindHandler();
            UnbindWeaponSystem();
        }

        // ── Public API (called by GameHUDController) ──────────────────────────

        /// <summary>
        /// Bind all mobile input handlers.
        /// Call from <see cref="NightHunt.UI.GameHUDController"/> after local player spawns.
        /// </summary>
        /// <param name="input">Active InputManager instance.</param>
        public void Bind(InputManager input)
        {
            _boundInput = input;

            if (_pinchZoomBridge != null && input?.CameraHandler != null)
                _pinchZoomBridge.BindHandler(input.CameraHandler);
                
            if (_fireButton != null && input != null)
                _fireButton.Initialize(input.CombatHandler);

            if (_movementBridge != null && input?.MovementHandler != null)
                _movementBridge.BindHandler(input.MovementHandler);
        }

        /// <summary>
        /// Bind player-context references (stat system, transform).
        /// Call from <see cref="NightHunt.UI.GameHUDController"/> after local player spawns.
        /// </summary>
        public void BindPlayerContext(Transform playerTransform, NightHunt.Gameplay.StatSystem.Core.Interfaces.IPlayerStatSystem statSystem)
        {
            if (_fireButton != null)
            {
                float vr = statSystem != null ? statSystem.GetStat(NightHunt.Gameplay.StatSystem.Core.Types.PlayerStatType.VisionRange) : 15f;
                if (vr <= 0f) vr = 15f;
                _fireButton.BindPlayerContext(playerTransform, vr);
            }

            UnbindWeaponSystem();

            var weaponSystem = playerTransform != null
                ? playerTransform.GetComponentInChildren<WeaponSystem>(true)
                : null;

            if (weaponSystem != null)
            {
                _boundWeaponSystem = weaponSystem;
                _boundWeaponSystem.OnActiveWeaponChanged += HandleActiveWeaponChanged;
                _boundWeaponSystem.OnWeaponEquipped += HandleWeaponInventoryChanged;
                _boundWeaponSystem.OnWeaponUnequipped += HandleWeaponInventoryChanged;
                _boundWeaponSystem.OnWeaponReloaded += HandleWeaponReloaded;
                _boundWeaponSystem.OnAmmoChanged += HandleAmmoChanged;
                _boundWeaponSystem.OnWeaponDepleted += HandleWeaponDepleted;
                _boundWeaponSystem.OnReloadStateChanged += HandleReloadStateChanged;
                Debug.Log($"[MobileHUD] Bound WeaponSystem for reload button. active={_boundWeaponSystem.GetActiveWeaponSlot()?.ToString() ?? "none"}");
            }
            else
            {
                Debug.LogWarning("[MobileHUD] WeaponSystem not found in player context; reload button will stay hidden.");
            }

            RefreshReloadButtonVisibility(forceLog: true);
        }

        /// <summary>
        /// Unbind all mobile input handlers.
        /// Call from <see cref="NightHunt.UI.GameHUDController"/> when the player despawns.
        /// </summary>
        public void Unbind()
        {
            _pinchZoomBridge?.UnbindHandler();
            _movementBridge?.UnbindHandler();
            _boundInput?.MovementHandler?.SetMobileMove(Vector2.zero);
            UnbindWeaponSystem();
            _boundInput = null;
        }

        /// <summary>
        /// Force the mobile root visibility without changing _forceMobileMode.
        /// Useful for toggling mobile UI from a settings screen at runtime.
        /// </summary>
        public void SetMobileUIVisible(bool visible)
        {
            if (_mobileRoot != null)
                _mobileRoot.SetActive(visible && IsMobile);
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void ApplyPlatformVisibility()
        {
            if (_mobileRoot != null)
                _mobileRoot.SetActive(IsMobile);
        }

        private void ResolveMissingButtonReferences()
        {
            _sprintButton ??= FindButtonByName("sprint", "run", "btn run", "btnrun");
            _crouchButton ??= FindButtonByName("crouch", "btncrouch", "btn crouch");
            _jumpButton ??= FindButtonByName("jump", "btnjump", "btn jump");
            _rollButton ??= FindButtonByName("roll", "btnroll", "btn roll");
            _reloadButton ??= FindButtonByName("reload", "btn_reload", "reloadbutton");
        }

        private Button FindButtonByName(params string[] nameTokens)
        {
            Transform root = _mobileRoot != null ? _mobileRoot.transform : transform;
            var buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                string buttonName = buttons[i].name.ToLowerInvariant();
                for (int t = 0; t < nameTokens.Length; t++)
                {
                    if (buttonName.Contains(nameTokens[t]))
                    {
                        Debug.Log($"[MobileHUD] Auto-bound button '{buttons[i].name}' for token '{nameTokens[t]}'.");
                        return buttons[i];
                    }
                }
            }

            return null;
        }

        private static void OnInventoryButtonClicked()
        {
            InputManager.Instance?.InventoryHandler?.SimulateToggle();
        }

        private void WireActionButtons()
        {
            AddPointerTrigger(_sprintButton, EventTriggerType.PointerDown, OnSprintPressed);
            AddPointerTrigger(_sprintButton, EventTriggerType.PointerUp, OnSprintReleased);
            AddPointerTrigger(_sprintButton, EventTriggerType.PointerExit, OnSprintReleased);

            if (_crouchButton != null) _crouchButton.onClick.AddListener(OnCrouchClicked);
            if (_jumpButton != null) _jumpButton.onClick.AddListener(OnJumpClicked);
            if (_rollButton != null) _rollButton.onClick.AddListener(OnRollClicked);
            if (_reloadButton != null) _reloadButton.onClick.AddListener(OnReloadClicked);
        }

        private void UnwireActionButtons()
        {
            if (_crouchButton != null) _crouchButton.onClick.RemoveListener(OnCrouchClicked);
            if (_jumpButton != null) _jumpButton.onClick.RemoveListener(OnJumpClicked);
            if (_rollButton != null) _rollButton.onClick.RemoveListener(OnRollClicked);
            if (_reloadButton != null) _reloadButton.onClick.RemoveListener(OnReloadClicked);
        }

        private MovementInputHandler ResolveMovementHandler()
            => _boundInput?.MovementHandler ?? InputManager.Instance?.MovementHandler;

        private void OnSprintPressed(BaseEventData _)
        {
            var movement = ResolveMovementHandler();
            Debug.Log($"[MOBILE_INPUT] Sprint press movement={(movement != null ? "ok" : "null")} enabled={movement?.IsInputEnabled.ToString() ?? "n/a"}");
            movement?.SimulateSprint(true);
        }

        private void OnSprintReleased(BaseEventData _)
        {
            var movement = ResolveMovementHandler();
            Debug.Log($"[MOBILE_INPUT] Sprint release movement={(movement != null ? "ok" : "null")} enabled={movement?.IsInputEnabled.ToString() ?? "n/a"}");
            movement?.SimulateSprint(false);
        }

        private void OnCrouchClicked()
        {
            var movement = ResolveMovementHandler();
            Debug.Log($"[MOBILE_INPUT] Crouch click movement={(movement != null ? "ok" : "null")} enabled={movement?.IsInputEnabled.ToString() ?? "n/a"}");
            movement?.SimulateCrouchToggle();
        }

        private void OnJumpClicked()
        {
            var movement = ResolveMovementHandler();
            Debug.Log($"[MOBILE_INPUT] Jump click movement={(movement != null ? "ok" : "null")} enabled={movement?.IsInputEnabled.ToString() ?? "n/a"}");
            movement?.SimulateJump();
        }

        private void OnRollClicked()
        {
            var movement = ResolveMovementHandler();
            Debug.Log($"[MOBILE_INPUT] Roll click movement={(movement != null ? "ok" : "null")} enabled={movement?.IsInputEnabled.ToString() ?? "n/a"} cooldown={_rollCooldownRemaining:F2}");
            movement?.SimulateRoll();
            if (_rollCooldownRemaining <= 0f)
                StartRollCooldown();
        }
        private void OnReloadClicked()
        {
            var activeSlot = _boundWeaponSystem?.GetActiveWeaponSlot();
            Debug.Log($"[WEAPON_FLOW] [00][ReloadButton.Click] input={(_boundInput != null ? "ok" : "null")} combat={(_boundInput?.CombatHandler != null ? "ok" : "null")} activeWeapon={activeSlot?.ToString() ?? "none"}");
            if (_boundWeaponSystem != null)
                _boundWeaponSystem.RequestReload();
            else
                _boundInput?.CombatHandler?.SimulateReload();
        }

        private void HandleActiveWeaponChanged(WeaponSlotType? oldSlot, WeaponSlotType? newSlot)
        {
            Debug.Log($"[MobileHUD] Active weapon changed: {oldSlot?.ToString() ?? "none"} -> {newSlot?.ToString() ?? "none"}");
            RefreshReloadButtonVisibility();
        }

        private void HandleWeaponInventoryChanged(WeaponSlotType slot, ItemInstance item)
        {
            RefreshReloadButtonVisibility();
        }

        private void HandleWeaponReloaded(WeaponSlotType slot, int newMagazineAmmo)
        {
            RefreshReloadButtonVisibility();
        }

        private void HandleAmmoChanged(int currentMagazine, int totalAmmoLeft, int magazineCapacity)
        {
            RefreshReloadButtonVisibility();
        }

        private void HandleWeaponDepleted(WeaponSlotType slot)
        {
            RefreshReloadButtonVisibility();
        }

        private void HandleReloadStateChanged(bool isReloading)
        {
            RefreshReloadButtonVisibility();
        }

        private void RefreshReloadButtonVisibility(bool forceLog = false)
        {
            var activeSlot = _boundWeaponSystem?.GetActiveWeaponSlot();
            bool show = IsMobile && _boundWeaponSystem != null && activeSlot.HasValue;
            bool canReload = show && _boundWeaponSystem.CanReload(activeSlot.Value);

            if (_reloadButton != null)
            {
                _reloadButton.gameObject.SetActive(show);
                _reloadButton.interactable = canReload;
            }
            else
            {
                if (!_reloadButtonMissingWarned)
                {
                    _reloadButtonMissingWarned = true;
                    Debug.LogWarning("[MobileHUD] Reload button reference is null. Add a Button named Reload/Btn_Reload under MobileHUDPanel.");
                }
            }

            if (forceLog || show != _lastReloadButtonShow || canReload != _lastReloadButtonInteractable)
            {
                Debug.Log($"[WEAPON_FLOW] [00][ReloadButton.Visibility] show={show} interactable={canReload} active={activeSlot?.ToString() ?? "none"} weapon={_boundWeaponSystem?.GetActiveWeapon()?.DefinitionID ?? "null"}");
                _lastReloadButtonShow = show;
                _lastReloadButtonInteractable = canReload;
            }
        }

        private void UnbindWeaponSystem()
        {
            if (_boundWeaponSystem == null)
                return;

            _boundWeaponSystem.OnActiveWeaponChanged -= HandleActiveWeaponChanged;
            _boundWeaponSystem.OnWeaponEquipped -= HandleWeaponInventoryChanged;
            _boundWeaponSystem.OnWeaponUnequipped -= HandleWeaponInventoryChanged;
            _boundWeaponSystem.OnWeaponReloaded -= HandleWeaponReloaded;
            _boundWeaponSystem.OnAmmoChanged -= HandleAmmoChanged;
            _boundWeaponSystem.OnWeaponDepleted -= HandleWeaponDepleted;
            _boundWeaponSystem.OnReloadStateChanged -= HandleReloadStateChanged;
            _boundWeaponSystem = null;
        }

        private static void AddPointerTrigger(Selectable selectable, EventTriggerType type, UnityAction<BaseEventData> callback)
        {
            if (selectable == null) return;

            var trigger = selectable.GetComponent<EventTrigger>() ?? selectable.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(callback);
            trigger.triggers.Add(entry);
        }

        // ── Roll Action Subscription  (drives cooldown UI only) ───────────────

        private void SubscribeRollCooldown()
        {
            _rollAction = InputLayerManager.Instance?.PlayerMap?.FindAction("Roll");
            if (_rollAction != null)
                _rollAction.started += OnRollActionStarted;
        }

        private void UnsubscribeRollCooldown()
        {
            if (_rollAction != null)
            {
                _rollAction.started -= OnRollActionStarted;
                _rollAction = null;
            }
        }

        private void OnRollActionStarted(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
        {
            if (!IsMobile || _rollCooldownRemaining > 0f) return;
            StartRollCooldown();
        }

        // ── Roll Cooldown ─────────────────────────────────────────────────────

        private void TickRollCooldown()
        {
            if (_rollCooldownRemaining <= 0f) return;

            _rollCooldownRemaining = Mathf.Max(0f, _rollCooldownRemaining - Time.deltaTime);

            if (_rollCooldownImage != null)
                _rollCooldownImage.fillAmount =
                    _rollCooldownRemaining / Mathf.Max(0.001f, RollCooldownDuration);

            if (_rollCooldownRemaining <= 0f)
                RefreshRollButton();
        }

        private void StartRollCooldown()
        {
            _rollCooldownRemaining = RollCooldownDuration;
            RefreshRollButton();
        }

        private void RefreshRollButton()
        {
            bool ready = _rollCooldownRemaining <= 0f;
            if (_rollButton != null)
                _rollButton.interactable = ready;
            if (_rollCooldownImage != null && ready)
                _rollCooldownImage.fillAmount = 0f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_mobileRoot == null)
                _mobileRoot = GetComponentInChildren<RectTransform>(true)?.gameObject;
            if (_joystick == null)
                _joystick = GetComponentInChildren<Joystick>(true);
            if (_movementBridge == null)
                _movementBridge = GetComponentInChildren<MobileMovementBridge>(true);
            if (_pinchZoomBridge == null)
                _pinchZoomBridge = GetComponentInChildren<MobilePinchZoomBridge>(true);
        }
#endif
    }
}
