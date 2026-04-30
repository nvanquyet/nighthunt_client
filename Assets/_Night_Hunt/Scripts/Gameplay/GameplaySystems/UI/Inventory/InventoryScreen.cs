using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Bridge;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Gameplay.Spectator;
using NightHunt.Utilities;
using NightHunt.UI;
using NightHunt.GameplaySystems.UI.Combat;
using System.Linq;

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

        [Header("Prefabs")]
        [SerializeField] private GameObject _inventorySlotPrefab;

        [Header("Roots")]
        [SerializeField] private RectTransform _inventoryGridRoot;

        [Header("Prefabs — Inventory")]
        // No trash slot. No DropArea. World-drop = drag outside panel.

        [Header("Buttons")]
        [SerializeField] private Button _sortButton;

        [Header("Sub-panels")]
        [Tooltip("Manages weapon + equipment cards with their inline attachment slots.")]
        [SerializeField] private WeaponEquipmentPanel _weaponEquipmentPanel;

        [SerializeField] private ItemTooltip     _itemTooltip;
        [SerializeField] private DropQuantityDialog _dropQuantityDialog;
        [SerializeField] private PlayerStatUIPanel  _playerStatPanel;

        [Header("Spectator")]
        [Tooltip("Label displayed when viewing another player's inventory.")]
        [SerializeField] private GameObject _spectatorBanner;
        [SerializeField] private TMPro.TextMeshProUGUI _spectatorLabel;

        // ── Runtime ───────────────────────────────────────────────────────────

        private readonly Dictionary<UISlotId, ItemSlotView> _slotViews = new();
        private UIPlayerContext _domainBridge;
        private bool _layoutBuilt;

        // Interaction state
        private ItemSlotView _hoveredSlot;
        private ItemSlotView _selectedSlot;
        private readonly HashSet<ItemSlotInput> _hookedSlotInputs = new();

        /// <summary>
        /// True when the inventory is displaying the LOCAL player's inventory
        /// and the player may interact with it.
        /// False in spectator mode — all slots are locked.
        /// </summary>
        private bool CanInteract => _domainBridge != null && _domainBridge.IsOwner;

        // ─────────────────────────────────────────────────────────────────────
        #region Unity Lifecycle

        private void Awake()
        {
        }

        private void OnDisable()
        {
            ClearTransientUI();
        }

        private void OnDestroy()
        {
            ClearTransientUI();
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
        public void Initialize(UIPlayerContext domainBridge)
        {
            _domainBridge = domainBridge;

            Debug.Log($"[INV] InventoryScreen.Initialize — domainBridge={(_domainBridge != null ? "ok" : "NULL")} " +
                      $"IsReady={_domainBridge?.IsReady} layoutBuilt={_layoutBuilt} " +
                      $"gridRoot={(_inventoryGridRoot != null ? _inventoryGridRoot.name : "NULL")} " +
                      $"slotPrefab={(_inventorySlotPrefab != null ? _inventorySlotPrefab.name : "NULL")}");

            InitSubPanels();
            _weaponEquipmentPanel?.Initialize(domainBridge);

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
            RefreshSlotInputHooks();

            if (_sortButton != null)
                _sortButton.onClick.AddListener(OnSortClicked);
        }

        /// <summary>
        /// Called by UIRootController when the spectated player changes.
        /// </summary>
        public void RefreshForNewPlayer(UIPlayerContext domainBridge)
        {
            ClearTransientUI();
            HookBridgeEvents(false);
            _domainBridge = domainBridge;
            _playerStatPanel?.RefreshForNewPlayer(_domainBridge);
            _itemTooltip?.Initialize(_domainBridge);
            _weaponEquipmentPanel?.Initialize(domainBridge);
            HookBridgeEvents(true);
            RefreshAllSlots();
            ApplyOwnerLock();
            RefreshSlotInputHooks();
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

            // Ensure we have enough slots for current items
            int maxIndex = _domainBridge?.Player?.GamePlaySystemBridge.Inventory?.GetMaxIndex() ?? -1;
            int initialSlots = Mathf.Max(20, maxIndex + 1); // Ensure at least 20 slots

            // Inventory grid slots
            SpawnSlotGroup(_inventoryGridRoot, UISlotType.Inventory, initialSlots, i => UISlotId.Inventory(i));

            // Weapon + equipment cards (with inline attachment slots) are managed by WeaponEquipmentPanel.
            // No weapon/equipment/attachment slots are spawned here.

            ForceRebuildLayouts();
            HookSlotEvents(true);
            RefreshSlotInputHooks();
        }

        private void SpawnSlotGroup(
            RectTransform root, UISlotType slotType, int count,
            System.Func<int, UISlotId> idFactory)
        {
            if (root == null || count <= 0) return;
            var prefab = _inventorySlotPrefab;
            if (prefab == null) return;

            for (int i = 0; i < count; i++)
            {
                var go = Instantiate(prefab, root, false);
                SetupSlotRect(go);
                var view = ResolveSlotView(go);
                if (view == null) continue;

                var id = idFactory(i);
                view.Initialize(id);
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
            view.Initialize(id);
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

            var slotSize = NightHunt.GameplaySystems.Core.Configs.InventoryConfig.Instance != null 
                ? NightHunt.GameplaySystems.Core.Configs.InventoryConfig.Instance.DefaultSlotSize 
                : new Vector2(100f, 100f);
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
                var player = _domainBridge?.Player;
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
            if (!_slotViews.TryGetValue(id, out var view))
            {
                if (id.Type == UISlotType.Inventory)
                {
                    EnsureSlotsUpTo(id.Index);
                    _slotViews.TryGetValue(id, out view);
                }
            }

            if (view != null)
                view.SetState(state);

            RefreshSlotInputHooks();

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

            _domainBridge?.PushInventorySnapshot();
        }

        private void EnsureSlotsUpTo(int index)
        {
            int currentCount = 0;
            foreach (var kvp in _slotViews)
            {
                if (kvp.Key.Type == UISlotType.Inventory) currentCount++;
            }

            if (index >= currentCount)
            {
                int needed = index - currentCount + 1;
                int startIdx = currentCount;
                SpawnSlotGroup(_inventoryGridRoot, UISlotType.Inventory, needed, i => UISlotId.Inventory(startIdx + i));
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Slot Hover / Press Events

        private void HookSlotEvents(bool subscribe)
        {
            foreach (var input in _hookedSlotInputs.ToArray())
            {
                input.OnSlotHoverEnter -= OnSlotHoverEnter;
                input.OnSlotHoverExit -= OnSlotHoverExit;
                input.OnSlotPressed -= OnSlotPressed;
                input.OnSlotDoubleClicked -= OnSlotDoubleClicked;
            }

            if (!subscribe)
            {
                _hookedSlotInputs.Clear();
                return;
            }

            RefreshSlotInputHooks();
        }

        private void RefreshSlotInputHooks()
        {
            foreach (var input in GetComponentsInChildren<ItemSlotInput>(true))
            {
                if (input == null || _hookedSlotInputs.Contains(input))
                    continue;

                input.OnSlotHoverEnter += OnSlotHoverEnter;
                input.OnSlotHoverExit += OnSlotHoverExit;
                input.OnSlotPressed += OnSlotPressed;
                input.OnSlotDoubleClicked += OnSlotDoubleClicked;
                _hookedSlotInputs.Add(input);
            }
        }

        private void OnSlotHoverEnter(ItemSlotView slot)
        {
            _hoveredSlot = slot;

            var item = slot.State?.Item;
            if (item == null)
            {
                ShowEmptySlotTooltip(slot);
                return;
            }

            if (_itemTooltip != null)
            {
                var slotRect = slot.transform as RectTransform;
                string label = BuildSlotLabel(slot.SlotId);
                _itemTooltip.Show(item, Input.mousePosition, slotRect, label);
            }
        }

        private void ShowEmptySlotTooltip(ItemSlotView slot)
        {
            if (_itemTooltip == null || slot == null || slot.SlotId.Type == UISlotType.Inventory)
            {
                _itemTooltip?.Hide();
                return;
            }

            var label = BuildSlotLabel(slot.SlotId);
            _itemTooltip.ShowSlot(label, Input.mousePosition, slot.transform as RectTransform, BuildSlotDescription(slot.SlotId));
        }

        private void OnSlotHoverExit(ItemSlotView slot)
        {
            if (_hoveredSlot != slot) return;
            _hoveredSlot = null;
            _itemTooltip?.Hide();
        }

        private void OnSlotPressed(ItemSlotView slot)
        {
            if (!CanInteract) return;

            // Toggle selection: pressing the same slot again deselects it.
            if (_selectedSlot == slot)
            {
                ClearSelection();
                return;
            }

            ClearSelection();
            if (slot?.State?.Item == null) return;

            _selectedSlot = slot;
            slot.SetSelectedVisual(true);
            _itemTooltip?.Hide(); // tooltip is redundant while a slot is selected
        }

        private void OnSlotDoubleClicked(ItemSlotView slot)
        {
            if (!CanInteract)
            {
                Debug.Log("[ITEM_SELECT_FLOW] Inventory double-click ignored: inventory is not owner-interactable.");
                return;
            }

            var item = slot?.State?.Item;
            if (item == null || _domainBridge?.Bridge == null)
            {
                Debug.Log($"[ITEM_SELECT_FLOW] Inventory double-click ignored: item={(item == null ? "null" : item.InstanceID)} bridge={(_domainBridge?.Bridge == null ? "null" : "ok")}.");
                return;
            }

            var def = ItemDatabase.GetDefinition(item.DefinitionID);
            Debug.Log($"[ITEM_SELECT_FLOW] Inventory double-click slot={slot.SlotId.Type} item={item.InstanceID} def={item.DefinitionID} type={(def != null ? def.Type.ToString() : "null")} qty={item.Quantity}");

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
                    case ItemType.Attachment:
                        TryAttachToFirstCompatibleHost(item);
                        break;

                    // Consumable: use immediately via ItemUse subsystem.
                    case ItemType.Consumable:
                        Debug.Log($"[ITEM_SELECT_FLOW] Consumable double-click -> UseItem + CloseInventory item={item.InstanceID}.");
                        _domainBridge.Bridge.ItemUse.UseItem(item);
                        ClearTransientUI();
                        var consumableHud = GetComponentInParent<GameHUDController>(true);
                        consumableHud?.LootContainerUI?.Hide();
                        consumableHud?.CloseInventory();
                        break;

                    // Throwable / Deployable → select/arm then exit inventory HUD.
                    // Actual throw/deploy confirmation must happen in combat/placement flow,
                    // never from the inventory double-click itself.
                    case ItemType.Throwable:
                    case ItemType.Deployable:
                        Debug.Log($"[ITEM_SELECT_FLOW] {def.Type} double-click -> RequestSelectItem + RequestUseSelectedItem + CloseInventory item={item.InstanceID}.");
                        _domainBridge.Bridge.ItemSelection?.RequestSelectItem(item.InstanceID);
                        _domainBridge.Bridge.ItemSelection?.RequestUseSelectedItem();
                        ClearTransientUI();
                        var itemHud = GetComponentInParent<GameHUDController>(true);
                        itemHud?.LootContainerUI?.Hide();
                        itemHud?.CloseInventory();
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
        }

        private void ClearTransientUI()
        {
            _hoveredSlot = null;
            ClearSelection();
            _itemTooltip?.Hide();
            DragDropController.Instance?.ResetAll();
        }

        private void OnSortClicked()
            => _domainBridge?.Bridge?.Inventory?.RequestSortByType();

        private bool TryAttachToFirstCompatibleHost(ItemInstance attachment)
        {
            var bridge = _domainBridge?.Bridge;
            if (attachment == null || bridge?.Attachment == null)
            {
                Debug.LogWarning("[ITEM_SELECT_FLOW] Attachment double-click failed: missing attachment item or AttachmentSystem.");
                return false;
            }

            if (TryAttachToFirstCompatibleHostInItems(attachment, bridge.GetAllWeapons()))
                return true;

            if (TryAttachToFirstCompatibleHostInItems(attachment, bridge.GetAllEquipped()))
                return true;

            Debug.LogWarning($"[ITEM_SELECT_FLOW] Attachment double-click found no compatible empty slot for item={attachment.InstanceID} def={attachment.DefinitionID}.");
            return false;
        }

        private bool TryAttachToFirstCompatibleHostInItems<TKey>(
            ItemInstance attachment,
            IReadOnlyDictionary<TKey, ItemInstance> hosts)
        {
            if (hosts == null)
                return false;

            foreach (var kvp in hosts)
            {
                var host = kvp.Value;
                if (host == null || host.AttachedItems == null)
                    continue;

                for (int slotIndex = 0; slotIndex < host.AttachedItems.Length; slotIndex++)
                {
                    if (!string.IsNullOrEmpty(host.GetAttachment(slotIndex)))
                        continue;

                    if (_domainBridge.Bridge.Attachment.CanAttach(attachment.InstanceID, host.InstanceID, slotIndex))
                    {
                        Debug.Log($"[ITEM_SELECT_FLOW] Attachment double-click -> AttachItem attachment={attachment.InstanceID} host={host.InstanceID} slot={slotIndex}.");
                        _domainBridge.Bridge.Attachment.AttachItem(attachment.InstanceID, host.InstanceID, slotIndex);
                        return true;
                    }
                }
            }

            return false;
        }

        private string BuildSlotLabel(UISlotId slotId)
        {
            return slotId.Type switch
            {
                UISlotType.Equipment => slotId.EquipmentSlot.HasValue
                    ? $"{slotId.EquipmentSlot.Value} Slot" : "Equipment Slot",
                UISlotType.Weapon => slotId.WeaponSlot.HasValue
                    ? $"{slotId.WeaponSlot.Value} Weapon Slot" : "Weapon Slot",
                UISlotType.Attachment => BuildAttachmentSlotLabel(slotId),
                _ => null
            };
        }

        private string BuildSlotDescription(UISlotId slotId)
        {
            return slotId.Type switch
            {
                UISlotType.Equipment => slotId.EquipmentSlot.HasValue
                    ? $"Accepts {slotId.EquipmentSlot.Value} equipment."
                    : "Accepts compatible equipment.",
                UISlotType.Weapon => slotId.WeaponSlot.HasValue
                    ? $"Accepts compatible {slotId.WeaponSlot.Value} weapon."
                    : "Accepts compatible weapon.",
                UISlotType.Attachment => $"Accepts a compatible {BuildAttachmentSlotLabel(slotId).ToLowerInvariant()} attachment.",
                _ => "Empty slot."
            };
        }

        private string BuildAttachmentSlotLabel(UISlotId slotId)
        {
            if (!string.IsNullOrEmpty(slotId.ParentInstanceID) && _domainBridge?.Bridge != null)
            {
                var parent = _domainBridge.Bridge.GetItemByInstanceID(slotId.ParentInstanceID);
                var def = parent != null ? ItemDatabase.GetDefinition(parent.DefinitionID) : null;
                var slots = def?.AttachmentSlots;
                if (slots != null && slotId.Index >= 0 && slotId.Index < slots.Length)
                    return slots[slotId.Index].ToString();
            }

            return slotId.Index >= 0 ? $"Attachment Slot {slotId.Index + 1}" : "Attachment Slot";
        }

        private Vector3 ResolveCursorGroundTarget()
        {
            var player = _domainBridge?.Player;
            Vector3 origin = player != null ? player.transform.position : Vector3.zero;

            Vector3 aimTarget = ItemAimController.AimWorldTarget;
            if (aimTarget != Vector3.zero)
            {
                Debug.Log($"[ITEM_SELECT_FLOW] Inventory throwable/deployable target uses ItemAimController cursor: {aimTarget}.");
                return aimTarget;
            }

            var cam = Camera.main;
            if (cam == null)
                return origin + Vector3.forward * 5f;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            Plane ground = new Plane(Vector3.up, origin);
            if (ground.Raycast(ray, out float distance))
                return ray.GetPoint(distance);

            Vector3 fallbackDir = player != null ? player.transform.forward : Vector3.forward;
            return origin + fallbackDir * 5f;
        }

        #endregion
    }
}
