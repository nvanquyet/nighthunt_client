using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.GameplaySystems.ItemUse;
using NightHunt.Gameplay.Input.Handlers.Combat;

namespace NightHunt.GameplaySystems.UI.Combat
{
    /// <summary>
    /// Container managing the expand/collapse flow for a single item-type filter slot.
    /// The collapsed slot owns the select/arm/cancel state machine; this panel owns
    /// list visibility and row creation.
    /// </summary>
    public class ItemFilterPanel : MonoBehaviour
    {
        [Header("Collapsed Slot Button")]
        [Tooltip("The SelectableItemButton displayed as the collapsed HUD icon for this filter type.")]
        [SerializeField] private SelectableItemButton _slotButton;

        [Header("Expanded List")]
        [SerializeField] private GameObject       _listRoot;
        [SerializeField] private Transform        _contentRoot;
        [SerializeField] private Button           _expandButton;
        [SerializeField] private ItemFilterButton _filterButtonPrefab;

        private readonly List<ItemFilterButton> _spawnedButtons = new();

        private ItemType             _filterType;
        private readonly List<ItemType> _filterTypes = new();
        private IItemSelectionSystem _selectionSystem;
        private IItemUseSystem       _itemUseSystem;
        private IInventorySystem     _inventorySystem;
        private CombatInputHandler   _combatInputHandler;
        private Coroutine            _pendingCollapseRoutine;

        public bool IsExpanded { get; private set; }
        private const float FilterSingleClickCollapseDelay = 0.3f;

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
            Initialize(new[] { filterType }, selectionSystem, inventorySystem, itemUseSystem, combatInputHandler);
        }

        public void Initialize(
            IReadOnlyList<ItemType> filterTypes,
            IItemSelectionSystem selectionSystem,
            IInventorySystem     inventorySystem    = null,
            IItemUseSystem       itemUseSystem      = null,
            CombatInputHandler   combatInputHandler = null)
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
            _combatInputHandler = combatInputHandler;

            if (_expandButton != null)
            {
                _expandButton.onClick.RemoveListener(ToggleList);
                _expandButton.onClick.AddListener(ToggleList);
            }

            if (_slotButton != null)
            {
                // Wire expand request so clicking an empty slot opens the list.
                _slotButton.OnExpandRequested -= ExpandList;
                _slotButton.OnExpandRequested += ExpandList;

                _slotButton.BindCombatHandler(combatInputHandler);
                _slotButton.Initialize(_filterTypes, selectionSystem, itemUseSystem, inventorySystem);
            }

            Subscribe();
            CollapseList();
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

        public void ActivateShortcut()
        {
            Debug.Log($"[ItemFilterPanel:{FormatFilterTypes(_filterTypes)}] shortcut activate expanded={IsExpanded} tracked='{_slotButton?.GetTrackedInstanceId() ?? "null"}'");
            _slotButton?.ActivateFromShortcut();
        }

        public void ActivateShortcut(ItemType preferredType)
        {
            Debug.Log($"[ItemFilterPanel:{FormatFilterTypes(_filterTypes)}] shortcut activate preferred={preferredType} expanded={IsExpanded} tracked='{_slotButton?.GetTrackedInstanceId() ?? "null"}'");
            _slotButton?.ActivateFromShortcut(preferredType);
        }

        public void SelectFromFilter(string instanceId, bool useImmediately)
        {
            if (string.IsNullOrEmpty(instanceId) || _selectionSystem == null)
            {
                Debug.LogWarning($"[ItemFilterPanel:{FormatFilterTypes(_filterTypes)}] SelectFromFilter ignored instance='{instanceId}' selection={(_selectionSystem != null ? "ok" : "null")}");
                return;
            }

            _slotButton?.SetTrackedItem(instanceId);
            Debug.Log($"[ITEM_FLOW] [02][FilterPanel.Select] filters={FormatFilterTypes(_filterTypes)} instance='{instanceId}' useImmediately={useImmediately}");

            if (useImmediately
                && _itemUseSystem != null
                && _itemUseSystem.IsUsingItem
                && _itemUseSystem.CurrentItem?.InstanceID != instanceId)
            {
                Debug.Log($"[ITEM_FLOW] [02][FilterPanel.SwitchWhileUsing] from='{_itemUseSystem.CurrentItem?.InstanceID ?? "null"}' to='{instanceId}' action=RequestCancelSelection");
                _selectionSystem.RequestCancelSelection();
            }

            _selectionSystem.RequestSelectItem(instanceId);

            if (_pendingCollapseRoutine != null)
            {
                StopCoroutine(_pendingCollapseRoutine);
                _pendingCollapseRoutine = null;
            }

            if (useImmediately)
            {
                Debug.Log($"[ITEM_FLOW] [03][FilterPanel.Use] instance='{instanceId}' action=RequestUseSelectedItem");
                _selectionSystem.RequestUseSelectedItem();
                CollapseList();
            }
            else
            {
                _pendingCollapseRoutine = StartCoroutine(CollapseAfterSingleClickWindow());
            }
        }

        private System.Collections.IEnumerator CollapseAfterSingleClickWindow()
        {
            yield return new WaitForSecondsRealtime(FilterSingleClickCollapseDelay);
            _pendingCollapseRoutine = null;
            CollapseList();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (_pendingCollapseRoutine != null)
            {
                StopCoroutine(_pendingCollapseRoutine);
                _pendingCollapseRoutine = null;
            }

            if (_expandButton != null)
                _expandButton.onClick.RemoveListener(ToggleList);

            if (_slotButton != null)
                _slotButton.OnExpandRequested -= ExpandList;
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
            if (!MatchesFilter(def))
                return;

            RefreshSelectionMarkersOnButtons();
            CollapseList();
        }

        private void HandleItemDeselected()
        {
            RefreshSelectionMarkersOnButtons();
        }

        private void HandleInventoryItemAdded(ItemInstance item)
        {
            if (item == null) return;
            var def = ItemDatabase.GetDefinition(item.DefinitionID);
            if (!MatchesFilter(def))
                return;

            if (IsExpanded) RebuildList();
        }

        private void HandleInventoryItemRemoved(ItemInstance item, int quantityRemoved)
        {
            if (item == null) return;
            var def = ItemDatabase.GetDefinition(item.DefinitionID);
            if (!MatchesFilter(def))
                return;

            if (IsExpanded) RebuildList();
        }

        private void ToggleList()
        {
            if (IsExpanded) CollapseList();
            else            ExpandList();
        }

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
                if (!MatchesFilter(def)) continue;

                var btn = Instantiate(_filterButtonPrefab, _contentRoot);
                btn.Bind(item, _selectionSystem, this, _combatInputHandler);
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

        private static string FormatFilterTypes(IReadOnlyList<ItemType> filterTypes)
        {
            if (filterTypes == null || filterTypes.Count == 0) return "None";
            if (filterTypes.Count == 1) return filterTypes[0].ToString();

            var text = filterTypes[0].ToString();
            for (int i = 1; i < filterTypes.Count; i++)
                text += "+" + filterTypes[i];
            return text;
        }
    }
}
