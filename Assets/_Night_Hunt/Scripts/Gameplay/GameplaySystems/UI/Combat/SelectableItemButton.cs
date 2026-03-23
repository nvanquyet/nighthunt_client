using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.UI.Combat
{
    /// <summary>
    /// Single row button inside an expanded <see cref="ItemFilterPanel"/>.
    ///
    /// Behaviour:
    ///   Single-click  → SelectItem (panel collapses via ItemFilterPanel.HandleItemSelected).
    ///   Double-click  → SelectItem + UseSelectedItem.
    ///                   For throwables this enters aim/arm mode immediately from the list.
    ///                   For consumables this starts use immediately.
    ///
    /// Double-click detection uses a stable per-instance timer so it survives
    /// panel rebuilds (the instance is destroyed and recreated on rebuild anyway,
    /// so the timer is always fresh — no stale-state risk).
    ///
    /// Inspector:
    ///   _button        – Button component (can be on this GO or a child).
    ///   _icon          – Item icon Image.
    ///   _quantityText  – Stack quantity TMP text.
    ///   _selectedMarker – GameObject shown when this item is the active selection.
    /// </summary>
    public class SelectableItemButton : MonoBehaviour
    {
        private const float DoubleClickThresholdSec = 0.3f;

        // ── Inspector ─────────────────────────────────────────────────────────────

        [SerializeField] private Button     _button;
        [SerializeField] private Image      _icon;
        [SerializeField] private TMP_Text   _quantityText;
        [SerializeField] private GameObject _selectedMarker;

        // ── Runtime ───────────────────────────────────────────────────────────────

        private string               _instanceId;
        private IItemSelectionSystem _selectionSystem;
        private ItemType             _itemType;

        /// <summary>
        /// Timestamp of the most recent press (unscaled so it works when Time.timeScale = 0).
        /// Initialised to -1 so the very first click is never treated as a double-click.
        /// </summary>
        private float _lastPressTime = -1f;

        // ─────────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Bind this button to an item and the item-selection system.
        /// Called by <see cref="ItemFilterPanel"/> when building the expanded list.
        /// </summary>
        public void Bind(ItemInstance item, IItemSelectionSystem selectionSystem)
        {
            _selectionSystem = selectionSystem;
            _instanceId      = item?.InstanceID;

            var def  = item != null ? ItemDatabase.GetDefinition(item.DefinitionID) : null;
            _itemType = def?.Type ?? default;

            // Visuals.
            if (_icon != null)
            {
                _icon.sprite  = def?.Icon;
                _icon.enabled = def?.Icon != null;
            }

            if (_quantityText != null)
                _quantityText.text = item != null && item.Quantity > 1
                    ? item.Quantity.ToString()
                    : string.Empty;

            // Selection marker: on if this item is currently selected.
            RefreshSelectedMarker();

            // Always start fresh — this instance was just spawned.
            _lastPressTime = -1f;

            if (_button != null)
            {
                _button.onClick.RemoveListener(OnPressed);
                _button.onClick.AddListener(OnPressed);
                _button.interactable = item != null;
            }
        }

        /// <summary>
        /// Refresh the selected-marker highlight.
        /// Called by <see cref="ItemFilterPanel"/> when the selection changes.
        /// </summary>
        public void RefreshSelectedMarker()
        {
            if (_selectedMarker == null) return;

            bool isSelected = !string.IsNullOrEmpty(_instanceId)
                           && _selectionSystem != null
                           && _selectionSystem.SelectedItem?.InstanceID == _instanceId;

            _selectedMarker.SetActive(isSelected);
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────────

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OnPressed);
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  Click Handler
        // ─────────────────────────────────────────────────────────────────────────

        private void OnPressed()
        {
            if (string.IsNullOrEmpty(_instanceId) || _selectionSystem == null) return;

            float now           = Time.unscaledTime;
            bool  isDoubleClick = _lastPressTime >= 0f
                               && (now - _lastPressTime) <= DoubleClickThresholdSec;
            _lastPressTime = now;

            // Always select first — even on a double-click this is needed to
            // confirm the selection before UseSelectedItem runs.
            _selectionSystem.SelectItem(_instanceId);

            // Double-click triggers immediate use for both consumable and throwable.
            //   Consumable → starts the use coroutine right away.
            //   Throwable  → enters aim/arm mode immediately (player can fire from the list).
            if (isDoubleClick)
                _selectionSystem.UseSelectedItem();
        }
    }
}