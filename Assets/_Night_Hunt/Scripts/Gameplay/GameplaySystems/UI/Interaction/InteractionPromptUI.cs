using UnityEngine;
using TMPro;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Interaction;
using NightHunt.Gameplay.Input;
using NightHunt.Gameplay.Input.Core;
using NightHunt.Gameplay.Input.Handlers.Interaction;
using NightHunt.GameplaySystems.UI;
using NightHunt.UI.Mobile;
using NightHunt.Diagnostics;

namespace NightHunt.GameplaySystems.UI.Interaction
{
    /// <summary>
    /// Owns contextual interaction presentation.
    /// Desktop shows the prompt panel; touch/mobile shows HUD action buttons.
    /// The mobile buttons still own their callback plumbing in MobileHUDPanel.
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject _promptPanel;

        [Header("Text")]
        [SerializeField] private TextMeshProUGUI _keyText;
        [SerializeField] private TextMeshProUGUI _actionText;

        [Header("Mobile")]
        [SerializeField] private MobileHUDPanel _mobileHUDPanel;

        private ActionProgressPresenter _progressPresenter;
        private RaycastDetector _detector;
        private PlayerInteractionSystem _interactionSystem;
        private ProximityInteractScanner _proximityScanner;
        private IInteractable _lastTarget;
        private IInteractable _lastBlockedTarget;
        private bool _presentingHoldProgress;
        private bool _loggedMissingDetector;
        private bool _lastMobileMode;

        public void Init(RaycastDetector detector, PlayerInteractionSystem interactionSystem)
        {
            _detector = detector;
            _interactionSystem = interactionSystem;
            _proximityScanner = ResolveProximityScanner(interactionSystem, detector);
            _loggedMissingDetector = false;
            ResolveMobileHUDPanel();

            PhaseTestLog.Log(
                PhaseTestLogCategory.Interaction,
                "PromptBound",
                $"detector={(detector != null ? detector.name : "null")} interactionSystem={(interactionSystem != null ? interactionSystem.name : "null")} proximity={(_proximityScanner != null ? _proximityScanner.name : "null")} mobileHud={(_mobileHUDPanel != null ? _mobileHUDPanel.name : "null")}",
                this);
        }

        public void BindProgress(ActionProgressPresenter presenter)
        {
            _progressPresenter = presenter;
        }

        public void Hide()
        {
            HideDesktopPrompt();
            HideMobileButtons();
            HideSharedProgress();
        }

        private void Awake()
        {
            ResolveMobileHUDPanel();
            Hide();
        }

        private void OnEnable()
        {
            var platform = PlatformInputDetector.Instance;
            if (platform != null)
                platform.OnPlatformChanged += HandlePlatformChanged;
        }

        private void OnDisable()
        {
            var platform = PlatformInputDetector.Instance;
            if (platform != null)
                platform.OnPlatformChanged -= HandlePlatformChanged;

            Hide();
        }

        private void Update()
        {
            if (_detector == null && _proximityScanner == null)
            {
                if (!_loggedMissingDetector)
                {
                    _loggedMissingDetector = true;
                    PhaseTestLog.Warning(
                        PhaseTestLogCategory.Interaction,
                        "PromptUnbound",
                        "reason=detector-and-proximity-null",
                        this);
                }

                Hide();
                return;
            }

            _loggedMissingDetector = false;

            GameObject interactor = ResolveInteractor();
            TargetContext context = ResolveTargetContext(interactor);
            IInteractable target = context.PromptTarget;

            // Clear stale references: underlying Unity Object destroyed (e.g. WorldItem despawned on reconnect)
            if (IsDestroyedInteractable(_lastTarget))
                _lastTarget = null;
            if (IsDestroyedInteractable(_lastBlockedTarget))
                _lastBlockedTarget = null;
            if (IsDestroyedInteractable(target))
                target = null;

            if (target == null)
            {
                if (_lastTarget != null)
                {
                    PhaseTestLog.Log(
                        PhaseTestLogCategory.Interaction,
                        "PromptHide",
                        $"reason=no-target previous={DescribeTarget(_lastTarget)}",
                        this);
                }

                _lastTarget = null;
                _lastBlockedTarget = null;
                Hide();
                return;
            }

            if (interactor != null && !CanTargetInteract(target, interactor))
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

                _lastTarget = null;
                Hide();
                return;
            }

            _lastBlockedTarget = null;

            bool isMobileMode = IsMobileMode();
            bool inputAllowed = IsGameplayInputAllowed();
            if (!inputAllowed)
            {
                if (_lastTarget != null)
                {
                    PhaseTestLog.Log(
                        PhaseTestLogCategory.Interaction,
                        "PromptHide",
                        $"reason=input-layer-blocked previous={DescribeTarget(_lastTarget)}",
                        this);
                }

                _lastTarget = null;
                Hide();
                return;
            }

