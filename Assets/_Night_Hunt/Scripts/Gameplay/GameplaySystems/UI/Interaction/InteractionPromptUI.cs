using UnityEngine;
using UnityEngine.Serialization;
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
        [SerializeField] private TextMeshProUGUI _actionLabelText;
        [SerializeField] private TextMeshProUGUI _keyText;
        [FormerlySerializedAs("_actionText")]
        [SerializeField] private TextMeshProUGUI _descriptionText;

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
            ResolvePromptTextReferences();
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
            ResolvePromptTextReferences();

            if (_promptPanel != null)
                _promptPanel.SetActive(true);

            bool isHold = target is IHoldInteractable h && h.HoldDuration > 0f;
            bool isPickup = target is IPickupable;
            PromptParts prompt = BuildPromptParts(target, isHold, isPickup);

            bool hasActionLabel = _actionLabelText != null;

            if (_actionLabelText != null)
                _actionLabelText.text = prompt.ActionLabel;

            if (_keyText != null)
                _keyText.text = prompt.Key;

            if (_descriptionText != null)
                _descriptionText.text = hasActionLabel ? prompt.Description : prompt.DescriptionWithInlineAction;

            PhaseTestLog.Log(
                PhaseTestLogCategory.Interaction,
                "PromptShow",
                $"target={DescribeTarget(target)} action={(_actionLabelText != null ? _actionLabelText.text : "null")} key={(_keyText != null ? _keyText.text : "null")} description={(_descriptionText != null ? _descriptionText.text : "null")} hold={isHold} pickup={isPickup}",
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

        private void ResolvePromptTextReferences()
        {
            if (_promptPanel == null)
                return;

            var texts = _promptPanel.GetComponentsInChildren<TextMeshProUGUI>(true);

            if (_actionLabelText == null)
                _actionLabelText = FindPromptText(texts, _keyText, _descriptionText, "Label_Press", "Lbl_Press", "ActionLabel", "Action_Label", "ActionType", "Action_Type");

            if (_actionLabelText == null)
                _actionLabelText = FindPromptTextByValue(texts, _keyText, _descriptionText, "Press", "Hold", "Click");

            if (_descriptionText == null)
                _descriptionText = FindPromptText(texts, _keyText, _actionLabelText, "Label_Action", "Lbl_Action", "ActionText", "Description", "Label_Description", "Lbl_Description");
        }

        private static TextMeshProUGUI FindPromptText(TextMeshProUGUI[] texts, TextMeshProUGUI excludeA, TextMeshProUGUI excludeB, params string[] names)
        {
            if (texts == null)
                return null;

            for (int i = 0; i < texts.Length; i++)
            {
                TextMeshProUGUI text = texts[i];
                if (text == null || text == excludeA || text == excludeB)
                    continue;

                string objectName = text.gameObject.name;
                for (int j = 0; j < names.Length; j++)
                {
                    if (string.Equals(objectName, names[j], System.StringComparison.OrdinalIgnoreCase))
                        return text;
                }
            }

            return null;
        }

        private static TextMeshProUGUI FindPromptTextByValue(TextMeshProUGUI[] texts, TextMeshProUGUI excludeA, TextMeshProUGUI excludeB, params string[] values)
        {
            if (texts == null)
                return null;

            for (int i = 0; i < texts.Length; i++)
            {
                TextMeshProUGUI text = texts[i];
                if (text == null || text == excludeA || text == excludeB)
                    continue;

                string current = text.text?.Trim();
                for (int j = 0; j < values.Length; j++)
                {
                    if (string.Equals(current, values[j], System.StringComparison.OrdinalIgnoreCase))
                        return text;
                }
            }

            return null;
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

        private static PromptParts BuildPromptParts(IInteractable target, bool isHold, bool isPickup)
        {
            string fallbackDescription = isPickup ? "Pick up" : "Interact";
            string label = SafeInteractLabel(target, fallbackDescription);
            string description = StripLeadingKeyHint(label);
            string actionLabel = isHold ? "Hold" : "Press";
            string key = isPickup ? "F" : "E";

            if (TryReadLeadingKeyHint(label, out string hint))
                ApplyKeyHint(hint, ref actionLabel, ref key);

            if (isHold)
                actionLabel = "Hold";

            if (isPickup)
                key = "F";

            if (string.IsNullOrWhiteSpace(description))
                description = fallbackDescription;

            return new PromptParts(actionLabel, key, description.Trim());
        }

        private static bool TryReadLeadingKeyHint(string label, out string hint)
        {
            hint = null;
            if (string.IsNullOrWhiteSpace(label))
                return false;

            string trimmed = label.TrimStart();
            if (!trimmed.StartsWith("["))
                return false;

            int end = trimmed.IndexOf(']');
            if (end <= 1)
                return false;

            hint = trimmed.Substring(1, end - 1).Trim();
            return !string.IsNullOrWhiteSpace(hint);
        }

        private static void ApplyKeyHint(string hint, ref string actionLabel, ref string key)
        {
            if (string.IsNullOrWhiteSpace(hint))
                return;

            string trimmed = hint.Trim();
            if (TryConsumeActionPrefix(trimmed, "Hold", out string holdKey))
            {
                actionLabel = "Hold";
                string parsedKey = NormalizeKeyText(holdKey);
                if (!string.IsNullOrWhiteSpace(parsedKey))
                    key = parsedKey;
                return;
            }

            if (TryConsumeActionPrefix(trimmed, "Press", out string pressKey))
            {
                actionLabel = "Press";
                string parsedKey = NormalizeKeyText(pressKey);
                if (!string.IsNullOrWhiteSpace(parsedKey))
                    key = parsedKey;
                return;
            }

            if (TryConsumeActionPrefix(trimmed, "Click", out string clickKey))
            {
                actionLabel = "Click";
                string parsedKey = NormalizeKeyText(clickKey);
                if (!string.IsNullOrWhiteSpace(parsedKey))
                    key = parsedKey;
                return;
            }

            key = NormalizeKeyText(trimmed);
        }

        private static bool TryConsumeActionPrefix(string value, string prefix, out string key)
        {
            key = null;
            if (!value.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                return false;

            if (value.Length == prefix.Length)
            {
                key = string.Empty;
                return true;
            }

            if (!char.IsWhiteSpace(value[prefix.Length]))
                return false;

            key = value.Substring(prefix.Length).Trim();
            return true;
        }

        private static string NormalizeKeyText(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return key;

            string trimmed = key.Trim();
            if (trimmed.Length >= 2 && trimmed[0] == '[' && trimmed[trimmed.Length - 1] == ']')
                trimmed = trimmed.Substring(1, trimmed.Length - 2).Trim();

            return trimmed;
        }

        private readonly struct PromptParts
        {
            public PromptParts(string actionLabel, string key, string description)
            {
                ActionLabel = actionLabel;
                Key = key;
                Description = description;
            }

            public string ActionLabel { get; }
            public string Key { get; }
            public string Description { get; }
            public string DescriptionWithInlineAction => string.Equals(ActionLabel, "Press", System.StringComparison.OrdinalIgnoreCase)
                ? Description
                : $"{ActionLabel} {Description}".Trim();
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
