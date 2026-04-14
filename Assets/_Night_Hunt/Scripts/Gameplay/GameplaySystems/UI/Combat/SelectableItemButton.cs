#if !UNITY_SERVER
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Gameplay.Spectator;

namespace NightHunt.GameplaySystems.UI.Combat
{
    /// <summary>
    /// Collapsed HUD slot button for a single item type (Throwable or Consumable).
    ///
    /// Extends <see cref="SlotHUDButton"/> â€” shares the same base as
    /// <see cref="WeaponSlotButton"/>: DOTween press animation, double-click
    /// detection, and CombatInputHandler fire-blocking.
    ///
    /// â”€â”€ Click State Machine â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    ///
    ///   EMPTY (nothing tracked):
    ///     â€¢ Press â†’ fires <see cref="OnExpandRequested"/> so <see cref="ItemFilterPanel"/>
    ///               can open the expanded list for the player to pick something.
    ///
    ///   TRACKED / IDLE (item remembered but not selected in system):
    ///     â€¢ Press         â†’ RequestSelectItem (re-select)
    ///     â€¢ Double-press  â†’ RequestCancelSelection (clear)
    ///
    ///   SELECTED (item in server SelectedItem, not yet armed):
    ///     â€¢ Press         â†’ RequestUseSelectedItem (holster weapon + arm item)
    ///     â€¢ Double-press  â†’ RequestCancelSelection (clear)
    ///
    ///   ARMED (item in hand, IsUsingItem == true):
    ///     â€¢ Press         â†’ RequestCancelSelection (put away, re-equip weapon)
    ///     â€¢ Double-press  â†’ RequestCancelSelection (same)
    ///
    /// â”€â”€ Auto-fill â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    ///   On Initialize and whenever the tracked slot becomes empty, the button
    ///   automatically searches the player's inventory for the first matching
    ///   item and quietly calls RequestSelectItem on it.
    ///
    /// â”€â”€ Inspector â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    ///   _icon          â€“ Item icon Image (collapsed view).
    ///   _quantityText  â€“ Stack quantity TMP text.
    ///   _emptyIndicator â€“ Shown when no item is tracked.
    /// </summary>
    public class SelectableItemButton : SlotHUDButton
    {
        // â”€â”€ Inspector â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [Header("Item Display")]
        [SerializeField] private Image      _icon;
        [SerializeField] private TMP_Text   _quantityText;
        [SerializeField] private GameObject _emptyIndicator;

