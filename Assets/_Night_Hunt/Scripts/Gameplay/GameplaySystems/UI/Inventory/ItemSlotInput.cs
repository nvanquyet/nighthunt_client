using UnityEngine;
using UnityEngine.EventSystems;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Input layer for a slot – forwards drag/drop events to DragDropController
    /// and surfaces pointer events as typed events for InventoryScreen.
    /// Detects double-click within <see cref="DoubleClickThreshold"/> seconds.
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
        private float _lastClickTime = -1f;
        private float DoubleClickThreshold => UISlotLayoutConfig.Instance != null
            ? UISlotLayoutConfig.Instance.DoubleClickThreshold
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

        public void OnPointerDown(PointerEventData eventData)
        {
            float now = Time.unscaledTime;
            if (now - _lastClickTime < DoubleClickThreshold)
            {
                _lastClickTime = -1f;
                OnSlotDoubleClicked?.Invoke(_view);
            }
            else
            {
                _lastClickTime = now;
                OnSlotPressed?.Invoke(_view);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            OnSlotHoverEnter?.Invoke(_view);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            OnSlotHoverExit?.Invoke(_view);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
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
    }
}

