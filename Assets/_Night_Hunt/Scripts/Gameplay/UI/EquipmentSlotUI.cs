using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using NightHunt.Gameplay.Inventory;

namespace NightHunt.Gameplay.UI
{
    /// <summary>
    /// UI component for equipment slot (backpack, armor, etc.)
    /// </summary>
    public class EquipmentSlotUI : MonoBehaviour, IDropHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI References")]
        [SerializeField] private Image slotBackground;
        [SerializeField] private Image itemIcon;
        [SerializeField] private TextMeshProUGUI slotLabelText;
        [SerializeField] private GameObject selectedIndicator;
        [SerializeField] private GameObject emptyIndicator;

        [Header("Slot Type")]
        [SerializeField] private EquipmentSlotType slotType = EquipmentSlotType.Backpack;

        private InventorySlot slot;
        private EquipmentPanel equipmentPanel;
        private bool isSelected = false;
        private ItemTooltip tooltip;
        private InventoryPanel inventoryPanel;
        private bool isDragging = false;

        /// <summary>
        /// Initialize equipment slot
        /// </summary>
        public void Initialize(EquipmentSlotType type, EquipmentPanel panel, InventoryPanel invPanel = null)
        {
            slotType = type;
            equipmentPanel = panel;
            inventoryPanel = invPanel;

            // Find tooltip
            tooltip = FindFirstObjectByType<ItemTooltip>();

            if (slotLabelText != null)
            {
                slotLabelText.text = GetSlotTypeName(type);
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
            bool isEmpty = slot == null || slot.IsEmpty;

            if (emptyIndicator != null)
            {
                emptyIndicator.SetActive(isEmpty);
            }

            if (itemIcon != null)
            {
                itemIcon.enabled = !isEmpty;
                // TODO: Load item icon from ItemConfigData
                // if (!isEmpty) itemIcon.sprite = slot.Item.Icon;
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
        /// Get slot type name
        /// </summary>
        private string GetSlotTypeName(EquipmentSlotType type)
        {
            return type switch
            {
                EquipmentSlotType.Backpack => "Backpack",
                EquipmentSlotType.Armor => "Armor",
                EquipmentSlotType.Helmet => "Helmet",
                EquipmentSlotType.Vest => "Vest",
                _ => "Equipment"
            };
        }

        /// <summary>
        /// Get slot type
        /// </summary>
        public EquipmentSlotType GetSlotType() => slotType;

        /// <summary>
        /// Get slot data
        /// </summary>
        public InventorySlot GetSlot() => slot;

        /// <summary>
        /// Check if slot is empty
        /// </summary>
        public bool IsEmpty() => slot == null || slot.IsEmpty;

        // Drop handler
        public void OnDrop(PointerEventData eventData)
        {
            // Handle drop from drag operation
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

        // Drag handlers - allow dragging from equipment slot
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (slot == null || slot.IsEmpty || inventoryPanel == null)
                return;

            isDragging = true;
            var dragHandler = inventoryPanel.GetComponentInChildren<DragDropHandler>();
            if (dragHandler != null)
            {
                dragHandler.StartDragFromEquipmentSlot(this, eventData);
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

    /// <summary>
    /// Equipment slot types
    /// </summary>
    public enum EquipmentSlotType
    {
        Backpack,
        Armor,
        Helmet,
        Vest
    }
}