            bool forcePresentation = target != _lastTarget || isMobileMode != _lastMobileMode;

            _lastTarget = target;
            _lastMobileMode = isMobileMode;

            ApplyTargetPresentation(context, inputAllowed, forcePresentation);
            UpdateHoldProgress(target);
        }

        private void ApplyTargetPresentation(TargetContext context, bool inputAllowed, bool forceLog)
        {
            if (IsMobileMode())
            {
                HideDesktopPrompt();
                ResolveMobileHUDPanel();
                _mobileHUDPanel?.SetInteractionActionButtonsVisible(
                    pickupVisible: inputAllowed && context.PickupTarget != null,
                    interactVisible: inputAllowed && context.InteractTarget != null,
                    inputAllowed: inputAllowed,
                    forceLog: forceLog);

                if (forceLog)
                {
                    PhaseTestLog.Log(
                        PhaseTestLogCategory.Interaction,
                        "PromptMobileActions",
                        $"pickup={DescribeTarget(context.PickupTarget)} interact={DescribeTarget(context.InteractTarget)} inputAllowed={inputAllowed}",
                        this);
                }

                return;
            }

            HideMobileButtons();
            if (forceLog || _promptPanel == null || !_promptPanel.activeSelf)
                ShowForTarget(context.PromptTarget);
        }

        private void UpdateHoldProgress(IInteractable target)
        {
            bool isHolding = _interactionSystem != null && _interactionSystem.IsHolding;
            bool isHoldTarget = target is IHoldInteractable h && h.HoldDuration > 0f;

            if (isHolding && isHoldTarget)
            {
                if (!_presentingHoldProgress)
                {
                    _progressPresenter?.Show(
                        ActionProgressKind.Interaction,
                        SafeInteractLabel(target, "Interact"),
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

        private TargetContext ResolveTargetContext(GameObject interactor)
        {
            EnsureInteractionSensors();

            IInteractable rayTarget = _detector != null ? _detector.CurrentInteractable : null;
            IInteractable interactTarget = null;
            IInteractable pickupTarget = null;

            if (IsValidPickupTarget(rayTarget, interactor))
                pickupTarget = rayTarget;
            else if (IsValidNonPickupInteractTarget(rayTarget, interactor))
                interactTarget = rayTarget;

            if (interactTarget == null)
                interactTarget = ResolveNearbyInteractTarget(interactor);

            if (pickupTarget == null)
                pickupTarget = ResolveNearbyPickupTarget(interactor);

            IInteractable promptTarget = null;
            if (rayTarget != null && (ReferenceEquals(rayTarget, interactTarget) || ReferenceEquals(rayTarget, pickupTarget)))
                promptTarget = rayTarget;
            else
                promptTarget = interactTarget ?? pickupTarget;

            return new TargetContext(promptTarget, interactTarget, pickupTarget);
        }

        private IInteractable ResolveNearbyInteractTarget(GameObject interactor)
        {
            if (_proximityScanner == null || interactor == null)
                return null;

            _proximityScanner.ForceScan();
            var nearby = _proximityScanner.NearbyInteractables;
            for (int i = 0; i < nearby.Count; i++)
            {
                if (IsValidNonPickupInteractTarget(nearby[i], interactor))
                    return nearby[i];
            }

            return null;
        }

        private IInteractable ResolveNearbyPickupTarget(GameObject interactor)
        {
            if (_proximityScanner == null || interactor == null)
                return null;

            _proximityScanner.ForceScan();

            var worldItems = _proximityScanner.NearbyWorldItems;
            for (int i = 0; i < worldItems.Count; i++)
            {
                if (IsValidPickupTarget(worldItems[i], interactor))
                    return worldItems[i];
            }

            var interactables = _proximityScanner.NearbyInteractables;
            for (int i = 0; i < interactables.Count; i++)
            {
                if (IsValidPickupTarget(interactables[i], interactor))
                    return interactables[i];
            }

            return null;
        }

        private void ShowForTarget(IInteractable target)
        {
            if (_promptPanel != null)
                _promptPanel.SetActive(true);

            bool isHold = target is IHoldInteractable h && h.HoldDuration > 0f;
            bool isPickup = target is IPickupable;

            if (_keyText != null)
                _keyText.text = isPickup ? "[F]" : isHold ? "[Hold E]" : "[E]";

            if (_actionText != null)
                _actionText.text = StripLeadingKeyHint(SafeInteractLabel(target, isPickup ? "Pick up" : "Interact"));

            PhaseTestLog.Log(
                PhaseTestLogCategory.Interaction,
                "PromptShow",
                $"target={DescribeTarget(target)} key={(_keyText != null ? _keyText.text : "null")} label={(_actionText != null ? _actionText.text : "null")} hold={isHold} pickup={isPickup}",
                this);
        }

        private GameObject ResolveInteractor()
        {
            if (_interactionSystem != null)
                return _interactionSystem.gameObject;

            if (_detector != null)
                return _detector.gameObject;

            return null;
        }

        private void EnsureInteractionSensors()
        {
            if (_proximityScanner == null)
                _proximityScanner = ResolveProximityScanner(_interactionSystem, _detector);
        }

        private static ProximityInteractScanner ResolveProximityScanner(PlayerInteractionSystem interactionSystem, RaycastDetector detector)
        {
            if (interactionSystem != null)
            {
                var scanner = interactionSystem.GetComponentInChildren<ProximityInteractScanner>(true)
                              ?? interactionSystem.GetComponentInParent<ProximityInteractScanner>(true);
                if (scanner != null)
                    return scanner;
            }

            if (detector != null)
            {
                var scanner = detector.GetComponentInChildren<ProximityInteractScanner>(true)
                              ?? detector.GetComponentInParent<ProximityInteractScanner>(true);
                if (scanner != null)
                    return scanner;
            }

            return null;
        }

        private void ResolveMobileHUDPanel()
        {
            if (_mobileHUDPanel != null)
                return;

            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                _mobileHUDPanel = canvas.GetComponentInChildren<MobileHUDPanel>(true);

            if (_mobileHUDPanel == null)
                _mobileHUDPanel = FindFirstObjectByType<MobileHUDPanel>(FindObjectsInactive.Include);
        }

        private void HandlePlatformChanged(PlatformInputDetector.InputPlatform _)
        {
            _lastTarget = null;
            _lastBlockedTarget = null;
            Hide();
        }

        private static bool IsMobileMode()
        {
            var platform = PlatformInputDetector.Instance;
            return platform != null ? platform.IsMobile : Application.isMobilePlatform;
        }

        private static bool IsGameplayInputAllowed()
        {
            var layers = InputLayerManager.Instance;
            return layers == null || layers.IsLayerActive(InputLayer.Player);
        }

        private static bool IsValidPickupTarget(IInteractable target, GameObject interactor)
        {
            return target is IPickupable
                && interactor != null
                && CanTargetInteract(target, interactor);
        }

        private static bool IsValidNonPickupInteractTarget(IInteractable target, GameObject interactor)
        {
            return target != null
                && target is not IPickupable
                && interactor != null
                && CanTargetInteract(target, interactor);
        }

        private void HideDesktopPrompt()
        {
            if (_promptPanel != null)
                _promptPanel.SetActive(false);
        }

        private void HideMobileButtons()
        {
            ResolveMobileHUDPanel();
            _mobileHUDPanel?.HideInteractionActionButtons();
        }

        private void HideSharedProgress()
        {
            if (_presentingHoldProgress && _progressPresenter != null)
                _progressPresenter.Hide(ActionProgressKind.Interaction);

            _presentingHoldProgress = false;
        }

        private static string DescribeTarget(IInteractable target)
        {
            if (target == null)
                return "null";

            // Guard: the underlying Unity Object may have been destroyed (e.g. WorldItem
            // despawned by FishNet during reconnect) while a C# reference still exists.
            if (IsDestroyedInteractable(target))
                return $"{target.GetType().Name} [DESTROYED]";

            if (target is Component component)
            {
                try
                {
                    return $"{target.GetType().Name} go={component.name} layer={PhaseTestLog.DescribeLayer(component.gameObject)} label='{SafeInteractLabel(target, "null")}'";
                }
                catch (MissingReferenceException)
                {
                    return $"{target.GetType().Name} [DESTROYED]";
                }
            }

            return $"{target.GetType().Name} label='{SafeInteractLabel(target, "null")}'";
        }

        private static bool CanTargetInteract(IInteractable target, GameObject interactor)
        {
            if (target == null || interactor == null || IsDestroyedInteractable(target))
                return false;

            try
            {
                return target.CanInteract(interactor);
            }
            catch (MissingReferenceException)
            {
                return false;
            }
        }

        private static string SafeInteractLabel(IInteractable target, string fallback)
        {
            if (target == null || IsDestroyedInteractable(target))
                return fallback;

            try
            {
                return target.InteractLabel ?? fallback;
            }
            catch (MissingReferenceException)
            {
                return fallback;
            }
        }

        private static bool IsDestroyedInteractable(IInteractable target)
        {
            if (target == null)
                return false;

            try
            {
                return target is Object unityObject && unityObject == null;
            }
            catch (MissingReferenceException)
            {
                return true;
            }
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

        private readonly struct TargetContext
        {
            public TargetContext(IInteractable promptTarget, IInteractable interactTarget, IInteractable pickupTarget)
            {
                PromptTarget = promptTarget;
                InteractTarget = interactTarget;
                PickupTarget = pickupTarget;
            }

            public IInteractable PromptTarget { get; }
            public IInteractable InteractTarget { get; }
            public IInteractable PickupTarget { get; }
        }
    }
}
