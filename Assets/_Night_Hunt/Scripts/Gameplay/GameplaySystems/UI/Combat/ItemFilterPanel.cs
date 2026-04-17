using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.Gameplay.Spectator;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.GameplaySystems.ItemUse;
using NightHunt.Gameplay.Input.Handlers.Combat;

namespace NightHunt.GameplaySystems.UI.Combat
{
    /// <summary>
    /// Container managing the expand/collapse flow for a single item-type filter slot.
    ///
    /// ── Roles after refactor ─────────────────────────────────────────────────────
    ///
    /// <see cref="SelectableItemButton"/> (_slotButton)
    ///   — The permanent collapsed icon button the player sees in the HUD.
    ///   — Manages its own icon / quantity display and auto-fill from inventory.
    ///   — Click state machine: select → arm → cancel (double-click = cancel).
    ///   — Fires <see cref="SelectableItemButton.OnExpandRequested"/> when clicked
    ///     while empty so this panel can open the list.
    ///
    /// <see cref="ItemFilterButton"/> (_filterButtonPrefab)
    ///   — One row per inventory item in the expanded list.
    ///   — Single press → RequestSelectItem + collapse panel.
    ///   — Selection marker shows the currently active item.
    ///
    /// This panel (ItemFilterPanel):
    ///   — Manages the expand / collapse transition.
    ///   — Rebuilds the ItemFilterButton list on inventory changes.
    ///   — Updates selection markers on ItemFilterButtons on selection changes.
    ///
    /// ── Inspector ────────────────────────────────────────────────────────────────
    ///   _slotButton          – The SelectableItemButton that acts as the HUD icon.
    ///   _listRoot            – Root GameObject toggled on/off for the expanded list.
    ///   _contentRoot         – Parent transform for spawned ItemFilterButton instances.
    ///   _expandButton        – Arrow / chevron button to manually expand / collapse.
    ///   _filterButtonPrefab  – Prefab with an ItemFilterButton component.
    /// </summary>
    public class ItemFilterPanel : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [Header("Collapsed Slot Button")]
        [Tooltip("The SelectableItemButton displayed as the collapsed HUD icon for this filter type.")]
        [SerializeField] private SelectableItemButton _slotButton;

        [Header("Expanded List")]
        [SerializeField] private GameObject       _listRoot;
        [SerializeField] private Transform        _contentRoot;
        [SerializeField] private Button           _expandButton;
        [SerializeField] private ItemFilterButton _filterButtonPrefab;

        // ── Runtime ───────────────────────────────────────────────────────────────

        private readonly List<ItemFilterButton> _spawnedButtons = new();

        private ItemType             _filterType;
        private IItemSelectionSystem _selectionSystem;
        private IInventorySystem     _inventorySystem;

        public bool IsExpanded { get; private set; }

        // ─────────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Bind this panel to a filter type and the current player's systems.
        /// Safe to call multiple times (re-init on player / spectate change).
        /// Passes all relevant system refs down to <see cref="SelectableItemButton"/>.
        /// </summary>
        public void Initialize(
            ItemType             filterType,
            IItemSelectionSystem selectionSystem,
            IInventorySystem     inventorySystem    = null,
            IItemUseSystem       itemUseSystem      = null,
            CombatInputHandler   combatInputHandler = null)
        {
            Debug.Log($"[ItemFilterPanel:{filterType}] Initialize");
            Unsubscribe();

            _filterType      = filterType;
            _selectionSystem = selectionSystem;
            _inventorySystem = inventorySystem;

            // ── Expand button ──────────────────────────────────────────────────
            if (_expandButton != null)
            {
                _expandButton.onClick.RemoveListener(ToggleList);
                _expandButton.onClick.AddListener(ToggleList);
            }

            // ── Slot button (SelectableItemButton) ────────────────────────────
            if (_slotButton != null)
            {
                // Wire expand request so clicking an empty slot opens the list.
                _slotButton.OnExpandRequested -= ExpandList;
                _slotButton.OnExpandRequested += ExpandList;

                _slotButton.BindCombatHandler(combatInputHandler);
                _slotButton.Initialize(filterType, selectionSystem, itemUseSystem, inventorySystem);
            }

            Subscribe();
            CollapseList();
        }

        public void ExpandList()
        {
            IsExpanded = true;
            RebuildList();
            if (_listRoot != null) _listRoot.SetActive(true);
            Debug.Log($"[ItemFilterPanel:{_filterType}] ExpandList — {_spawnedButtons.Count} buttons");
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

            if (_slotButton != null)
                _slotButton.OnExpandRequested -= ExpandList;
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
        //  Selection Events
        // ─────────────────────────────────────────────────────────────────────────

        private void HandleItemSelected(ItemInstance item)
        {
            var def = item != null ? ItemDatabase.GetDefinition(item.DefinitionID) : null;
            if (def == null || def.Type != _filterType) return;

            Debug.Log($"[ItemFilterPanel:{_filterType}] HandleItemSelected '{item.InstanceID}' → collapse + refresh markers");
            RefreshSelectionMarkersOnButtons();
            CollapseList();
        }

        private void HandleItemDeselected()
        {
            Debug.Log($"[ItemFilterPanel:{_filterType}] HandleItemDeselected → refresh markers");
            RefreshSelectionMarkersOnButtons();
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  Inventory Events (list rebuild only — auto-fill is in SelectableItemButton)
        // ─────────────────────────────────────────────────────────────────────────

        private void HandleInventoryItemAdded(ItemInstance item)
        {
            if (item == null) return;
            var def = ItemDatabase.GetDefinition(item.DefinitionID);
            if (def == null || def.Type != _filterType) return;

            Debug.Log($"[ItemFilterPanel:{_filterType}] HandleInventoryItemAdded '{item.InstanceID}'");
            if (IsExpanded) RebuildList();
        }

        private void HandleInventoryItemRemoved(ItemInstance item, int quantityRemoved)
        {
            if (item == null) return;
            var def = ItemDatabase.GetDefinition(item.DefinitionID);
            if (def == null || def.Type != _filterType) return;

            Debug.Log($"[ItemFilterPanel:{_filterType}] HandleInventoryItemRemoved '{item.InstanceID}'");
            if (IsExpanded) RebuildList();
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

            if (_contentRoot == null || _filterButtonPrefab == null || _selectionSystem == null) return;

            var items = GetCurrentPlayerItems();
            if (items == null) return;

            foreach (var item in items)
            {
                if (item == null || item.InventoryIndex < 0 || item.Quantity <= 0) continue;
                var def = ItemDatabase.GetDefinition(item.DefinitionID);
                if (def == null || def.Type != _filterType) continue;

                var btn = Instantiate(_filterButtonPrefab, _contentRoot);
                btn.Bind(item, _selectionSystem, this);
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
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────────

        private IReadOnlyList<ItemInstance> GetCurrentPlayerItems()
        {
            return SpectateManager.Instance
                ?.GetCurrentPlayer()
                ?.GamePlaySystemBridge
                ?.GetAllItems();
        }
    }
}
