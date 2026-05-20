using System.Collections.Generic;
using NightHunt.Config;
using Michsky.UI.Shift;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

namespace NightHunt.UI.Settings
{
    [DisallowMultipleComponent]
    public sealed class GraphicsSettingsPanel : MonoBehaviour
    {
        [Header("Quality Preset")]
        [SerializeField] private TMP_Dropdown qualityDropdown;

        [Header("Resolution")]
        [SerializeField] private TMP_Dropdown resolutionDropdown;

        [Header("Toggles")]
        [SerializeField] private Toggle vsyncToggle;
        [SerializeField] private SwitchManager vsyncSwitch;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private SwitchManager fullscreenSwitch;
        [SerializeField] private SwitchManager bloomSwitch;
        [SerializeField] private SwitchManager motionBlurSwitch;

        [Header("Selectors")]
        [SerializeField] private HorizontalSelector antiAliasingSelector;
        [SerializeField] private HorizontalSelector anisotropicSelector;
        [SerializeField] private HorizontalSelector shadowQualitySelector;
        [SerializeField] private HorizontalSelector textureQualitySelector;

        [Header("Sliders")]
        [SerializeField] private Slider fovSlider;
        [SerializeField] private TextMeshProUGUI fovValueLabel;
        [SerializeField] private Slider drawDistanceSlider;
        [SerializeField] private TextMeshProUGUI drawDistanceValueLabel;

        [Header("Apply / Reset Buttons")]
        [SerializeField] private Button applyButton;
        [SerializeField] private GameObject pendingLabel;

        private Resolution[] _availableResolutions;
        private int _pendingResolutionIndex = -1;
        private int _appliedResolutionIndex;

        private void Start()
        {
            BuildQualityDropdown();
            BuildResolutionDropdown();
            LoadAndApply();
            WireCallbacks();
            SetPendingLabel(false);
        }

        public void RefreshFromPrefs()
        {
            BuildQualityDropdown();
            BuildResolutionDropdown();
            LoadAndApply();
            SetPendingLabel(false);
        }

        private void OnDestroy()
        {
            if (qualityDropdown != null) qualityDropdown.onValueChanged.RemoveListener(HandleQualityChanged);
            if (resolutionDropdown != null) resolutionDropdown.onValueChanged.RemoveListener(HandleResolutionDropdownChanged);
            if (vsyncToggle != null) vsyncToggle.onValueChanged.RemoveListener(HandleVSyncChanged);
            if (fullscreenToggle != null) fullscreenToggle.onValueChanged.RemoveListener(HandleFullscreenChanged);
            if (fovSlider != null) fovSlider.onValueChanged.RemoveListener(HandleFOVChanged);
            if (drawDistanceSlider != null) drawDistanceSlider.onValueChanged.RemoveListener(HandleDrawDistanceChanged);
            if (applyButton != null) applyButton.onClick.RemoveListener(Apply);
        }

        private void BuildQualityDropdown()
        {
            if (qualityDropdown == null) return;
            qualityDropdown.ClearOptions();
            var options = new List<TMP_Dropdown.OptionData>();
            foreach (var n in QualitySettings.names) options.Add(new TMP_Dropdown.OptionData(n));
            qualityDropdown.AddOptions(options);
        }

        private void BuildResolutionDropdown()
        {
            if (resolutionDropdown == null) return;
            _availableResolutions = Screen.resolutions;
            resolutionDropdown.ClearOptions();
            var options = new List<TMP_Dropdown.OptionData>();
            foreach (var r in _availableResolutions)
                options.Add(new TMP_Dropdown.OptionData($"{r.width} × {r.height} @ {r.refreshRateRatio.value:F0}Hz"));
            resolutionDropdown.AddOptions(options);
        }

        private void LoadAndApply()
        {
            var settings = GameSettings.Instance;
            if (settings == null) return;

            if (qualityDropdown != null) qualityDropdown.SetValueWithoutNotify(settings.QualityLevel);
            if (vsyncToggle != null) vsyncToggle.SetIsOnWithoutNotify(settings.VSync);
            ShiftUIBridge.SetSwitchSilently(vsyncSwitch, settings.VSync);
            if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(settings.Fullscreen);
            ShiftUIBridge.SetSwitchSilently(fullscreenSwitch, settings.Fullscreen);

            SetSelectorIndex(antiAliasingSelector, settings.AntiAliasing == 0 ? 0 : settings.AntiAliasing == 2 ? 1 : settings.AntiAliasing == 4 ? 2 : 3);
            SetSelectorIndex(anisotropicSelector, settings.AnisotropicFiltering);
            SetSelectorIndex(shadowQualitySelector, settings.ShadowQuality);
            SetSelectorIndex(textureQualitySelector, settings.TextureQuality);

            ShiftUIBridge.SetSwitchSilently(bloomSwitch, settings.Bloom);
            ShiftUIBridge.SetSwitchSilently(motionBlurSwitch, settings.MotionBlur);

            int savedResIdx = settings.ResolutionIndex;
            if (savedResIdx == -1) savedResIdx = FindCurrentResolutionIndex();
            _appliedResolutionIndex = _pendingResolutionIndex = savedResIdx;
            if (resolutionDropdown != null) resolutionDropdown.SetValueWithoutNotify(savedResIdx);

            if (fovSlider != null) fovSlider.SetValueWithoutNotify(settings.FOV);
            UpdateFOVLabel(settings.FOV);
            if (drawDistanceSlider != null) drawDistanceSlider.SetValueWithoutNotify(settings.DrawDistance);
            UpdateDrawDistanceLabel(settings.DrawDistance);

            settings.ApplySettings();
        }

