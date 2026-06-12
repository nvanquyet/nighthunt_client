using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using NightHunt.Config;
using NightHunt.Gameplay.Core.State;
using NightHunt.Gameplay.Input;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Gameplay.Input.Handlers.Combat;
using NightHunt.Gameplay.Input.Handlers.Camera;
using NightHunt.Gameplay.Input.Handlers.Interaction;
using NightHunt.Gameplay.Input.Handlers.Movement;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Interaction;
using NightHunt.GameplaySystems.UI.Combat;
using NightHunt.GameplaySystems.Weapon;
using NightHunt.Diagnostics;
using NightHunt.Utilities;

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

        [Tooltip("Interact button. Pointer-down mirrors E; pointer-up/exit releases hold interactions.")]
        [SerializeField] private Button _interactButton;

        [Tooltip("Pickup button. Mirrors F / Pickup.")]
        [SerializeField] private Button _pickupButton;

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
        public bool IsMobile =>
            PlatformInputDetector.Instance != null
                ? PlatformInputDetector.Instance.IsMobile
                : Application.isMobilePlatform;

        private bool ShouldProcessMobileControls =>
            IsMobile && _mobileHudAllowed &&
            (_mobileRoot == null || _mobileRoot.activeInHierarchy);

        private float _rollCooldownRemaining;
        private UnityEngine.InputSystem.InputAction _rollAction;
        private InputManager _boundInput;
        private IWeaponSystem _boundWeaponSystem;
        private PlayerInteractionSystem _boundPlayerInteractionSystem;
        private CharacterLifecycleController _boundLifecycle;
        private Transform _boundPlayerTransform;
        private readonly List<(EventTrigger Trigger, EventTrigger.Entry Entry)> _pointerTriggerBindings = new();
        private bool _mobileHudAllowed = true;
        private bool _lastReloadButtonShow;
        private bool _lastReloadButtonInteractable;
        private bool _lastPickupButtonVisible;
        private bool _lastInteractButtonVisible;
        private bool _reloadButtonMissingWarned;
        private bool _weaponSystemMissingWarned;
        private Vector2 _lastLoggedMoveDir;
        private string _lastLoggedJoystickName;
        private float _nextMoveJoystickLogTime;
        private Joystick _movementJoystickWithLifecycleHooks;
        private bool _movementJoystickPressed;
        private bool _movementJoystickPressTrackingReady;

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
            GameSettings.OnSettingsChanged += HandleGameSettingsChanged;
            if (PlatformInputDetector.Instance != null)
                PlatformInputDetector.Instance.OnPlatformChanged += HandlePlatformChanged;
            ApplyPlatformVisibility();
            _rollCooldownRemaining = 0f;
            RefreshRollButton();
            if (IsMobile) SubscribeRollCooldown();
            RefreshReloadButtonVisibility(forceLog: true);
            HideInteractionActionButtons(forceLog: true);
        }

        private void OnDisable()
        {
            GameSettings.OnSettingsChanged -= HandleGameSettingsChanged;
            if (PlatformInputDetector.Instance != null)
                PlatformInputDetector.Instance.OnPlatformChanged -= HandlePlatformChanged;
            UnsubscribeRollCooldown();
            ResetMobileMoveInput();
            HideInteractionActionButtons(forceLog: true);
        }

        private void Update()
        {
            if (!ShouldProcessMobileControls) return;
            TickRollCooldown();

            RefreshMobileMoveInput();

            RefreshReloadButtonVisibility();
        }

        private void OnDestroy()
        {
            if (_inventoryButton != null)
                _inventoryButton.onClick.RemoveListener(OnInventoryButtonClicked);

            UnwireActionButtons();
            _pinchZoomBridge?.UnbindHandler();
            _cameraDragArea?.UnbindHandler();
            ResetMobileMoveInput();
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
            Debug.Log($"[MOBILE_INPUT] Bind input={(input != null ? "ok" : "null")} combat={(input?.CombatHandler != null ? "ok" : "null")} movement={(input?.MovementHandler != null ? "ok" : "null")}");

            if (_pinchZoomBridge != null && input?.CameraHandler != null)
                _pinchZoomBridge.BindHandler(input.CameraHandler);

            if (_cameraDragArea == null)
                _cameraDragArea = GetComponentInChildren<MobileCameraDragArea>(true);
            if (_cameraDragArea != null && input?.CameraHandler != null)
                _cameraDragArea.BindHandler(input.CameraHandler);
            else
                Debug.LogWarning($"[MOBILE_INPUT] Camera drag area binding incomplete: dragArea={(_cameraDragArea != null ? "ok" : "null")} cameraHandler={(input?.CameraHandler != null ? "ok" : "null")}");
                
            if (_fireButton != null && input != null)
                _fireButton.Initialize(input.CombatHandler);

            if (_movementBridge != null && input?.MovementHandler != null)
                _movementBridge.BindHandler(input.MovementHandler);
        }

        /// <summary>
        /// Bind player-context references (stat system, transform).
        /// Call from <see cref="NightHunt.UI.GameHUDController"/> after local player spawns.
        /// </summary>
        public void BindPlayerContext(
            Transform playerTransform,
            NightHunt.Gameplay.StatSystem.Core.Interfaces.IPlayerStatSystem statSystem,
            IWeaponSystem weaponSystemOverride = null)
        {
            _boundPlayerTransform = playerTransform;
            _boundPlayerInteractionSystem = playerTransform != null
                ? playerTransform.GetComponentInChildren<PlayerInteractionSystem>(true)
                  ?? playerTransform.GetComponentInParent<PlayerInteractionSystem>(true)
                : null;
            _boundLifecycle = playerTransform != null
                ? playerTransform.GetComponentInChildren<CharacterLifecycleController>(true)
                  ?? playerTransform.GetComponentInParent<CharacterLifecycleController>(true)
                : null;
            Debug.Log($"[MOBILE_INPUT] BindPlayerContext player={(playerTransform != null ? playerTransform.name : "null")} interactionSystem={(_boundPlayerInteractionSystem != null ? _boundPlayerInteractionSystem.name : "null")} joystick={(_joystick != null ? _joystick.name : "null")}");

            if (_cameraDragArea == null)
                _cameraDragArea = GetComponentInChildren<MobileCameraDragArea>(true);
            var cameraStateManager = playerTransform != null
                ? playerTransform.GetComponentInChildren<NightHunt.Gameplay.Camera.CameraStateManager>(true)
                  ?? playerTransform.GetComponentInParent<NightHunt.Gameplay.Camera.CameraStateManager>(true)
                : null;
            _cameraDragArea?.BindCameraStateManager(cameraStateManager);

            if (_fireButton != null)
            {
                float vr = statSystem != null ? statSystem.GetStat(NightHunt.Gameplay.StatSystem.Core.Types.PlayerStatType.VisionRange) : 15f;
                if (vr <= 0f) vr = 15f;
                _fireButton.BindPlayerContext(playerTransform, vr);
            }

            UnbindWeaponSystem();

            var weaponSystem = weaponSystemOverride ?? ResolveWeaponSystemFrom(playerTransform);
            BindWeaponSystem(weaponSystem, weaponSystemOverride != null ? "bridge" : "player-transform");

            RefreshReloadButtonVisibility(forceLog: true);
            HideInteractionActionButtons(forceLog: true);
        }

        /// <summary>
        /// Unbind all mobile input handlers.
        /// Call from <see cref="NightHunt.UI.GameHUDController"/> when the player despawns.
        /// </summary>
        public void Unbind()
        {
            _pinchZoomBridge?.UnbindHandler();
            _cameraDragArea?.UnbindHandler();
            _movementBridge?.UnbindHandler();
            ResetMobileMoveInput();
            UnbindWeaponSystem();
            _boundInput = null;
            _boundPlayerTransform = null;
            _boundPlayerInteractionSystem = null;
            _boundLifecycle = null;
            HideInteractionActionButtons(forceLog: true);
            SetContextualButtonVisible(_reloadButton, false);
        }

        /// <summary>
        /// Sets whether mobile HUD is allowed in the current game UI state.
        /// The actual root visibility still follows PlatformInputDetector.
        /// </summary>
        public void SetMobileUIVisible(bool visible)
        {
            _mobileHudAllowed = visible;
            ApplyPlatformVisibility();
            RefreshReloadButtonVisibility(forceLog: true);
            HideInteractionActionButtons(forceLog: true);
        }

        /// <summary>
        /// Called by InteractionPromptUI. Button callbacks remain wired in this panel.
        /// </summary>
        public void SetInteractionActionButtonsVisible(
            bool pickupVisible,
            bool interactVisible,
            bool inputAllowed = true,
            bool forceLog = false)
        {
            bool showPickup = ShouldProcessMobileControls && pickupVisible;
            bool showInteract = ShouldProcessMobileControls && interactVisible;

            SetContextualButtonVisible(_pickupButton, showPickup);
            SetContextualButtonVisible(_interactButton, showInteract);

            if (_pickupButton != null)
                _pickupButton.interactable = inputAllowed && showPickup;
            if (_interactButton != null)
                _interactButton.interactable = inputAllowed && showInteract;

            LogContextButtonVisibility(showPickup, showInteract, inputAllowed, forceLog);
        }

        public void HideInteractionActionButtons(bool forceLog = false)
        {
            SetInteractionActionButtonsVisible(false, false, false, forceLog);
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void ApplyPlatformVisibility()
        {
            if (_mobileRoot != null)
                _mobileRoot.SetActive(_mobileHudAllowed && IsMobile);

            if (!ShouldProcessMobileControls)
            {
                ResetMobileMoveInput();
                HideInteractionActionButtons(forceLog: true);
            }
        }

        private void ResolveMissingButtonReferences()
        {
            _sprintButton ??= FindButtonByName("sprint", "run", "btn run", "btnrun");
            _crouchButton ??= FindButtonByName("crouch", "btncrouch", "btn crouch");
            _jumpButton ??= FindButtonByName("jump", "btnjump", "btn jump");
            _rollButton ??= FindButtonByName("roll", "btnroll", "btn roll");
            _reloadButton ??= FindButtonByName("reload", "btn_reload", "reloadbutton");
            _inventoryButton ??= FindButtonByName("inventory", "bag", "backpack");
            _interactButton ??= FindButtonByName("interact", "use", "btnuse", "btn interact");
            _pickupButton ??= FindButtonByName("pickup", "loot", "take", "btnpickup");
            _joystick ??= FindMovementJoystick();
            _cameraDragArea ??= GetComponentInChildren<MobileCameraDragArea>(true);
            _pinchZoomBridge ??= GetComponentInChildren<MobilePinchZoomBridge>(true);

            _interactButton ??= CreateRuntimeButton("InteractButton", "Use", new Vector2(-178f, 168f));
            _pickupButton ??= CreateRuntimeButton("PickupButton", "Pick", new Vector2(-82f, 230f));
            _reloadButton ??= CreateRuntimeButton("ReloadButton", "Reload", new Vector2(-130f, 290f));
            SetContextualButtonVisible(_interactButton, false);
            SetContextualButtonVisible(_pickupButton, false);
            SetContextualButtonVisible(_reloadButton, false);

            // Fix4: warn if inventory button still null after all auto-resolve attempts
            if (_inventoryButton == null)
            {
                Debug.LogWarning("[MobileHUDPanel] _inventoryButton chưa được wire trong Inspector " +
                                 "và không tìm thấy button tên 'inventory'/'bag'/'backpack' trong children.");
            }

            EnsureMovementJoystickLifecycleWired();

            Debug.Log($"[MOBILE_INPUT] MobileHUD refs root={(_mobileRoot != null ? _mobileRoot.name : "null")} rootActive={(_mobileRoot != null && _mobileRoot.activeInHierarchy)} isMobile={IsMobile} reload={(_reloadButton != null ? _reloadButton.name : "null")} interact={(_interactButton != null ? _interactButton.name : "null")} pickup={(_pickupButton != null ? _pickupButton.name : "null")} cameraDrag={(_cameraDragArea != null ? _cameraDragArea.name : "null")} pinch={(_pinchZoomBridge != null ? _pinchZoomBridge.name : "null")}");
        }

        private void HandleGameSettingsChanged()
        {
            ApplyPlatformVisibility();
            RefreshReloadButtonVisibility(forceLog: true);
            HideInteractionActionButtons(forceLog: true);
        }

        private void HandlePlatformChanged(PlatformInputDetector.InputPlatform _)
        {
            UnsubscribeRollCooldown();
            if (IsMobile)
                SubscribeRollCooldown();

            ApplyPlatformVisibility();
            RefreshReloadButtonVisibility(forceLog: true);
            HideInteractionActionButtons(forceLog: true);
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

        private Joystick FindMovementJoystick()
        {
            Transform root = _mobileRoot != null ? _mobileRoot.transform : transform;
            var joysticks = root.GetComponentsInChildren<Joystick>(true);
            Joystick best = null;
            int bestScore = int.MinValue;

            for (int i = 0; i < joysticks.Length; i++)
            {
                Joystick joystick = joysticks[i];
                if (joystick == null)
                    continue;

                string name = joystick.name.ToLowerInvariant();
                string parentName = joystick.transform.parent != null
                    ? joystick.transform.parent.name.ToLowerInvariant()
                    : string.Empty;
                string combined = $"{name} {parentName}";

                int score = 0;
                if (joystick.gameObject.activeInHierarchy) score += 100;
                if (joystick.enabled) score += 50;
                if (combined.Contains("move") || combined.Contains("movement") || combined.Contains("left")) score += 250;
                if (combined.Contains("fixed")) score += 25;
                if (combined.Contains("fire") || combined.Contains("aim") || combined.Contains("attack") || combined.Contains("throw")) score -= 500;
                if (_fireButton != null && joystick.transform.IsChildOf(_fireButton.transform)) score -= 500;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = joystick;
                }
            }

            if (best != null)
                Debug.Log($"[MOBILE_INPUT] Movement joystick bound: '{best.name}' score={bestScore}");

            return best;
        }

        private void RefreshMobileMoveInput()
        {
            if (_joystick == null || IsFireAimJoystick(_joystick))
                _joystick = FindMovementJoystick();
            EnsureMovementJoystickLifecycleWired();

            var movement = ResolveMovementHandler();
            bool canReadJoystick = _joystick != null &&
                                   _joystick.isActiveAndEnabled &&
                                   _joystick.gameObject.activeInHierarchy;
            bool joystickHeld = !_movementJoystickPressTrackingReady || _movementJoystickPressed;
            Vector2 dir = canReadJoystick && joystickHeld ? _joystick.Direction : Vector2.zero;
            movement?.SetMobileMove(dir);
            LogMoveJoystick(dir, movement);
        }

        private void ResetMobileMoveInput()
        {
            _movementJoystickPressed = false;
            ResolveMovementHandler()?.SetMobileMove(Vector2.zero);
        }

        private void EnsureMovementJoystickLifecycleWired()
        {
            if (_joystick == null || ReferenceEquals(_movementJoystickWithLifecycleHooks, _joystick))
                return;

            _movementJoystickWithLifecycleHooks = _joystick;
            _movementJoystickPressTrackingReady = true;
            AddPointerTrigger(_joystick.gameObject, EventTriggerType.PointerDown, OnMovementJoystickPressed);
            AddPointerTrigger(_joystick.gameObject, EventTriggerType.BeginDrag, OnMovementJoystickPressed);
            AddPointerTrigger(_joystick.gameObject, EventTriggerType.PointerUp, OnMovementJoystickReleased);
            AddPointerTrigger(_joystick.gameObject, EventTriggerType.EndDrag, OnMovementJoystickReleased);
            AddPointerTrigger(_joystick.gameObject, EventTriggerType.Cancel, OnMovementJoystickReleased);
        }

        private void OnMovementJoystickPressed(BaseEventData _)
        {
            _movementJoystickPressed = true;
        }

        private void OnMovementJoystickReleased(BaseEventData eventData)
        {
            _movementJoystickPressed = false;
            if (_joystick != null)
            {
                var pointer = eventData as PointerEventData;
                if (pointer != null || EventSystem.current != null)
                    _joystick.OnPointerUp(pointer ?? new PointerEventData(EventSystem.current));
            }
            ResetMobileMoveInput();
        }

        private bool IsFireAimJoystick(Joystick joystick)
        {
            if (joystick == null)
                return false;

            string name = joystick.name.ToLowerInvariant();
            if (name.Contains("fire") || name.Contains("aim") || name.Contains("attack") || name.Contains("throw"))
                return true;

            return _fireButton != null && joystick.transform.IsChildOf(_fireButton.transform);
        }

        private void LogMoveJoystick(Vector2 dir, MovementInputHandler movement)
        {
            string joystickName = _joystick != null ? _joystick.name : "null";
            bool changed = (_lastLoggedMoveDir - dir).sqrMagnitude > 0.01f || _lastLoggedJoystickName != joystickName;
            bool active = dir.sqrMagnitude > 0.01f;
            bool missing = _joystick == null || movement == null;

            if (!changed && !missing)
                return;

            if (!missing && Time.unscaledTime < _nextMoveJoystickLogTime)
                return;

            _nextMoveJoystickLogTime = Time.unscaledTime + 0.5f;
            _lastLoggedMoveDir = dir;
            _lastLoggedJoystickName = joystickName;
            Debug.Log($"[MOBILE_INPUT] [MoveJoystick] joystick={joystickName} dir={dir:F2} handler={(movement != null ? "ok" : "null")} inputEnabled={movement?.IsInputEnabled.ToString() ?? "n/a"} mobileMode={ShouldProcessMobileControls}");
        }

        private void OnInventoryButtonClicked()
        {
            var input = _boundInput ?? InputManager.Instance;
            if (input?.InventoryHandler != null)
                input.InventoryHandler.SimulateToggle();
            else
                GameActionBus.RequestInventoryToggle();
        }

        private Button CreateRuntimeButton(string buttonName, string label, Vector2 anchoredPosition)
        {
            if (_mobileRoot == null && !IsMobile)
                return null;

            Transform root = _mobileRoot != null ? _mobileRoot.transform : transform;
            var go = new GameObject(buttonName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(root, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(88f, 56f);
            rt.anchoredPosition = anchoredPosition;

            var image = go.GetComponent<Image>();
            image.color = new Color(0.08f, 0.10f, 0.12f, 0.82f);

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGo.GetComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.fontSize = 18;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 12;
            text.resizeTextMaxSize = 18;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                     ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (text.font == null)
                text.font = Font.CreateDynamicFontFromOSFont("Arial", 18);

            Debug.Log($"[MobileHUD] Created runtime fallback button '{buttonName}'. Add a named prefab button to override its layout.");
            return go.GetComponent<Button>();
        }

        private void WireActionButtons()
        {
            AddPointerTrigger(_sprintButton, EventTriggerType.PointerDown, OnSprintPressed);
            AddPointerTrigger(_sprintButton, EventTriggerType.PointerUp, OnSprintReleased);
            AddPointerTrigger(_sprintButton, EventTriggerType.PointerExit, OnSprintReleased);

            AddPointerTrigger(_interactButton, EventTriggerType.PointerDown, OnInteractPressed);
            AddPointerTrigger(_interactButton, EventTriggerType.PointerUp, OnInteractReleased);
            AddPointerTrigger(_interactButton, EventTriggerType.PointerExit, OnInteractReleased);
            AddPointerTrigger(_reloadButton, EventTriggerType.PointerDown, OnReloadPressed);
            AddPointerTrigger(_pickupButton, EventTriggerType.PointerDown, OnPickupPressed);

            if (_crouchButton != null) _crouchButton.onClick.AddListener(OnCrouchClicked);
            if (_jumpButton != null) _jumpButton.onClick.AddListener(OnJumpClicked);
            if (_rollButton != null) _rollButton.onClick.AddListener(OnRollClicked);
        }

        private void UnwireActionButtons()
        {
            for (int i = 0; i < _pointerTriggerBindings.Count; i++)
            {
                var binding = _pointerTriggerBindings[i];
                if (binding.Trigger != null)
                    binding.Trigger.triggers.Remove(binding.Entry);
            }
            _pointerTriggerBindings.Clear();

            if (_crouchButton != null) _crouchButton.onClick.RemoveListener(OnCrouchClicked);
            if (_jumpButton != null) _jumpButton.onClick.RemoveListener(OnJumpClicked);
            if (_rollButton != null) _rollButton.onClick.RemoveListener(OnRollClicked);
        }

        private MovementInputHandler ResolveMovementHandler()
            => _boundInput?.MovementHandler ?? InputManager.Instance?.MovementHandler;

        private InteractionInputHandler ResolveInteractionHandler()
            => _boundInput?.InteractionHandler ?? InputManager.Instance?.InteractionHandler;

        private CombatInputHandler ResolveCombatHandler()
            => _boundInput?.CombatHandler ?? InputManager.Instance?.CombatHandler;

        private PlayerInteractionSystem ResolvePlayerInteractionSystem()
        {
            if (_boundPlayerInteractionSystem == null && _boundPlayerTransform != null)
            {
                _boundPlayerInteractionSystem =
                    _boundPlayerTransform.GetComponentInChildren<PlayerInteractionSystem>(true)
                    ?? _boundPlayerTransform.GetComponentInParent<PlayerInteractionSystem>(true);
            }

            return _boundPlayerInteractionSystem;
        }

        private bool CanProcessGameplayAction()
        {
            if (!ShouldProcessMobileControls)
                return false;

            if (_boundLifecycle == null && _boundPlayerTransform != null)
            {
                _boundLifecycle =
                    _boundPlayerTransform.GetComponentInChildren<CharacterLifecycleController>(true)
                    ?? _boundPlayerTransform.GetComponentInParent<CharacterLifecycleController>(true);
            }

            if (_boundLifecycle != null && _boundLifecycle.IsDead)
                return false;

            // Use InputLayerManager as the single source of truth for input context.
            // Player layer active = gameplay actions allowed.
            // This automatically handles all InputState cases (Inventory, Paused, Map, etc.)
            // without enumerating states here — future states need no changes in this file.
            if (InputLayerManager.Instance != null)
                return InputLayerManager.Instance.IsLayerActive(InputLayer.Player);

            // Fallback when InputLayerManager is not yet initialized (early frame).
            return true;
        }

        private void OnSprintPressed(BaseEventData _)
        {
            if (!CanProcessGameplayAction()) return;
            var movement = ResolveMovementHandler();
            Debug.Log($"[MOBILE_INPUT] Sprint press movement={(movement != null ? "ok" : "null")} enabled={movement?.IsInputEnabled.ToString() ?? "n/a"}");
            movement?.SimulateSprint(true);
        }

        private void OnSprintReleased(BaseEventData _)
        {
            if (!ShouldProcessMobileControls) return;
            var movement = ResolveMovementHandler();
            Debug.Log($"[MOBILE_INPUT] Sprint release movement={(movement != null ? "ok" : "null")} enabled={movement?.IsInputEnabled.ToString() ?? "n/a"}");
            movement?.SimulateSprint(false);
        }

        private void OnCrouchClicked()
        {
            if (!CanProcessGameplayAction()) return;
            var movement = ResolveMovementHandler();
            Debug.Log($"[MOBILE_INPUT] Crouch click movement={(movement != null ? "ok" : "null")} enabled={movement?.IsInputEnabled.ToString() ?? "n/a"}");
            movement?.SimulateCrouchToggle();
        }

        private void OnJumpClicked()
        {
            if (!CanProcessGameplayAction()) return;
            var movement = ResolveMovementHandler();
            Debug.Log($"[MOBILE_INPUT] Jump click movement={(movement != null ? "ok" : "null")} enabled={movement?.IsInputEnabled.ToString() ?? "n/a"}");
            movement?.SimulateJump();
        }

        private void OnRollClicked()
        {
            if (!CanProcessGameplayAction()) return;
            var movement = ResolveMovementHandler();
            Debug.Log($"[MOBILE_INPUT] Roll click movement={(movement != null ? "ok" : "null")} enabled={movement?.IsInputEnabled.ToString() ?? "n/a"} cooldown={_rollCooldownRemaining:F2}");
            movement?.SimulateRoll();
            if (_rollCooldownRemaining <= 0f)
                StartRollCooldown();
        }
        private void OnReloadPressed(BaseEventData _)
        {
            if (!CanProcessGameplayAction()) return;
            var weaponSystem = ResolveWeaponSystem();
            var activeSlot = weaponSystem?.GetActiveWeaponSlot();
            Debug.Log($"[WEAPON_FLOW] [00][ReloadButton.Press] weaponSystem={(weaponSystem != null ? "ok" : "null")} input={(_boundInput != null ? "ok" : "null")} combat={(_boundInput?.CombatHandler != null ? "ok" : "null")} activeWeapon={activeSlot?.ToString() ?? "none"}");
            ResolveCombatHandler()?.SimulateReload();
        }

        private void OnInteractPressed(BaseEventData _)
        {
            if (!CanProcessGameplayAction()) return;
            var interaction = ResolveInteractionHandler();
            Debug.Log($"[MOBILE_INPUT] Interact press interaction={(interaction != null ? "ok" : "null")} enabled={interaction?.IsInputEnabled.ToString() ?? "n/a"}");
            interaction?.SimulateInteractPressed();
        }

        private void OnInteractReleased(BaseEventData _)
        {
            if (!ShouldProcessMobileControls) return;
            var interaction = ResolveInteractionHandler();
            Debug.Log($"[MOBILE_INPUT] Interact release interaction={(interaction != null ? "ok" : "null")} enabled={interaction?.IsInputEnabled.ToString() ?? "n/a"}");
            interaction?.SimulateInteractReleased();
        }

        private void OnPickupPressed(BaseEventData _)
        {
            if (!CanProcessGameplayAction()) return;
            var interaction = ResolveInteractionHandler();
            Debug.Log($"[MOBILE_INPUT] Pickup press interaction={(interaction != null ? "ok" : "null")} enabled={interaction?.IsInputEnabled.ToString() ?? "n/a"}");
            interaction?.SimulatePickup();
        }

        private void HandleActiveWeaponChanged(WeaponSlotType? oldSlot, WeaponSlotType? newSlot)
        {
            Debug.Log($"[MobileHUD] Active weapon changed: {oldSlot?.ToString() ?? "none"} -> {newSlot?.ToString() ?? "none"}");
            RefreshReloadButtonVisibility(forceLog: true);
        }

        private void HandleWeaponInventoryChanged(WeaponSlotType slot, ItemInstance item)
        {
            RefreshReloadButtonVisibility(forceLog: true);
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
            var weaponSystem = ResolveWeaponSystem();
            var activeSlot = weaponSystem?.GetActiveWeaponSlot();
            bool inputAllowed = CanProcessGameplayAction();
            bool show = ShouldProcessMobileControls && weaponSystem != null && activeSlot.HasValue;
            bool canReload = show && weaponSystem.CanReload(activeSlot.Value);

            if (_reloadButton != null)
            {
                _reloadButton.gameObject.SetActive(show);
                _reloadButton.interactable = canReload && inputAllowed;
            }
            else
            {
                if (!_reloadButtonMissingWarned)
                {
                    _reloadButtonMissingWarned = true;
                    Debug.LogWarning("[MobileHUD] Reload button reference is null. Add a Button named Reload/Btn_Reload under MobileHUDPanel.");
                }
            }

            bool interactable = canReload && inputAllowed;
            if (forceLog || show != _lastReloadButtonShow || interactable != _lastReloadButtonInteractable)
            {
                string details = $"show={show} interactable={interactable} canReload={canReload} inputAllowed={inputAllowed} {DescribeWeaponSystem(weaponSystem)} mobileMode={ShouldProcessMobileControls} button={DescribeButton(_reloadButton)}";
                Debug.Log($"[WEAPON_FLOW] [00][ReloadButton.Visibility] {details}");
                PhaseTestLog.Log(PhaseTestLogCategory.Input, "ReloadButtonVisibility", details, this);
                _lastReloadButtonShow = show;
                _lastReloadButtonInteractable = interactable;
            }
        }

        private IWeaponSystem ResolveWeaponSystem()
        {
            if (_boundWeaponSystem == null && _boundPlayerTransform != null)
            {
                var resolved = ResolveWeaponSystemFrom(_boundPlayerTransform);
                BindWeaponSystem(resolved, "late-resolve");
            }

            return _boundWeaponSystem;
        }

        private static IWeaponSystem ResolveWeaponSystemFrom(Transform playerTransform)
        {
            if (playerTransform == null)
                return null;

            return ComponentResolver.Find<IWeaponSystem>(playerTransform)
                .OnSelf()
                .InChildren()
                .InParent()
                .InRootChildren()
                .Resolve();
        }

        private void BindWeaponSystem(IWeaponSystem weaponSystem, string source)
        {
            if (ReferenceEquals(_boundWeaponSystem, weaponSystem))
                return;

            UnbindWeaponSystem();

            if (weaponSystem == null)
            {
                if (!_weaponSystemMissingWarned)
                {
                    _weaponSystemMissingWarned = true;
                    Debug.LogWarning($"[MobileHUD] WeaponSystem not found from {source}; reload button will stay hidden.");
                    PhaseTestLog.Warning(
                        PhaseTestLogCategory.Input,
                        "MobileWeaponBind",
                        $"source={source} result=null player={(_boundPlayerTransform != null ? _boundPlayerTransform.name : "null")}",
                        this);
                }
                return;
            }

            _weaponSystemMissingWarned = false;
            _boundWeaponSystem = weaponSystem;
            _boundWeaponSystem.OnActiveWeaponChanged += HandleActiveWeaponChanged;
            _boundWeaponSystem.OnWeaponEquipped += HandleWeaponInventoryChanged;
            _boundWeaponSystem.OnWeaponUnequipped += HandleWeaponInventoryChanged;
            _boundWeaponSystem.OnWeaponReloaded += HandleWeaponReloaded;
            _boundWeaponSystem.OnAmmoChanged += HandleAmmoChanged;
            _boundWeaponSystem.OnWeaponDepleted += HandleWeaponDepleted;
            _boundWeaponSystem.OnReloadStateChanged += HandleReloadStateChanged;

            string details = $"source={source} {DescribeWeaponSystem(_boundWeaponSystem)}";
            Debug.Log($"[MobileHUD] Bound WeaponSystem for reload button. {details}");
            PhaseTestLog.Log(PhaseTestLogCategory.Input, "MobileWeaponBind", details, this);
        }

        private void LogContextButtonVisibility(bool pickupVisible, bool interactVisible, bool inputAllowed, bool force = false)
        {
            if (!force && pickupVisible == _lastPickupButtonVisible && interactVisible == _lastInteractButtonVisible)
                return;

            _lastPickupButtonVisible = pickupVisible;
            _lastInteractButtonVisible = interactVisible;
            var inputLayers = InputLayerManager.Instance;
            string inputState = inputLayers != null ? inputLayers.CurrentState.ToString() : "null";
            string activeLayers = inputLayers != null ? inputLayers.ActiveLayers.ToString() : "null";

            Debug.Log($"[MOBILE_INPUT] [ContextButtons.Visibility] pickup={pickupVisible} interact={interactVisible} " +
                      $"inputAllowed={inputAllowed} state={inputState} layers={activeLayers} mobileMode={ShouldProcessMobileControls} " +
                      $"pickupButton={DescribeButton(_pickupButton)} interactButton={DescribeButton(_interactButton)}");
            PhaseTestLog.Log(
                PhaseTestLogCategory.Input,
                "MobileContextButtons",
                $"pickup={pickupVisible} interact={interactVisible} inputAllowed={inputAllowed} state={inputState} layers={activeLayers} mobileMode={ShouldProcessMobileControls} pickupButton={DescribeButton(_pickupButton)} interactButton={DescribeButton(_interactButton)}",
                this);
        }

        private static string DescribeWeaponSystem(IWeaponSystem weaponSystem)
        {
            if (weaponSystem == null)
                return "weaponSystem=null";

            var activeSlot = weaponSystem.GetActiveWeaponSlot();
            var activeWeapon = weaponSystem.GetActiveWeapon();
            var weapons = weaponSystem.GetAllWeapons();
            var parts = new List<string>();
            if (weapons != null)
            {
                foreach (var kvp in weapons)
                    parts.Add($"{kvp.Key}:{kvp.Value?.DefinitionID ?? "null"}");
            }

            return $"active={activeSlot?.ToString() ?? "none"} activeWeapon={activeWeapon?.DefinitionID ?? "null"} weapons=[{string.Join(",", parts)}]";
        }

        private static string DescribeButton(Button button)
        {
            if (button == null)
                return "null";

            var go = button.gameObject;
            return $"{go.name}(activeSelf={go.activeSelf},activeInHierarchy={go.activeInHierarchy},interactable={button.interactable})";
        }

        private static void SetContextualButtonVisible(Button button, bool visible)
        {
            if (button != null && button.gameObject.activeSelf != visible)
                button.gameObject.SetActive(visible);
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

        private void AddPointerTrigger(Selectable selectable, EventTriggerType type, UnityAction<BaseEventData> callback)
        {
            if (selectable == null) return;

            AddPointerTrigger(selectable.gameObject, type, callback);
        }

        private void AddPointerTrigger(GameObject target, EventTriggerType type, UnityAction<BaseEventData> callback)
        {
            if (target == null) return;

            var trigger = target.GetComponent<EventTrigger>() ?? target.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(callback);
            trigger.triggers.Add(entry);
            _pointerTriggerBindings.Add((trigger, entry));
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
                _joystick = FindMovementJoystick();
            if (_movementBridge == null)
                _movementBridge = GetComponentInChildren<MobileMovementBridge>(true);
            if (_pinchZoomBridge == null)
                _pinchZoomBridge = GetComponentInChildren<MobilePinchZoomBridge>(true);
            if (_cameraDragArea == null)
                _cameraDragArea = GetComponentInChildren<MobileCameraDragArea>(true);
            if (_interactButton == null)
                _interactButton = FindButtonByName("interact", "use", "btnuse", "btn interact");
            if (_pickupButton == null)
                _pickupButton = FindButtonByName("pickup", "loot", "take", "btnpickup");
        }
#endif
    }
}
