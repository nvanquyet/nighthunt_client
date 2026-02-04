using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Events;

namespace NightHunt.Inventory.UI.Panels
{
    /// <summary>
    /// Popup for splitting item stacks.
    /// Uses slider to select amount to drop/split.
    /// </summary>
    public class StackSplitPopup : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject popupPanel;
        [SerializeField] private Slider amountSlider;
        [SerializeField] private TextMeshProUGUI amountText;
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private Image itemIcon;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button dropAllButton;
        [SerializeField] private Button dropAmountButton;
        
        [Header("Settings")]
        [SerializeField] private bool closeOnAction = true;
        
        private ItemInstance sourceStack;
        private int maxAmount;
        
        #region Lifecycle
        
        void Awake()
        {
            if (popupPanel != null)
            {
                popupPanel.SetActive(false);
            }
            
            // Setup button listeners
            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(OnCancel);
            }
            
            if (dropAllButton != null)
            {
                dropAllButton.onClick.AddListener(OnDropAll);
            }
            
            if (dropAmountButton != null)
            {
                dropAmountButton.onClick.AddListener(OnDropAmount);
            }
            
            // Setup slider listener
            if (amountSlider != null)
            {
                amountSlider.onValueChanged.AddListener(OnSliderChanged);
            }
        }
        
        void OnDestroy()
        {
            // Clean up listeners
            if (cancelButton != null)
                cancelButton.onClick.RemoveListener(OnCancel);
            
            if (dropAllButton != null)
                dropAllButton.onClick.RemoveListener(OnDropAll);
            
            if (dropAmountButton != null)
                dropAmountButton.onClick.RemoveListener(OnDropAmount);
            
            if (amountSlider != null)
                amountSlider.onValueChanged.RemoveListener(OnSliderChanged);
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Shows the popup for splitting a stack.
        /// </summary>
        public void Show(ItemInstance stack)
        {
            if (stack == null || !stack.Definition.IsStackable)
            {
                Debug.LogWarning("[StackSplitPopup] Cannot split non-stackable item");
                return;
            }
            
            sourceStack = stack;
            maxAmount = stack.StackSize;
            
            // Setup UI
            if (itemNameText != null)
            {
                itemNameText.text = stack.Definition.ItemId;
            }
            
            if (itemIcon != null)
            {
                itemIcon.sprite = stack.Definition.Icon;
            }
            
            if (amountSlider != null)
            {
                amountSlider.minValue = 1;
                amountSlider.maxValue = maxAmount;
                amountSlider.value = maxAmount; // Default to max
                amountSlider.wholeNumbers = true;
            }
            
            UpdateAmountText(maxAmount);
            
            // Show panel
            if (popupPanel != null)
            {
                popupPanel.SetActive(true);
            }
        }
        
        /// <summary>
        /// Hides the popup.
        /// </summary>
        public void Hide()
        {
            if (popupPanel != null)
            {
                popupPanel.SetActive(false);
            }
            
            sourceStack = null;
        }
        
        #endregion
        
        #region Event Handlers
        
        private void OnSliderChanged(float value)
        {
            int amount = Mathf.RoundToInt(value);
            UpdateAmountText(amount);
        }
        
        private void OnCancel()
        {
            Hide();
        }
        
        private void OnDropAll()
        {
            if (sourceStack == null) return;
            
            InventoryEvents.InvokeRequestDropStack(sourceStack, maxAmount);
            
            if (closeOnAction)
            {
                Hide();
            }
        }
        
        private void OnDropAmount()
        {
            if (sourceStack == null) return;
            
            int amount = Mathf.RoundToInt(amountSlider.value);
            InventoryEvents.InvokeRequestDropStack(sourceStack, amount);
            
            if (closeOnAction)
            {
                Hide();
            }
        }
        
        #endregion
        
        #region Helper Methods
        
        private void UpdateAmountText(int amount)
        {
            if (amountText != null)
            {
                amountText.text = $"{amount} / {maxAmount}";
            }
        }
        
        #endregion
    }
}