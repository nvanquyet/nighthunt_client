using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.StatSystem.Configs;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Renders a single player stat row (icon + label + slider + value text).
    /// Spawned dynamically by PlayerStatUIPanel based on PlayerStatUIConfig.
    /// </summary>
    public class PlayerStatUIView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image              _iconImage;
        [SerializeField] private TextMeshProUGUI    _labelText;
        [SerializeField] private Slider             _valueSlider;
        [SerializeField] private TextMeshProUGUI    _valueText;
        [SerializeField] private Image              _sliderFillImage;   // Slider fill child image

        private PlayerStatType          _statType;
        private PlayerStatUIDefinition  _uiDef;
        private UIDomainBridge          _domainBridge;

        // ─────────────────────────────────────────────────────────────────────
        //  Init
        // ─────────────────────────────────────────────────────────────────────

        public void Initialize(PlayerStatType statType, PlayerStatUIDefinition uiDef, UIDomainBridge bridge)
        {
            _statType     = statType;
            _uiDef        = uiDef;
            _domainBridge = bridge;

            // Debug log khi initialize
            if (statType == PlayerStatType.CurrentWeight || statType == PlayerStatType.WeightCapacity)
            {
                Debug.Log($"[PlayerStatUIView] Initialize: {statType}, bridge.IsReady={bridge?.IsReady}, bridge.Bridge={bridge?.Bridge}");
            }

            ApplyStaticUI();
            UpdateValue();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Static setup (runs once on Initialize)
        // ─────────────────────────────────────────────────────────────────────

        private void ApplyStaticUI()
        {
            // Resolve text color: use TextColor when alpha is meaningful, otherwise fall back to DisplayColor
            Color textColor = _uiDef.TextColor.a > 0.01f ? _uiDef.TextColor : _uiDef.DisplayColor;

            // Icon
            if (_iconImage != null)
            {
                if (_uiDef.Icon != null)
                {
                    _iconImage.sprite = _uiDef.Icon;
                    _iconImage.color  = Color.white;    // Keep original sprite colors
                }
                else
                {
                    _iconImage.color = _uiDef.DisplayColor; // Tint placeholder with accent
                }
            }

            // Label
            if (_labelText != null)
            {
                _labelText.text  = _uiDef.DisplayName;
                _labelText.color = textColor;
            }

            // Value text (color only – content set in UpdateValue)
            if (_valueText != null)
            {
                _valueText.color = textColor;
            }

            // Slider fill – accent color
            if (_sliderFillImage != null)
            {
                _sliderFillImage.color = _uiDef.DisplayColor;
            }

            // Slider range setup
            if (_valueSlider != null)
            {
                _valueSlider.minValue = 0f;
                _valueSlider.maxValue = 1f;

                // Text-only stats: lock slider at full so it acts as a decorative bar
                if (_uiDef.DisplayType == StatDisplayType.Text)
                {
                    _valueSlider.value = 1f;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Dynamic update (called on stat change events)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Re-reads current value from the bridge and refreshes the row.
        /// </summary>
        public void UpdateValue()
        {
            if (_domainBridge == null)
            {
                if (_statType == PlayerStatType.CurrentWeight || _statType == PlayerStatType.WeightCapacity)
                {
                    Debug.LogWarning($"[PlayerStatUIView] UpdateValue: _domainBridge is null for {_statType}");
                }
                return;
            }

            if (!_domainBridge.IsReady)
            {
                if (_statType == PlayerStatType.CurrentWeight || _statType == PlayerStatType.WeightCapacity)
                {
                    Debug.LogWarning($"[PlayerStatUIView] UpdateValue: bridge not ready for {_statType}");
                }
                return;
            }

            if (_domainBridge.Bridge == null)
            {
                if (_statType == PlayerStatType.CurrentWeight || _statType == PlayerStatType.WeightCapacity)
                {
                    Debug.LogWarning($"[PlayerStatUIView] UpdateValue: bridge.Bridge is null for {_statType}");
                }
                return;
            }

            float currentValue = _domainBridge.Bridge.GetStat(_statType);
            UpdateValue(currentValue);
        }

        /// <summary>
        /// Refreshes the row with a known current value.
        /// </summary>
        public void UpdateValue(float currentValue)
        {
            // Debug log để track value updates
            if (_statType == PlayerStatType.CurrentWeight || _statType == PlayerStatType.WeightCapacity)
            {
                Debug.Log($"[PlayerStatUIView] UpdateValue({_statType}): currentValue={currentValue:F1}");
            }

            // Fetch max value when needed for slider or "current/max" text
            float maxValue = GetMaxValue();

            UpdateSlider(currentValue, maxValue);
            UpdateText(currentValue, maxValue);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        private float GetMaxValue()
        {
            // Only needed for slider-based display types
            if (_uiDef.DisplayType != StatDisplayType.SliderWithMax &&
                _uiDef.DisplayType != StatDisplayType.ProgressBar)
                return 0f;

            // RelatedMaxStatType == Type means no dynamic max
            if (_uiDef.RelatedMaxStatType == _statType) return 0f;

            if (_domainBridge == null || !_domainBridge.IsReady) return 0f;

            return _domainBridge.Bridge.GetStat(_uiDef.RelatedMaxStatType);
        }

        private void UpdateSlider(float current, float max)
        {
            if (_valueSlider == null) return;

            switch (_uiDef.DisplayType)
            {
                case StatDisplayType.SliderWithMax:
                    _valueSlider.value = max > 0f ? Mathf.Clamp01(current / max) : 0f;
                    break;

                case StatDisplayType.ProgressBar:
                    // Allow > 1 to visually represent overweight
                    _valueSlider.value = max > 0f ? Mathf.Clamp(current / max, 0f, 1.5f) : 0f;
                    break;

                case StatDisplayType.SliderWithRange:
                    // Normalize within [MinValue, MaxValue] from UI config (stored per-stat in UIDefinition)
                    // We don't store min/max in UIDefinition, so we keep slider at 1 for Text-like display.
                    // Subclasses or extensions can override if a MinValue/MaxValue is needed.
                    _valueSlider.value = 1f;
                    break;

                case StatDisplayType.Text:
                    _valueSlider.value = 1f;    // Always full – decorative bar
                    break;
            }
        }

        private void UpdateText(float current, float max)
        {
            if (_valueText == null) return;

            switch (_uiDef.DisplayType)
            {
                case StatDisplayType.SliderWithMax:
                case StatDisplayType.ProgressBar:
                    if (_uiDef.ShowMaxValue && max > 0f)
                    {
                        _valueText.text = $"{current.ToString(_uiDef.DisplayFormat)}/{max.ToString(_uiDef.DisplayFormat)}";
                    }
                    else
                    {
                        _valueText.text = current.ToString(_uiDef.DisplayFormat);
                    }
                    break;

                default:    // SliderWithRange, Text
                    _valueText.text = current.ToString(_uiDef.DisplayFormat);
                    break;
            }
        }
    }
}
