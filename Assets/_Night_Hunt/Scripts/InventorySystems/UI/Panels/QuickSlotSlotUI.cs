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
    /// Individual quick slot UI for Inventory Panel.
    /// Supports drag & drop only (no use functionality).
    /// Implements state management for visual feedback.
    /// </summary>
    public class QuickSlotSlotUI : MonoBehaviour,
        IDropHandler, IPointerEnterHandler, IPointerExitHandler,
        IUISlotStateManager
    {
        [Header("UI References")]
        [SerializeField] private Image slotBackground;
        [SerializeField] private Image itemIcon;
        [SerializeField] private TextMeshProUGUI slotNumberText;
        
        [Header("Visual Settings")]
        [SerializeField] private Color emptyColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        [SerializeField] private Color occupiedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color hoverColor = new Color(0.4f, 0.4f, 0.4f, 1f);
        
        private ItemInstance currentItem;
        private int slotIndex;
        private QuickSlotPanelUI parentPanel;
        private UISlotState currentState = UISlotState.Empty;
        
        #region Initialization
        
        public void Initialize(int index, QuickSlotPanelUI panel)
        {
            slotIndex = index;
            parentPanel = panel;
            
            if (slotNumberText != null)
            {
                slotNumberText.text = (index + 1).ToString();
            }
            
            SetItem(null);
            SetState(UISlotState.Empty);
        }
        
        public void SetItem(ItemInstance item)
        {
            currentItem = item;
            
            if (item != null)
            {
                if (itemIcon != null)
                {
                    itemIcon.sprite = item.Definition.Icon;
                    itemIcon.enabled = true;
                }
                
                SetState(UISlotState.Occupied);
            }
            else
            {
                if (itemIcon != null)
                {
                    itemIcon.enabled = false;
                }
                
                SetState(UISlotState.Empty);
            }
        }
        
        #endregion
        
        #region Drag & Drop
        
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
            
            // Validate and assign via parent panel
            parentPanel.OnItemDroppedOnSlot(draggedCell.GetItemData(), slotIndex);
            
            var dropContext = new DragContext
            {
                SourceLocation = draggedCell.GetLocationType(),
                SourceIndex = draggedCell.GetSlotIndex(),
                TargetLocation = SlotLocationType.QuickSlot,
                TargetIndex = slotIndex,
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
            if (slotBackground == null) return;
            
            switch (currentState)
            {
                case UISlotState.Empty:
                    slotBackground.color = emptyColor;
                    break;
                    
                case UISlotState.Occupied:
                    slotBackground.color = occupiedColor;
                    break;
                    
                case UISlotState.Hover:
                    slotBackground.color = hoverColor;
                    break;
                    
                case UISlotState.Selected:
                    slotBackground.color = hoverColor;
                    break;
                    
                case UISlotState.Unselected:
                    slotBackground.color = occupiedColor;
                    break;
            }
        }
        
        #endregion
        
        #region Pointer Events
        
        void IPointerEnterHandler.OnPointerEnter(PointerEventData eventData)
        {
            OnPointerEnter();
            
            if (currentItem != null)
            {
                TooltipEvents.InvokeShowTooltip(currentItem, transform.position);
            }
            else
            {
                TooltipEvents.InvokeShowSlotInfo(SlotLocationType.QuickSlot, slotIndex, transform.position);
            }
        }
        
        void IPointerExitHandler.OnPointerExit(PointerEventData eventData)
        {
            OnPointerExit();
            TooltipEvents.InvokeHideTooltip();
        }
        
        #endregion
        
        #region Public API
        
        public int GetSlotIndex() => slotIndex;
        
        public ItemInstance GetItem() => currentItem;
        
        #endregion
    }
}
