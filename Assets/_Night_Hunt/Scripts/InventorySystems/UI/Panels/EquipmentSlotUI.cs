using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Enums;

namespace NightHunt.Inventory.UI.Panels
{
    /// <summary>
    /// Individual equipment slot UI (Helmet, Armor, Backpack).
    /// Supports drag & drop, tooltip, and right-click unequip.
    /// </summary>
    public class EquipmentSlotUI : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IDropHandler, IPointerEnterHandler, IPointerExitHandler,
        IPointerClickHandler
    {
        [Header("UI References")]
        [SerializeField] private Image slotIcon;
        [SerializeField] private Image itemIcon;
        [SerializeField] private TextMeshProUGUI slotNameText;
        [SerializeField] private Button unequipButton;
        
        [Header("Visual Settings")]
        [SerializeField] private Color emptyColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        [SerializeField] private Color occupiedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        
        private ItemInstance currentItem;
        private EquipmentSlotType slotType;
        private EquipmentPanelUI parentPanel;
        
        #region Initialization
        
        public void Initialize(EquipmentSlotType type, EquipmentPanelUI panel)
        {
            slotType = type;
            parentPanel = panel;
            
            if (slotNameText != null)
            {
                slotNameText.text = type.ToString();
            }
            
            if (unequipButton != null)
            {
                unequipButton.onClick.AddListener(OnUnequipClicked);
                unequipButton.gameObject.SetActive(false);
            }
            
            SetItem(null);
        }
        
        public void SetItem(ItemInstance item)
        {
            currentItem = item;
            
            if (item != null)
            {
                // Show item
                if (itemIcon != null)
                {
                    itemIcon.sprite = item.Definition.Icon;
                    itemIcon.enabled = true;
                }
                
                if (slotIcon != null)
                {
                    slotIcon.color = occupiedColor;
                }
                
                if (unequipButton != null)
                {
                    unequipButton.gameObject.SetActive(true);
                }
            }
            else
            {
                // Empty slot
                if (itemIcon != null)
                {
                    itemIcon.enabled = false;
                }
                
                if (slotIcon != null)
                {
                    slotIcon.color = emptyColor;
                }
                
                if (unequipButton != null)
                {
                    unequipButton.gameObject.SetActive(false);
                }
            }
        }
        
        #endregion
        
        #region Drag & Drop
        
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (currentItem == null) return;
            
            var context = new DragContext
            {
                SourceLocation = SlotLocationType.Equipment,
                SourceIndex = (int)slotType,
                ItemInstance = currentItem
            };
            
            DragDropEvents.InvokeBeginDrag(context);
        }
        
        public void OnDrag(PointerEventData eventData)
        {
            DragDropEvents.InvokeDragging(eventData.position);
        }
        
        public void OnEndDrag(PointerEventData eventData)
        {
            DragDropEvents.InvokeEndDrag();
        }
        
        public void OnDrop(PointerEventData eventData)
        {
            var draggedCell = eventData.pointerDrag?.GetComponent<UI.Cells.InventoryCellUI>();
            if (draggedCell == null || draggedCell.GetItemData() == null)
            {
                return;
            }
            
            // Validate and equip via parent panel
            parentPanel.OnItemDroppedOnSlot(draggedCell.GetItemData(), slotType);
            
            var dropContext = new DragContext
            {
                SourceLocation = draggedCell.GetLocationType(),
                SourceIndex = draggedCell.GetSlotIndex(),
                TargetLocation = SlotLocationType.Equipment,
                TargetIndex = (int)slotType,
                ItemInstance = draggedCell.GetItemData()
            };
            
            DragDropEvents.InvokeDrop(dropContext);
        }
        
        #endregion
        
        #region Tooltip
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (currentItem != null)
            {
                TooltipEvents.InvokeShowTooltip(currentItem, transform.position);
            }
            else
            {
                TooltipEvents.InvokeShowSlotInfo(SlotLocationType.Equipment, (int)slotType, transform.position);
            }
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            TooltipEvents.InvokeHideTooltip();
        }
        
        #endregion
        
        #region Right-Click Unequip
        
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right && currentItem != null)
            {
                OnUnequipClicked();
            }
        }
        
        private void OnUnequipClicked()
        {
            parentPanel.OnUnequipRequested(slotType);
        }
        
        #endregion
    }
}