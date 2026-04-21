using UnityEngine;
using UnityEngine.UI;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Bridge;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Floating context menu for inventory, equipment, weapon, and attachment slots.
    /// Shows contextual action buttons depending on the item type and the source slot.
    ///
    /// BUTTONS:
    ///   Inventory — Weapon/Equipment : [Equip]   [Drop] [Cancel]
    ///   Inventory — Consumable        : [Use]     [Drop] [Cancel]
    ///   Inventory — Throwable         : [Use]     [Drop] [Cancel]
    ///   Inventory — Deployable        : [Deploy]  [Drop] [Cancel]
    ///   Equipment slot                : [Unequip] [Drop] [Cancel]
    ///   Weapon slot                   : [Unequip] [Drop] [Cancel]
    ///   Attachment slot               : [Detach]  [Drop] [Cancel]
    ///
    /// POSITION: right (or left) of the selected slot, auto-flipped to stay on screen.
    ///           Vertically clamped so the menu never clips top/bottom edge.
    ///
    /// OWNER GUARD: buttons hidden (menu shows nothing) when the inventory is read-only
    ///              (spectator mode). In read-only mode Show() returns immediately.
    /// </summary>
    public class ItemContextMenu : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Panel")]
        [SerializeField] private RectTransform _rootRect;
        [SerializeField] private Canvas        _canvas;

        [Header("Buttons")]
        [SerializeField] private Button _useButton;
        [SerializeField] private Button _deployButton;
        [SerializeField] private Button _equipButton;
        [SerializeField] private Button _unequipButton;
        [SerializeField] private Button _detachButton;
        [SerializeField] private Button _dropButton;
        [SerializeField] private Button _cancelButton;

        // ── Runtime ───────────────────────────────────────────────────────────

        private ItemInstance    _currentItem;
        private UISlotId        _currentSlotId;
        private UIDomainBridge  _bridge;
        private DropQuantityDialog _dropDialog;
        private UISlotLayoutConfig _uiConfig;

        public bool IsVisible => _rootRect != null && _rootRect.gameObject.activeSelf;

        // ─────────────────────────────────────────────────────────────────────
        #region Unity Lifecycle

        private void Awake()
        {
            Hide();

            Register(_useButton,     OnUseClicked);
            Register(_deployButton,  OnDeployClicked);
            Register(_equipButton,   OnEquipClicked);
            Register(_unequipButton, OnUnequipClicked);
            Register(_detachButton,  OnDetachClicked);
            Register(_dropButton,    OnDropClicked);
            Register(_cancelButton,  OnCancelClicked);
        }

        private void OnDestroy()
        {
            Unregister(_useButton,     OnUseClicked);
            Unregister(_deployButton,  OnDeployClicked);
            Unregister(_equipButton,   OnEquipClicked);
            Unregister(_unequipButton, OnUnequipClicked);
            Unregister(_detachButton,  OnDetachClicked);
            Unregister(_dropButton,    OnDropClicked);
            Unregister(_cancelButton,  OnCancelClicked);
        }

        private static void Register(Button b, UnityEngine.Events.UnityAction a)
        { if (b != null) b.onClick.AddListener(a); }

        private static void Unregister(Button b, UnityEngine.Events.UnityAction a)
        { if (b != null) b.onClick.RemoveListener(a); }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Public API

        /// <summary>
        /// Show the context menu for the given item and slot.
        /// Returns immediately without showing if the bridge owner guard fails (spectator mode).
        /// </summary>
        public void Show(
            ItemInstance       item,
            UISlotId           slotId,
            RectTransform      slotRect,
            UIDomainBridge     bridge,
            DropQuantityDialog dropDialog,
            UISlotLayoutConfig uiConfig = null)
        {
            if (item == null) { Hide(); return; }

            // Spectator mode: no interactions allowed.
            if (bridge != null && !bridge.IsCurrentPlayerOwner) return;

            _currentItem   = item;
            _currentSlotId = slotId;
            _bridge        = bridge;
            _dropDialog    = dropDialog;
            _uiConfig      = uiConfig;

            RefreshButtons();
            PositionAt(slotRect);

            if (_rootRect != null)
                _rootRect.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (_rootRect != null)
                _rootRect.gameObject.SetActive(false);
            _currentItem = null;
            _bridge      = null;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Button Visibility

        private void RefreshButtons()
        {
            var def = ItemDatabase.GetDefinition(_currentItem.DefinitionID);

            bool isEquipSlot  = _currentSlotId.Type == UISlotType.Equipment
                             || _currentSlotId.Type == UISlotType.Weapon;
            bool isAttachSlot = _currentSlotId.Type == UISlotType.Attachment;

            bool isWeapon      = def != null && def.Type == ItemType.Weapon;
            bool isEquipment   = def != null && def.Type == ItemType.Equipment;
            bool isConsumable  = def != null && def.Type == ItemType.Consumable;
            bool isThrowable   = def != null && def.Type == ItemType.Throwable;
            bool isDeployable  = def != null && def.Type == ItemType.Deployable;

            // [Use]     — consumable, throwable (from inventory only)
            SetVisible(_useButton,    (isConsumable || isThrowable) && !isEquipSlot && !isAttachSlot);

            // [Deploy]  — deployable (from inventory only)
            SetVisible(_deployButton, isDeployable && !isEquipSlot && !isAttachSlot);

            // [Equip]   — weapon/equipment from inventory
            SetVisible(_equipButton,  (isWeapon || isEquipment) && !isEquipSlot && !isAttachSlot);

            // [Unequip] — weapon/equipment slots
            SetVisible(_unequipButton, isEquipSlot);

            // [Detach]  — attachment slots only
            SetVisible(_detachButton,  isAttachSlot);

            // [Drop]    — always visible; all slot types support drop-to-world
            SetVisible(_dropButton, true);

            // [Cancel]  — always
            SetVisible(_cancelButton, true);
        }

        private static void SetVisible(Button b, bool visible)
        {
            if (b != null) b.gameObject.SetActive(visible);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Positioning — auto-flip + screen-edge clamp

        private void PositionAt(RectTransform slotRect)
        {
            if (slotRect == null || _rootRect == null) return;

            Canvas canvas = ResolveCanvas();
            if (canvas == null) return;

            Camera cam = null;
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera ||
                canvas.renderMode == RenderMode.WorldSpace)
                cam = canvas.worldCamera ?? Camera.main;

            var corners = new Vector3[4];
            slotRect.GetWorldCorners(corners);
            // corners[0]=BL, [1]=TL, [2]=TR, [3]=BR

            ContextMenuSide side = _uiConfig?.ContextMenuPreferredSide ?? ContextMenuSide.Right;
            float gap = _uiConfig?.ContextMenuGap ?? 8f;

            // Preferred anchor: right = TR corner (corners[2]), left = TL (corners[1])
            Vector3 preferredWorld = side == ContextMenuSide.Right ? corners[2] : corners[1];

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                RectTransformUtility.WorldToScreenPoint(cam, preferredWorld),
                cam, out Vector2 local);

            // Determine menu width to check screen-edge overflow
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rootRect);
            float menuW = _rootRect.rect.width;
            float menuH = _rootRect.rect.height;
            float canvasW = (canvas.transform as RectTransform)?.rect.width ?? Screen.width;
            float canvasH = (canvas.transform as RectTransform)?.rect.height ?? Screen.height;
            float halfW = canvasW * 0.5f;
            float halfH = canvasH * 0.5f;

            // Flip horizontal if preferred side overflows
            float xPos;
            if (side == ContextMenuSide.Right)
            {
                xPos = local.x + gap;
                if (xPos + menuW > halfW) // clips right edge
                    xPos = local.x - menuW - gap; // flip to left
            }
            else
            {
                xPos = local.x - menuW - gap;
                if (xPos < -halfW) // clips left edge
                    xPos = local.x + gap; // flip to right
            }

            // Vertical: align top of menu to top of slot, clamp so menu stays on screen
            float yPos = local.y; // top-left start
            if (yPos - menuH < -halfH)   // clips bottom
                yPos = -halfH + menuH;
            if (yPos > halfH)            // clips top
                yPos = halfH;

            _rootRect.pivot         = new Vector2(0f, 1f);
            _rootRect.anchorMin     = new Vector2(0.5f, 0.5f);
            _rootRect.anchorMax     = new Vector2(0.5f, 0.5f);
            _rootRect.anchoredPosition = new Vector2(xPos, yPos);
        }

        private Canvas ResolveCanvas()
        {
            if (_canvas != null) return _canvas;
            return ComponentResolver.Find<Canvas>(_rootRect)
                .InParent().InRootChildren()
                .OrLogWarning("[ItemContextMenu] Canvas not found.")
                .Resolve();
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Button Handlers

        private void OnUseClicked()
        {
            if (_currentItem == null || _bridge?.Bridge == null) return;

            var def = ItemDatabase.GetDefinition(_currentItem.DefinitionID);
            if (def != null && (def.Type == ItemType.Throwable || def.Type == ItemType.Deployable))
            {
                // Arm for aiming via selection system — transitions HUD to aim mode.
                _bridge.Bridge.SelectItem(_currentItem.InstanceID);
            }
            else
            {
                // Consumable: use immediately via ItemUse system.
                _bridge.Bridge.ItemUse.UseItem(_currentItem);
            }
            Hide();
        }

        private void OnDeployClicked()
        {
            if (_currentItem == null || _bridge?.Bridge == null) return;
            _bridge.Bridge.SelectItem(_currentItem.InstanceID);
            Hide();
        }

        private void OnEquipClicked()
        {
            if (_currentItem == null || _bridge?.Bridge == null) return;
            var def = ItemDatabase.GetDefinition(_currentItem.DefinitionID);
            if (def != null && def.Type == ItemType.Weapon)
                _bridge.Bridge.EquipWeapon(_currentItem.InstanceID);
            else
                _bridge.Bridge.EquipItem(_currentItem.InstanceID);
            Hide();
        }

        private void OnUnequipClicked()
        {
            if (_currentItem == null || _bridge?.Bridge == null) return;
            if (_currentSlotId.Type == UISlotType.Weapon && _currentSlotId.WeaponSlot.HasValue)
                _bridge.Bridge.UnequipWeapon(_currentSlotId.WeaponSlot.Value);
            else if (_currentSlotId.EquipmentSlot.HasValue)
                _bridge.Bridge.UnequipItem(_currentSlotId.EquipmentSlot.Value);
            Hide();
        }

        private void OnDetachClicked()
        {
            if (_currentItem == null || _bridge?.Bridge == null) return;

            // Attachment slots carry parentInstanceID and slot index in their UISlotId.
            if (_currentSlotId.Type == UISlotType.Attachment &&
                !string.IsNullOrEmpty(_currentSlotId.ParentInstanceID) &&
                _currentSlotId.Index >= 0)
            {
                _bridge.Bridge.Attachment.DetachItem(_currentSlotId.ParentInstanceID, _currentSlotId.Index);
            }
            else
            {
                Debug.LogWarning("[ItemContextMenu] Detach called but slot has no valid parentInstanceID.");
            }
            Hide();
        }

        private void OnDropClicked()
        {
            if (_currentItem == null || _bridge?.Bridge == null) return;

            var item   = _currentItem;
            var bridge = _bridge;
            var slotId = _currentSlotId;

            // Do NOT hide before dialog if qty > 1 — we must re-show menu on cancel.
            if (_dropDialog != null && item.Quantity > 1)
            {
                _dropDialog.Show(
                    item,
                    qty =>
                    {
                        // Confirmed: route to the correct drop API based on slot type.
                        CommitDrop(bridge, slotId, item, qty);
                        Hide();
                    },
                    () =>
                    {
                        // Cancelled: dialog closes but context menu remains visible.
                        // Nothing to do — menu already shown.
                    });
            }
            else
            {
                CommitDrop(bridge, slotId, item, item.Quantity);
                Hide();
            }
        }

        private static void CommitDrop(UIDomainBridge bridge, UISlotId slotId, ItemInstance item, int qty)
        {
            switch (slotId.Type)
            {
                case UISlotType.Weapon when slotId.WeaponSlot.HasValue:
                    // Unequip to inventory first, then drop from inventory.
                    bridge.Bridge.UnequipWeapon(slotId.WeaponSlot.Value);
                    bridge.Bridge.DropItem(item.InstanceID, qty);
                    break;

                case UISlotType.Equipment when slotId.EquipmentSlot.HasValue:
                    // Unequip to inventory (detaches attachments if any), then drop.
                    bridge.Bridge.UnequipItem(slotId.EquipmentSlot.Value);
                    bridge.Bridge.DropItem(item.InstanceID, qty);
                    break;

                case UISlotType.Attachment:
                    // Detach to inventory first, then drop.
                    if (!string.IsNullOrEmpty(slotId.ParentInstanceID) && slotId.Index >= 0)
                        bridge.Bridge.Attachment.DetachItem(slotId.ParentInstanceID, slotId.Index);
                    bridge.Bridge.DropItem(item.InstanceID, qty);
                    break;

                default:
                    // Inventory slot: drop directly.
                    bridge.Bridge.DropItem(item.InstanceID, qty);
                    break;
            }
        }

        private void OnCancelClicked() => Hide();

        #endregion
    }
}
