using System;
using UnityEngine;
using NightHunt.Core;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace NightHunt.Gameplay.Input.Core
{
    /// <summary>
    /// Detects the active input platform (KeyboardMouse, Gamepad, Touch) at runtime and
    /// fires <see cref="OnPlatformChanged"/> whenever the user switches devices.
    ///
    /// SETUP: Place on a dedicated GO in 00_DS_Boot or 01_Home — survives all scene loads
    /// via DontDestroyOnLoad. Do NOT place a second copy in the game scene.
    /// GameHUD and other HUDs subscribe to OnPlatformChanged to show/hide platform-specific UI.
    /// </summary>
    public sealed class PlatformInputDetector : SingletonPersistent<PlatformInputDetector>
    {
        // ── Types ──────────────────────────────────────────────────────────────

        public enum InputPlatform
        {
            KeyboardMouse,
            Gamepad,
            Touch,
        }

        // ── Debug overrides (Inspector only — set false before production build) ──

        [Header("Debug Overrides")]
        [Tooltip("Force Touch/Mobile input mode regardless of actual platform. " +
                 "Set FALSE before production build.")]
        [SerializeField] private bool forceMobileInput;

        [Tooltip("Force KeyboardMouse input mode regardless of actual platform. " +
                 "Takes priority over forceMobileInput if both are true. " +
                 "Set FALSE before production build.")]
        [SerializeField] private bool forceDesktopInput;

        // ── State ──────────────────────────────────────────────────────────────

        public InputPlatform Current { get; private set; } = InputPlatform.KeyboardMouse;

        /// <summary>Fired whenever the active input platform changes.</summary>
        public event Action<InputPlatform> OnPlatformChanged;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            DetectPlatform();
        }

        private void OnEnable()
        {
            InputSystem.onDeviceChange += HandleDeviceChange;
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= HandleDeviceChange;
        }

        // ── Detection ──────────────────────────────────────────────────────────

        private void DetectPlatform()
        {
            InputPlatform detected;

            // Debug overrides — forceDesktopInput wins if both are set
            if (forceDesktopInput)
            {
                detected = InputPlatform.KeyboardMouse;
                ApplyPlatform(detected);
                return;
            }
            if (forceMobileInput)
            {
                detected = InputPlatform.Touch;
                ApplyPlatform(detected);
                return;
            }

#if UNITY_IOS || UNITY_ANDROID
            detected = InputPlatform.Touch;
#else
            if (Application.isMobilePlatform || Touchscreen.current != null)
            {
                detected = InputPlatform.Touch;
            }
            else if (Gamepad.current != null && Gamepad.current.native)
            {
                // Only treat native (physical) gamepads as Gamepad platform.
                // Virtual on-screen devices also appear as Gamepad.current but must be ignored.
                detected = InputPlatform.Gamepad;
            }
            else
            {
                detected = InputPlatform.KeyboardMouse;
            }
#endif

            ApplyPlatform(detected);
        }

        private void HandleDeviceChange(InputDevice device, InputDeviceChange change)
        {
            // Ignore virtual / on-screen devices (native == false).
            // OnScreenControl.OnEnable() registers a virtual Gamepad-like device which would
            // otherwise incorrectly trigger a platform switch to Gamepad.
            if (!device.native) return;

            if (change == InputDeviceChange.Added || change == InputDeviceChange.Removed)
                DetectPlatform();
        }

        private void ApplyPlatform(InputPlatform platform)
        {
            if (Current == platform)
                return;

            Current = platform;
            Debug.Log($"[PlatformInputDetector] Input platform changed to: {platform}");
            try
            {
                OnPlatformChanged?.Invoke(platform);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlatformInputDetector] Subscriber threw while handling platform change: {ex}");
            }
        }

        // ── Public API ─────────────────────────────────────────────────────────

        public bool IsMobile      => Current == InputPlatform.Touch;
        public bool IsGamepad     => Current == InputPlatform.Gamepad;
        public bool IsKeyboardMouse => Current == InputPlatform.KeyboardMouse;
    }
}
