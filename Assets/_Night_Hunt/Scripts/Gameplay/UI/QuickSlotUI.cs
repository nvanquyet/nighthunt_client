using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using NightHunt.Gameplay.Inventory;

namespace NightHunt.Gameplay.UI
{
    /// <summary>
    /// UI component for quick slot (4 slots for quick-use items)
    /// </summary>
    public class QuickSlotUI : MonoBehaviour, IDropHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI References")]
        [SerializeField] private Image slotBackground;
        [SerializeField] private Image itemIcon;
        [SerializeField] private TextMeshProUGUI slotNumberText;
        [SerializeField] private TextMeshProUGUI quantityText;
        [SerializeField] private GameObject selectedIndicator;

        private int slotIndex;
        private InventorySlot slot;
        private PlayerHUD playerHUD;
        private bool isSelected = false;
        private ItemTooltip tooltip;
        private InventoryPanel inventoryPanel;
        private bool isDragging = false;

        /// <summary>
        /// Initialize quick slot
        /// </summary>
        public void Initialize(int index, PlayerHUD hud, InventoryPanel panel = null)
        {
            slotIndex = index;
            playerHUD = hud;
            inventoryPanel = panel;

            // Find tooltip
            tooltip = FindFirstObjectByType<ItemTooltip>();

            if (slotNumberText != null)
            {
                slotNumberText.text = (index + 1).ToString();
            }

            UpdateDisplay();
        }

        /// <summary>
        /// Update slot display
        /// </summary>
        public void UpdateSlot(InventorySlot slotData)
        {
            slot = slotData;
            UpdateDisplay();
        }

        /// <summary>
        /// Update visual display
        /// </summary>
        private void UpdateDisplay()
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
                    // itemIcon.sprite = slot.Item.Icon;
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
        /// Use item in quick slot
        /// </summary>
        public void UseItem()
        {
            if (slot == null || slot.IsEmpty || playerHUD == null)
                return;

            var inventorySystem = playerHUD.GetInventorySystem();
            if (inventorySystem != null)
            {
                inventorySystem.UseItem(slot.Item.ItemId);
            }
        }

        /// <summary>
        /// Get slot index
        /// </summary>
        public int GetSlotIndex() => slotIndex;

        /// <summary>
        /// Get slot data
        /// </summary>
        public InventorySlot GetSlot() => slot;

        // Drop handler - allows dragging items onto quick slot
        public void OnDrop(PointerEventData eventData)
        {
            // Handle drop from drag operation
            // This will be handled by DragDropHandler
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

        // Drag handlers - allow dragging from quick slot
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (slot == null || slot.IsEmpty || inventoryPanel == null)
                return;

            isDragging = true;
            var dragHandler = inventoryPanel.GetComponentInChildren<DragDropHandler>();
            if (dragHandler != null)
            {
                dragHandler.StartDragFromQuickSlot(this, eventData);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (isDragging && inventoryPanel != null)
            {
                var dragHandler = inventoryPanel.GetComponentInChildren<DragDropHandler>();
                if (dragHandler != null)
                {
                    dragHandler.UpdateDrag(eventData);
                }
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (isDragging && inventoryPanel != null)
            {
                var dragHandler = inventoryPanel.GetComponentInChildren<DragDropHandler>();
                if (dragHandler != null)
                {
                    dragHandler.EndDragAny(eventData);
                }
            }
            isDragging = false;
        }
    }
}
