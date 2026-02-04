using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.UI.Visuals;

namespace NightHunt.Inventory.UI.Cells
{
    /// <summary>
    /// UI cell for displaying an inventory item.
    /// Implements drag & drop and tooltip functionality.
    /// </summary>
    public class InventoryCellUI : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI References")]
        [SerializeField] private Image itemIcon;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI stackText;
        [SerializeField] private Image durabilityBar;
        
        [Header("Visual Settings")]
        [SerializeField] private Color emptyColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        [SerializeField] private Color occupiedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        
        private ItemInstance itemData;
        private int slotIndex;
        private SlotLocationType locationType = SlotLocationType.Inventory;
        
        #region Initialization
        
        /// <summary>
        /// Initializes the cell with item data.
        /// </summary>
        public void Initialize(ItemInstance item, int index, SlotLocationType location)
        {
            itemData = item;
            slotIndex = index;
            locationType = location;
            Refresh();
        }
        
        /// <summary>
        /// Updates the visual state of the cell.
        /// </summary>
        public void Refresh()
        {
            if (itemData != null)
            {
                // Show item
                itemIcon.sprite = itemData.Definition.Icon;
                itemIcon.enabled = true;
                backgroundImage.color = occupiedColor;
                
                // Show stack size if stackable
                if (itemData.Definition.IsStackable && itemData.StackSize > 1)
                {
                    stackText.text = itemData.StackSize.ToString();
                    stackText.enabled = true;
                }
                else
                {
                    stackText.enabled = false;
                }
                
                // Show durability bar
                if (durabilityBar != null && itemData.Definition.MaxDurability > 0)
                {
                    durabilityBar.fillAmount = itemData.CurrentDurability / itemData.Definition.MaxDurability;
                    durabilityBar.enabled = true;
                }
                else if (durabilityBar != null)
                {
                    durabilityBar.enabled = false;
                }
            }
            else
            {
                // Empty slot
                itemIcon.enabled = false;
                stackText.enabled = false;
                backgroundImage.color = emptyColor;
                
                if (durabilityBar != null)
                {
                    durabilityBar.enabled = false;
                }
            }
        }
        
        #endregion
        
        #region Drag & Drop Implementation
        
        public void OnBeginDrag(PointerEventData eventData)
        {
            // Only left-click drag
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (itemData == null) return;
            
            var context = new DragContext
            {
                SourceLocation = locationType,
                SourceIndex = slotIndex,
                ItemInstance = itemData
            };
            
            DragDropEvents.InvokeBeginDrag(context);
        }
        
        public void OnDrag(PointerEventData eventData)
        {
            DragDropEvents.InvokeDragging(eventData.position);
        }
        
        public void OnEndDrag(PointerEventData eventData)
        {
            // Detect drop target
            var targetCell = eventData.pointerCurrentRaycast.gameObject?.GetComponent<InventoryCellUI>();
            
            if (targetCell != null)
            {
                var dropContext = new DragContext
                {
                    SourceLocation = locationType,
                    SourceIndex = slotIndex,
                    TargetLocation = targetCell.locationType,
                    TargetIndex = targetCell.slotIndex,
                    ItemInstance = itemData
                };
                
                DragDropEvents.InvokeDrop(dropContext);
            }
            else
            {
                // Drop outside valid zone - cancel
                DragDropEvents.InvokeDragCancelled();
            }
            
            DragDropEvents.InvokeEndDrag();
        }
        
        #endregion
        
        #region Tooltip Implementation
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (itemData != null)
            {
                // Show item tooltip
                TooltipEvents.InvokeShowTooltip(itemData, transform.position);
            }
            else if (locationType != SlotLocationType.Inventory)
            {
                // Show slot info for empty non-inventory slots
                TooltipEvents.InvokeShowSlotInfo(locationType, slotIndex, transform.position);
            }
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            // Check if moving to tooltip (don't hide if so)
            var tooltipObj = eventData.pointerEnter?.GetComponent<TooltipHoverDetector>();
            if (tooltipObj != null)
            {
                // Moving to tooltip - don't hide
                return;
            }
            
            TooltipEvents.InvokeHideTooltip();
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Gets the item data in this cell.
        /// </summary>
        public ItemInstance GetItemData() => itemData;
        
        /// <summary>
        /// Gets the slot index.
        /// </summary>
        public int GetSlotIndex() => slotIndex;
        
        /// <summary>
        /// Gets the slot location type.
        /// </summary>
        public SlotLocationType GetLocationType() => locationType;
        
        /// <summary>
        /// Checks if cell is empty.
        /// </summary>
        public bool IsEmpty() => itemData == null;
        
        #endregion
    }
}