using System;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NightHunt.UI.Settings
{
    /// <summary>
    /// Displays the current binding for a single InputAction and allows interactive rebinding.
    /// Wire up per-action entries in ControlsSettingsPanel._rebindableActions.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RebindActionUI : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────

        [Header("Action")]
        [Tooltip("The InputAction to display and rebind.")]
        [SerializeField] private InputActionReference _actionRef;

        [Tooltip("Binding index within the action (0 for primary binding).")]
        [SerializeField] private int _bindingIndex;

        [Header("UI")]
        [Tooltip("Label showing the human-readable binding, e.g. 'Space', 'LMB'.")]
        [SerializeField] private TextMeshProUGUI _bindingText;

        [Tooltip("Button that starts interactive rebind when clicked.")]
        [SerializeField] private Button _rebindButton;

        [Tooltip("Button that resets this action to its default binding.")]
        [SerializeField] private Button _resetButton;

        [Tooltip("Optional overlay shown while waiting for key press.")]
        [SerializeField] private GameObject _waitingOverlay;

        // ── Events ─────────────────────────────────────────────────────────────

        /// <summary>Fired after the player successfully assigns a new binding.</summary>
        public event Action OnBindingChanged;

        // ── State ──────────────────────────────────────────────────────────────

        private InputActionRebindingExtensions.RebindingOperation _rebindOperation;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Start()
        {
            if (_rebindButton != null) _rebindButton.onClick.AddListener(StartRebinding);
            if (_resetButton  != null) _resetButton.onClick.AddListener(ResetBinding);

            UpdateBindingDisplay();
        }

        private void OnDestroy()
        {
            _rebindOperation?.Dispose();
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Update the binding label from the current action override (or default).</summary>
        public void UpdateBindingDisplay()
        {
            if (_actionRef == null || _bindingText == null)
                return;

            var action = _actionRef.action;
            if (action == null) return;

            string path = action.bindings.Count > _bindingIndex
                ? action.bindings[_bindingIndex].effectivePath
                : string.Empty;

            _bindingText.text = FormatPath(path);
        }

        // ── Rebind ─────────────────────────────────────────────────────────────

        private void StartRebinding()
        {
            if (_actionRef == null) return;

            var action = _actionRef.action;
            if (action == null) return;

            // Show overlay while waiting.
            if (_waitingOverlay != null) _waitingOverlay.SetActive(true);
            if (_rebindButton   != null) _rebindButton.interactable = false;

            action.Disable();

            _rebindOperation = action.PerformInteractiveRebinding(_bindingIndex)
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(_ => FinishRebind())
                .OnCancel(_ => CancelRebind())
                .Start();
        }

        private void FinishRebind()
        {
            _rebindOperation?.Dispose();
            _rebindOperation = null;

            _actionRef.action.Enable();

            if (_waitingOverlay != null) _waitingOverlay.SetActive(false);
            if (_rebindButton   != null) _rebindButton.interactable = true;

            UpdateBindingDisplay();
            OnBindingChanged?.Invoke();
        }

        private void CancelRebind()
        {
            _rebindOperation?.Dispose();
            _rebindOperation = null;

            _actionRef.action.Enable();

            if (_waitingOverlay != null) _waitingOverlay.SetActive(false);
            if (_rebindButton   != null) _rebindButton.interactable = true;
        }

        private void ResetBinding()
        {
            if (_actionRef == null) return;

            var action = _actionRef.action;
            if (action == null) return;

            action.RemoveBindingOverride(_bindingIndex);
            UpdateBindingDisplay();
            OnBindingChanged?.Invoke();
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Converts an InputSystem binding path like "&lt;Keyboard&gt;/space" into a
        /// human-readable label like "Space".
        /// </summary>
        private static string FormatPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "—";

            // Strip device prefix: "<Keyboard>/space" → "space"
            int slash = path.LastIndexOf('/');
            string name = slash >= 0 ? path.Substring(slash + 1) : path;

            // Strip angle-bracket wrappers if the whole path was just "<Device>"
            name = Regex.Replace(name, @"[<>]", string.Empty);

            // Capitalise first letter
            if (name.Length > 0)
                name = char.ToUpper(name[0]) + name.Substring(1);

            // Common shorthands
            name = name switch
            {
                "LeftButton"  => "LMB",
                "RightButton" => "RMB",
                "MiddleButton"=> "MMB",
                "LeftShift"   => "L.Shift",
                "RightShift"  => "R.Shift",
                "LeftCtrl"    => "L.Ctrl",
                "RightCtrl"   => "R.Ctrl",
                "LeftAlt"     => "L.Alt",
                "RightAlt"    => "R.Alt",
                _             => name
            };

            return name;
        }
    }
}
