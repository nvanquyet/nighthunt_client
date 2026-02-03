using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Inventory.Core;
using NightHunt.Inventory.Events;

namespace NightHunt.Inventory.UI
{
    /// <summary>
    /// Popup for splitting stacks with slider-based amount selection.
    /// </summary>
    public class StackSplitPopup : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject popupPanel;
        [SerializeField] private Slider amountSlider;
        [SerializeField] private TextMeshProUGUI amountText;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button dropAllButton;
        [SerializeField] private Button dropAmountButton;
        
        private ItemInstance sourceStack;
        private int maxAmount;
        
        public void Show(ItemInstance stack)
        {
            sourceStack = stack;
            maxAmount = stack.StackSize;
            
            amountSlider.minValue = 1;
            amountSlider.maxValue = maxAmount;
            amountSlider.value = maxAmount; // Default to max
            amountSlider.wholeNumbers = true;
            
            UpdateAmountText(maxAmount);
            
            amountSlider.onValueChanged.RemoveAllListeners();
            amountSlider.onValueChanged.AddListener(OnSliderChanged);
            
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(OnCancel);
            
            dropAllButton.onClick.RemoveAllListeners();
            dropAllButton.onClick.AddListener(OnDropAll);
            
            dropAmountButton.onClick.RemoveAllListeners();
            dropAmountButton.onClick.AddListener(OnDropAmount);
            
            popupPanel.SetActive(true);
        }
        
        private void OnSliderChanged(float value)
        {
            int amount = Mathf.RoundToInt(value);
            UpdateAmountText(amount);
        }
        
        private void UpdateAmountText(int amount)
        {
            amountText.text = $"{amount} / {maxAmount}";
        }
        
        private void OnCancel()
        {
            Close();
        }
        
        private void OnDropAll()
        {
            InventoryEvents.FireRequestDropStack(sourceStack, maxAmount);
            Close();
        }
        
        private void OnDropAmount()
        {
            int amount = Mathf.RoundToInt(amountSlider.value);
            InventoryEvents.FireRequestDropStack(sourceStack, amount);
            Close();
        }
        
        private void Close()
        {
            popupPanel.SetActive(false);
        }
    }
}
