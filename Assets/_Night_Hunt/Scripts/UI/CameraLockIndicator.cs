using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Gameplay.Input.Handlers.Movement;

namespace NightHunt.UI
{
    /// <summary>
    /// HUD indicator that reflects the current camera-lock (Strafe/Tank) toggle state.
    ///
    /// WIRING:
    ///   • Call <see cref="Bind"/> from <c>GameHUDController</c> after the local player spawns,
    ///     passing the player's <see cref="MovementInputHandler"/>.
    ///   • Call <see cref="Unbind"/> when the player despawns.
    ///
    /// INPUT SOURCE:
    ///   Desktop  — X key → <see cref="MovementInputHandler.OnCameraLockToggled"/>
    ///   Mobile   — Camera Lock button → <see cref="GameActionBus.SetCameraLock"/>
    ///              (both route through <see cref="GameActionBus.OnCameraLockChanged"/>)
    ///
    /// SCENE SETUP:
    ///   Add to a child GameObject inside the HUD Canvas (e.g. "CameraLockIndicator").
    ///   Assign _lockedIcon, _freeIcon, _label, _background in the Inspector.
    ///   The indicator is desktop-visible by default; hide via _canvasGroup or disable GO on mobile.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraLockIndicator : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [Header("Icons (assign one or both)")]
        [Tooltip("GameObject shown when camera is LOCKED (Strafe mode). Can be an Image with a lock icon.")]
        [SerializeField] private GameObject _lockedIcon;

        [Tooltip("GameObject shown when camera is FREE (Tank mode). Can be an Image with an unlock icon.")]
        [SerializeField] private GameObject _freeIcon;

        [Header("Label (optional)")]
        [Tooltip("TextMeshPro label. Shows 'STRAFE' when locked, 'TANK' when free.")]
        [SerializeField] private TextMeshProUGUI _label;

        [Header("Background tint (optional)")]
        [Tooltip("Image whose color tints green when locked, orange when free.")]
        [SerializeField] private Image _background;

        [Header("Colors")]
        [SerializeField] private Color _lockedColor = new Color(0.20f, 0.82f, 0.40f, 0.90f);
        [SerializeField] private Color _freeColor   = new Color(0.90f, 0.55f, 0.10f, 0.90f);

        [Header("Strings")]
        [SerializeField] private string _lockedText = "STRAFE";
        [SerializeField] private string _freeText   = "TANK";

        // ── Runtime ───────────────────────────────────────────────────────────────

        private MovementInputHandler _handler;

        // ── Unity Lifecycle ───────────────────────────────────────────────────────

        private void OnDestroy()
        {
            Unbind();
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Bind to the local player's <see cref="MovementInputHandler"/>.
        /// Safe to call multiple times — previous binding is released first.
        /// </summary>
        public void Bind(MovementInputHandler handler)
        {
            Unbind();

            _handler = handler;

            if (_handler == null)
            {
                UpdateDisplay(false);
                return;
            }

            _handler.OnCameraLockToggled += OnCameraLockToggled;

            // Also subscribe to the canonical bus in case mobile/other sources
            // change lock state without going through the MovementInputHandler event.
            Gameplay.Input.Core.GameActionBus.OnCameraLockChanged += OnCameraLockToggled;

            // Reflect current state immediately
            UpdateDisplay(_handler.IsCameraLocked());
        }

        /// <summary>
        /// Unbind from the current handler. Call when the player despawns.
        /// </summary>
        public void Unbind()
        {
            if (_handler != null)
            {
                _handler.OnCameraLockToggled -= OnCameraLockToggled;
                _handler = null;
            }

            Gameplay.Input.Core.GameActionBus.OnCameraLockChanged -= OnCameraLockToggled;
        }

        // ── Private ───────────────────────────────────────────────────────────────

        private void OnCameraLockToggled(bool isLocked)
        {
            UpdateDisplay(isLocked);
        }

        private void UpdateDisplay(bool isLocked)
        {
            if (_lockedIcon != null) _lockedIcon.SetActive(isLocked);
            if (_freeIcon   != null) _freeIcon.SetActive(!isLocked);

            if (_label != null)
                _label.text = isLocked ? _lockedText : _freeText;

            if (_background != null)
                _background.color = isLocked ? _lockedColor : _freeColor;
        }
    }
}
