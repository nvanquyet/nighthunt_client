using UnityEngine;
using UnityEngine.EventSystems;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Trash slot - drop items vào đây để xóa khỏi inventory.
    /// Chỉ nhận drop, không hiển thị item.
    /// </summary>
    [RequireComponent(typeof(ItemSlotView))]
    public class TrashSlotView : MonoBehaviour, IDropHandler
    {
        private ItemSlotView _slotView;
        
        private void Awake()
        {
            _slotView = GetComponent<ItemSlotView>();
        }
        
        public void OnDrop(PointerEventData eventData)
        {
            var controller = DragDropController.Instance;
            if (controller == null)
                return;
            
            // Forward drop event tới DragDropController
            controller.NotifyDropTarget(_slotView);
        }
    }
}
