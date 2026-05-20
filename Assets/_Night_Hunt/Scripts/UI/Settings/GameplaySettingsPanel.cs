using NightHunt.Config;
using Michsky.UI.Shift;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NightHunt.UI.Settings
{
    [DisallowMultipleComponent]
    public sealed class GameplaySettingsPanel : MonoBehaviour
    {
        [Header("General")]
        [SerializeField] private HorizontalSelector languageSelector;
        [SerializeField] private SwitchManager tutorialsSwitch;
        [SerializeField] private SwitchManager devConsoleSwitch;
        [SerializeField] private SwitchManager quickSwapSwitch;

        [Header("UI & HUD")]
        [SerializeField] private SwitchManager hudSwitch;
        [SerializeField] private HorizontalSelector uiScaleSelector;
        [SerializeField] private HorizontalSelector crosshairSelector;

        [Header("Reset")]
        [SerializeField] private Button resetButton;

        private void Start() { LoadAndApply(); WireCallbacks(); }

        public void RefreshFromPrefs() { LoadAndApply(); }

        private void LoadAndApply()
        {
            var settings = GameSettings.Instance;
            if (settings == null) return;

            if (uiScaleSelector != null) { uiScaleSelector.index = Mathf.RoundToInt((settings.UIScale - 0.8f) / 0.1f); uiScaleSelector.UpdateUI(); }
            if (crosshairSelector != null) { crosshairSelector.index = settings.CrosshairType; crosshairSelector.UpdateUI(); }
            
            ShiftUIBridge.SetSwitchSilently(tutorialsSwitch, settings.EnableTutorials);
            ShiftUIBridge.SetSwitchSilently(devConsoleSwitch, settings.EnableDevConsole);
            ShiftUIBridge.SetSwitchSilently(quickSwapSwitch, settings.QuickSwap);
            
            if (languageSelector != null) { languageSelector.index = settings.Language == "English" ? 0 : 1; languageSelector.UpdateUI(); }
        }

        private void WireCallbacks()
        {
            if (uiScaleSelector != null) uiScaleSelector.onValueChanged.AddListener(HandleUIScaleChanged);
            if (crosshairSelector != null) crosshairSelector.onValueChanged.AddListener(HandleCrosshairChanged);
            if (tutorialsSwitch != null) { tutorialsSwitch.OnEvents.AddListener(() => HandleTutorialsChanged(true)); tutorialsSwitch.OffEvents.AddListener(() => HandleTutorialsChanged(false)); }
            if (devConsoleSwitch != null) { devConsoleSwitch.OnEvents.AddListener(() => HandleDevConsoleChanged(true)); devConsoleSwitch.OffEvents.AddListener(() => HandleDevConsoleChanged(false)); }
            if (quickSwapSwitch != null) { quickSwapSwitch.OnEvents.AddListener(() => HandleQuickSwapChanged(true)); quickSwapSwitch.OffEvents.AddListener(() => HandleQuickSwapChanged(false)); }
            if (languageSelector != null) languageSelector.onValueChanged.AddListener(HandleLanguageChanged);
            if (resetButton != null) resetButton.onClick.AddListener(ResetToDefaults);
        }

        private void HandleUIScaleChanged(int index) { float scale = 0.8f + (index * 0.1f); if (GameSettings.Instance != null) { GameSettings.Instance.UIScale = scale; GameSettings.Instance.SaveSettings(); GlobalUIController.Instance?.ApplyUIScale(scale); } }
        private void HandleCrosshairChanged(int index) { if (GameSettings.Instance != null) { GameSettings.Instance.CrosshairType = index; GameSettings.Instance.SaveSettings(); GlobalUIController.Instance?.ApplyCrosshair(index); } }
        private void HandleTutorialsChanged(bool on) { if (GameSettings.Instance != null) { GameSettings.Instance.EnableTutorials = on; GameSettings.Instance.SaveSettings(); } }
        private void HandleDevConsoleChanged(bool on) { if (GameSettings.Instance != null) { GameSettings.Instance.EnableDevConsole = on; GameSettings.Instance.SaveSettings(); } }
        private void HandleQuickSwapChanged(bool on) { if (GameSettings.Instance != null) { GameSettings.Instance.QuickSwap = on; GameSettings.Instance.SaveSettings(); } }
        private void HandleLanguageChanged(int index) { string lang = index == 0 ? "English" : "Vietnamese"; if (GameSettings.Instance != null) { GameSettings.Instance.Language = lang; GameSettings.Instance.SaveSettings(); } }

        public void ResetToDefaults() { if (GameSettings.Instance != null) { GameSettings.Instance.ResetToDefaults(); LoadAndApply(); GlobalUIController.Instance?.ApplyAll(); } }

        private void OnValidate()
        {
            var content = transform.Find("Content/List/List Content");
            if (content == null) return;
            if (uiScaleSelector == null) uiScaleSelector = content.Find("UI Scale")?.GetComponentInChildren<HorizontalSelector>();
            if (crosshairSelector == null) crosshairSelector = content.Find("Crosshair")?.GetComponentInChildren<HorizontalSelector>();
            if (languageSelector == null) languageSelector = content.Find("Language")?.GetComponentInChildren<HorizontalSelector>();
            if (tutorialsSwitch == null) tutorialsSwitch = content.Find("Enable tutorials")?.GetComponentInChildren<SwitchManager>();
            if (devConsoleSwitch == null) devConsoleSwitch = content.Find("Enable dev console")?.GetComponentInChildren<SwitchManager>();
            if (quickSwapSwitch == null) quickSwapSwitch = content.Find("Quick swap")?.GetComponentInChildren<SwitchManager>();
            if (hudSwitch == null) hudSwitch = content.Find("Enable HUD")?.GetComponentInChildren<SwitchManager>();
            if (resetButton == null) resetButton = transform.Find("Content/Reset Button")?.GetComponent<Button>();
        }
    }
}