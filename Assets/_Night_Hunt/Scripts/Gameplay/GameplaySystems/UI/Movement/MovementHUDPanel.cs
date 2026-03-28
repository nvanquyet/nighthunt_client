using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Gameplay.Character.Movement;

namespace NightHunt.GameplaySystems.UI.Movement
{
    /// <summary>
    /// Platform-aware mobile HUD panel for movement input.
    ///
    /// ── Platform detection ───────────────────────────────────────────────────
    ///   bool IsMobile => _forceMobileMode || Application.isMobilePlatform
    ///   On PC:  _mobileOnlyRoot is hidden; no input overrides.
    ///   On Mobile: _mobileOnlyRoot shown; OnScreenStick + OnScreenButton
    ///              components on child GameObjects drive the InputActions
    ///              directly — no polling or manual setter calls needed.
    ///
    /// ── Movement Joystick ────────────────────────────────────────────────────
    ///   OnScreenStick on the joystick GO (controlPath = "<Gamepad>/leftStick",
    ///   behaviour = ExactPositionWithDynamicOrigin) writes to the Move action.
    ///   MovementInputHandler.OnMovePerformed/Canceled handle the rest.
    ///
    /// ── Sprint / Crouch / Jump / Roll buttons ────────────────────────────────
    ///   OnScreenButton on each button GO (controlPath matching the Gamepad
    ///   binding added to each action) drives the InputAction directly.
    ///   No EventTrigger callbacks required.
    ///
    /// ── Roll UI Cooldown ─────────────────────────────────────────────────────
    ///   This panel subscribes to Player/Roll.started to trigger the visual
    ///   cooldown overlay (_rollCooldownImage fillAmount 1→0) and disable the
    ///   button for rollDuration seconds.  The OnScreenButton handles actual
    ///   input; this is purely cosmetic feedback.
    ///
    /// Inspector setup:
    ///   _forceMobileMode    – treat editor as mobile for testing
    ///   _mobileOnlyRoot     – parent GO for joystick + all mobile buttons
    ///   _movementSettings   – MovementSettings SO (roll cooldown duration)
    ///   _sprintButton       – has OnScreenButton: controlPath = "<Gamepad>/leftShoulder"
    ///   _rollButton         – has OnScreenButton: controlPath = "<Gamepad>/buttonWest"
    ///   _crouchButton       – has OnScreenButton: controlPath = "<Gamepad>/rightShoulder"
    ///   _rollCooldownImage  – filled Image overlay (fillAmount: 1 = cooling, 0 = ready)
    /// </summary>
    public class MovementHUDPanel : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────────────────────────────

        [Header("Platform")]
        [Tooltip("Force this panel to behave as if running on mobile (useful in Editor testing).")]
        [SerializeField] private bool _forceMobileMode = false;

        [Tooltip("Root GO that contains the joystick and all mobile-only buttons. " +
                 "Automatically shown/hidden based on platform at startup.")]
        [SerializeField] private GameObject _mobileOnlyRoot;

        [Header("Movement Config")]
        [Tooltip("Same MovementSettings SO used by the gameplay character. " +
                 "Roll UI cooldown duration is read from rollDuration.")]
        [SerializeField] private MovementSettings _movementSettings;

        [Header("Buttons (mobile only — all need OnScreenButton component)")]
        [Tooltip("Sprint button. OnScreenButton controlPath = \"<Gamepad>/leftShoulder\".")]
        [SerializeField] private Button _sprintButton;

        [Tooltip("Roll button. OnScreenButton controlPath = \"<Gamepad>/buttonWest\".")]
        [SerializeField] private Button _rollButton;

        [Tooltip("Crouch button. OnScreenButton controlPath = \"<Gamepad>/rightShoulder\". Optional.")]
        [SerializeField] private Button _crouchButton;

        [Header("Roll Cooldown Overlay")]
        [Tooltip("Filled Image on top of _rollButton used as radial cooldown indicator. " +
                 "fillAmount: 1 = just rolled (cooling), 0 = ready.")]
        [SerializeField] private Image _rollCooldownImage;

        // ─────────────────────────────────────────────────────────────────────
        //  Runtime
        // ─────────────────────────────────────────────────────────────────────

        // Same pattern as QuickSlotAimController / ItemAimController.
        private bool IsMobile => _forceMobileMode || Application.isMobilePlatform;

        private float _rollCooldownRemaining;
        private InputAction _rollAction;

        // Sourced from MovementSettings SO; falls back to default 0.35 s.
        private float RollCooldownDuration =>
            (_movementSettings != null && _movementSettings.enableRoll)
                ? _movementSettings.rollDuration
                : 0.35f;

        // ─────────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            ApplyPlatformVisibility();
        }

        private void OnEnable()
        {
            // Re-apply in case platform changed at runtime (e.g., editor toggle).
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
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Platform Visibility
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Show mobile controls on mobile; hide on PC.
        /// The mobileOnlyRoot covers the joystick + all buttons in one go.
        /// </summary>
        private void ApplyPlatformVisibility()
        {
            if (_mobileOnlyRoot != null)
                _mobileOnlyRoot.SetActive(IsMobile);
        }

        /// <summary>
        /// Toggle mobile UI visibility at runtime (e.g. called from a settings screen).
        /// </summary>
        public void SetMobileUIVisible(bool visible)
        {
            if (_mobileOnlyRoot != null)
                _mobileOnlyRoot.SetActive(visible && IsMobile);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Roll Action Subscription  (drives cooldown UI only)
        // ─────────────────────────────────────────────────────────────────────

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

        private void OnRollActionStarted(InputAction.CallbackContext ctx)
        {
            if (!IsMobile || _rollCooldownRemaining > 0f) return;
            StartRollCooldown();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Roll Cooldown
        // ─────────────────────────────────────────────────────────────────────

        private void TickRollCooldown()
        {
            if (_rollCooldownRemaining <= 0f) return;

            _rollCooldownRemaining = Mathf.Max(0f, _rollCooldownRemaining - Time.deltaTime);

            // fillAmount counts DOWN: 1 = just rolled, 0 = ready.
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

        /// <summary>
        /// Sync _rollButton.interactable and overlay to current cooldown state.
        /// </summary>
        private void RefreshRollButton()
        {
            bool ready = _rollCooldownRemaining <= 0f;
            if (_rollButton != null)
                _rollButton.interactable = ready;
            if (_rollCooldownImage != null && ready)
                _rollCooldownImage.fillAmount = 0f;
        }

    }
}
