using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Gameplay.Input.Handlers.Combat;

namespace NightHunt.GameplaySystems.UI.Combat
{
    /// <summary>
    /// Row button inside an expanded <see cref="ItemFilterPanel"/> list.
    /// Single click selects the item; double click selects and arms/uses it.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ItemFilterButton : ActionButton
    {
        [Header("Item Display")]
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _quantityText;
        [SerializeField] private GameObject _selectedMarker;

        private string _instanceId;
        private IItemSelectionSystem _selectionSystem;
        private ItemFilterPanel _parentPanel;
        private CombatInputHandler _combatInputHandler;
        private float _lastPressTime = -999f;

        private const float DoubleClickThreshold = 0.45f;

        public void Bind(ItemInstance item, IItemSelectionSystem selectionSystem, ItemFilterPanel parentPanel, CombatInputHandler combatInputHandler = null)
        {
            _selectionSystem = selectionSystem;
            _parentPanel = parentPanel;
            _combatInputHandler = combatInputHandler;
            _instanceId = item?.InstanceID;

            var def = item != null ? ItemDatabase.GetDefinition(item.DefinitionID) : null;

            if (_icon != null)
            {
                _icon.sprite = def?.Icon;
                _icon.enabled = def?.Icon != null;
            }

            if (_quantityText != null)
            {
                _quantityText.text = item != null && item.Quantity > 1
                    ? item.Quantity.ToString()
                    : string.Empty;
            }

            RefreshSelectedMarker();
            SetInteractable(item != null);
        }

        public void RefreshSelectedMarker()
        {
            if (_selectedMarker == null)
                return;

            bool isSelected = !string.IsNullOrEmpty(_instanceId)
                              && _selectionSystem != null
                              && _selectionSystem.SelectedItem?.InstanceID == _instanceId;

            _selectedMarker.SetActive(isSelected);
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (!IsInteractable) return;

            base.OnPointerDown(eventData);
            _combatInputHandler?.NotifyUIConsumedPress();

            if (string.IsNullOrEmpty(_instanceId) || _selectionSystem == null)
            {
                Debug.LogWarning($"[ItemFilterButton] press ignored instance='{_instanceId}' selectionSystem={(_selectionSystem != null ? "ok" : "null")} go='{name}'");
                return;
            }

            bool isDouble = Time.unscaledTime - _lastPressTime <= DoubleClickThreshold;
            _lastPressTime = Time.unscaledTime;

            Debug.Log($"[ITEM_FLOW] [01][FilterRowClick] instance='{_instanceId}' double={isDouble} go='{name}' action={(isDouble ? "select+use" : "selectOnly")}");
            _parentPanel?.SelectFromFilter(_instanceId, useImmediately: isDouble);
        }
    }
}
