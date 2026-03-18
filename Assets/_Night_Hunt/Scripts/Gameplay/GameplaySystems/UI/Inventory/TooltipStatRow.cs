using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Gameplay.StatSystem.Core.Types;
using NightHunt.Gameplay.StatSystem.Configs;
using NightHunt.Gameplay.StatSystem.Core.Data;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Component cho mỗi stat row trong tooltip.
    /// </summary>
    public class TooltipStatRow : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI _labelText;
        [SerializeField] private TextMeshProUGUI _valueText;
        [SerializeField] private Image _iconImage;
        
        public void SetItemStat(ItemStatDefinition statDef, float value)
        {
            // TextColor fallback về DisplayColor nếu chưa set (alpha = 0)
            Color textColor = statDef.TextColor.a > 0.01f
                ? statDef.TextColor
                : statDef.DisplayColor;

            if (_labelText != null)
            {
                _labelText.text = statDef.DisplayName + ":";
                _labelText.color = textColor;
            }
            
            if (_valueText != null)
            {
                _valueText.text = value.ToString(statDef.DisplayFormat);
                _valueText.color = textColor;
            }
            
            if (_iconImage != null && statDef.Icon != null)
            {
                _iconImage.sprite = statDef.Icon;
                _iconImage.color = statDef.DisplayColor; // Icon dùng accent color
                _iconImage.gameObject.SetActive(true);
            }
            else if (_iconImage != null)
            {
                _iconImage.gameObject.SetActive(false);
            }
        }
        
        public void SetPlayerModifier(PlayerStatModifier modifier)
        {
            if (_labelText != null)
            {
                _labelText.text = GetPlayerStatName(modifier.StatType) + ":";
                _labelText.color = Color.white;
            }
            
            if (_valueText != null)
            {
                string sign = modifier.Value > 0 ? "+" : "";
                string valueStr = modifier.ModifierType == ModifierType.Percentage 
                    ? $"{sign}{modifier.Value:F1}%" 
                    : $"{sign}{modifier.Value:F0}";
                
                _valueText.text = valueStr;
                _valueText.color = modifier.Value > 0 
                    ? new Color(0.4f, 1f, 0.4f) // Light green
                    : new Color(1f, 0.6f, 0.6f); // Light red
            }
            
            if (_iconImage != null)
                _iconImage.gameObject.SetActive(false);
        }
        
        private string GetPlayerStatName(PlayerStatType type)
        {
            // Format enum name to readable string
            string name = type.ToString();
            // Replace underscores with spaces
            name = name.Replace("_", " ");
            // Add spaces before capital letters (except first)
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]))
                    sb.Append(' ');
                sb.Append(name[i]);
            }
            return sb.ToString();
        }
    }
}
