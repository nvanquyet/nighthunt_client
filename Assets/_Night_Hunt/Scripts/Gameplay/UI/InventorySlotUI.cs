using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using NightHunt.Gameplay.Inventory;
using NightHunt.Data;

namespace NightHunt.Gameplay.UI
{
    /// <summary>
    /// UI component for inventory slot
    /// Handles display, click, and drag operations
    /// </summary>
    public class InventorySlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI References")]
        [SerializeField] private Image slotBackground;
        [SerializeField] private Image itemIcon;
        [SerializeField] private TextMeshProUGUI quantityText;
        [SerializeField] private GameObject selectedIndicator;

        private InventorySlot slot;
        private InventoryPanel inventoryPanel;
        private int gridX = -1;
        private int gridY = -1;
        private bool isSelected = false;
        private bool isDragging = false;
        private ItemTooltip tooltip;
        private bool isNestedEquipment = false; // Flag to indicate if this is nested equipment (attached to another item)

        /// <summary>
        /// Initialize slot with inventory data
        /// </summary>
        public void Initialize(InventorySlot slotData, InventoryPanel panel, int x, int y, bool nested = false)
        {
            slot = slotData;
            inventoryPanel = panel;
            gridX = x;
            gridY = y;
            isNestedEquipment = nested;
            
            // Find tooltip
            tooltip = FindFirstObjectByType<ItemTooltip>();
            if (tooltip == null && inventoryPanel != null)
            {
                // Try to get from inventory panel
                tooltip = inventoryPanel.GetComponentInChildren<ItemTooltip>();
            }
            
            UpdateDisplay();
        }

        /// <summary>
        /// Update slot display
        /// </summary>
        public void UpdateDisplay()
        {
            if (slot == null || slot.IsEmpty)
            {
                // Empty slot
                if (itemIcon != null)
                {
                    itemIcon.sprite = null;
                    itemIcon.enabled = false;
                }

                if (quantityText != null)
                {
                    quantityText.text = "";
                }
            }
            else
            {
                // Slot with item
                if (itemIcon != null)
                {
                    // TODO: Load item icon from ItemConfigData
                    itemIcon.enabled = true;
                    // itemIcon.sprite = slot.Item.Icon; // Need to add icon to ItemConfigData
                }

                if (quantityText != null)
                {
                    quantityText.text = slot.Quantity > 1 ? slot.Quantity.ToString() : "";
                }
            }

            UpdateSelectedState();
        }

        /// <summary>
        /// Update selected visual state
        /// </summary>
        private void UpdateSelectedState()
        {
            if (selectedIndicator != null)
            {
                selectedIndicator.SetActive(isSelected);
            }
        }

        /// <summary>
        /// Set selected state
        /// </summary>
        public void SetSelected(bool selected)
        {
            isSelected = selected;
            UpdateSelectedState();
        }

        /// <summary>
        /// Get slot data
        /// </summary>
        public InventorySlot GetSlot() => slot;

        /// <summary>
        /// Get grid position
        /// </summary>
        public (int x, int y) GetGridPosition() => (gridX, gridY);

        /// <summary>
        /// Check if slot is empty
        /// </summary>
        public bool IsEmpty() => slot == null || slot.IsEmpty;

        // Pointer click handler
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                // Select item
                if (!slot.IsEmpty && inventoryPanel != null)
                {
                    inventoryPanel.SelectItem(this);
                }
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                // Right click actions (use item, split stack, etc.)
                if (!slot.IsEmpty && inventoryPanel != null)
                {
                    inventoryPanel.HandleSlotRightClick(this);
                }
            }
        }

        // Drag handlers
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (slot.IsEmpty || inventoryPanel == null)
                return;

            isDragging = true;
            inventoryPanel.StartDrag(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (isDragging && inventoryPanel != null)
            {
                inventoryPanel.UpdateDrag(eventData);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (isDragging && inventoryPanel != null)
            {
                inventoryPanel.EndDrag(this, eventData);
            }
            isDragging = false;
        }

        // Hover handlers for tooltip
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (slot != null && !slot.IsEmpty && tooltip != null)
            {
                tooltip.ShowTooltip(slot, eventData.position);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (tooltip != null)
            {
                tooltip.HideTooltip();
            }
        }

        /// <summary>
        /// Check if this is nested equipment
        /// </summary>
        public bool IsNestedEquipment() => isNestedEquipment;

        /// <summary>
        /// Set nested equipment flag
        /// </summary>
        public void SetNestedEquipment(bool nested)
        {
            isNestedEquipment = nested;
        }
    }
}
