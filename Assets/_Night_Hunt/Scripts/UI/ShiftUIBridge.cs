using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MUIPCustomDropdown = Michsky.MUIP.CustomDropdown;
using ShiftFriendsPanelManager = Michsky.UI.Shift.FriendsPanelManager;
using ShiftMainButton = Michsky.UI.Shift.MainButton;
using ShiftModalWindowManager = Michsky.UI.Shift.ModalWindowManager;
using ShiftSliderManager = Michsky.UI.Shift.SliderManager;
using ShiftSwitchManager = Michsky.UI.Shift.SwitchManager;

namespace NightHunt.UI
{
    /// <summary>
    /// Thin adapter over Shift UI / MUIP presentation components.
    /// NightHunt controllers own business flow and lifecycle; this class only
    /// syncs visual widgets or starts package animations in a consistent way.
    /// </summary>
    public static class ShiftUIBridge
    {
        public const string SwitchOnState = "Switch On";
        public const string SwitchOffState = "Switch Off";

        public static bool PlayAnimatorState(Animator animator, string stateName, bool crossFade = false, float fadeDuration = 0.1f)
        {
            if (animator == null ||
                animator.runtimeAnimatorController == null ||
                !animator.isActiveAndEnabled ||
                !animator.gameObject.activeInHierarchy ||
                string.IsNullOrWhiteSpace(stateName))
                return false;

            if (crossFade)
                animator.CrossFade(stateName, fadeDuration);
            else
                animator.Play(stateName);

            return true;
        }

        public static void OpenModal(ShiftModalWindowManager modalManager)
        {
            if (modalManager == null)
                return;

            if (!EnsureModalAnimator(modalManager))
                return;

            modalManager.ModalWindowIn();
        }

        public static void CloseModal(ShiftModalWindowManager modalManager)
        {
            if (modalManager == null)
                return;

            if (!EnsureModalAnimator(modalManager))
                return;

            modalManager.ModalWindowOut();
        }

        public static bool OpenShiftWindow(Component owner)
        {
            var friendsPanel = ResolveShiftWindow(owner);
            if (friendsPanel == null)
                return false;

            if (!EnsureWindowAnimator(friendsPanel))
                return false;

            friendsPanel.WindowIn();
            return true;
        }

        public static bool CloseShiftWindow(Component owner)
        {
            var friendsPanel = ResolveShiftWindow(owner);
            if (friendsPanel == null)
                return false;

            if (!EnsureWindowAnimator(friendsPanel))
                return false;

            friendsPanel.WindowOut();
            return true;
        }

        public static bool GetSwitchValue(ShiftSwitchManager switchManager, bool fallback = false)
        {
            return switchManager != null ? switchManager.isOn : fallback;
        }

        public static void SetSwitchSilently(ShiftSwitchManager switchManager, bool isOn)
        {
            if (switchManager == null)
                return;

            EnsureSwitchAnimator(switchManager);
            switchManager.isOn = isOn;

            if (switchManager.switchAnimator != null)
                switchManager.switchAnimator.Play(isOn ? SwitchOnState : SwitchOffState);

            if (switchManager.saveValue)
                PlayerPrefs.SetString(switchManager.switchTag + "Switch", isOn ? "true" : "false");
        }

        public static void ToggleSwitchByUser(ShiftSwitchManager switchManager)
        {
            if (switchManager == null)
                return;

            EnsureSwitchAnimator(switchManager);
            switchManager.AnimateSwitch();
        }

        public static Slider ResolveUnitySlider(ShiftSliderManager shiftSlider)
        {
            return shiftSlider != null ? shiftSlider.GetComponent<Slider>() : null;
        }

        public static void SetMainButtonLabel(ShiftMainButton mainButton, string text)
        {
            if (mainButton == null)
                return;

            string safeText = text ?? string.Empty;
            mainButton.buttonText = safeText;

            SetText(mainButton.normalText, safeText);
            SetText(mainButton.highlightedText, safeText);
            SetText(mainButton.pressedText, safeText);
        }

        public static void SetDropdownIndexSilently(MUIPCustomDropdown dropdown, int index)
        {
            if (dropdown == null || dropdown.items == null || dropdown.items.Count == 0)
                return;

            int safeIndex = Mathf.Clamp(index, 0, dropdown.items.Count - 1);
            dropdown.SetDropdownIndex(safeIndex, bypassSound: true);
        }

        private static ShiftFriendsPanelManager ResolveShiftWindow(Component owner)
        {
            if (owner == null)
                return null;

            return owner.GetComponentInParent<ShiftFriendsPanelManager>(true)
                ?? owner.GetComponentInChildren<ShiftFriendsPanelManager>(true);
        }

        private static void EnsureSwitchAnimator(ShiftSwitchManager switchManager)
        {
            if (switchManager != null && switchManager.switchAnimator == null)
                switchManager.switchAnimator = switchManager.GetComponent<Animator>();
        }

        private static bool EnsureModalAnimator(ShiftModalWindowManager modalManager)
        {
            SetPrivateAnimatorIfMissing(modalManager, "mWindowAnimator");
            return modalManager != null && modalManager.GetComponent<Animator>() != null;
        }

        private static bool EnsureWindowAnimator(ShiftFriendsPanelManager windowManager)
        {
            if (windowManager == null)
                return false;

            // FriendsPanelManager initializes its private animator in Start().
            // Runtime callback fallbacks can happen before Start, so seed it here.
            SetPrivateAnimatorIfMissing(windowManager, "windowAnimator");
            return windowManager.GetComponent<Animator>() != null;
        }

        private static void SetPrivateAnimatorIfMissing(Component component, string fieldName)
        {
            if (component == null)
                return;

            var field = component.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (field == null || field.GetValue(component) != null)
                return;

            field.SetValue(component, component.GetComponent<Animator>());
        }

        private static void SetText(TextMeshProUGUI label, string text)
        {
            if (label != null)
                label.text = text;
        }
    }
}
