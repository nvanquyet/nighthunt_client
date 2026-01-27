using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using NightHunt.InteractionSystem.Core.Structs;

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
        private PlayerHUD playerHUD;
        private bool isSelected = false;
        private ItemTooltip tooltip;
        private InventoryPanel inventoryPanel;
        private bool isDragging = false;
        private ItemInstance? slot;

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
        // Legacy UpdateSlot kept for compatibility, no-op with package inventory
        public void UpdateSlot(object _)
        {
            UpdateDisplay();
        }

        /// <summary>
        /// Update visual display
        /// </summary>
        private void UpdateDisplay()
        {
            // TODO: Reconnect to package inventory quick slots
            bool isEmpty = true;
            if (isEmpty)
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
                if (itemIcon != null)
                {
                    itemIcon.enabled = true;
                }

                if (quantityText != null)
                {
                    quantityText.text = "";
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
            if (playerHUD == null)
                return;
            // TODO: Call ItemUsageSystem on player based on quick slot binding
        }

        /// <summary>
        /// Get slot index
        /// </summary>
        public int GetSlotIndex() => slotIndex;

        /// <summary>
        /// Get slot data
        /// </summary>
        public ItemInstance? GetSlot() => slot;

        // Drop handler - allows dragging items onto quick slot
        public void OnDrop(PointerEventData eventData)
        {
            // Handle drop from drag operation
            // This will be handled by DragDropHandler
        }

        // Hover handlers for tooltip
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (slot.HasValue && slot.Value.IsValid() && tooltip != null)
            {
                // TODO: Update ItemTooltip to accept ItemInstance instead of InventorySlot
                // tooltip.ShowTooltip(slot.Value, eventData.position);
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
            if (!slot.HasValue || !slot.Value.IsValid() || inventoryPanel == null)
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
