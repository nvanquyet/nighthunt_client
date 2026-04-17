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
    /// SETUP: Place on a persistent GameObject in 00_DS_Boot or 01_Home (DontDestroyOnLoad).
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

#if UNITY_IOS || UNITY_ANDROID
            detected = InputPlatform.Touch;
#else
            if (Application.isMobilePlatform || Touchscreen.current != null)
            {
                detected = InputPlatform.Touch;
            }
            else if (Gamepad.current != null)
            {
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
            if (change == InputDeviceChange.Added || change == InputDeviceChange.Removed)
                DetectPlatform();
        }

        private void ApplyPlatform(InputPlatform platform)
        {
            if (Current == platform)
                return;

            Current = platform;
            Debug.Log($"[PlatformInputDetector] Input platform changed to: {platform}");
            OnPlatformChanged?.Invoke(platform);
        }

        // ── Public API ─────────────────────────────────────────────────────────

        public bool IsMobile      => Current == InputPlatform.Touch;
        public bool IsGamepad     => Current == InputPlatform.Gamepad;
        public bool IsKeyboardMouse => Current == InputPlatform.KeyboardMouse;
    }
}
