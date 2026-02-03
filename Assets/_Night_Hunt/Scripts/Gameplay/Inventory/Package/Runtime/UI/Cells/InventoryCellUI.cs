using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using NightHunt.Inventory.Core;
using NightHunt.Inventory.Events;

namespace NightHunt.Inventory.UI
{
    /// <summary>
    /// Individual inventory cell UI with drag & drop support.
    /// </summary>
    public class InventoryCellUI : MonoBehaviour, 
        IBeginDragHandler, IDragHandler, IEndDragHandler, 
        IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI stackSizeText;
        [SerializeField] private Image durabilityBar;
        
        private ItemInstance itemData;
        private SlotLocationType locationType;
        private int slotIndex;
        
        public ItemInstance ItemInstance => itemData;
        public SlotLocationType LocationType => locationType;
        public int SlotIndex => slotIndex;
        
        public void Initialize(ItemInstance item, SlotLocationType location, int index)
        {
            itemData = item;
            locationType = location;
            slotIndex = index;
            
            Refresh();
        }
        
        public void Refresh()
        {
            if (itemData == null)
            {
                // Empty slot
                iconImage.sprite = null;
                iconImage.color = new Color(1, 1, 1, 0);
                stackSizeText.text = "";
                durabilityBar.gameObject.SetActive(false);
                return;
            }
            
            // Show item
            iconImage.sprite = itemData.Definition.Icon;
            iconImage.color = Color.white;
            
            // Stack size
            if (itemData.Definition.IsStackable && itemData.StackSize > 1)
            {
                stackSizeText.text = itemData.StackSize.ToString();
            }
            else
            {
                stackSizeText.text = "";
            }
            
            // Durability bar
            if (itemData.Definition.MaxDurability > 0)
            {
                durabilityBar.gameObject.SetActive(true);
                float durabilityPercent = itemData.CurrentDurability / itemData.Definition.MaxDurability;
                durabilityBar.fillAmount = durabilityPercent;
                
                // Color based on durability
                if (durabilityPercent > 0.5f)
                    durabilityBar.color = Color.green;
                else if (durabilityPercent > 0.25f)
                    durabilityBar.color = Color.yellow;
                else
                    durabilityBar.color = Color.red;
            }
            else
            {
                durabilityBar.gameObject.SetActive(false);
            }
        }
        
        public void OnBeginDrag(PointerEventData eventData)
        {
            // Only left-click drag
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (itemData == null) return;
            
            var context = new DragContext
            {
                SourceLocation = locationType,
                SourceIndex = slotIndex,
                ItemInstance = itemData,
            };
            
            DragDropEvents.FireBeginDrag(context);
        }
        
        public void OnDrag(PointerEventData eventData)
        {
            DragDropEvents.FireDragging(eventData.position);
        }
        
        public void OnEndDrag(PointerEventData eventData)
        {
            // Detect drop target
            var target = eventData.pointerCurrentRaycast.gameObject?.GetComponent<InventoryCellUI>();
            
            if (target != null)
            {
                var dropContext = new DragContext
                {
                    SourceLocation = locationType,
                    SourceIndex = slotIndex,
                    TargetLocation = target.locationType,
                    TargetIndex = target.slotIndex,
                    ItemInstance = itemData,
                };
                
                DragDropEvents.FireDrop(dropContext);
            }
            else
            {
                // Drop outside valid zone - cancel
                DragDropEvents.FireDragCancelled();
            }
            
            DragDropEvents.FireEndDrag();
        }
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            // Show tooltip
            if (itemData != null)
            {
                TooltipEvents.FireShowTooltip(itemData, transform.position);
            }
            else if (locationType != SlotLocationType.Inventory)
            {
                // Empty non-inventory slot - show slot description
                TooltipEvents.FireShowSlotInfo(locationType, slotIndex, transform.position);
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
            
            TooltipEvents.FireHideTooltip();
        }
    }
}
