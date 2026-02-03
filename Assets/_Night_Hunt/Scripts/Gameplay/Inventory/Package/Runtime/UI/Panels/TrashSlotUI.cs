using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using NightHunt.Inventory.Events;

namespace NightHunt.Inventory.UI
{
    /// <summary>
    /// Trash slot UI - no confirmation, direct trash on drop.
    /// </summary>
    public class TrashSlotUI : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image trashIcon;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color hoverColor = Color.red;
        
        public void OnDrop(PointerEventData eventData)
        {
            var draggedCell = eventData.pointerDrag?.GetComponent<InventoryCellUI>();
            if (draggedCell == null) return;
            
            // NO confirmation - direct trash
            InventoryEvents.FireRequestTrashItem(draggedCell.ItemInstance);
        }
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            trashIcon.color = hoverColor;
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            trashIcon.color = normalColor;
        }
    }
}
