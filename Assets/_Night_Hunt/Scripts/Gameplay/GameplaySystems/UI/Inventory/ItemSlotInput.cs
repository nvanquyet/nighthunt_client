using UnityEngine;
using UnityEngine.EventSystems;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Input layer for a slot – forwards drag/drop events to DragDropController
    /// and surfaces pointer events as typed events for InventoryScreen.
    /// Detects double-click within <see cref="DoubleClickThreshold"/> seconds.
    ///
    /// FIX: Double-click no longer fires OnSlotPressed on the second tap.
    ///   Previous behaviour: tap 1 → OnSlotPressed, tap 2 → OnSlotDoubleClicked
    ///   (InventoryScreen had already set _selectedSlot on tap 1, causing a
    ///   visual flicker as ClearSelection ran inside the double-click handler).
    ///
    ///   New behaviour:
    ///     • Tap 1: record time, queue a pending-press. Do NOT fire OnSlotPressed yet.
    ///     • If tap 2 arrives within threshold → cancel pending-press, fire OnSlotDoubleClicked.
    ///     • If threshold elapses without a second tap → fire OnSlotPressed (delayed by threshold).
    ///
    ///   The small delay on single-press is acceptable for inventory interactions and
    ///   eliminates all visual flicker on double-click.
    /// </summary>
    [RequireComponent(typeof(ItemSlotView))]
    public class ItemSlotInput : MonoBehaviour,
        IPointerDownHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IDropHandler,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        private ItemSlotView _view;
        private float        _lastClickTime  = -1f;
        private bool         _pendingPress   = false;
        private float        _pendingPressTime = -1f;
        private bool         _fallbackTooltipVisible;

        private float DoubleClickThreshold => NightHunt.GameplaySystems.Core.Configs.InventoryConfig.Instance != null
            ? NightHunt.GameplaySystems.Core.Configs.InventoryConfig.Instance.DoubleClickThreshold
            : 0.3f;

        public event System.Action<ItemSlotView> OnSlotHoverEnter;
        public event System.Action<ItemSlotView> OnSlotHoverExit;
        public event System.Action<ItemSlotView> OnSlotPressed;
        public event System.Action<ItemSlotView> OnSlotDoubleClicked;

        private void Awake()
        {
            _view = ComponentResolver.Find<ItemSlotView>(this)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] ItemSlotView not found")
                .Resolve();
        }

        private void Update()
        {
            // FIX: Flush a pending single-press once the double-click window has expired.
            if (_pendingPress && Time.unscaledTime - _pendingPressTime >= DoubleClickThreshold)
            {
                _pendingPress = false;
                OnSlotPressed?.Invoke(_view);
            }
        }

        private void OnDisable()
        {
            _pendingPress = false;
            HideFallbackTooltip();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            float now = Time.unscaledTime;

            if (_pendingPress && now - _pendingPressTime < DoubleClickThreshold)
            {
                // Second tap within threshold → double-click.
                // Cancel the pending single-press so OnSlotPressed never fires.
                _pendingPress   = false;
                _lastClickTime  = -1f;
                OnSlotDoubleClicked?.Invoke(_view);
            }
            else
            {
                // First tap (or outside threshold) → start waiting for possible second tap.
                _pendingPress    = true;
                _pendingPressTime = now;
                _lastClickTime   = now;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            bool handledByScreen = OnSlotHoverEnter != null;
            OnSlotHoverEnter?.Invoke(_view);
            if (!handledByScreen)
                ShowFallbackTooltip();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            bool handledByScreen = OnSlotHoverExit != null;
            OnSlotHoverExit?.Invoke(_view);
            if (!handledByScreen)
                HideFallbackTooltip();
        }

        // ── Drag events — cancel pending press immediately on drag start ──────

        public void OnBeginDrag(PointerEventData eventData)
        {
            // A drag cancels any pending single-press — the interaction is now a drag, not a click.
            _pendingPress = false;

            var controller = DragDropController.Instance;
            if (controller == null)
                return;

            controller.BeginDrag(_view, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            var controller = DragDropController.Instance;
            if (controller == null)
                return;

            controller.UpdateDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            var controller = DragDropController.Instance;
            if (controller == null)
                return;

            controller.EndDrag(eventData);
        }

        public void OnDrop(PointerEventData eventData)
        {
            var controller = DragDropController.Instance;
            if (controller == null)
                return;

            controller.NotifyDropTarget(_view);
        }

        private void ShowFallbackTooltip()
        {
            var item = _view?.State?.Item;
            if (item == null)
            {
                ShowFallbackEmptySlotTooltip();
                return;
            }

            var tooltip = FindFirstObjectByType<ItemTooltip>(FindObjectsInactive.Include);
            if (tooltip == null)
                return;

            tooltip.Show(item, Input.mousePosition, _view.transform as RectTransform, BuildSlotLabel(_view.SlotId));
            _fallbackTooltipVisible = true;
        }

        private void ShowFallbackEmptySlotTooltip()
        {
            if (_view == null || _view.SlotId.Type == UISlotType.Inventory)
            {
                HideFallbackTooltip();
                return;
            }

            var tooltip = FindFirstObjectByType<ItemTooltip>(FindObjectsInactive.Include);
            if (tooltip == null)
                return;

            tooltip.ShowSlot(
                BuildSlotLabel(_view.SlotId),
                Input.mousePosition,
                _view.transform as RectTransform,
                BuildSlotDescription(_view.SlotId));
            _fallbackTooltipVisible = true;
        }

        private void HideFallbackTooltip()
        {
            if (!_fallbackTooltipVisible)
                return;

            FindFirstObjectByType<ItemTooltip>(FindObjectsInactive.Include)?.Hide();
            _fallbackTooltipVisible = false;
        }

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

        private static string BuildSlotDescription(UISlotId slotId)
        {
            return slotId.Type switch
            {
                UISlotType.Equipment => slotId.EquipmentSlot.HasValue
                    ? $"Accepts {slotId.EquipmentSlot.Value} equipment."
                    : "Accepts compatible equipment.",
                UISlotType.Weapon => slotId.WeaponSlot.HasValue
                    ? $"Accepts compatible {slotId.WeaponSlot.Value} weapon."
                    : "Accepts compatible weapon.",
                UISlotType.Attachment => "Accepts a compatible attachment.",
                _ => "Empty slot."
            };
        }
    }
}
