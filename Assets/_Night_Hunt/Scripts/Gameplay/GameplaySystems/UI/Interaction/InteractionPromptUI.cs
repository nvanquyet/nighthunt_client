using UnityEngine;
using TMPro;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Interaction;
using NightHunt.Gameplay.Input.Handlers.Interaction;
using NightHunt.GameplaySystems.UI;
using NightHunt.Diagnostics;

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
        private IInteractable            _lastBlockedTarget;
        private bool                     _presentingHoldProgress;
        private bool                     _loggedMissingDetector;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Call after the local player is spawned and its components are ready.
        /// </summary>
        public void Init(RaycastDetector detector, PlayerInteractionSystem interactionSystem)
        {
            _detector          = detector;
            _interactionSystem = interactionSystem;
            _loggedMissingDetector = false;
            PhaseTestLog.Log(
                PhaseTestLogCategory.Interaction,
                "PromptBound",
                $"detector={(detector != null ? detector.name : "null")} interactionSystem={(interactionSystem != null ? interactionSystem.name : "null")}",
                this);
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
                if (!_loggedMissingDetector)
                {
                    _loggedMissingDetector = true;
                    PhaseTestLog.Warning(
                        PhaseTestLogCategory.Interaction,
                        "PromptUnbound",
                        "reason=detector-null",
                        this);
                }

                Hide();
                return;
            }

            _loggedMissingDetector = false;

            var target = _detector.CurrentInteractable;
            GameObject interactor = _interactionSystem != null ? _interactionSystem.gameObject : null;

            // ── No target ────────────────────────────────────────────────────
            if (target == null)
            {
                if (_lastTarget != null)
                {
                    PhaseTestLog.Log(
                        PhaseTestLogCategory.Interaction,
                        "PromptHide",
                        $"reason=no-target previous={DescribeTarget(_lastTarget)}",
                        this);
                    Hide();
                }
                _lastTarget = null;
                _lastBlockedTarget = null;
                HideSharedProgress();
                return;
            }

            if (interactor != null && !target.CanInteract(interactor))
            {
                if (target != _lastBlockedTarget)
                {
                    _lastBlockedTarget = target;
                    PhaseTestLog.Log(
                        PhaseTestLogCategory.Interaction,
                        "PromptHide",
                        $"reason=blocked target={DescribeTarget(target)} interactor={interactor.name}",
                        this);
                }
                if (_lastTarget != null) Hide();
                _lastTarget = null;
                HideSharedProgress();
                return;
            }

            _lastBlockedTarget = null;

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
            bool isPickup = target is IPickupable;

            if (_keyText != null)
                _keyText.text = isPickup ? "[F]" : isHold ? "[Hold E]" : "[E]";

            if (_actionText != null)
                _actionText.text = StripLeadingKeyHint(target.InteractLabel ?? (isPickup ? "Pick up" : "Interact"));

            PhaseTestLog.Log(
                PhaseTestLogCategory.Interaction,
                "PromptShow",
                $"target={DescribeTarget(target)} key={(_keyText != null ? _keyText.text : "null")} label={(_actionText != null ? _actionText.text : "null")} hold={isHold} pickup={isPickup}",
                this);
        }

        private static string DescribeTarget(IInteractable target)
        {
            if (target == null)
                return "null";

            if (target is Component component)
                return $"{target.GetType().Name} go={component.name} layer={PhaseTestLog.DescribeLayer(component.gameObject)} label='{target.InteractLabel ?? "null"}'";

            return $"{target.GetType().Name} label='{target.InteractLabel ?? "null"}'";
        }

        private static string StripLeadingKeyHint(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return label;

            string trimmed = label.TrimStart();
            if (!trimmed.StartsWith("["))
                return label;

            int end = trimmed.IndexOf(']');
            return end >= 0 && end + 1 < trimmed.Length
                ? trimmed.Substring(end + 1).TrimStart()
                : label;
        }

        private void HideSharedProgress()
        {
            if (_presentingHoldProgress && _progressPresenter != null)
                _progressPresenter.Hide(ActionProgressKind.Interaction);

            _presentingHoldProgress = false;
        }
    }
}
