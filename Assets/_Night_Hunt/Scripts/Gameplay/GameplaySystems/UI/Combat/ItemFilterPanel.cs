using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.Gameplay.Spectator;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.UI.Combat
{
    /// <summary>
    /// Compact item-selection panel for a single ItemType (Consumable or Throwable).
    ///
    /// ── Visual States ────────────────────────────────────────────────────────────
    /// Collapsed  — shows selected item icon + quantity + expand-arrow.
    ///              Tapping the icon:
    ///                • Consumable → UseSelectedItem() immediately.
    ///                • Throwable  → UseSelectedItem() = enter aim/arm mode
    ///                               (fire button then executes the throw).
    ///              Tapping the icon when nothing is selected → open expanded list.
    ///
    /// Expanded   — scrollable list of all items of this type.
    ///              Single-click  → SelectItem + collapse.
    ///              Double-click  → SelectItem + UseSelectedItem (consumable shortcut).
    ///
    /// ── Auto-Select Rules ────────────────────────────────────────────────────────
    /// • First item of this type is picked up and nothing is selected → auto-select.
    /// • Currently selected item fully depletes → auto-select next available.
    /// • None remain → deselect (only if this panel owned the selection).
    ///
    /// ── SpectateManager Usage ────────────────────────────────────────────────────
    /// GetAllItems() always reads via SpectateManager.GetCurrentPlayer() so the panel
    /// stays correct when playing locally AND when spectating another player.
    /// _inventorySystem is injected only to subscribe to inventory change events.
    ///
    /// ── Inspector ────────────────────────────────────────────────────────────────
    /// _selectedIcon         – Image for the selected item icon (collapsed view).
    /// _selectedQuantityText – TMP text for quantity.
    /// _selectedItemButton   – Button over the icon area; tap = use / arm.
    /// _emptyIndicator       – Shown when no item is selected.
    /// _listRoot             – Parent toggled on/off for the expanded list.
    /// _contentRoot          – Transform receiving SelectableItemButton instances.
    /// _expandButton         – Arrow/toggle button to expand/collapse.
    /// _buttonPrefab         – Prefab with SelectableItemButton component.
    /// </summary>
    public class ItemFilterPanel : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [Header("Collapsed View")]
        [SerializeField] private Image      _selectedIcon;
        [SerializeField] private TMP_Text   _selectedQuantityText;
        [Tooltip("Button overlaid on the selected-item icon.\n" +
                 "Tap = UseSelectedItem (consumable: immediate; throwable: enter aim mode).")]
        [SerializeField] private Button     _selectedItemButton;
        [SerializeField] private GameObject _emptyIndicator;

        [Header("Expanded List")]
        [SerializeField] private GameObject           _listRoot;
        [SerializeField] private Transform            _contentRoot;
        [SerializeField] private Button               _expandButton;
        [SerializeField] private SelectableItemButton _buttonPrefab;

        // ── Runtime ───────────────────────────────────────────────────────────────

        private readonly List<SelectableItemButton> _spawnedButtons = new();

        private ItemType             _filterType;
        private IItemSelectionSystem _selectionSystem;
        private IInventorySystem     _inventorySystem;   // event subscription only

        /// <summary>
        /// Cached instance ID of the last selected item.
        /// Required because SelectedItem can return null the instant an item is
        /// removed from inventory — which happens before OnItemDeselected fires
        /// (ConsumeItem runs inside CompleteUse, before the event).
        /// </summary>
        private string _lastSelectedInstanceID;

        public bool IsExpanded { get; private set; }

        // ─────────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Bind this panel to a filter type and the current player's systems.
        /// Safe to call multiple times (re-init on player / spectate change).
        /// </summary>
        public void Initialize(
            ItemType             filterType,
            IItemSelectionSystem selectionSystem,
            IInventorySystem     inventorySystem = null)
        {
            Unsubscribe();

            _filterType      = filterType;
            _selectionSystem = selectionSystem;
            _inventorySystem = inventorySystem;

            if (_expandButton != null)
            {
                _expandButton.onClick.RemoveListener(ToggleList);
                _expandButton.onClick.AddListener(ToggleList);
            }

            if (_selectedItemButton != null)
            {
                _selectedItemButton.onClick.RemoveListener(OnSelectedIconPressed);
                _selectedItemButton.onClick.AddListener(OnSelectedIconPressed);
            }

            Subscribe();
            CollapseList();
            RefreshSelectedDisplay();
        }

        public void ExpandList()
        {
            IsExpanded = true;
            RebuildList();
            if (_listRoot != null) _listRoot.SetActive(true);
        }

        public void CollapseList()
        {
            IsExpanded = false;
            if (_listRoot != null) _listRoot.SetActive(false);
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────────

        private void OnDestroy()
        {
            Unsubscribe();

            if (_expandButton != null)
                _expandButton.onClick.RemoveListener(ToggleList);

            if (_selectedItemButton != null)
                _selectedItemButton.onClick.RemoveListener(OnSelectedIconPressed);
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  Event Subscriptions
        // ─────────────────────────────────────────────────────────────────────────

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

        // ─────────────────────────────────────────────────────────────────────────
        //  Collapsed Icon — Tap to Use / Arm
        // ─────────────────────────────────────────────────────────────────────────

        private void OnSelectedIconPressed()
        {
            if (_selectionSystem == null) return;

            // Nothing selected → open list so the player can pick something.
            if (string.IsNullOrEmpty(_lastSelectedInstanceID))
            {
                ExpandList();
                return;
            }

            // Re-confirm selection (handles case where another panel owns it).
            _selectionSystem.SelectItem(_lastSelectedInstanceID);

            // UseSelectedItem:
            //   Consumable  → starts use coroutine immediately.
            //   Throwable   → enters aim/arm mode; fire button executes the throw.
            _selectionSystem.UseSelectedItem();
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  Selection System Events
        // ─────────────────────────────────────────────────────────────────────────

        private void HandleItemSelected(ItemInstance item)
        {
            // Only react to items belonging to this panel's type.
            var def = item != null ? ItemDatabase.GetDefinition(item.DefinitionID) : null;
            if (def == null || def.Type != _filterType) return;

            _lastSelectedInstanceID = item.InstanceID;
            RefreshSelectedDisplay();
            RefreshSelectionMarkersOnButtons();
            CollapseList();     // always collapse when a selection is made
        }

        private void HandleItemDeselected()
        {
            // Only clear if this panel owned the selection.
            if (string.IsNullOrEmpty(_lastSelectedInstanceID)) return;

            _lastSelectedInstanceID = null;
            RefreshSelectedDisplay();
            RefreshSelectionMarkersOnButtons();
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  Inventory System Events
        // ─────────────────────────────────────────────────────────────────────────

        private void HandleInventoryItemAdded(ItemInstance item)
        {
            if (item == null) return;
            var def = ItemDatabase.GetDefinition(item.DefinitionID);
            if (def == null || def.Type != _filterType) return;

            if (IsExpanded) RebuildList();

            // Auto-select first item of this type when nothing is selected yet.
            if (_selectionSystem != null && !_selectionSystem.HasSelection)
                TryAutoSelectFirst();
        }

        private void HandleInventoryItemRemoved(ItemInstance item, int quantityRemoved)
        {
            if (item == null) return;
            var def = ItemDatabase.GetDefinition(item.DefinitionID);
            if (def == null || def.Type != _filterType) return;

            if (IsExpanded) RebuildList();

            // _lastSelectedInstanceID is checked here because SelectedItem may already
            // return null (item removed from inventory before this event fires).
            bool wasSelected = !string.IsNullOrEmpty(_lastSelectedInstanceID)
                            && _lastSelectedInstanceID == item.InstanceID;
            bool fullyGone   = item.Quantity - quantityRemoved <= 0;

            if (wasSelected && fullyGone)
            {
                _lastSelectedInstanceID = null;     // clear before searching
                TryAutoSelectFirst();
            }

            RefreshSelectedDisplay();
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  Auto-Select
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Selects the first available item of <see cref="_filterType"/> with qty > 0.
        /// Uses SpectateManager so it works correctly in spectate mode.
        /// Deselects if none remain and this panel currently owns the selection.
        /// </summary>
        private void TryAutoSelectFirst()
        {
            if (_selectionSystem == null) return;

            var items = GetCurrentPlayerItems();
            if (items != null)
            {
                foreach (var inv in items)
                {
                    if (inv == null || inv.InventoryIndex < 0 || inv.Quantity <= 0) continue;
                    var def = ItemDatabase.GetDefinition(inv.DefinitionID);
                    if (def == null || def.Type != _filterType) continue;

                    _selectionSystem.SelectItem(inv.InstanceID);
                    return;
                }
            }

            // Nothing left of this type — deselect only if we own the selection.
            if (_selectionSystem.HasSelection)
            {
                var current    = _selectionSystem.SelectedItem;
                var currentDef = current != null ? ItemDatabase.GetDefinition(current.DefinitionID) : null;
                if (currentDef?.Type == _filterType)
                    _selectionSystem.DeselectItem();
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  Expand / Collapse Toggle
        // ─────────────────────────────────────────────────────────────────────────

        private void ToggleList()
        {
            if (IsExpanded) CollapseList();
            else            ExpandList();
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  Expanded List Builder
        // ─────────────────────────────────────────────────────────────────────────

        private void RebuildList()
        {
            ClearSpawnedButtons();

            if (_contentRoot == null || _buttonPrefab == null || _selectionSystem == null) return;

            var items = GetCurrentPlayerItems();
            if (items == null) return;

            foreach (var item in items)
            {
                if (item == null || item.InventoryIndex < 0 || item.Quantity <= 0) continue;
                var def = ItemDatabase.GetDefinition(item.DefinitionID);
                if (def == null || def.Type != _filterType) continue;

                var btn = Instantiate(_buttonPrefab, _contentRoot);
                btn.Bind(item, _selectionSystem);
                _spawnedButtons.Add(btn);
            }
        }

        private void ClearSpawnedButtons()
        {
            foreach (var btn in _spawnedButtons)
                if (btn != null) Destroy(btn.gameObject);
            _spawnedButtons.Clear();
        }

        private void RefreshSelectionMarkersOnButtons()
        {
            foreach (var btn in _spawnedButtons)
                btn?.RefreshSelectedMarker();
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  Collapsed View — Selected Item Display
        // ─────────────────────────────────────────────────────────────────────────

        private void RefreshSelectedDisplay()
        {
            var selected = _selectionSystem?.SelectedItem;
            var def      = selected != null ? ItemDatabase.GetDefinition(selected.DefinitionID) : null;

            // Only show items that belong to this panel's filter type.
            bool hasSelection = selected != null && def != null && def.Type == _filterType;

            if (_selectedIcon != null)
            {
                _selectedIcon.sprite  = hasSelection ? def.Icon : null;
                _selectedIcon.enabled = hasSelection && def.Icon != null;
            }

            if (_selectedQuantityText != null)
                _selectedQuantityText.text = hasSelection && selected.Quantity > 1
                    ? selected.Quantity.ToString()
                    : string.Empty;

            // Tap-to-use only makes sense when something is selected.
            // When nothing is selected the button still works (opens the list).
            if (_selectedItemButton != null)
                _selectedItemButton.interactable = true;

            if (_emptyIndicator != null)
                _emptyIndicator.SetActive(!hasSelection);
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns all items for whoever is currently being viewed (local or spectated).
        /// This is a READ-ONLY data path — no actions are taken here — so SpectateManager
        /// is the correct source of truth rather than a fixed _inventorySystem reference.
        /// </summary>
        private IReadOnlyList<ItemInstance> GetCurrentPlayerItems()
        {
            return SpectateManager.Instance
                ?.GetCurrentPlayer()
                ?.GamePlaySystemBridge
                ?.GetAllItems();
        }
    }
}