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
    /// Individual attachment slot UI (Scope, Grip, Muzzle, etc.).
    /// Supports drag & drop, tooltip, right-click detach.
    /// </summary>
    public class AttachmentSlotUI : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IDropHandler, IPointerEnterHandler, IPointerExitHandler,
        IPointerClickHandler
    {
        [Header("UI References")]
        [SerializeField] private Image slotBackground;
        [SerializeField] private Image attachmentIcon;
        [SerializeField] private TextMeshProUGUI slotNameText;
        [SerializeField] private Button detachButton;
        
        [Header("Visual Settings")]
        [SerializeField] private Color emptyColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        [SerializeField] private Color occupiedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        
        private ItemInstance currentAttachment;
        private AttachmentSlotType slotType;
        private AttachmentPanelUI parentPanel;
        
        #region Initialization
        
        public void Initialize(AttachmentSlotType type, AttachmentPanelUI panel)
        {
            slotType = type;
            parentPanel = panel;
            
            if (slotNameText != null)
            {
                slotNameText.text = type.ToString();
            }
            
            if (detachButton != null)
            {
                detachButton.onClick.AddListener(OnDetachClicked);
                detachButton.gameObject.SetActive(false);
            }
            
            SetAttachment(null);
        }
        
        public void SetAttachment(ItemInstance attachment)
        {
            currentAttachment = attachment;
            
            if (attachment != null)
            {
                // Show attachment
                if (attachmentIcon != null)
                {
                    attachmentIcon.sprite = attachment.Definition.Icon;
                    attachmentIcon.enabled = true;
                }
                
                if (slotBackground != null)
                {
                    slotBackground.color = occupiedColor;
                }
                
                if (detachButton != null)
                {
                    detachButton.gameObject.SetActive(true);
                }
            }
            else
            {
                // Empty slot
                if (attachmentIcon != null)
                {
                    attachmentIcon.enabled = false;
                }
                
                if (slotBackground != null)
                {
                    slotBackground.color = emptyColor;
                }
                
                if (detachButton != null)
                {
                    detachButton.gameObject.SetActive(false);
                }
            }
        }
        
        #endregion
        
        #region Drag & Drop
        
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (currentAttachment == null) return;
            
            var context = new DragContext
            {
                SourceLocation = SlotLocationType.Attachment,
                SourceIndex = (int)slotType,
                ItemInstance = currentAttachment
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
            
            parentPanel.OnAttachmentDropped(draggedCell.GetItemData(), slotType);
            
            var dropContext = new DragContext
            {
                SourceLocation = draggedCell.GetLocationType(),
                SourceIndex = draggedCell.GetSlotIndex(),
                TargetLocation = SlotLocationType.Attachment,
                TargetIndex = (int)slotType,
                ItemInstance = draggedCell.GetItemData()
            };
            
            DragDropEvents.InvokeDrop(dropContext);
        }
        
        #endregion
        
        #region Tooltip
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (currentAttachment != null)
            {
                TooltipEvents.InvokeShowTooltip(currentAttachment, transform.position);
            }
            else
            {
                TooltipEvents.InvokeShowSlotInfo(SlotLocationType.Attachment, (int)slotType, transform.position);
            }
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            TooltipEvents.InvokeHideTooltip();
        }
        
        #endregion
        
        #region Click Handlers
        
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right && currentAttachment != null)
            {
                OnDetachClicked();
            }
        }
        
        private void OnDetachClicked()
        {
            parentPanel.OnDetachRequested(slotType);
        }
        
        #endregion
    }
}