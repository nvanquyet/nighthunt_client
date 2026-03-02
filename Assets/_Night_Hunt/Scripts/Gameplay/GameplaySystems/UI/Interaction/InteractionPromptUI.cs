using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Interaction;
using NightHunt.Gameplay.Input.Handlers.Interaction;

namespace NightHunt.GameplaySystems.UI.Interaction
{
    /// <summary>
    /// Displays a contextual interaction hint at the centre-bottom of the screen.
    ///
    /// Modes
    ///   • Instant interact — "[E]  Open door"
    ///   • Hold interact    — "[E]  Open chest"  +  progress bar filling
    ///   • Hidden           — no interaction target in range
    ///
    /// Setup:
    ///   1. Add to the GameHUD canvas (child of the HUD root).
    ///   2. Call <see cref="Init"/> once the local player is spawned.
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Panel")]
        [SerializeField] private GameObject _promptPanel;

        [Header("Text")]
        [SerializeField] private TextMeshProUGUI _keyText;        // "[E]"
        [SerializeField] private TextMeshProUGUI _actionText;     // "Pick up AK-47"

        [Header("Hold progress bar (only shown during hold)")]
        [SerializeField] private GameObject   _holdProgressRoot;
        [SerializeField] private Slider       _holdProgressSlider;

        // ── Runtime ───────────────────────────────────────────────────────────

        private RaycastDetector          _detector;
        private PlayerInteractionSystem  _interactionSystem;
        private IInteractable            _lastTarget;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Call after the local player is spawned and its components are ready.
        /// </summary>
        public void Init(RaycastDetector detector, PlayerInteractionSystem interactionSystem)
        {
            _detector          = detector;
            _interactionSystem = interactionSystem;
        }

        public void Hide()
        {
            if (_promptPanel != null) _promptPanel.SetActive(false);
        }

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            Hide();
            if (_holdProgressRoot != null) _holdProgressRoot.SetActive(false);
        }

        private void Update()
        {
            if (_detector == null)
            {
                Hide();
                return;
            }

            var target = _detector.CurrentInteractable;

            // ── No target ────────────────────────────────────────────────────
            if (target == null)
            {
                if (_lastTarget != null) Hide();
                _lastTarget = null;
                return;
            }

            // ── Target changed ───────────────────────────────────────────────
            if (target != _lastTarget)
            {
                _lastTarget = target;
                ShowForTarget(target);
            }

            // ── Hold progress update ─────────────────────────────────────────
            bool isHolding = _interactionSystem != null && _interactionSystem.IsHolding;
            bool isHoldTarget = target is IHoldInteractable h && h.HoldDuration > 0f;

            if (_holdProgressRoot != null)
                _holdProgressRoot.SetActive(isHolding && isHoldTarget);

            if (isHolding && isHoldTarget && _holdProgressSlider != null && _interactionSystem != null)
                _holdProgressSlider.value = _interactionSystem.HoldProgress;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void ShowForTarget(IInteractable target)
        {
            if (_promptPanel != null) _promptPanel.SetActive(true);

            bool isHold = target is IHoldInteractable h2 && h2.HoldDuration > 0f;

            if (_keyText != null)
                _keyText.text = isHold ? "[Hold E]" : "[E]";

            if (_actionText != null)
                _actionText.text = target.InteractLabel ?? "Interact";
        }
    }
}
