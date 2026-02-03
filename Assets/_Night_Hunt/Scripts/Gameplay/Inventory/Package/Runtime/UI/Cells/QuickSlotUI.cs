using UnityEngine;
using UnityEngine.EventSystems;
using NightHunt.Inventory.Core;
using NightHunt.Inventory.Events;
using NightHunt.Inventory.UI;

namespace NightHunt.Inventory.UI
{
    /// <summary>
    /// QuickSlot UI with double-click support.
    /// </summary>
    public class QuickSlotUI : InventoryCellUI, IPointerClickHandler
    {
        private float lastClickTime;
        private const float doubleClickThreshold = 0.3f;
        
        public void InitializeQuickSlot(ItemInstance item, int index)
        {
            Initialize(item, SlotLocationType.QuickSlot, index);
        }
        
        public void OnPointerClick(PointerEventData eventData)
        {
            if (ItemInstance == null) return;
            
            float timeSinceLastClick = Time.time - lastClickTime;
            
            if (timeSinceLastClick < doubleClickThreshold)
            {
                // Double-click detected
                OnDoubleClick();
            }
            
            lastClickTime = Time.time;
        }
        
        private void OnDoubleClick()
        {
            // Only trigger if inventory closed
            if (InventoryUIController.Instance != null && InventoryUIController.Instance.IsInventoryOpen) return;
            
            QuickSlotEvents.FireQuickSlotDoubleClicked(ItemInstance, SlotIndex);
        }
    }
}