        private void SetSelectorIndex(HorizontalSelector selector, int index)
        {
            if (selector != null) { selector.index = index; selector.UpdateUI(); }
        }

        private void WireCallbacks()
        {
            if (qualityDropdown != null) qualityDropdown.onValueChanged.AddListener(HandleQualityChanged);
            if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(HandleResolutionDropdownChanged);
            if (vsyncToggle != null) vsyncToggle.onValueChanged.AddListener(HandleVSyncChanged);
            if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(HandleFullscreenChanged);
            if (fovSlider != null) fovSlider.onValueChanged.AddListener(HandleFOVChanged);
            if (drawDistanceSlider != null) drawDistanceSlider.onValueChanged.AddListener(HandleDrawDistanceChanged);

            if (vsyncSwitch != null) { vsyncSwitch.OnEvents.AddListener(() => HandleVSyncChanged(true)); vsyncSwitch.OffEvents.AddListener(() => HandleVSyncChanged(false)); }
            if (fullscreenSwitch != null) { fullscreenSwitch.OnEvents.AddListener(() => HandleFullscreenChanged(true)); fullscreenSwitch.OffEvents.AddListener(() => HandleFullscreenChanged(false)); }
            if (bloomSwitch != null) { bloomSwitch.OnEvents.AddListener(() => HandleBloomChanged(true)); bloomSwitch.OffEvents.AddListener(() => HandleBloomChanged(false)); }
            if (motionBlurSwitch != null) { motionBlurSwitch.OnEvents.AddListener(() => HandleMotionBlurChanged(true)); motionBlurSwitch.OffEvents.AddListener(() => HandleMotionBlurChanged(false)); }

            if (antiAliasingSelector != null) antiAliasingSelector.onValueChanged.AddListener(HandleAntiAliasingChanged);
            if (anisotropicSelector != null) anisotropicSelector.onValueChanged.AddListener(HandleAnisotropicChanged);
            if (shadowQualitySelector != null) shadowQualitySelector.onValueChanged.AddListener(HandleShadowQualityChanged);
            if (textureQualitySelector != null) textureQualitySelector.onValueChanged.AddListener(HandleTextureQualityChanged);

            if (applyButton != null) applyButton.onClick.AddListener(Apply);
        }

        private void HandleQualityChanged(int index) { if (GameSettings.Instance != null) { GameSettings.Instance.QualityLevel = index; GameSettings.Instance.ApplySettings(); GameSettings.Instance.SaveSettings(); } }
        private void HandleResolutionDropdownChanged(int index) { _pendingResolutionIndex = index; SetPendingLabel(_pendingResolutionIndex != _appliedResolutionIndex); }
        private void HandleVSyncChanged(bool on) { if (GameSettings.Instance != null) { GameSettings.Instance.VSync = on; GameSettings.Instance.ApplySettings(); GameSettings.Instance.SaveSettings(); } if (vsyncToggle != null && vsyncToggle.isOn != on) vsyncToggle.SetIsOnWithoutNotify(on); ShiftUIBridge.SetSwitchSilently(vsyncSwitch, on); }
        private void HandleFullscreenChanged(bool on) { if (GameSettings.Instance != null) { GameSettings.Instance.Fullscreen = on; GameSettings.Instance.ApplySettings(); GameSettings.Instance.SaveSettings(); } if (fullscreenToggle != null && fullscreenToggle.isOn != on) fullscreenToggle.SetIsOnWithoutNotify(on); ShiftUIBridge.SetSwitchSilently(fullscreenSwitch, on); }
        private void HandleFOVChanged(float value) { UpdateFOVLabel(value); if (GameSettings.Instance != null) { GameSettings.Instance.FOV = value; GameSettings.Instance.SaveSettings(); } }
        private void UpdateFOVLabel(float value) { if (fovValueLabel != null) fovValueLabel.text = value.ToString("F0"); }
        private void HandleDrawDistanceChanged(float value) { UpdateDrawDistanceLabel(value); if (GameSettings.Instance != null) { GameSettings.Instance.DrawDistance = value; GameSettings.Instance.SaveSettings(); } }
        private void UpdateDrawDistanceLabel(float value) { if (drawDistanceValueLabel != null) drawDistanceValueLabel.text = value.ToString("F0"); }
        private void HandleBloomChanged(bool on) { if (GameSettings.Instance != null) { GameSettings.Instance.Bloom = on; GameSettings.Instance.SaveSettings(); } }
        private void HandleMotionBlurChanged(bool on) { if (GameSettings.Instance != null) { GameSettings.Instance.MotionBlur = on; GameSettings.Instance.SaveSettings(); } }
        private void HandleAntiAliasingChanged(int index) { int val = index == 0 ? 0 : index == 1 ? 2 : index == 2 ? 4 : 8; if (GameSettings.Instance != null) { GameSettings.Instance.AntiAliasing = val; GameSettings.Instance.ApplySettings(); GameSettings.Instance.SaveSettings(); } }
        private void HandleAnisotropicChanged(int index) { if (GameSettings.Instance != null) { GameSettings.Instance.AnisotropicFiltering = index; GameSettings.Instance.ApplySettings(); GameSettings.Instance.SaveSettings(); } }
        private void HandleShadowQualityChanged(int index) { if (GameSettings.Instance != null) { GameSettings.Instance.ShadowQuality = index; GameSettings.Instance.ApplySettings(); GameSettings.Instance.SaveSettings(); } }
        private void HandleTextureQualityChanged(int index) { if (GameSettings.Instance != null) { GameSettings.Instance.TextureQuality = index; GameSettings.Instance.ApplySettings(); GameSettings.Instance.SaveSettings(); } }

