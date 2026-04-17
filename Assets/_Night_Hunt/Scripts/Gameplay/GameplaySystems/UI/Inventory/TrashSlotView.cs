using UnityEngine;
using UnityEngine.EventSystems;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.UI.Inventory
{
    /// <summary>
    /// Trash slot - drop items vào đây để xóa khỏi inventory.
    /// Chỉ nhận drop, không display item.
    /// </summary>
    [RequireComponent(typeof(ItemSlotView))]
    public class TrashSlotView : MonoBehaviour, IDropHandler
    {
        private ItemSlotView _slotView;
        
        private void Awake()
        {
            _slotView = ComponentResolver.Find<ItemSlotView>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] ItemSlotView not found")
        .Resolve();
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
