using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.StatSystem.Core.Types;
using NightHunt.StatSystem.Configs;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// HUD panel hiển thị Health / Stamina / Speed / Armor và Weight,
    /// đọc data từ UIDomainBridge.
    /// </summary>
    public class PlayerHUDPanel : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField] private Slider _healthSlider;
        [SerializeField] private TextMeshProUGUI _healthText;

        [Header("Stamina")]
        [SerializeField] private Slider _staminaSlider;
        [SerializeField] private TextMeshProUGUI _staminaText;

        [Header("Speed")]
        [SerializeField] private Slider _speedSlider;
        [SerializeField] private TextMeshProUGUI _speedText;

        [Header("Armor")]
        [SerializeField] private Slider _armorSlider;
        [SerializeField] private TextMeshProUGUI _armorText;

        [Header("Weight")]
        [SerializeField] private Slider _weightSlider;
        [SerializeField] private TextMeshProUGUI _weightText;

        private UIDomainBridge _domainBridge;
        private PlayerStatConfig _statConfig;

        public void Initialize(UIDomainBridge bridge)
        {
            _domainBridge = bridge;
            
            // Get PlayerStatConfig from stat system via bridge
            if (_domainBridge != null && _domainBridge.IsReady && _domainBridge.Bridge != null)
            {
                var statSystem = _domainBridge.Bridge.Stat;
                if (statSystem != null)
                {
                    _statConfig = statSystem.GetStatConfig();
                }
            }
            
            HookBridgeEvents(true);
        }

        private void OnDestroy()
        {
            HookBridgeEvents(false);
        }

        private void HookBridgeEvents(bool subscribe)
        {
            if (_domainBridge == null) return;

            if (subscribe)
            {
                _domainBridge.OnStatChanged += HandleStatChanged;
                _domainBridge.OnWeightChanged += HandleWeightChanged;
            }
            else
            {
                _domainBridge.OnStatChanged -= HandleStatChanged;
                _domainBridge.OnWeightChanged -= HandleWeightChanged;
            }
        }

        private void HandleStatChanged(PlayerStatType type, float oldValue, float newValue)
        {
            if (_statConfig == null || _domainBridge == null || !_domainBridge.IsReady)
                return;
            
            // Check if this stat is a "max stat" for another stat
            // If so, update the related stat's UI as well
            UpdateRelatedStatsIfNeeded(type);
            
            // Get display metadata from config
            var displayType = _statConfig.GetDisplayType(type);
            var relatedMax = _statConfig.GetRelatedMaxStat(type);
            var showMax = _statConfig.ShouldShowMaxValue(type);
            
            // Skip if this is a max stat that shouldn't be displayed separately
            var def = _statConfig.GetStatDefinition(type);
            if (!def.ShowInUI && relatedMax == null)
                return;
            
            // Update UI based on display type
            switch (displayType)
            {
                case StatDisplayType.SliderWithMax:
                    if (relatedMax.HasValue)
                    {
                        float maxValue = _domainBridge.Bridge.GetStat(relatedMax.Value);
                        UpdateSliderWithMax(type, newValue, maxValue, showMax);
                    }
                    break;
                    
                case StatDisplayType.SliderWithRange:
                    UpdateSliderWithRange(type, newValue, def.MinValue, def.MaxValue);
                    break;
                    
                case StatDisplayType.Text:
                    UpdateText(type, newValue, def.DisplayFormat);
                    break;
                    
                case StatDisplayType.ProgressBar:
                    if (relatedMax.HasValue)
                    {
                        float maxValue = _domainBridge.Bridge.GetStat(relatedMax.Value);
                        UpdateProgressBar(type, newValue, maxValue, showMax);
                    }
                    break;
            }
        }
        
        /// <summary>
        /// Check if changed stat is a "max stat" for another stat, and update that stat's UI
        /// </summary>
        private void UpdateRelatedStatsIfNeeded(PlayerStatType changedType)
        {
            // Check all stats to see if any use this stat as their RelatedMaxStat
            if (_statConfig == null || _statConfig.Stats == null)
                return;
            
            foreach (var statDef in _statConfig.Stats)
            {
                // Use GetRelatedMaxStat to properly check if this stat has a max stat
                var relatedMax = _statConfig.GetRelatedMaxStat(statDef.Type);
                if (relatedMax.HasValue && relatedMax.Value == changedType && statDef.ShowInUI)
                {
                    // This stat uses the changed stat as its max stat
                    // Trigger update for this stat
                    float currentValue = _domainBridge.Bridge.GetStat(statDef.Type);
                    float maxValue = _domainBridge.Bridge.GetStat(changedType);
                    
                    var displayType = statDef.DisplayType;
                    var showMax = statDef.ShowMaxValue;
                    
                    switch (displayType)
                    {
                        case StatDisplayType.SliderWithMax:
                            UpdateSliderWithMax(statDef.Type, currentValue, maxValue, showMax);
                            break;
                        case StatDisplayType.ProgressBar:
                            UpdateProgressBar(statDef.Type, currentValue, maxValue, showMax);
                            break;
                    }
                }
            }
        }

        private void HandleWeightChanged(float current, float capacity)
        {
            if (_weightSlider != null)
            {
                float percent = capacity > 0f ? current / capacity : 0f;
                _weightSlider.value = percent;
            }

            if (_weightText != null)
            {
                _weightText.text = $"{current:F1}/{capacity:F1} kg";
            }
        }

        private void UpdateSliderWithMax(PlayerStatType type, float current, float max, bool showMax)
        {
            Slider slider = GetSliderForStat(type);
            TextMeshProUGUI text = GetTextForStat(type);
            
            if (slider != null)
            {
                slider.value = max > 0 ? current / max : 0f;
                slider.maxValue = 1f; // Normalized
            }
            
            if (text != null)
            {
                if (showMax)
                {
                    text.text = $"{current:F0}/{max:F0}";
                }
                else
                {
                    text.text = current.ToString("F0");
                }
            }
        }
        
        private void UpdateSliderWithRange(PlayerStatType type, float value, float min, float max)
        {
            Slider slider = GetSliderForStat(type);
            TextMeshProUGUI text = GetTextForStat(type);
            
            if (slider != null)
            {
                float range = max - min;
                slider.value = range > 0 ? (value - min) / range : 0f;
                slider.maxValue = 1f; // Normalized
            }
            
            if (text != null)
            {
                var def = _statConfig.GetStatDefinition(type);
                text.text = value.ToString(def.DisplayFormat);
            }
        }
        
        private void UpdateText(PlayerStatType type, float value, string format)
        {
            TextMeshProUGUI text = GetTextForStat(type);
            if (text != null)
            {
                text.text = value.ToString(format);
            }
        }
        
        private void UpdateProgressBar(PlayerStatType type, float current, float max, bool showMax)
        {
            Slider slider = GetSliderForStat(type);
            TextMeshProUGUI text = GetTextForStat(type);
            
            if (slider != null)
            {
                // Progress bar can exceed 100% (overweight)
                slider.value = max > 0 ? current / max : 0f;
                slider.maxValue = 1.5f; // Allow up to 150% for overweight display
            }
            
            if (text != null)
            {
                if (showMax)
                {
                    text.text = $"{current:F1}/{max:F1} kg";
                }
                else
                {
                    text.text = current.ToString("F1");
                }
            }
        }
        
        private Slider GetSliderForStat(PlayerStatType type)
        {
            return type switch
            {
                PlayerStatType.Health => _healthSlider,
                PlayerStatType.Stamina => _staminaSlider,
                PlayerStatType.MovementSpeed => _speedSlider,
                PlayerStatType.Armor => _armorSlider,
                PlayerStatType.CurrentWeight => _weightSlider,
                _ => null
            };
        }
        
        private TextMeshProUGUI GetTextForStat(PlayerStatType type)
        {
            return type switch
            {
                PlayerStatType.Health => _healthText,
                PlayerStatType.Stamina => _staminaText,
                PlayerStatType.MovementSpeed => _speedText,
                PlayerStatType.Armor => _armorText,
                PlayerStatType.CurrentWeight => _weightText,
                _ => null
            };
        }
    }
}