        public void Apply() { if (GameSettings.Instance != null && _pendingResolutionIndex != _appliedResolutionIndex) { GameSettings.Instance.ResolutionIndex = _pendingResolutionIndex; GameSettings.Instance.ApplySettings(); GameSettings.Instance.SaveSettings(); _appliedResolutionIndex = _pendingResolutionIndex; SetPendingLabel(false); } }
        public void ResetToDefaults() { if (GameSettings.Instance != null) { GameSettings.Instance.ResetToDefaults(); LoadAndApply(); } }

        private void OnValidate()
        {
            var content = transform.Find("Content/List/List Content");
            if (content == null) return;
            if (qualityDropdown == null) qualityDropdown = content.Find("Quality Preset")?.GetComponentInChildren<TMP_Dropdown>();
            if (resolutionDropdown == null) resolutionDropdown = content.Find("Resolution")?.GetComponentInChildren<TMP_Dropdown>();
            if (vsyncSwitch == null) vsyncSwitch = content.Find("V-Sync")?.GetComponentInChildren<SwitchManager>();
            if (fullscreenSwitch == null) fullscreenSwitch = content.Find("Window mode")?.GetComponentInChildren<SwitchManager>();
            if (bloomSwitch == null) bloomSwitch = content.Find("Bloom")?.GetComponentInChildren<SwitchManager>();
            if (motionBlurSwitch == null) motionBlurSwitch = content.Find("Motion blur")?.GetComponentInChildren<SwitchManager>();
            if (antiAliasingSelector == null) antiAliasingSelector = content.Find("Anti-Aliasing")?.GetComponentInChildren<HorizontalSelector>();
            if (anisotropicSelector == null) anisotropicSelector = content.Find("Anisotropic filtering")?.GetComponentInChildren<HorizontalSelector>();
            if (shadowQualitySelector == null) shadowQualitySelector = content.Find("Shadows")?.GetComponentInChildren<HorizontalSelector>();
            if (textureQualitySelector == null) textureQualitySelector = content.Find("Textures")?.GetComponentInChildren<HorizontalSelector>();
            if (fovSlider == null) fovSlider = content.Find("Field of view")?.GetComponentInChildren<Slider>();
            if (fovValueLabel == null && fovSlider != null) fovValueLabel = fovSlider.transform.Find("Value")?.GetComponent<TextMeshProUGUI>();
            if (drawDistanceSlider == null) drawDistanceSlider = content.Find("Draw distance")?.GetComponentInChildren<Slider>();
            if (drawDistanceValueLabel == null && drawDistanceSlider != null) drawDistanceValueLabel = drawDistanceSlider.transform.Find("Value")?.GetComponent<TextMeshProUGUI>();
            if (applyButton == null) applyButton = transform.Find("Content/Apply Button")?.GetComponent<Button>();
        }

        private int FindCurrentResolutionIndex()
        {
            if (_availableResolutions == null) return 0;
            int w = Screen.width; int h = Screen.height;
            for (int i = 0; i < _availableResolutions.Length; i++) { if (_availableResolutions[i].width == w && _availableResolutions[i].height == h) return i; }
            return _availableResolutions.Length - 1;
        }

        private void SetPendingLabel(bool active) { if (pendingLabel != null) pendingLabel.SetActive(active); }
    }
}