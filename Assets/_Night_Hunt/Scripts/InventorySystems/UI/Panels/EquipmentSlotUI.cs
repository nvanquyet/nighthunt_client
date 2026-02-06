using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core;
using NightHunt.Inventory.UI.Core;

namespace NightHunt.Inventory.UI.Panels
{
    /// <summary>
    /// Individual equipment slot UI (Helmet, Armor, Backpack).
    /// Supports drag & drop, tooltip, and right-click unequip.
    /// Implements state management for visual feedback.
    /// </summary>
    public class EquipmentSlotUI : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IDropHandler, IPointerEnterHandler, IPointerExitHandler,
        IPointerClickHandler, IUISlotStateManager
    {
        [Header("UI References")]
        [SerializeField] private Image slotIcon;
        [SerializeField] private Image itemIcon;
        [SerializeField] private TextMeshProUGUI slotNameText;
        [SerializeField] private Button unequipButton;
        
        [Header("Visual Settings")]
        [SerializeField] private Color emptyColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        [SerializeField] private Color occupiedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color hoverColor = new Color(0.4f, 0.4f, 0.4f, 1f);
        
        private ItemInstance currentItem;
        private EquipmentSlotType slotType;
        private EquipmentPanelUI parentPanel;
        private UISlotState currentState = UISlotState.Empty;
        
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
                
                if (unequipButton != null)
                {
                    unequipButton.gameObject.SetActive(true);
                }
                
                SetState(UISlotState.Occupied);
            }
            else
            {
                // Empty slot
                if (itemIcon != null)
                {
                    itemIcon.enabled = false;
                }
                
                if (unequipButton != null)
                {
                    unequipButton.gameObject.SetActive(false);
                }
                
                SetState(UISlotState.Empty);
            }
        }
        
        #endregion
        
        #region Drag & Drop
        
        public void OnBeginDrag(PointerEventData eventData)
        {
            // Block drag if not local player (spectating)
            if (!SpectateManager.Instance?.IsCurrentPlayerLocal() ?? true)
            {
                return; // Spectating - cannot drag
            }
            
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
            // Block drop if not local player (spectating)
            if (!SpectateManager.Instance?.IsCurrentPlayerLocal() ?? true)
            {
                return; // Spectating - cannot drop
            }
            
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
        
        #region State Management
        
        public void SetState(UISlotState state)
        {
            currentState = state;
            UpdateVisualState();
        }
        
        public UISlotState GetCurrentState() => currentState;
        
        public void OnPointerEnter()
        {
            if (currentState != UISlotState.Empty)
            {
                SetState(UISlotState.Hover);
            }
        }
        
        public void OnPointerExit()
        {
            if (currentState == UISlotState.Hover)
            {
                SetState(currentItem != null ? UISlotState.Occupied : UISlotState.Empty);
            }
        }
        
        public void OnSelect()
        {
            if (currentItem != null)
            {
                SetState(UISlotState.Selected);
            }
        }
        
        public void OnUnselect()
        {
            if (currentState == UISlotState.Selected)
            {
                SetState(currentItem != null ? UISlotState.Occupied : UISlotState.Empty);
            }
        }
        
        private void UpdateVisualState()
        {
            if (slotIcon == null) return;
            
            switch (currentState)
            {
                case UISlotState.Empty:
                    slotIcon.color = emptyColor;
                    break;
                    
                case UISlotState.Occupied:
                    slotIcon.color = occupiedColor;
                    break;
                    
                case UISlotState.Hover:
                    slotIcon.color = hoverColor;
                    break;
                    
                case UISlotState.Selected:
                    slotIcon.color = hoverColor;
                    break;
                    
                case UISlotState.Unselected:
                    slotIcon.color = occupiedColor;
                    break;
            }
        }
        
        #endregion
        
        #region Tooltip
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            OnPointerEnter();
            
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
            OnPointerExit();
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