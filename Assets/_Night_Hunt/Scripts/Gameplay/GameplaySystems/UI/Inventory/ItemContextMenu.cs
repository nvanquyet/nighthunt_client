using UnityEngine;
using UnityEngine.UI;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Bridge;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Floating context menu for inventory slots.
    /// Positioned at the selected slot – lives outside slot prefabs, like a tooltip.
    /// Shows contextual actions: Use / Equip / Unequip / Drop depending on slot type and item type.
    /// </summary>
    public class ItemContextMenu : MonoBehaviour
    {
        [Header("Panel Root")]
        [SerializeField] private RectTransform _rootRect;
        [SerializeField] private Canvas _canvas;

        [Header("Action Buttons")]
        [SerializeField] private Button _useButton;
        [SerializeField] private Button _equipButton;
        [SerializeField] private Button _unequipButton;
        [SerializeField] private Button _dropButton;

        [Header("Position Offset from slot top-right corner")]
        [SerializeField] private Vector2 _offset = new Vector2(8f, 0f);

        private ItemInstance _currentItem;
        private UISlotId _currentSlotId;
        private UIDomainBridge _domainBridge;
        private DropQuantityDialog _dropDialog;

        public bool IsVisible => _rootRect != null && _rootRect.gameObject.activeSelf;

        private void Awake()
        {
            Hide();
            if (_useButton != null)     _useButton.onClick.AddListener(OnUseClicked);
            if (_equipButton != null)   _equipButton.onClick.AddListener(OnEquipClicked);
            if (_unequipButton != null) _unequipButton.onClick.AddListener(OnUnequipClicked);
            if (_dropButton != null)    _dropButton.onClick.AddListener(OnDropClicked);
        }

        private void OnDestroy()
        {
            if (_useButton != null)     _useButton.onClick.RemoveListener(OnUseClicked);
            if (_equipButton != null)   _equipButton.onClick.RemoveListener(OnEquipClicked);
            if (_unequipButton != null) _unequipButton.onClick.RemoveListener(OnUnequipClicked);
            if (_dropButton != null)    _dropButton.onClick.RemoveListener(OnDropClicked);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        public void Show(
            ItemInstance item,
            UISlotId slotId,
            RectTransform slotRect,
            UIDomainBridge bridge,
            DropQuantityDialog dropDialog)
        {
            if (item == null) { Hide(); return; }

            _currentItem   = item;
            _currentSlotId = slotId;
            _domainBridge  = bridge;
            _dropDialog    = dropDialog;

            RefreshButtons();
            PositionAt(slotRect);

            if (_rootRect != null) _rootRect.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (_rootRect != null) _rootRect.gameObject.SetActive(false);
            _currentItem  = null;
            _domainBridge = null;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Button Visibility
        // ─────────────────────────────────────────────────────────────────────

        private void RefreshButtons()
        {
            var def = ItemDatabase.GetDefinition(_currentItem.DefinitionID);
            bool isEquipSlot  = _currentSlotId.Type == UISlotType.Equipment
                              || _currentSlotId.Type == UISlotType.Weapon;
            bool isWeapon     = def != null && def.Type == ItemType.Weapon;
            bool isEquippable = def != null && (isWeapon || def.Type == ItemType.Equipment);
            bool isUsable     = def != null && (def.Type == ItemType.Consumable
                                             || def.Type == ItemType.Throwable
                                             || def.Type == ItemType.Deployable);

            if (_useButton != null)     _useButton.gameObject.SetActive(isUsable && !isEquipSlot);
            if (_equipButton != null)   _equipButton.gameObject.SetActive(isEquippable && !isEquipSlot);
            if (_unequipButton != null) _unequipButton.gameObject.SetActive(isEquipSlot);
            if (_dropButton != null)    _dropButton.gameObject.SetActive(true);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Positioning (anchors to slot top-right, like tooltip)
        // ─────────────────────────────────────────────────────────────────────

        private void PositionAt(RectTransform slotRect)
        {
            if (slotRect == null || _rootRect == null) return;

            Canvas targetCanvas = _canvas;
            if (targetCanvas == null)
                targetCanvas = ComponentResolver.Find<Canvas>(_rootRect)
                    .InParent()
                    .InRootChildren()
                    .OrLogWarning("[ItemContextMenu] Canvas not found")
                    .Resolve();
            if (targetCanvas == null) return;

            Camera cam = null;
            if (targetCanvas.renderMode == RenderMode.ScreenSpaceCamera ||
                targetCanvas.renderMode == RenderMode.WorldSpace)
                cam = targetCanvas.worldCamera ?? Camera.main;

            var worldCorners = new Vector3[4];
            slotRect.GetWorldCorners(worldCorners);
            Vector2 screenPt = RectTransformUtility.WorldToScreenPoint(cam, worldCorners[2]); // top-right

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                targetCanvas.transform as RectTransform, screenPt, cam, out Vector2 local);

            _rootRect.anchoredPosition = local + _offset;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Button Handlers
        // ─────────────────────────────────────────────────────────────────────

        private void OnUseClicked()
        {
            if (_currentItem == null || _domainBridge?.Bridge == null) return;
            _domainBridge.Bridge.SelectItem(_currentItem.InstanceID);
            Hide();
        }

        private void OnEquipClicked()
        {
            if (_currentItem == null || _domainBridge?.Bridge == null) return;
            var def = ItemDatabase.GetDefinition(_currentItem.DefinitionID);
            if (def != null && def.Type == ItemType.Weapon)
                _domainBridge.Bridge.EquipWeapon(_currentItem.InstanceID);
            else
                _domainBridge.Bridge.EquipItem(_currentItem.InstanceID);
            Hide();
        }

        private void OnUnequipClicked()
        {
            if (_currentItem == null || _domainBridge?.Bridge == null) return;
            if (_currentSlotId.Type == UISlotType.Weapon && _currentSlotId.WeaponSlot.HasValue)
                _domainBridge.Bridge.UnequipWeapon(_currentSlotId.WeaponSlot.Value);
            else if (_currentSlotId.Type == UISlotType.Equipment && _currentSlotId.EquipmentSlot.HasValue)
                _domainBridge.Bridge.UnequipItem(_currentSlotId.EquipmentSlot.Value);
            Hide();
        }

        private void OnDropClicked()
        {
            if (_currentItem == null || _domainBridge?.Bridge == null) return;

            var item   = _currentItem;
            var bridge = _domainBridge;
            Hide(); // hide before dialog in case it blocks input

            if (_dropDialog != null && item.Quantity > 1)
            {
                void HandleConfirmed(ItemInstance confirmed, int qty)
                {
                    _dropDialog.OnDropConfirmed -= HandleConfirmed;
                    _dropDialog.OnCanceled      -= HandleCanceled;
                    if (confirmed?.InstanceID == item.InstanceID)
                        bridge.Bridge.DropItem(item.InstanceID, qty);
                }
                void HandleCanceled()
                {
                    _dropDialog.OnDropConfirmed -= HandleConfirmed;
                    _dropDialog.OnCanceled      -= HandleCanceled;
                }
                _dropDialog.OnDropConfirmed += HandleConfirmed;
                _dropDialog.OnCanceled      += HandleCanceled;
                _dropDialog.Show(item);
            }
            else
            {
                bridge.Bridge.DropItem(item.InstanceID, 1);
            }
        }
    }
}
