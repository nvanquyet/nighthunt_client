using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Dialog để chọn số lượng drop item.
    /// Hiển thị slider + buttons: Cancel, Drop 1, Drop All, Drop (với số lượng từ slider)
    /// </summary>
    public class DropQuantityDialog : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private GameObject _root;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _hintText;
        
        [Header("Quantity Selection")]
        [SerializeField] private Slider _quantitySlider;
        [SerializeField] private TMP_InputField _quantityInput;
        [SerializeField] private GameObject _sliderContainer; // Container để show/hide slider
        
        [Header("Buttons")]
        [SerializeField] private Button _cancelButton;
        [SerializeField] private Button _dropOneButton;
        [SerializeField] private Button _dropAllButton;
        [SerializeField] private Button _dropButton; // Drop với số lượng từ slider
        
        private ItemInstance _currentItem;
        private int _maxQuantity;
        private int _selectedQuantity = 1;
        
        public event Action<ItemInstance, int> OnDropConfirmed;
        public event Action OnCanceled;
        
        private void Awake()
        {
            if (_root != null)
                _root.SetActive(false);
            
            // Hook button events
            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(OnCancelClicked);
            
            if (_dropOneButton != null)
                _dropOneButton.onClick.AddListener(OnDropOneClicked);
            
            if (_dropAllButton != null)
                _dropAllButton.onClick.AddListener(OnDropAllClicked);
            
            if (_dropButton != null)
                _dropButton.onClick.AddListener(OnDropClicked);
            
            // Hook slider và input field
            if (_quantitySlider != null)
            {
                _quantitySlider.onValueChanged.AddListener(OnSliderValueChanged);
            }
            
            if (_quantityInput != null)
            {
                _quantityInput.onEndEdit.AddListener(OnInputValueChanged);
            }
        }
        
        private void OnDestroy()
        {
            // Unhook events
            if (_cancelButton != null)
                _cancelButton.onClick.RemoveListener(OnCancelClicked);
            
            if (_dropOneButton != null)
                _dropOneButton.onClick.RemoveListener(OnDropOneClicked);
            
            if (_dropAllButton != null)
                _dropAllButton.onClick.RemoveListener(OnDropAllClicked);
            
            if (_dropButton != null)
                _dropButton.onClick.RemoveListener(OnDropClicked);
            
            if (_quantitySlider != null)
                _quantitySlider.onValueChanged.RemoveListener(OnSliderValueChanged);
            
            if (_quantityInput != null)
                _quantityInput.onEndEdit.RemoveListener(OnInputValueChanged);
        }
        
        /// <summary>
        /// Show dialog cho item
        /// </summary>
        public void Show(ItemInstance item)
        {
            if (item == null)
            {
                Hide();
                return;
            }
            
            _currentItem = item;
            _maxQuantity = Mathf.Max(1, item.Quantity);
            
            // Nếu chỉ có 1 item thì không cần dialog, drop luôn
            if (_maxQuantity == 1)
            {
                OnDropConfirmed?.Invoke(item, 1);
                return;
            }
            
            SetupUI();
            
            if (_root != null)
                _root.SetActive(true);
        }
        
        /// <summary>
        /// Hide dialog
        /// </summary>
        public void Hide()
        {
            if (_root != null)
                _root.SetActive(false);
            
            _currentItem = null;
            _maxQuantity = 0;
            _selectedQuantity = 1;
        }
        
        private void SetupUI()
        {
            if (_currentItem == null) return;
            
            var def = ItemDatabase.GetDefinition(_currentItem.DefinitionID);
            string itemName = def != null ? def.DisplayName : _currentItem.DefinitionID;
            
            // Set title
            if (_titleText != null)
                _titleText.text = $"Drop {itemName}";
            
            // Set hint
            if (_hintText != null)
                _hintText.text = $"Select quantity to drop (1 - {_maxQuantity})";
            
            // Setup slider chỉ khi quantity > 2
            bool showSlider = _maxQuantity > 2;
            if (_sliderContainer != null)
                _sliderContainer.SetActive(showSlider);
            
            if (showSlider && _quantitySlider != null)
            {
                _quantitySlider.minValue = 1;
                _quantitySlider.maxValue = _maxQuantity;
                _quantitySlider.wholeNumbers = true;
                _quantitySlider.value = 1;
                _selectedQuantity = 1;
            }
            
            if (showSlider && _quantityInput != null)
            {
                _quantityInput.text = "1";
                _quantityInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            }
            
            // Setup buttons
            if (_dropOneButton != null)
            {
                _dropOneButton.interactable = true;
            }
            
            if (_dropAllButton != null)
            {
                _dropAllButton.interactable = _maxQuantity > 1;
            }
            
            if (_dropButton != null)
            {
                // Drop button chỉ display khi có slider (quantity > 2)
                _dropButton.gameObject.SetActive(showSlider);
            }
        }
        
        private void OnSliderValueChanged(float value)
        {
            int qty = Mathf.RoundToInt(value);
            _selectedQuantity = Mathf.Clamp(qty, 1, _maxQuantity);
            
            if (_quantityInput != null)
            {
                _quantityInput.text = _selectedQuantity.ToString();
            }
        }
        
        private void OnInputValueChanged(string value)
        {
            if (int.TryParse(value, out int qty))
            {
                _selectedQuantity = Mathf.Clamp(qty, 1, _maxQuantity);
                
                if (_quantitySlider != null)
                {
                    _quantitySlider.value = _selectedQuantity;
                }
                
                if (_quantityInput != null)
                {
                    _quantityInput.text = _selectedQuantity.ToString();
                }
            }
            else
            {
                // Invalid input, reset to current selected quantity
                if (_quantityInput != null)
                    _quantityInput.text = _selectedQuantity.ToString();
            }
        }
        
        private void OnCancelClicked()
        {
            OnCanceled?.Invoke();
            Hide();
        }
        
        private void OnDropOneClicked()
        {
            if (_currentItem != null)
            {
                OnDropConfirmed?.Invoke(_currentItem, 1);
            }
            Hide();
        }
        
        private void OnDropAllClicked()
        {
            if (_currentItem != null)
            {
                OnDropConfirmed?.Invoke(_currentItem, _maxQuantity);
            }
            Hide();
        }
        
        private void OnDropClicked()
        {
            if (_currentItem != null)
            {
                OnDropConfirmed?.Invoke(_currentItem, _selectedQuantity);
            }
            Hide();
        }
    }
}