        // â”€â”€ Runtime â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private ItemType             _filterType;
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

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        //  Events
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Fired when the slot is empty and the user presses the button.
        /// <see cref="ItemFilterPanel"/> subscribes to this event and opens the
        /// expanded list so the player can pick an item.
        /// </summary>
        public event Action OnExpandRequested;

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        //  Unity Lifecycle
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        protected override void OnDestroy()
        {
            Unsubscribe();
            base.OnDestroy();
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        //  Public API
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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
            Unsubscribe();

            _filterType      = filterType;
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
        /// Does NOT send any RPC â€” the caller is responsible for RequestSelectItem.
        /// </summary>
        public void SetTrackedItem(string instanceId)
        {
            Debug.Log($"[SelectableItemButton:{_filterType}] SetTrackedItem: '{instanceId}'");
            _trackedInstanceId = instanceId;
            RefreshDisplay();
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        //  SlotHUDButton override
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);  // DOTween animation + NotifyUIConsumedPress

            if (_selectionSystem == null) return;

            bool isDouble = ConsumeDoubleClick();

            // â”€â”€ Double-click: always cancel / put away â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            if (isDouble)
            {
                Debug.Log($"[SelectableItemButton:{_filterType}] double-click â†’ RequestCancelSelection");
                _selectionSystem.RequestCancelSelection();
                return;
            }

            // â”€â”€ Determine current state â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

            Debug.Log($"[SelectableItemButton:{_filterType}] press â€” tracked='{_trackedInstanceId}' " +
                      $"isSelected={isSelected} isArmed={isArmed}");

            // â”€â”€ Empty slot â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            if (!hasTracked)
            {
                Debug.Log($"[SelectableItemButton:{_filterType}] empty â†’ OnExpandRequested");
                OnExpandRequested?.Invoke();
                return;
            }

            // â”€â”€ Armed: item is in the player's hand already â†’ put it away â”€â”€â”€â”€â”€â”€â”€
            if (isArmed)
            {
                Debug.Log($"[SelectableItemButton:{_filterType}] armed â†’ RequestCancelSelection");
                _selectionSystem.RequestCancelSelection();
                return;
            }

            // â”€â”€ Selected but not yet armed â†’ ARM (holster weapon + hold item) â”€â”€â”€
            if (isSelected)
            {
                Debug.Log($"[SelectableItemButton:{_filterType}] selected â†’ RequestUseSelectedItem");
                _selectionSystem.RequestUseSelectedItem();
                return;
            }

            // â”€â”€ Tracked but not currently selected (e.g. another panel stole it,
            //    or server cancelled) â†’ re-select so the player can arm next click. â”€
            Debug.Log($"[SelectableItemButton:{_filterType}] tracked not selected â†’ RequestSelectItem('{_trackedInstanceId}')");
            _selectionSystem.RequestSelectItem(_trackedInstanceId);
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        //  Display
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void RefreshDisplay()
        {
            // Re-fetch from inventory so quantity is always current.
            var item = !string.IsNullOrEmpty(_trackedInstanceId)
                ? GetItemByInstanceId(_trackedInstanceId)
                : null;

            var def = item != null ? ItemDatabase.GetDefinition(item.DefinitionID) : null;
            bool hasItem = item != null && def != null && def.Type == _filterType;

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

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        //  Auto-fill
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// If the tracked slot is empty and the player has a matching item in their
        /// inventory, silently select it and track it.  No visual confirmation needed
        /// â€” the collapsed icon will update automatically via the selection event.
        /// </summary>
        private void TryAutoFill()
        {
            if (!string.IsNullOrEmpty(_trackedInstanceId)) return;  // already tracking something
            if (_selectionSystem == null) return;

            var items = GetCurrentPlayerItems();
            if (items == null) return;

            foreach (var inv in items)
            {
                if (inv == null || inv.InventoryIndex < 0 || inv.Quantity <= 0) continue;
                var def = ItemDatabase.GetDefinition(inv.DefinitionID);
                if (def == null || def.Type != _filterType) continue;

                Debug.Log($"[SelectableItemButton:{_filterType}] TryAutoFill â†’ RequestSelectItem('{inv.InstanceID}')");
                _trackedInstanceId = inv.InstanceID;  // optimistic local update before RPC confirms
                _selectionSystem.RequestSelectItem(inv.InstanceID);
                RefreshDisplay();
                return;
            }
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        //  Event Subscriptions
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

        // â”€â”€ Selection events â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void HandleItemSelected(ItemInstance item)
        {
            var def = item != null ? ItemDatabase.GetDefinition(item.DefinitionID) : null;
            if (def == null || def.Type != _filterType) return;

            Debug.Log($"[SelectableItemButton:{_filterType}] HandleItemSelected: '{item.InstanceID}'");
            _trackedInstanceId = item.InstanceID;
            RefreshDisplay();
        }

        private void HandleItemDeselected()
        {
            // A deselect does NOT clear _trackedInstanceId â€” the item is still
            // remembered so the next click can quickly re-select + arm it.
            // Only a full inventory removal clears the tracked slot.
            Debug.Log($"[SelectableItemButton:{_filterType}] HandleItemDeselected (tracking '{_trackedInstanceId}')");
            RefreshDisplay();
        }

        // â”€â”€ Inventory events â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void HandleInventoryItemAdded(ItemInstance item)
        {
            if (item == null) return;
            var def = ItemDatabase.GetDefinition(item.DefinitionID);
            if (def == null || def.Type != _filterType) return;

            Debug.Log($"[SelectableItemButton:{_filterType}] HandleInventoryItemAdded: '{item.InstanceID}' qty={item.Quantity}");

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
            if (def == null || def.Type != _filterType) return;

            bool wasTracked = item.InstanceID == _trackedInstanceId;
            bool fullyGone  = item.Quantity - quantityRemoved <= 0;

            Debug.Log($"[SelectableItemButton:{_filterType}] HandleInventoryItemRemoved: '{item.InstanceID}' " +
                      $"wasTracked={wasTracked} fullyGone={fullyGone}");

            if (wasTracked)
            {
                RefreshDisplay();  // update qty display

                if (fullyGone)
                {
                    Debug.Log($"[SelectableItemButton:{_filterType}] tracked item depleted â†’ clearing + TryAutoFill");
                    _trackedInstanceId = null;
                    RefreshDisplay();
                    TryAutoFill();
                }
            }
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        //  Helpers
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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
            return SpectateManager.Instance
                ?.GetCurrentPlayer()
                ?.GamePlaySystemBridge
                ?.GetAllItems();
        }
    }
}
#endif // !UNITY_SERVER
