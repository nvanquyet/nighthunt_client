using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.UI.Combat
{
    /// <summary>
    /// Single row inside the expanded <see cref="ItemFilterPanel"/> list.
    ///
    /// Extends <see cref="ActionButton"/> for the DOTween press animation.
    /// A separate <see cref="CanvasGroup"/> is required on the prefab root.
    ///
    /// Behaviour:
    ///   Single press → <see cref="IItemSelectionSystem.RequestSelectItem"/> and
    ///                   collapse the parent panel.
    ///   The selected marker is shown when this item matches the system's SelectedItem.
    ///
    /// Inspector:
    ///   _icon           – Item icon Image.
    ///   _quantityText   – Stack quantity TMP text (hidden for qty ≤ 1).
    ///   _selectedMarker – GameObject shown when this item is the active selection.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ItemFilterButton : ActionButton
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [Header("Item Display")]
        [SerializeField] private Image      _icon;
        [SerializeField] private TMP_Text   _quantityText;
        [SerializeField] private GameObject _selectedMarker;

        // ── Runtime ───────────────────────────────────────────────────────────────

        private string               _instanceId;
        private IItemSelectionSystem _selectionSystem;
        private ItemFilterPanel      _parentPanel;   // collapsed on selection

        // ─────────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Bind this button to an inventory item.
        /// Called by <see cref="ItemFilterPanel"/> when building the expanded list.
        /// </summary>
        public void Bind(ItemInstance item, IItemSelectionSystem selectionSystem, ItemFilterPanel parentPanel)
        {
            _selectionSystem = selectionSystem;
            _parentPanel     = parentPanel;
            _instanceId      = item?.InstanceID;

            var def = item != null ? ItemDatabase.GetDefinition(item.DefinitionID) : null;

            if (_icon != null)
            {
                _icon.sprite  = def?.Icon;
                _icon.enabled = def?.Icon != null;
            }

            if (_quantityText != null)
                _quantityText.text = item != null && item.Quantity > 1
                    ? item.Quantity.ToString()
                    : string.Empty;

            RefreshSelectedMarker();
            SetInteractable(item != null);

            Debug.Log($"[ItemFilterButton] Bind: '{_instanceId}' ({def?.DisplayName}) qty={item?.Quantity}");
        }

        /// <summary>
        /// Refresh the highlight shown when this is the active selection.
        /// Called by <see cref="ItemFilterPanel"/> after any selection change.
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
        //  ActionButton override
        // ─────────────────────────────────────────────────────────────────────────

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);  // DOTween animation

            if (string.IsNullOrEmpty(_instanceId) || _selectionSystem == null) return;

            Debug.Log($"[ItemFilterButton] pressed '{_instanceId}' → RequestSelectItem");

            // Select the item on the server. The SyncVar OnChange event will propagate
            // back to all clients including the local SelectableItemButton which will
            // update its _trackedInstanceId and display.
            _selectionSystem.RequestSelectItem(_instanceId);

            // Collapse the panel so the player sees the selected item in the collapsed view.
            _parentPanel?.CollapseList();
        }
    }
}
