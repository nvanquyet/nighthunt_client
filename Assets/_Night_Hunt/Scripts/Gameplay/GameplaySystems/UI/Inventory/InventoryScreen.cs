using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Bridge;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Gameplay.Spectator;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Thin mediator for the Inventory screen.
    ///
    /// RESPONSIBILITIES:
    ///   - Build the slot grid once and keep it alive between player changes.
    ///   - Route hover / press / drag events to the correct sub-panels.
    ///   - Enforce OWNER-ONLY interaction: lock all slots in spectator mode.
    ///   - Apply double-click shortcuts (equip / use-immediately for consumable/throwable/deployable).
    ///
    /// NO business logic here. All decisions are delegated to UIDomainBridge or child panels.
    /// </summary>
    public class InventoryScreen : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Config")]
        [SerializeField] private UISlotLayoutConfig _uiConfig;

        [Header("Roots")]
        [SerializeField] private RectTransform _inventoryGridRoot;

        [Header("Prefabs — Inventory")]
        // No trash slot. No DropArea. World-drop = drag outside panel.

        [Header("Buttons")]
        [SerializeField] private Button _sortButton;

        [Header("Sub-panels")]
        [Tooltip("Manages weapon + equipment cards with their inline attachment slots.")]
        [SerializeField] private WeaponEquipmentPanel _weaponEquipmentPanel;
        [Tooltip("Floating context menu — lives outside slot prefabs.")]
        [SerializeField] private ItemContextMenu _itemContextMenu;
        [SerializeField] private ItemTooltip     _itemTooltip;
        [SerializeField] private DropQuantityDialog _dropQuantityDialog;
        [SerializeField] private PlayerStatUIPanel  _playerStatPanel;

        [Header("Spectator")]
        [Tooltip("Label displayed when viewing another player's inventory.")]
        [SerializeField] private GameObject _spectatorBanner;
        [SerializeField] private TMPro.TextMeshProUGUI _spectatorLabel;

        // ── Runtime ───────────────────────────────────────────────────────────

        private readonly Dictionary<UISlotId, ItemSlotView> _slotViews = new();
        private UIDomainBridge _domainBridge;
        private bool _layoutBuilt;

        // Interaction state
        private ItemSlotView _hoveredSlot;
        private ItemSlotView _selectedSlot;

        /// <summary>
        /// True when the inventory is displaying the LOCAL player's inventory
        /// and the player may interact with it.
        /// False in spectator mode — all slots are locked.
        /// </summary>
        private bool CanInteract => _domainBridge != null && _domainBridge.IsCurrentPlayerOwner;

        // ─────────────────────────────────────────────────────────────────────
        #region Unity Lifecycle

        private void Awake()
        {
            if (_uiConfig != null && _uiConfig.DefaultSlotSize != default)
                return; // config driven — slot size comes from config
        }

        private void OnDestroy()
        {
            HookBridgeEvents(false);
            HookSlotEvents(false);

            if (_sortButton != null)
                _sortButton.onClick.RemoveListener(OnSortClicked);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Public API

        /// <summary>
        /// Called once by UIRootController when the inventory screen is first opened.
        /// </summary>
        public void Initialize(UIDomainBridge domainBridge)
        {
            _domainBridge = domainBridge;

            InitSubPanels();
            _weaponEquipmentPanel?.Initialize(domainBridge, _uiConfig);

            if (!_layoutBuilt)
            {
                BuildLayout();
                _layoutBuilt = true;
            }
            else
            {
                RefreshAllSlots();
            }

            HookBridgeEvents(true);
            ApplyOwnerLock();

            if (_sortButton != null)
                _sortButton.onClick.AddListener(OnSortClicked);
        }

        /// <summary>
        /// Called by UIRootController when the spectated player changes.
        /// </summary>
        public void RefreshForNewPlayer(UIDomainBridge domainBridge)
        {
            HookBridgeEvents(false);
            _domainBridge = domainBridge;
            InitSubPanels();
            _weaponEquipmentPanel?.Initialize(domainBridge, _uiConfig);
            HookBridgeEvents(true);
            RefreshAllSlots();
            ApplyOwnerLock();
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Layout

        private void BuildLayout()
        {
            if (_slotViews.Count > 0)
            {
                Debug.LogWarning("[InventoryScreen] BuildLayout called but slots already exist. Skipping.");
                return;
            }

            // Inventory grid slots
            SpawnSlotGroup(_inventoryGridRoot, UISlotType.Inventory, _uiConfig?.InventoryTotalSlots ?? 0,
                i => UISlotId.Inventory(i));

            // Weapon + equipment cards (with inline attachment slots) are managed by WeaponEquipmentPanel.
            // No weapon/equipment/attachment slots are spawned here.

            ForceRebuildLayouts();
            HookSlotEvents(true);
        }

        private void SpawnSlotGroup(
            RectTransform root, UISlotType slotType, int count,
            System.Func<int, UISlotId> idFactory)
        {
            if (root == null || count <= 0) return;
            var prefab = _uiConfig?.GetSlotPrefab(slotType);
            if (prefab == null) return;

            for (int i = 0; i < count; i++)
            {
                var go = Instantiate(prefab, root, false);
                SetupSlotRect(go);
                var view = ResolveSlotView(go);
                if (view == null) continue;

                var id = idFactory(i);
                view.Initialize(_uiConfig, id);
                view.SetEmptyState();
                _slotViews[id] = view;
                DragDropController.Instance?.RegisterSlotView(view);
            }
        }

        private void SpawnSingleSlot(GameObject prefab, RectTransform root, UISlotId id)
        {
            if (root == null || prefab == null) return;
            var go = Instantiate(prefab, root, false);
            SetupSlotRect(go);
            var view = ResolveSlotView(go);
            if (view == null) return;
            view.Initialize(_uiConfig, id);
            view.SetEmptyState();
            _slotViews[id] = view;
            DragDropController.Instance?.RegisterSlotView(view);
        }

        private static ItemSlotView ResolveSlotView(GameObject go)
            => ComponentResolver.Find<ItemSlotView>(go)
                .OnSelf().InChildren()
                .OrLogWarning("[InventoryScreen] ItemSlotView not found on slot prefab.")
                .Resolve();

        private void SetupSlotRect(GameObject slotGO)
        {
            var rt = slotGO.GetComponent<RectTransform>();
            if (rt == null) return;
            if (!slotGO.activeSelf) slotGO.SetActive(true);

            rt.localScale    = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.localPosition = Vector3.zero;
            rt.anchorMin     = new Vector2(0.5f, 0.5f);
            rt.anchorMax     = new Vector2(0.5f, 0.5f);
            rt.pivot         = new Vector2(0.5f, 0.5f);

            var slotSize = _uiConfig != null ? _uiConfig.DefaultSlotSize : new Vector2(100f, 100f);
            rt.sizeDelta  = slotSize;

            var parentLayout = rt.parent?.GetComponent<UnityEngine.UI.LayoutGroup>();
            if (parentLayout != null)
            {
                var le = slotGO.GetComponent<UnityEngine.UI.LayoutElement>()
                      ?? slotGO.AddComponent<UnityEngine.UI.LayoutElement>();
                le.preferredWidth  = slotSize.x;
                le.preferredHeight = slotSize.y;
                le.minWidth        = slotSize.x * 0.5f;
                le.minHeight       = slotSize.y * 0.5f;
                le.flexibleWidth   = 0f;
                le.flexibleHeight  = 0f;
            }
        }

        private void ForceRebuildLayouts()
        {
            if (_inventoryGridRoot != null)
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_inventoryGridRoot);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Sub-panel Init

        private void InitSubPanels()
        {
            _playerStatPanel?.Initialize(_domainBridge);
            _itemTooltip?.Initialize(_domainBridge);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Owner / Spectator Lock

        /// <summary>
        /// Applies or removes the spectator read-only lock on every slot
        /// and shows/hides the spectator banner.
        /// </summary>
        private void ApplyOwnerLock()
        {
            bool locked = !CanInteract;

            foreach (var kvp in _slotViews)
                kvp.Value?.SetLockedVisual(locked);

            // Lock weapon + equipment cards (and their attachment sub-slots) too.
            _weaponEquipmentPanel?.SetLockedVisual(locked);

            if (_spectatorBanner != null)
                _spectatorBanner.SetActive(locked);

            if (locked && _spectatorLabel != null)
            {
                var player = _domainBridge?.CurrentPlayer;
                _spectatorLabel.text = player != null
                    ? $"Viewing {player.name}'s Inventory"
                    : "Viewing Inventory (Read-Only)";
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Bridge Events

        private void HookBridgeEvents(bool subscribe)
        {
            if (_domainBridge == null) return;

            if (subscribe)
            {
                _domainBridge.OnInventorySlotChanged += HandleSlotChanged;
                _domainBridge.OnEquipmentSlotChanged += HandleSlotChanged;
                _domainBridge.OnWeaponSlotChanged    += HandleSlotChanged;
            }
            else
            {
                _domainBridge.OnInventorySlotChanged -= HandleSlotChanged;
                _domainBridge.OnEquipmentSlotChanged -= HandleSlotChanged;
                _domainBridge.OnWeaponSlotChanged    -= HandleSlotChanged;
            }
        }

        private void HandleSlotChanged(UISlotId id, UISlotState state)
        {
            if (_slotViews.TryGetValue(id, out var view))
                view.SetState(state);

            // If the affected slot is currently selected and is now empty → dismiss.
            if (_selectedSlot != null &&
                _selectedSlot.SlotId.Equals(id) &&
                state?.Item == null)
            {
                ClearSelection();
            }
        }

        private void RefreshAllSlots()
        {
            foreach (var kvp in _slotViews)
                kvp.Value?.SetEmptyState();

            _domainBridge?.Refresh();
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Slot Hover / Press Events

        private void HookSlotEvents(bool subscribe)
        {
            foreach (var kvp in _slotViews)
            {
                var input = ComponentResolver.Find<ItemSlotInput>(kvp.Value)
                    .OnSelf().InChildren()
                    .OrLogWarning("[InventoryScreen] ItemSlotInput not found.")
                    .Resolve();
                if (input == null) continue;

                if (subscribe)
                {
                    input.OnSlotHoverEnter    += OnSlotHoverEnter;
                    input.OnSlotHoverExit     += OnSlotHoverExit;
                    input.OnSlotPressed       += OnSlotPressed;
                    input.OnSlotDoubleClicked += OnSlotDoubleClicked;
                }
                else
                {
                    input.OnSlotHoverEnter    -= OnSlotHoverEnter;
                    input.OnSlotHoverExit     -= OnSlotHoverExit;
                    input.OnSlotPressed       -= OnSlotPressed;
                    input.OnSlotDoubleClicked -= OnSlotDoubleClicked;
                }
            }
        }

        private void OnSlotHoverEnter(ItemSlotView slot)
        {
            _hoveredSlot = slot;

            var item = slot.State?.Item;
            if (item == null)
            {
                _itemTooltip?.Hide();
                return;
            }

            // Tooltip
            if (_itemTooltip != null)
            {
                var slotRect = slot.transform as RectTransform;
                string label = BuildSlotLabel(slot.SlotId);
                _itemTooltip.Show(item, Input.mousePosition, slotRect, label);
            }
        }

        private void OnSlotHoverExit(ItemSlotView slot)
        {
            if (_hoveredSlot != slot) return;
            _hoveredSlot = null;
            _itemTooltip?.Hide();
        }

        private void OnSlotPressed(ItemSlotView slot)
        {
            // In spectator mode: do nothing.
            if (!CanInteract) return;

            // Toggle off: pressing the same selected slot again collapses the menu.
            if (_selectedSlot == slot && _itemContextMenu != null && _itemContextMenu.IsVisible)
            {
                ClearSelection();
                return;
            }

            ClearSelection();

            if (slot?.State?.Item == null) return;

            _selectedSlot = slot;
            slot.SetSelectedVisual(true);
            _itemTooltip?.Hide(); // context menu takes over information display

            if (_itemContextMenu != null)
            {
                var slotRect = slot.transform as RectTransform;
                _itemContextMenu.Show(
                    slot.State.Item,
                    slot.SlotId,
                    slotRect,
                    _domainBridge,
                    _dropQuantityDialog,
                    _uiConfig);
            }
        }

        private void OnSlotDoubleClicked(ItemSlotView slot)
        {
            if (!CanInteract) return;

            var item = slot?.State?.Item;
            if (item == null || _domainBridge?.Bridge == null) return;

            var def = ItemDatabase.GetDefinition(item.DefinitionID);

            bool isEquipSlot = slot.SlotId.Type == UISlotType.Equipment
                            || slot.SlotId.Type == UISlotType.Weapon;

            if (isEquipSlot)
            {
                // Double-click on equipped slot → unequip.
                if (slot.SlotId.Type == UISlotType.Weapon && slot.SlotId.WeaponSlot.HasValue)
                    _domainBridge.Bridge.UnequipWeapon(slot.SlotId.WeaponSlot.Value);
                else if (slot.SlotId.EquipmentSlot.HasValue)
                    _domainBridge.Bridge.UnequipItem(slot.SlotId.EquipmentSlot.Value);
            }
            else if (def != null)
            {
                switch (def.Type)
                {
                    // Equippables → equip.
                    case ItemType.Weapon:
                        _domainBridge.Bridge.EquipWeapon(item.InstanceID);
                        break;
                    case ItemType.Equipment:
                        _domainBridge.Bridge.EquipItem(item.InstanceID);
                        break;

                    // Consumable: use immediately via ItemUse subsystem.
                    case ItemType.Consumable:
                        _domainBridge.Bridge.ItemUse.UseItem(item);
                        break;

                    // Throwable / Deployable → select to hand → exits inventory HUD.
                    case ItemType.Throwable:
                    case ItemType.Deployable:
                        _domainBridge.Bridge.SelectItem(item.InstanceID);
                        // Auto-close inventory so the aim HUD is visible.
                        gameObject.SetActive(false);
                        break;
                }
            }

            ClearSelection();
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Helpers

        private void ClearSelection()
        {
            _selectedSlot?.SetSelectedVisual(false);
            _selectedSlot = null;
            _itemContextMenu?.Hide();
        }

        private void OnSortClicked()
            => _domainBridge?.Bridge?.Inventory?.RequestSortByType();

        private static string BuildSlotLabel(UISlotId slotId)
        {
            return slotId.Type switch
            {
                UISlotType.Equipment => slotId.EquipmentSlot.HasValue
                    ? $"{slotId.EquipmentSlot.Value} Slot" : "Equipment Slot",
                UISlotType.Weapon => slotId.WeaponSlot.HasValue
                    ? $"{slotId.WeaponSlot.Value} Weapon Slot" : "Weapon Slot",
                UISlotType.Attachment => $"Attachment [{slotId.Index}]",
                _ => null
            };
        }

        #endregion
    }
}