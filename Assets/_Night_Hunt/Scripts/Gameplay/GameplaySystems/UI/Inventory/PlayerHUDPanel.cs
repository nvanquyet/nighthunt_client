using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.StatSystem.Core.Types;
using NightHunt.StatSystem.Configs;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// HUD panel that shows Health / Stamina / Speed / Armor / Weight.
    /// Reads live data from UIDomainBridge and display metadata from PlayerStatUIConfig.
    /// </summary>
    public class PlayerHUDPanel : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField] private Slider             _healthSlider;
        [SerializeField] private TextMeshProUGUI    _healthText;

        [Header("Stamina")]
        [SerializeField] private Slider             _staminaSlider;
        [SerializeField] private TextMeshProUGUI    _staminaText;

        [Header("Speed")]
        [SerializeField] private Slider             _speedSlider;
        [SerializeField] private TextMeshProUGUI    _speedText;

        [Header("Armor")]
        [SerializeField] private Slider             _armorSlider;
        [SerializeField] private TextMeshProUGUI    _armorText;

        [Header("Weight")]
        [SerializeField] private Slider             _weightSlider;
        [SerializeField] private TextMeshProUGUI    _weightText;

        [Header("Config")]
        [Tooltip("UI display config – provides display types, colors, format strings, etc.")]
        [SerializeField] private PlayerStatUIConfig _statUIConfig;

        private UIDomainBridge _domainBridge;

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        public void Initialize(UIDomainBridge bridge)
        {
            _domainBridge = bridge;
            SubscribeBridgeEvents(true);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void OnDestroy()
        {
            SubscribeBridgeEvents(false);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Event Wiring
        // ─────────────────────────────────────────────────────────────────────

        private void SubscribeBridgeEvents(bool subscribe)
        {
            if (_domainBridge == null) return;

            if (subscribe)
            {
                _domainBridge.OnStatChanged  += HandleStatChanged;
                _domainBridge.OnWeightChanged += HandleWeightChanged;
            }
            else
            {
                _domainBridge.OnStatChanged  -= HandleStatChanged;
                _domainBridge.OnWeightChanged -= HandleWeightChanged;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Handlers
        // ─────────────────────────────────────────────────────────────────────

        private void HandleStatChanged(PlayerStatType type, float oldValue, float newValue)
        {
            if (_domainBridge == null || !_domainBridge.IsReady) return;

            // When a "max stat" changes (e.g. MaxHealth) we must also refresh the
            // dependent "current stat" row (e.g. Health) so the slider ratio is correct.
            RefreshRelatedCurrentStats(type);

            // Refresh the stat that actually changed
            RefreshStatUI(type, newValue);
        }

        private void HandleWeightChanged(float current, float capacity)
        {
            if (_weightSlider != null)
            {
                _weightSlider.value = capacity > 0f ? current / capacity : 0f;
            }

            if (_weightText != null)
            {
                _weightText.text = $"{current:F1}/{capacity:F1} kg";
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Refresh Helpers
        // ─────────────────────────────────────────────────────────────────────

        private void RefreshRelatedCurrentStats(PlayerStatType changedType)
        {
            if (_statUIConfig == null || _statUIConfig.Stats == null) return;

            foreach (var uiDef in _statUIConfig.Stats)
            {
                // A "current stat" row uses changedType as its dynamic ceiling
                if (uiDef.ShowInUI &&
                    uiDef.RelatedMaxStatType == changedType &&
                    uiDef.Type != changedType)
                {
                    float currentValue = _domainBridge.Bridge.GetStat(uiDef.Type);
                    RefreshStatUI(uiDef.Type, currentValue);
                }
            }
        }

        private void RefreshStatUI(PlayerStatType type, float newValue)
        {
            // Always look up fresh UI metadata from config
            if (_statUIConfig == null) return;

            var uiDef = _statUIConfig.GetUIDefinition(type);
            if (!uiDef.ShowInUI) return;

            switch (uiDef.DisplayType)
            {
                case StatDisplayType.SliderWithMax:
                {
                    float maxValue = GetRelatedMaxValue(uiDef);
                    UpdateSliderWithMax(type, newValue, maxValue, uiDef.ShowMaxValue, uiDef.DisplayFormat);
                    break;
                }

                case StatDisplayType.SliderWithRange:
                    UpdateText(type, newValue, uiDef.DisplayFormat);
                    break;

                case StatDisplayType.Text:
                    UpdateText(type, newValue, uiDef.DisplayFormat);
                    break;

                case StatDisplayType.ProgressBar:
                {
                    float maxValue = GetRelatedMaxValue(uiDef);
                    UpdateProgressBar(type, newValue, maxValue, uiDef.ShowMaxValue);
                    break;
                }
            }
        }

        private float GetRelatedMaxValue(PlayerStatUIDefinition uiDef)
        {
            // RelatedMaxStatType == Type → no dynamic max
            if (uiDef.RelatedMaxStatType == uiDef.Type) return 0f;
            if (_domainBridge == null || !_domainBridge.IsReady) return 0f;

            return _domainBridge.Bridge.GetStat(uiDef.RelatedMaxStatType);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Low-level UI setters
        // ─────────────────────────────────────────────────────────────────────

        private void UpdateSliderWithMax(PlayerStatType type, float current, float max, bool showMax, string format)
        {
            var slider = GetSliderForStat(type);
            var text   = GetTextForStat(type);

            if (slider != null)
            {
                slider.value = max > 0f ? current / max : 0f;
            }

            if (text != null)
            {
                text.text = showMax && max > 0f
                    ? $"{current.ToString(format)}/{max.ToString(format)}"
                    : current.ToString(format);
            }
        }

        private void UpdateText(PlayerStatType type, float value, string format)
        {
            var text = GetTextForStat(type);
            if (text != null)
            {
                text.text = value.ToString(format);
            }
        }

        private void UpdateProgressBar(PlayerStatType type, float current, float max, bool showMax)
        {
            var slider = GetSliderForStat(type);
            var text   = GetTextForStat(type);

            if (slider != null)
            {
                // Allow > 1 to visually show overweight
                slider.value    = max > 0f ? current / max : 0f;
                slider.maxValue = 1.5f;
            }

            if (text != null)
            {
                text.text = showMax && max > 0f
                    ? $"{current:F1}/{max:F1} kg"
                    : $"{current:F1}";
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Stat → Widget mapping
        // ─────────────────────────────────────────────────────────────────────

        private Slider GetSliderForStat(PlayerStatType type)
        {
            return type switch
            {
                PlayerStatType.Health        => _healthSlider,
                PlayerStatType.Stamina       => _staminaSlider,
                PlayerStatType.MovementSpeed => _speedSlider,
                PlayerStatType.Armor         => _armorSlider,
                PlayerStatType.CurrentWeight => _weightSlider,
                _                            => null
            };
        }

        private TextMeshProUGUI GetTextForStat(PlayerStatType type)
        {
            return type switch
            {
                PlayerStatType.Health        => _healthText,
                PlayerStatType.Stamina       => _staminaText,
                PlayerStatType.MovementSpeed => _speedText,
                PlayerStatType.Armor         => _armorText,
                PlayerStatType.CurrentWeight => _weightText,
                _                            => null
            };
        }
    }
}
