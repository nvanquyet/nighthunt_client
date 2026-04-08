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
    /// â”€â”€ Roles after refactor â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    ///
    /// <see cref="SelectableItemButton"/> (_slotButton)
    ///   â€” The permanent collapsed icon button the player sees in the HUD.
    ///   â€” Manages its own icon / quantity display and auto-fill from inventory.
    ///   â€” Click state machine: select â†’ arm â†’ cancel (double-click = cancel).
    ///   â€” Fires <see cref="SelectableItemButton.OnExpandRequested"/> when clicked
    ///     while empty so this panel can open the list.
    ///
    /// <see cref="ItemFilterButton"/> (_filterButtonPrefab)
    ///   â€” One row per inventory item in the expanded list.
    ///   â€” Single press â†’ RequestSelectItem + collapse panel.
    ///   â€” Selection marker shows the currently active item.
    ///
    /// This panel (ItemFilterPanel):
    ///   â€” Manages the expand / collapse transition.
    ///   â€” Rebuilds the ItemFilterButton list on inventory changes.
    ///   â€” Updates selection markers on ItemFilterButtons on selection changes.
    ///
    /// â”€â”€ Inspector â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    ///   _slotButton          â€“ The SelectableItemButton that acts as the HUD icon.
    ///   _listRoot            â€“ Root GameObject toggled on/off for the expanded list.
    ///   _contentRoot         â€“ Parent transform for spawned ItemFilterButton instances.
    ///   _expandButton        â€“ Arrow / chevron button to manually expand / collapse.
    ///   _filterButtonPrefab  â€“ Prefab with an ItemFilterButton component.
    /// </summary>
    public class ItemFilterPanel : MonoBehaviour
    {
        // â”€â”€ Inspector â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [Header("Collapsed Slot Button")]
        [Tooltip("The SelectableItemButton displayed as the collapsed HUD icon for this filter type.")]
        [SerializeField] private SelectableItemButton _slotButton;

        [Header("Expanded List")]
        [SerializeField] private GameObject       _listRoot;
        [SerializeField] private Transform        _contentRoot;
        [SerializeField] private Button           _expandButton;
        [SerializeField] private ItemFilterButton _filterButtonPrefab;

        // â”€â”€ Runtime â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private readonly List<ItemFilterButton> _spawnedButtons = new();

        private ItemType             _filterType;
        private IItemSelectionSystem _selectionSystem;
        private IInventorySystem     _inventorySystem;

        public bool IsExpanded { get; private set; }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        //  Public API
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

            // â”€â”€ Expand button â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            if (_expandButton != null)
            {
                _expandButton.onClick.RemoveListener(ToggleList);
                _expandButton.onClick.AddListener(ToggleList);
            }

            // â”€â”€ Slot button (SelectableItemButton) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
            Debug.Log($"[ItemFilterPanel:{_filterType}] ExpandList â€” {_spawnedButtons.Count} buttons");
        }

        public void CollapseList()
        {
            IsExpanded = false;
            if (_listRoot != null) _listRoot.SetActive(false);
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        //  Unity Lifecycle
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void OnDestroy()
        {
            Unsubscribe();

            if (_expandButton != null)
                _expandButton.onClick.RemoveListener(ToggleList);

            if (_slotButton != null)
                _slotButton.OnExpandRequested -= ExpandList;
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

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        //  Selection Events
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void HandleItemSelected(ItemInstance item)
        {
            var def = item != null ? ItemDatabase.GetDefinition(item.DefinitionID) : null;
            if (def == null || def.Type != _filterType) return;

            Debug.Log($"[ItemFilterPanel:{_filterType}] HandleItemSelected '{item.InstanceID}' â†’ collapse + refresh markers");
            RefreshSelectionMarkersOnButtons();
            CollapseList();
        }

        private void HandleItemDeselected()
        {
            Debug.Log($"[ItemFilterPanel:{_filterType}] HandleItemDeselected â†’ refresh markers");
            RefreshSelectionMarkersOnButtons();
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        //  Inventory Events (list rebuild only â€” auto-fill is in SelectableItemButton)
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        //  Expand / Collapse Toggle
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void ToggleList()
        {
            if (IsExpanded) CollapseList();
            else            ExpandList();
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        //  Expanded List Builder
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        //  Helpers
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private IReadOnlyList<ItemInstance> GetCurrentPlayerItems()
        {
            return SpectateManager.Instance
                ?.GetCurrentPlayer()
                ?.GamePlaySystemBridge
                ?.GetAllItems();
        }

#if UNITY_EDITOR
        // ─────────────────────────────────────────────────────────────────────
        //  Editor — Context Menu: Create ItemFilterButton Template Prefab
        // ─────────────────────────────────────────────────────────────────────

        [ContextMenu("NightHunt/Create ItemFilterButton Template Prefab")]
        private void Editor_CreateItemFilterButtonPrefab()
        {
            const string parent = "Assets/_Night_Hunt/Prefabs";
            const string dir    = parent + "/UI";
            if (!UnityEditor.AssetDatabase.IsValidFolder(dir))
                UnityEditor.AssetDatabase.CreateFolder(parent, "UI");

            const string path = dir + "/ItemFilterButton_Template.prefab";
            if (UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.Log($"[ItemFilterPanel] ItemFilterButton_Template already exists at {path}");
                return;
            }

            var go  = new GameObject("ItemFilterButton_Template");
            var rt  = go.AddComponent<UnityEngine.RectTransform>();
            rt.sizeDelta = new UnityEngine.Vector2(280f, 50f);
            go.AddComponent<UnityEngine.UI.Image>().color = new UnityEngine.Color(0.15f, 0.15f, 0.15f, 0.9f);
            go.AddComponent<UnityEngine.UI.Button>();

            // Item icon
            var iconGo = new GameObject("ItemIcon", typeof(UnityEngine.RectTransform), typeof(UnityEngine.UI.Image));
            iconGo.transform.SetParent(go.transform, false);
            var iconRt = iconGo.GetComponent<UnityEngine.RectTransform>();
            iconRt.anchorMin = new UnityEngine.Vector2(0f, 0.1f);
            iconRt.anchorMax = new UnityEngine.Vector2(0.18f, 0.9f);
            iconRt.offsetMin = iconRt.offsetMax = UnityEngine.Vector2.zero;

            // Item name
            var nameGo  = new GameObject("ItemName", typeof(UnityEngine.RectTransform), typeof(TMPro.TextMeshProUGUI));
            nameGo.transform.SetParent(go.transform, false);
            var nameRt  = nameGo.GetComponent<UnityEngine.RectTransform>();
            nameRt.anchorMin = new UnityEngine.Vector2(0.2f, 0.3f);
            nameRt.anchorMax = new UnityEngine.Vector2(0.8f, 0.9f);
            nameRt.offsetMin = nameRt.offsetMax = UnityEngine.Vector2.zero;
            var nameTmp = nameGo.GetComponent<TMPro.TextMeshProUGUI>();
            nameTmp.text = "Item Name"; nameTmp.fontSize = 13f;

            // Quantity badge
            var qtyGo  = new GameObject("QtyText", typeof(UnityEngine.RectTransform), typeof(TMPro.TextMeshProUGUI));
            qtyGo.transform.SetParent(go.transform, false);
            var qtyRt  = qtyGo.GetComponent<UnityEngine.RectTransform>();
            qtyRt.anchorMin = new UnityEngine.Vector2(0.2f, 0.05f);
            qtyRt.anchorMax = new UnityEngine.Vector2(0.5f, 0.38f);
            qtyRt.offsetMin = qtyRt.offsetMax = UnityEngine.Vector2.zero;
            var qtyTmp = qtyGo.GetComponent<TMPro.TextMeshProUGUI>();
            qtyTmp.text = "x3"; qtyTmp.fontSize = 11f;

            // Active selection marker
            var selGo  = new GameObject("SelectedMarker", typeof(UnityEngine.RectTransform), typeof(UnityEngine.UI.Image));
            selGo.transform.SetParent(go.transform, false);
            var selRt  = selGo.GetComponent<UnityEngine.RectTransform>();
            selRt.anchorMin = new UnityEngine.Vector2(0.85f, 0.15f);
            selRt.anchorMax = new UnityEngine.Vector2(0.98f, 0.85f);
            selRt.offsetMin = selRt.offsetMax = UnityEngine.Vector2.zero;
            selGo.GetComponent<UnityEngine.UI.Image>().color = UnityEngine.Color.green;
            selGo.SetActive(false); // hidden by default

            var saved = UnityEditor.PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);

            if (_filterButtonPrefab == null)
            {
                _filterButtonPrefab = saved.GetComponent<ItemFilterButton>();
                UnityEditor.EditorUtility.SetDirty(this);
            }
            Debug.Log($"[ItemFilterPanel] Created ItemFilterButton_Template at {path}. " +
                      "Add ItemFilterButton component and wire icon/name/qty/selectedMarker fields.");
        }
#endif
    }
}
