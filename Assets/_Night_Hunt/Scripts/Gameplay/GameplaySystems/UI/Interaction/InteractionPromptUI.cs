using UnityEngine;
using TMPro;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Interaction;
using NightHunt.Gameplay.Input.Handlers.Interaction;
using NightHunt.GameplaySystems.UI;

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

        // Injected at runtime by GameHUDController.WireInteractionPrompt.
        private ActionProgressPresenter _progressPresenter;

        // ── Runtime ───────────────────────────────────────────────────────────

        private RaycastDetector          _detector;
        private PlayerInteractionSystem  _interactionSystem;
        private IInteractable            _lastTarget;
        private bool                     _presentingHoldProgress;

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
            HideSharedProgress();
        }

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            Hide();
        }

        /// <summary>
        /// Called by GameHUDController.WireInteractionPrompt after the local player spawns.
        /// </summary>
        public void BindProgress(ActionProgressPresenter presenter)
        {
            _progressPresenter = presenter;
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
                HideSharedProgress();
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

            if (isHolding && isHoldTarget)
            {
                if (!_presentingHoldProgress)
                {
                    _progressPresenter?.Show(
                        ActionProgressKind.Interaction,
                        target.InteractLabel ?? "Interact",
                        false);
                    _presentingHoldProgress = true;
                }

                _progressPresenter?.SetProgress(ActionProgressKind.Interaction, _interactionSystem.HoldProgress);
            }
            else
            {
                HideSharedProgress();
            }
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

        private void HideSharedProgress()
        {
            if (_presentingHoldProgress && _progressPresenter != null)
                _progressPresenter.Hide(ActionProgressKind.Interaction);

            _presentingHoldProgress = false;
        }
    }
}
