using UnityEngine;
using UnityEngine.EventSystems;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Input layer cho slot – forward các sự kiện drag/drop lên DragDropController.
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
        
        // Event để InventoryScreen subscribe
        public event System.Action<ItemSlotView> OnSlotHoverEnter;
        public event System.Action<ItemSlotView> OnSlotHoverExit;

        private void Awake()
        {
            _view = GetComponent<ItemSlotView>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // Reserved cho select / context menu nếu cần sau này.
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

