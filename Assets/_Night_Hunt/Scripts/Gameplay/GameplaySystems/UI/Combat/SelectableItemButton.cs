using System;
using System.Collections.Generic;
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
    /// Collapsed HUD slot button for a single item type (Throwable or Consumable).
    ///
    /// Extends <see cref="SlotHUDButton"/> and shares the same base as
    /// <see cref="WeaponSlotButton"/>: DOTween press animation, double-click
    /// detection, and CombatInputHandler fire-blocking.
    ///
    ///   EMPTY (nothing tracked):
    ///     Press opens the expanded list through <see cref="OnExpandRequested"/>.
    ///               can open the expanded list for the player to pick something.
    ///
    ///   TRACKED / IDLE (item remembered but not selected in system):
    ///     Press sends RequestSelectItem and RequestUseSelectedItem.
    ///
    ///   SELECTED (item in server SelectedItem, not yet armed):
    ///     Press sends RequestUseSelectedItem.
    ///
    ///   ARMED (item in hand, IsUsingItem == true):
    ///     Press sends RequestCancelSelection.
    ///
    ///   On Initialize and whenever the tracked slot becomes empty, the button
    ///   automatically searches the player's inventory for the first matching
    ///   item and tracks it locally. It does not select or arm the item.
    ///
    /// Double-press opens the expanded list so the player can switch tracked item.
    /// </summary>
    public class SelectableItemButton : SlotHUDButton
    {
        [Header("Item Display")]
        [SerializeField] private Image      _icon;
        [SerializeField] private TMP_Text   _quantityText;
        [SerializeField] private GameObject _emptyIndicator;

        private ItemType             _filterType;
        private readonly List<ItemType> _filterTypes = new();
        private IItemSelectionSystem _selectionSystem;
        private IItemUseSystem       _itemUseSystem;
        private IInventorySystem     _inventorySystem;

        /// <summary>
        /// The instance ID this slot is currently "remembering".
        /// Set by auto-fill, by <see cref="ItemFilterPanel"/> after the user
        /// picks from the expanded list, and by the selection-changed event.
        /// Cleared only when the item is fully removed from inventory.
        /// </summary>
        private string _trackedInstanceId;

        /// <summary>
        /// Fired when the slot is empty and the user presses the button.
        /// <see cref="ItemFilterPanel"/> subscribes to this event and opens the
        /// expanded list so the player can pick an item.
        /// </summary>
        public event Action OnExpandRequested;

        protected override void OnDestroy()
        {
            Unsubscribe();
            base.OnDestroy();
        }

        /// <summary>
        /// Bind to the local player's systems.
        /// Safe to call multiple times (re-init on player / spectate change).
        /// </summary>
        public void Initialize(
            ItemType             filterType,
            IItemSelectionSystem selectionSystem,
            IItemUseSystem       itemUseSystem      = null,
            IInventorySystem     inventorySystem    = null)
        {
            Initialize(new[] { filterType }, selectionSystem, itemUseSystem, inventorySystem);
        }

        public void Initialize(
            IReadOnlyList<ItemType> filterTypes,
            IItemSelectionSystem selectionSystem,
            IItemUseSystem       itemUseSystem      = null,
            IInventorySystem     inventorySystem    = null)
        {
            Unsubscribe();

            _filterTypes.Clear();
            if (filterTypes != null)
            {
                for (int i = 0; i < filterTypes.Count; i++)
                {
                    if (!_filterTypes.Contains(filterTypes[i]))
                        _filterTypes.Add(filterTypes[i]);
                }
            }

            if (_filterTypes.Count == 0)
                _filterTypes.Add(ItemType.Consumable);

            _filterType      = _filterTypes[0];
            _selectionSystem = selectionSystem;
            _itemUseSystem   = itemUseSystem;
            _inventorySystem = inventorySystem;

            _trackedInstanceId = null;

            Subscribe();
            RefreshDisplay();
            TryAutoFill();
        }

        /// <summary>
        /// Explicitly set the tracked item (called by <see cref="ItemFilterPanel"/>
        /// when the user picks an item from the expanded list).
        /// Does not send RPCs; the caller owns the selection/use action.
        /// </summary>
        public void SetTrackedItem(string instanceId)
        {

            _trackedInstanceId = instanceId;
            RefreshDisplay();
        }

        /// <summary>
        /// Returns the instance ID currently tracked by this slot (may be null/empty if slot is empty).
        /// Used for shortcut logging and filter-panel state.
        /// </summary>
        public string GetTrackedInstanceId() => _trackedInstanceId;

        public void ActivateFromShortcut()
        {
            ActivateFromShortcut(null);
        }

        public void ActivateFromShortcut(ItemType preferredType)
        {
            ActivateFromShortcut((ItemType?)preferredType);
        }

        private void ActivateFromShortcut(ItemType? preferredType)
        {
            if (preferredType.HasValue)
                TryTrackFirstMatching(preferredType.Value, allowReplace: true);

            Debug.Log($"[SelectableItemButton:{FormatFilterTypes()}] shortcut press preferred={preferredType?.ToString() ?? "none"} tracked='{_trackedInstanceId ?? "null"}'");
            HandlePress(isDouble: false);
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (!IsInteractable) return;

            base.OnPointerDown(eventData);  // DOTween animation + NotifyUIConsumedPress

            if (_selectionSystem == null) return;

            HandlePress(ConsumeDoubleClick());
        }

        private void HandlePress(bool isDouble)
        {
            if (_selectionSystem == null) return;

            // Double-click opens the expanded panel to switch item.
            if (isDouble)
            {
                OnExpandRequested?.Invoke();
                return;
            }
            // Determine current state.
            bool hasTracked = !string.IsNullOrEmpty(_trackedInstanceId);

            // IsArmed: the tracked item is currently held in hand and being used.
            bool isArmed = hasTracked
                        && _itemUseSystem != null
                        && _itemUseSystem.IsUsingItem
                        && _selectionSystem.SelectedItem?.InstanceID == _trackedInstanceId;

            // IsSelected: the tracked item is marked as selected in the system
            //             but not yet armed (just recorded, weapon still holstered when clicked).
            bool isSelected = hasTracked
                           && !isArmed
                           && _selectionSystem.SelectedItem?.InstanceID == _trackedInstanceId;

            Debug.Log($"[ITEM_FLOW] [01][CollapsedClick] filters={FormatFilterTypes()} tracked='{_trackedInstanceId}' " +
                      $"isSelected={isSelected} isArmed={isArmed}");

            if (!hasTracked)
            {
                Debug.Log($"[ITEM_FLOW] [01][CollapsedClick.Empty] filters={FormatFilterTypes()} action=expand");
                OnExpandRequested?.Invoke();
                return;
            }

            if (isArmed)
            {
                Debug.Log($"[ITEM_FLOW] [01][CollapsedClick.Armed] filters={FormatFilterTypes()} action=cancel");
                _selectionSystem.RequestCancelSelection();
                return;
            }

            if (!isSelected)
            {
                Debug.Log($"[ITEM_FLOW] [02][CollapsedSelectUse] filters={FormatFilterTypes()} instance='{_trackedInstanceId}' action=RequestSelectItem+RequestUseSelectedItem");
                _selectionSystem.RequestSelectItem(_trackedInstanceId);
                _selectionSystem.RequestUseSelectedItem();
                return;
            }

            Debug.Log($"[ITEM_FLOW] [03][CollapsedUse] filters={FormatFilterTypes()} instance='{_trackedInstanceId}' action=RequestUseSelectedItem");
            _selectionSystem.RequestUseSelectedItem();
        }

        private void RefreshDisplay()
        {
            // Re-fetch from inventory so quantity is always current.
            var item = !string.IsNullOrEmpty(_trackedInstanceId)
                ? GetItemByInstanceId(_trackedInstanceId)
                : null;

            var def = item != null ? ItemDatabase.GetDefinition(item.DefinitionID) : null;
            bool hasItem = item != null && MatchesFilter(def);

            if (_icon != null)
            {
                _icon.sprite  = hasItem ? def.Icon : null;
                _icon.enabled = hasItem && def.Icon != null;
            }

            if (_quantityText != null)
                _quantityText.text = hasItem && item.Quantity > 1
                    ? item.Quantity.ToString()
                    : string.Empty;

            if (_emptyIndicator != null)
                _emptyIndicator.SetActive(!hasItem);
        }

        /// <summary>
        /// If the tracked slot is empty and the player has a matching item in their
        /// inventory, track it locally so the collapsed slot shows an icon. This must
        /// not send selection RPCs; adding loot should never auto-arm or auto-use items.
        /// </summary>
        private bool TryAutoFill()
        {
            return TryTrackFirstMatching(null, allowReplace: false);
        }

        private bool TryTrackFirstMatching(ItemType? preferredType, bool allowReplace)
        {
            if (!allowReplace && !string.IsNullOrEmpty(_trackedInstanceId)) return false;
            if (_selectionSystem == null) return false;

            var items = GetCurrentPlayerItems();
            if (items == null) return false;

            foreach (var inv in items)
            {
                if (inv == null || inv.InventoryIndex < 0 || inv.Quantity <= 0) continue;
                var def = ItemDatabase.GetDefinition(inv.DefinitionID);
                if (preferredType.HasValue)
                {
                    if (!MatchesPreferredFilter(def, preferredType.Value)) continue;
                }
                else if (!MatchesFilter(def)) continue;

                _trackedInstanceId = inv.InstanceID;
                RefreshDisplay();
                return true;
            }

            return false;
        }

        private void Subscribe()
        {
            if (_selectionSystem != null)
            {
                _selectionSystem.OnItemSelected   += HandleItemSelected;
                _selectionSystem.OnItemDeselected += HandleItemDeselected;
            }

            if (_inventorySystem != null)
            {
                _inventorySystem.OnItemAdded   += HandleInventoryItemAdded;
                _inventorySystem.OnItemRemoved += HandleInventoryItemRemoved;
            }
        }

        private void Unsubscribe()
        {
            if (_selectionSystem != null)
            {
                _selectionSystem.OnItemSelected   -= HandleItemSelected;
                _selectionSystem.OnItemDeselected -= HandleItemDeselected;
            }

            if (_inventorySystem != null)
            {
                _inventorySystem.OnItemAdded   -= HandleInventoryItemAdded;
                _inventorySystem.OnItemRemoved -= HandleInventoryItemRemoved;
            }
        }

        private void HandleItemSelected(ItemInstance item)
        {
            var def = item != null ? ItemDatabase.GetDefinition(item.DefinitionID) : null;
            if (!MatchesFilter(def)) return;

            _trackedInstanceId = item.InstanceID;
            RefreshDisplay();
        }

        private void HandleItemDeselected()
        {
            // A deselect does not clear _trackedInstanceId; the item is still
            // remembered so the next click can quickly re-select + arm it.
            // Only a full inventory removal clears the tracked slot.
            RefreshDisplay();
        }

        private void HandleInventoryItemAdded(ItemInstance item)
        {
            if (item == null) return;
            var def = ItemDatabase.GetDefinition(item.DefinitionID);
            if (!MatchesFilter(def)) return;

            // Refresh quantity display if this is the tracked item.
            if (item.InstanceID == _trackedInstanceId)
            {
                RefreshDisplay();
                return;
            }

            // If slot is empty, auto-fill with this newly added item.
            if (string.IsNullOrEmpty(_trackedInstanceId))
                TryAutoFill();
        }

        private void HandleInventoryItemRemoved(ItemInstance item, int quantityRemoved)
        {
            if (item == null) return;
            var def = ItemDatabase.GetDefinition(item.DefinitionID);
            if (!MatchesFilter(def)) return;

            bool wasTracked = item.InstanceID == _trackedInstanceId;
            bool fullyGone  = item.Quantity - quantityRemoved <= 0;

            if (wasTracked)
            {
                RefreshDisplay();  // update qty display

                if (fullyGone)
                {
                    _trackedInstanceId = null;
                    RefreshDisplay();
                    TryAutoFill();
                }
            }
        }

        private ItemInstance GetItemByInstanceId(string instanceId)
        {
            var items = GetCurrentPlayerItems();
            if (items == null) return null;
            foreach (var item in items)
                if (item?.InstanceID == instanceId) return item;
            return null;
        }

        private IReadOnlyList<ItemInstance> GetCurrentPlayerItems()
        {
            return _inventorySystem?.GetAllItems();
        }

        private bool MatchesFilter(ItemDefinition def)
        {
            if (def == null) return false;
            for (int i = 0; i < _filterTypes.Count; i++)
            {
                if (def.Type == _filterTypes[i]) return true;
                if (_filterTypes[i] == ItemType.Throwable && def.Type == ItemType.Deployable) return true;
            }
            return false;
        }

        private bool MatchesPreferredFilter(ItemDefinition def, ItemType preferredType)
        {
            if (def == null) return false;
            if (def.Type == preferredType) return true;
            return preferredType == ItemType.Throwable && def.Type == ItemType.Deployable;
        }

        private string FormatFilterTypes()
        {
            if (_filterTypes.Count == 0) return _filterType.ToString();
            if (_filterTypes.Count == 1) return _filterTypes[0].ToString();

            var text = _filterTypes[0].ToString();
            for (int i = 1; i < _filterTypes.Count; i++)
                text += "+" + _filterTypes[i];
            return text;
        }
    }
}
