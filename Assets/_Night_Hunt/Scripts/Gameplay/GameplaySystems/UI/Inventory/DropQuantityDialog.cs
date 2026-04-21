using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Modal dialog for selecting how many items to drop.
    ///
    /// FLOW:
    ///   item.Quantity == 1 → fires onConfirm(1) immediately, dialog never shown.
    ///   item.Quantity  > 1 → Show() opens the dialog:
    ///     • Slider / input field for quantity selection.
    ///     • [Drop 1]   → onConfirm(1)
    ///     • [Drop All] → onConfirm(maxQty)
    ///     • [Drop]     → onConfirm(selectedQty)   (only shown when maxQty > 2)
    ///     • [Cancel]   → onCancel() — dialog closes, caller may re-open context menu.
    ///
    /// Callbacks are cleared automatically after each Show/Hide cycle.
    /// </summary>
    public class DropQuantityDialog : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private GameObject        _root;
        [SerializeField] private TextMeshProUGUI   _titleText;
        [SerializeField] private TextMeshProUGUI   _hintText;

        [Header("Quantity Selection")]
        [SerializeField] private Slider         _quantitySlider;
        [SerializeField] private TMP_InputField _quantityInput;
        [SerializeField] private GameObject    _sliderContainer;

        [Header("Buttons")]
        [SerializeField] private Button _cancelButton;
        [SerializeField] private Button _dropOneButton;
        [SerializeField] private Button _dropAllButton;
        [SerializeField] private Button _dropButton; // shown only when qty > 2

        // ── Callbacks set per Show() call ─────────────────────────────────────
        private Action<int> _onConfirmCallback;
        private Action      _onCancelCallback;

        // ── Runtime ───────────────────────────────────────────────────────────
        private ItemInstance _currentItem;
        private int          _maxQuantity;
        private int          _selectedQuantity = 1;

        // ─────────────────────────────────────────────────────────────────────
        #region Unity Lifecycle

        private void Awake()
        {
            if (_root != null) _root.SetActive(false);

            Register(_cancelButton,  OnCancelClicked);
            Register(_dropOneButton, OnDropOneClicked);
            Register(_dropAllButton, OnDropAllClicked);
            Register(_dropButton,    OnDropClicked);

            if (_quantitySlider  != null) _quantitySlider.onValueChanged.AddListener(OnSliderValueChanged);
            if (_quantityInput   != null) _quantityInput.onEndEdit.AddListener(OnInputValueChanged);
        }

        private void OnDestroy()
        {
            Unregister(_cancelButton,  OnCancelClicked);
            Unregister(_dropOneButton, OnDropOneClicked);
            Unregister(_dropAllButton, OnDropAllClicked);
            Unregister(_dropButton,    OnDropClicked);

            if (_quantitySlider != null) _quantitySlider.onValueChanged.RemoveListener(OnSliderValueChanged);
            if (_quantityInput  != null) _quantityInput.onEndEdit.RemoveListener(OnInputValueChanged);
        }

        private static void Register(Button b, UnityEngine.Events.UnityAction a)
        { if (b != null) b.onClick.AddListener(a); }

        private static void Unregister(Button b, UnityEngine.Events.UnityAction a)
        { if (b != null) b.onClick.RemoveListener(a); }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Public API

        /// <summary>
        /// Show the quantity selection dialog.
        ///
        /// If <paramref name="item"/>.Quantity == 1: fires <paramref name="onConfirm"/> immediately
        /// and returns without showing the dialog.
        ///
        /// <paramref name="onConfirm"/>  — called with the selected quantity when the player confirms.
        /// <paramref name="onCancel"/>   — called with NO args when the player presses Cancel.
        ///                                 The dialog hides; callers should keep the context menu visible.
        /// </summary>
        public void Show(ItemInstance item, Action<int> onConfirm, Action onCancel = null)
        {
            if (item == null)
            {
                onCancel?.Invoke();
                return;
            }

            _currentItem     = item;
            _maxQuantity     = Mathf.Max(1, item.Quantity);
            _onConfirmCallback = onConfirm;
            _onCancelCallback  = onCancel;

            if (_maxQuantity == 1)
            {
                // Single-item stack: skip dialog, confirm immediately.
                Confirm(1);
                return;
            }

            SetupUI();
            if (_root != null) _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
            _currentItem       = null;
            _maxQuantity       = 0;
            _selectedQuantity  = 1;
            _onConfirmCallback = null;
            _onCancelCallback  = null;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region UI Setup

        private void SetupUI()
        {
            var def      = ItemDatabase.GetDefinition(_currentItem.DefinitionID);
            string name_ = def != null ? def.DisplayName : _currentItem.DefinitionID;

            if (_titleText != null)
                _titleText.text = $"Drop {name_}";

            if (_hintText != null)
                _hintText.text = $"Select quantity to drop (1 – {_maxQuantity})";

            // Slider only when qty > 2
            bool showSlider = _maxQuantity > 2;
            if (_sliderContainer != null)
                _sliderContainer.SetActive(showSlider);

            if (showSlider && _quantitySlider != null)
            {
                _quantitySlider.minValue    = 1;
                _quantitySlider.maxValue    = _maxQuantity;
                _quantitySlider.wholeNumbers = true;
                _quantitySlider.value       = 1;
            }

            if (showSlider && _quantityInput != null)
            {
                _quantityInput.text        = "1";
                _quantityInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            }

            _selectedQuantity = 1;

            if (_dropButton != null)
                _dropButton.gameObject.SetActive(showSlider);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Slider / Input Sync

        private void OnSliderValueChanged(float value)
        {
            _selectedQuantity = Mathf.Clamp(Mathf.RoundToInt(value), 1, _maxQuantity);
            if (_quantityInput != null) _quantityInput.text = _selectedQuantity.ToString();
        }

        private void OnInputValueChanged(string value)
        {
            if (int.TryParse(value, out int qty))
            {
                _selectedQuantity = Mathf.Clamp(qty, 1, _maxQuantity);
                if (_quantitySlider != null) _quantitySlider.value = _selectedQuantity;
            }

            if (_quantityInput != null) _quantityInput.text = _selectedQuantity.ToString();
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Button Handlers

        private void OnDropOneClicked()  => Confirm(1);
        private void OnDropAllClicked()  => Confirm(_maxQuantity);
        private void OnDropClicked()     => Confirm(_selectedQuantity);

        private void Confirm(int qty)
        {
            var cb = _onConfirmCallback;
            Hide();
            cb?.Invoke(qty);
        }

        private void OnCancelClicked()
        {
            var cb = _onCancelCallback;
            Hide();
            cb?.Invoke();
        }

        #endregion
    }
}
