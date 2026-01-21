using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace NightHunt.UI.Inventory
{
    /// <summary>
    /// Drop amount selector UI component
    /// Follows Single Responsibility Principle
    /// </summary>
    public class DropAmountSelector : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject selectorPanel;
        [SerializeField] private Slider amountSlider;
        [SerializeField] private TMP_InputField amountInput;
        [SerializeField] private TextMeshProUGUI maxAmountText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button preset1Button;
        [SerializeField] private Button presetHalfButton;
        [SerializeField] private Button presetAllButton;

        private int currentMaxAmount = 1;
        private Action<int> onConfirmCallback;

        private void Awake()
        {
            if (selectorPanel != null)
            {
                selectorPanel.SetActive(false);
            }

            // Setup button listeners
            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(OnConfirm);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(OnCancel);
            }

            if (preset1Button != null)
            {
                preset1Button.onClick.AddListener(() => SetPresetAmount(1));
            }

            if (presetHalfButton != null)
            {
                presetHalfButton.onClick.AddListener(() => SetPresetAmount(-1)); // -1 = half
            }

            if (presetAllButton != null)
            {
                presetAllButton.onClick.AddListener(() => SetPresetAmount(-2)); // -2 = all
            }

            // Setup slider
            if (amountSlider != null)
            {
                amountSlider.onValueChanged.AddListener(OnSliderValueChanged);
            }

            // Setup input field
            if (amountInput != null)
            {
                amountInput.onEndEdit.AddListener(OnInputValueChanged);
            }
        }

        /// <summary>
        /// Show drop amount selector
        /// </summary>
        public void Show(string itemId, int maxQuantity, Action<int> onConfirm)
        {
            currentMaxAmount = maxQuantity;
            onConfirmCallback = onConfirm;

            if (selectorPanel != null)
            {
                selectorPanel.SetActive(true);
            }

            // Setup slider range
            if (amountSlider != null)
            {
                amountSlider.minValue = 1;
                amountSlider.maxValue = maxQuantity;
                amountSlider.value = 1;
            }

            // Setup input field
            if (amountInput != null)
            {
                amountInput.text = "1";
                amountInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            }

            // Update max amount text
            if (maxAmountText != null)
            {
                maxAmountText.text = $"Max: {maxQuantity}";
            }
        }

        /// <summary>
        /// Hide selector
        /// </summary>
        public void Hide()
        {
            if (selectorPanel != null)
            {
                selectorPanel.SetActive(false);
            }
            onConfirmCallback = null;
        }

        /// <summary>
        /// Set preset amount
        /// </summary>
        private void SetPresetAmount(int preset)
        {
            int amount = preset;
            if (preset == -1) // Half
            {
                amount = Mathf.Max(1, currentMaxAmount / 2);
            }
            else if (preset == -2) // All
            {
                amount = currentMaxAmount;
            }

            if (amountSlider != null)
            {
                amountSlider.value = amount;
            }

            if (amountInput != null)
            {
                amountInput.text = amount.ToString();
            }
        }

        /// <summary>
        /// On slider value changed
        /// </summary>
        private void OnSliderValueChanged(float value)
        {
            int intValue = Mathf.RoundToInt(value);
            if (amountInput != null)
            {
                amountInput.text = intValue.ToString();
            }
        }

        /// <summary>
        /// On input value changed
        /// </summary>
        private void OnInputValueChanged(string value)
        {
            if (int.TryParse(value, out int intValue))
            {
                intValue = Mathf.Clamp(intValue, 1, currentMaxAmount);
                if (amountSlider != null)
                {
                    amountSlider.value = intValue;
                }
                if (amountInput != null)
                {
                    amountInput.text = intValue.ToString();
                }
            }
        }

        /// <summary>
        /// On confirm button clicked
        /// </summary>
        private void OnConfirm()
        {
            int amount = 1;
            if (amountSlider != null)
            {
                amount = Mathf.RoundToInt(amountSlider.value);
            }
            else if (amountInput != null && int.TryParse(amountInput.text, out int parsed))
            {
                amount = parsed;
            }

            amount = Mathf.Clamp(amount, 1, currentMaxAmount);
            onConfirmCallback?.Invoke(amount);
            Hide();
        }

        /// <summary>
        /// On cancel button clicked
        /// </summary>
        private void OnCancel()
        {
            Hide();
        }
    }
}

