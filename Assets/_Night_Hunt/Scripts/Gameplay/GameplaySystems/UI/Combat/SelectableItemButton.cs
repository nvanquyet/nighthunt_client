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
    /// Collapsed HUD slot button for a single item type (Throwable, Consumable, or Deployable).
    ///
    /// Extends <see cref="SlotHUDButton"/> and shares the same base as
    /// <see cref="WeaponSlotButton"/>: DOTween press animation, double-click
    /// detection, and CombatInputHandler fire-blocking.
    ///
    ///   EMPTY (nothing tracked):
    ///     Any press opens the expanded list via <see cref="OnExpandRequested"/>.
    ///
    ///   TRACKED / IDLE (item remembered, not selected in system):
    ///     Single click → RequestSelectItem  (select only; NOT armed yet).
    ///     Double click → RequestSelectItem + RequestUseSelectedItem (arm/use).
    ///
    ///   SELECTED (item selected in system, not yet armed):
    ///     Single click → RequestDeselectItem (toggle off).
    ///     Double click → RequestUseSelectedItem (arm/use).
    ///
    ///   ARMED (IsUsingItem == true for the tracked item):
    ///     Any click → RequestCancelSelection (put down).
    ///
    ///   Double-press on an empty slot opens the expanded list instead.
    ///
    ///   Keyboard shortcuts (ActivateFromShortcut) always arm — treated as double-click intent.
    /// </summary>
    public class SelectableItemButton : SlotHUDButton, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const float MobileDragFullMagnitudePixels = 120f;

        [Header("Item Display")]
        [SerializeField] private Image      _icon;
        [SerializeField] private TMP_Text   _quantityText;
        [SerializeField] private GameObject _emptyIndicator;
        [SerializeField] private GameObject _selectedIndicator;
        [SerializeField] private Color _selectedIconColor = new Color(0.35f, 0.85f, 1f, 1f);
        [SerializeField] private Color _armedIconColor    = new Color(0.35f, 1f,  0.45f, 1f);

        private ItemType             _filterType;
        private readonly List<ItemType> _filterTypes = new();
        private IItemSelectionSystem _selectionSystem;
        private IItemUseSystem       _itemUseSystem;
        private IInventorySystem     _inventorySystem;
        private ThrowableAimController    _aimController;
        private Vector2 _dragStartScreenPosition;
        private float _lastDragMagnitude;
        private bool _dragAimStarted;

        /// <summary>
        /// The instance ID this slot is currently "remembering".
        /// Set by auto-fill, by <see cref="ItemFilterPanel"/> after the user
        /// picks from the expanded list, and by the selection-changed event.
        /// Cleared only when the item is fully removed from inventory.
        /// </summary>
        private string _trackedInstanceId;

        /// <summary>
        /// Fired when the slot is empty and the user presses the button.
        /// <see cref="ItemFilterPanel"/> subscribes and opens the expanded list.
        /// </summary>
        public event Action OnExpandRequested;

        protected override void OnDestroy()
        {
            Unsubscribe();
            base.OnDestroy();
        }

        // ── Initialization ────────────────────────────────────────────────────────

        /// <summary>
        /// Bind to the local player's systems.
        /// Safe to call multiple times (re-init on player / spectate change).
        /// </summary>
        public void Initialize(
            ItemType             filterType,
            IItemSelectionSystem selectionSystem,
            IItemUseSystem       itemUseSystem   = null,
            IInventorySystem     inventorySystem = null,
            ThrowableAimController aimController  = null)
        {
            Initialize(new[] { filterType }, selectionSystem, itemUseSystem, inventorySystem, aimController);
        }

        public void Initialize(
            IReadOnlyList<ItemType> filterTypes,
            IItemSelectionSystem    selectionSystem,
            IItemUseSystem          itemUseSystem   = null,
            IInventorySystem        inventorySystem = null,
            ThrowableAimController  aimController   = null)
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
            _aimController   = aimController;

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
            Debug.Log($"[NH_FLOW][04][SelectableItem.SetTracked] button={name} filters={FormatFilterTypes()} tracked='{_trackedInstanceId ?? "null"}'");
            RefreshDisplay();
        }

        /// <summary>
        /// Returns the instance ID currently tracked by this slot (may be null/empty).
        /// Used for shortcut logging and filter-panel state.
        /// </summary>
        public string GetTrackedInstanceId() => _trackedInstanceId;

        // ── Shortcut / keyboard activation ───────────────────────────────────────

        public void ActivateFromShortcut() => ActivateFromShortcut(null);

        public void ActivateFromShortcut(ItemType preferredType) =>
            ActivateFromShortcut((ItemType?)preferredType);

        /// <summary>
        /// Keyboard shortcut activation: treated as double-click (arm/use intent).
        /// Optionally switches the tracked item to <paramref name="preferredType"/> first.
        /// </summary>
        private void ActivateFromShortcut(ItemType? preferredType)
        {
            if (preferredType.HasValue)
                TryTrackFirstMatching(preferredType.Value, allowReplace: true);

            Debug.Log($"[SelectableItemButton:{FormatFilterTypes()}] shortcut preferred={preferredType?.ToString() ?? "none"} tracked='{_trackedInstanceId ?? "null"}'");
            Debug.Log($"[NH_FLOW][04][SelectableItem.Shortcut] button={name} preferred={preferredType?.ToString() ?? "none"} {DescribeItemButtonState()}");
            HandlePress(isDouble: true);
        }

        // ── Input handling ────────────────────────────────────────────────────────

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (!IsInteractable) return;
            base.OnPointerDown(eventData);  // DOTween animation + NotifyUIConsumedPress
            if (_selectionSystem == null)
            {
                Debug.LogWarning($"[NH_FLOW][04][SelectableItem.PointerDownIgnored] button={name} reason=null-selection {DescribeItemButtonState()}");
                return;
            }
            bool isDouble = ConsumeDoubleClick();
            Debug.Log($"[NH_FLOW][04][SelectableItem.PointerDown] button={name} double={isDouble} position={eventData.position} {DescribeItemButtonState()}");
            HandlePress(isDouble);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!IsInteractable || string.IsNullOrEmpty(_trackedInstanceId) || !IsTrackedAimItem())
                return;

            if (_aimController == null)
                _aimController = ResolveAimController();

            _dragStartScreenPosition = eventData.pressPosition.sqrMagnitude > 0.001f
                ? eventData.pressPosition
                : eventData.position;
            _lastDragMagnitude = 0f;

            _dragAimStarted = _aimController != null &&
                (_aimController.IsInAimMode ||
                 _aimController.IsInDeployMode ||
                 TryStartAimControllerFlow(_trackedInstanceId));
            Debug.Log($"[NH_FLOW][15][SelectableItem.BeginDrag] button={name} dragAimStarted={_dragAimStarted} {DescribeItemButtonState()}");
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragAimStarted || _aimController == null)
                return;

            Vector2 delta = eventData.position - _dragStartScreenPosition;
            Vector2 joystick = Vector2.ClampMagnitude(delta / MobileDragFullMagnitudePixels, 1f);
            _lastDragMagnitude = Mathf.Clamp01(joystick.magnitude);
            _aimController.OnMobileDrag(joystick);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_dragAimStarted && _aimController != null)
                _aimController.OnMobileDragEndIfStillActive(_lastDragMagnitude);

            Debug.Log($"[NH_FLOW][17][SelectableItem.EndDrag] button={name} dragAimStarted={_dragAimStarted} lastMagnitude={_lastDragMagnitude:F2} {DescribeItemButtonState()}");
            _dragAimStarted = false;
            _lastDragMagnitude = 0f;
        }

        /// <summary>
        /// Core interaction logic.
        ///   isDouble=true  → arm / use the tracked item (or open picker if empty).
        ///   isDouble=false → select-toggle (select if idle, deselect if selected, cancel if armed).
        /// </summary>
        private void HandlePress(bool isDouble)
        {
            if (_selectionSystem == null) return;



            bool hasTracked = !string.IsNullOrEmpty(_trackedInstanceId);
            Debug.Log($"[NH_FLOW][05][SelectableItem.HandlePress] button={name} double={isDouble} hasTracked={hasTracked} {DescribeItemButtonState()}");

            // ── Double click: arm / use ───────────────────────────────────────────
            if (isDouble)
            {
                if (!hasTracked)
                {
                    Debug.Log($"[NH_FLOW][05][SelectableItem.ExpandRequested] button={name} reason=double-empty {DescribeItemButtonState()}");
                    OnExpandRequested?.Invoke();
                    return;
                }

                // Ensure item is selected before arming (unselected → arm in one gesture).
                if (TryStartAimControllerFlow(_trackedInstanceId))
                {
                    Debug.Log($"[NH_FLOW][06][SelectableItem.UseViaAimController] button={name} {DescribeItemButtonState()}");
                    return;
                }

                string selId = _selectionSystem.SelectedItem?.InstanceID;
                if (selId != _trackedInstanceId)
                {
                    Debug.Log($"[NH_FLOW][07][SelectableItem.RequestSelectBeforeUse] button={name} selected='{selId ?? "null"}' tracked='{_trackedInstanceId}'");
                    _selectionSystem.RequestSelectItem(_trackedInstanceId);
                }

                Debug.Log($"[ITEM_FLOW] [01][DoubleClick.Arm] filters={FormatFilterTypes()} instance='{_trackedInstanceId}' action=RequestUseSelectedItem");
                Debug.Log($"[NH_FLOW][07][SelectableItem.RequestUseSelected] button={name} {DescribeItemButtonState()}");
                _selectionSystem.RequestUseSelectedItem();
                return;
            }

            // ── Single click: select toggle ───────────────────────────────────────
            if (!hasTracked)
            {
                Debug.Log($"[NH_FLOW][05][SelectableItem.ExpandRequested] button={name} reason=single-empty {DescribeItemButtonState()}");
                OnExpandRequested?.Invoke();
                return;
            }

            string selectedId   = _selectionSystem.SelectedItem?.InstanceID;
            string currentUseId = _itemUseSystem?.CurrentItem?.InstanceID;

            bool isArmed = hasTracked
                        && _itemUseSystem != null
                        && _itemUseSystem.IsUsingItem
                        && currentUseId == _trackedInstanceId;

            bool isSelected = hasTracked
                           && !isArmed
                           && selectedId == _trackedInstanceId;

            Debug.Log($"[ITEM_FLOW] [01][SingleClick] filters={FormatFilterTypes()} tracked='{_trackedInstanceId}' " +
                      $"selected='{selectedId ?? "null"}' currentUse='{currentUseId ?? "null"}' isSelected={isSelected} isArmed={isArmed}");
            Debug.Log($"[NH_FLOW][05][SelectableItem.SingleClickState] button={name} isSelected={isSelected} isArmed={isArmed} {DescribeItemButtonState()}");

            if (isArmed)
            {
                // Put the item down.
                Debug.Log($"[ITEM_FLOW] [01][SingleClick.Armed] action=cancel");
                Debug.Log($"[NH_FLOW][07][SelectableItem.RequestCancelSelection] button={name} reason=armed {DescribeItemButtonState()}");
                _selectionSystem.RequestCancelSelection();
                return;
            }

            if (isSelected)
            {
                // Toggle off.
                Debug.Log($"[ITEM_FLOW] [01][SingleClick.Selected] action=deselect");
                Debug.Log($"[NH_FLOW][07][SelectableItem.RequestDeselect] button={name} reason=selected {DescribeItemButtonState()}");
                _selectionSystem.RequestDeselectItem();
                return;
            }

            // Not yet selected — select only. Deployables are armed by an explicit use/fire gesture.
            Debug.Log($"[ITEM_FLOW] [01][SingleClick.NotSelected] action=selectOnly instance='{_trackedInstanceId}'");
            Debug.Log($"[NH_FLOW][07][SelectableItem.RequestSelectOnly] button={name} {DescribeItemButtonState()}");
            _selectionSystem.RequestSelectItem(_trackedInstanceId);
        }

        // ── Display refresh ───────────────────────────────────────────────────────

        private string DescribeItemButtonState()
        {
            var tracked = !string.IsNullOrEmpty(_trackedInstanceId)
                ? GetItemByInstanceId(_trackedInstanceId)
                : null;
            var trackedDef = tracked != null ? ItemDatabase.GetDefinition(tracked.DefinitionID) : null;
            var selected = _selectionSystem?.SelectedItem;
            var selectedDef = selected != null ? ItemDatabase.GetDefinition(selected.DefinitionID) : null;
            var current = _itemUseSystem?.CurrentItem;
            var currentDef = current != null ? ItemDatabase.GetDefinition(current.DefinitionID) : null;
            return $"filters={FormatFilterTypes()} tracked='{_trackedInstanceId ?? "null"}' trackedDef={trackedDef?.ItemID ?? "null"} trackedType={trackedDef?.Type.ToString() ?? "null"} " +
                   $"selected='{selected?.InstanceID ?? "null"}' selectedDef={selectedDef?.ItemID ?? "null"} selectedType={selectedDef?.Type.ToString() ?? "null"} " +
                   $"using={_itemUseSystem?.IsUsingItem.ToString() ?? "null"} current='{current?.InstanceID ?? "null"}' currentDef={currentDef?.ItemID ?? "null"} currentType={currentDef?.Type.ToString() ?? "null"} " +
                   $"aimController={(_aimController != null ? _aimController.name : "null")}";
        }

        private void RefreshDisplay()
        {
            // Re-fetch from inventory so quantity is always current.
            var item = !string.IsNullOrEmpty(_trackedInstanceId)
                ? GetItemByInstanceId(_trackedInstanceId)
                : null;

            var def     = item != null ? ItemDatabase.GetDefinition(item.DefinitionID) : null;
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

            RefreshStateVisuals(hasItem);
        }

        private void RefreshStateVisuals(bool hasItem)
        {
            bool isSelected = hasItem
                           && _selectionSystem != null
                           && _selectionSystem.SelectedItem?.InstanceID == _trackedInstanceId;

            bool isArmed = hasItem
                        && _itemUseSystem != null
                        && _itemUseSystem.IsUsingItem
                        && _itemUseSystem.CurrentItem?.InstanceID == _trackedInstanceId;

            if (_selectedIndicator != null)
                _selectedIndicator.SetActive(isSelected || isArmed);

            if (_icon != null)
                _icon.color = isArmed
                    ? _armedIconColor
                    : (isSelected ? _selectedIconColor : Color.white);
        }

        // ── Auto-fill ─────────────────────────────────────────────────────────────

        /// <summary>
        /// If the tracked slot is empty and the player has a matching item in their
        /// inventory, track it locally so the collapsed slot shows an icon.
        /// Does NOT send selection RPCs — adding loot should never auto-arm items.
        /// </summary>
        private bool TryAutoFill() => TryTrackFirstMatching(null, allowReplace: false);

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

        // ── Event subscriptions ───────────────────────────────────────────────────

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

            if (item.InstanceID == _trackedInstanceId)
            {
                RefreshDisplay();
                return;
            }

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
                RefreshDisplay();

                if (fullyGone)
                {
                    _trackedInstanceId = null;
                    RefreshDisplay();
                    TryAutoFill();
                }
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private ItemInstance GetItemByInstanceId(string instanceId)
        {
            var items = GetCurrentPlayerItems();
            if (items == null) return null;
            foreach (var item in items)
                if (item?.InstanceID == instanceId) return item;
            return null;
        }

        private IReadOnlyList<ItemInstance> GetCurrentPlayerItems() =>
            _inventorySystem?.GetAllItems();

        private bool MatchesFilter(ItemDefinition def)
        {
            if (def == null) return false;
            for (int i = 0; i < _filterTypes.Count; i++)
            {
                if (def.Type == _filterTypes[i]) return true;
                // Deployables are grouped under the Throwable slot.
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

        private bool TryStartAimControllerFlow(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
                return false;

            if (_aimController == null)
                _aimController = ResolveAimController();

            if (_aimController == null)
            {
                Debug.LogWarning($"[ITEM_FLOW] [01][AimController.Missing] filters={FormatFilterTypes()} instance='{instanceId}'");
                return false;
            }

            bool handled = _aimController.TryBeginAim(instanceId);
            if (handled)
                Debug.Log($"[ITEM_FLOW] [01][AimController.Handled] filters={FormatFilterTypes()} instance='{instanceId}'");

            return handled;
        }

        private static ThrowableAimController ResolveAimController()
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<ThrowableAimController>(FindObjectsInactive.Include);
#else
            return FindObjectOfType<ThrowableAimController>(true);
#endif
        }

        private bool IsTrackedDeployable()
        {
            var item = !string.IsNullOrEmpty(_trackedInstanceId)
                ? GetItemByInstanceId(_trackedInstanceId)
                : null;
            var def = item != null ? ItemDatabase.GetDefinition(item.DefinitionID) : null;
            return def != null && def.Type == ItemType.Deployable;
        }

        private bool IsTrackedAimItem()
        {
            var item = !string.IsNullOrEmpty(_trackedInstanceId)
                ? GetItemByInstanceId(_trackedInstanceId)
                : null;
            var def = item != null ? ItemDatabase.GetDefinition(item.DefinitionID) : null;
            return def != null && (def.Type == ItemType.Throwable || def.Type == ItemType.Deployable);
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

        private NightHunt.Gameplay.Character.CharacterAnimationController _cachedAnimController;
        private NightHunt.Gameplay.Character.CharacterAnimationController ResolveAnimController()
        {
            if (_cachedAnimController != null) return _cachedAnimController;
            if (_combatInputHandler != null)
            {
                _cachedAnimController = _combatInputHandler.GetComponent<NightHunt.Gameplay.Character.CharacterAnimationController>();
                if (_cachedAnimController == null)
                    _cachedAnimController = _combatInputHandler.GetComponentInChildren<NightHunt.Gameplay.Character.CharacterAnimationController>();
            }
            if (_cachedAnimController == null)
            {
#if UNITY_2023_1_OR_NEWER
                _cachedAnimController = FindFirstObjectByType<NightHunt.Gameplay.Character.CharacterAnimationController>(FindObjectsInactive.Include);
#else
                _cachedAnimController = FindObjectOfType<NightHunt.Gameplay.Character.CharacterAnimationController>(true);
#endif
            }
            return _cachedAnimController;
        }
    }
}
