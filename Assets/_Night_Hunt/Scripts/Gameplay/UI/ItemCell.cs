using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.Events;
using NightHunt.Gameplay.Inventory;
using NightHunt.InteractionSystem.Core.Interfaces;

namespace NightHunt.Gameplay.UI
{
    /// <summary>
    /// Enum xác định vị trí của ItemCell trong UI
    /// </summary>
    public enum ItemCellLocation
    {
        Inventory, // Inventory grid
        Container, // Loot container
        QuickSlot, // Quick slot (1-4)
        Weapon, // Weapon slot (Primary/Secondary)
        Equipment, // Equipment slot (Backpack, Armor, Helmet, Vest)
        Attachment // Attachment slot (nested equipment)
    }

    /// <summary>
    /// UI component cho tất cả các loại item cell (inventory, container, quick slot, weapon, equipment, attachment)
    /// Handles display, click, and drag operations
    /// Logic chỉ cần index trong List/Array, UI thì do người chơi tự kéo thả
    /// </summary>
    public class ItemCell : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI References")] [SerializeField]
        private Image slotBackground;

        [SerializeField] private Image itemIcon;
        [SerializeField] private TextMeshProUGUI quantityText;
        [SerializeField] private TextMeshProUGUI slotLabelText; // For quick slot number, weapon label, etc.
        [SerializeField] private GameObject selectedIndicator;
        [SerializeField] private GameObject emptyIndicator;

        [Header("Cell Configuration")] [SerializeField]
        private ItemCellLocation cellLocation = ItemCellLocation.Inventory;

        [SerializeField] private int cellIndex = -1; // Index trong list/array (cho quick slot, weapon slot)

        [SerializeField]
        private EquipmentSlotType
            equipmentSlotType = EquipmentSlotType.Backpack; // Chỉ dùng khi cellLocation == Equipment
 
        [Header("Events")] [SerializeField] private UnityEvent<ItemCell> onCellHover;
        [SerializeField] private UnityEvent<ItemCell> onCellUnhover;
        [SerializeField] private UnityEvent<ItemCell> onCellSelect;
        [SerializeField] private UnityEvent<ItemCell> onCellUnselect;

        private InventorySlot slot;
        private InventoryPanel inventoryPanel;
        private bool isSelected = false;
        private bool isDragging = false;
        private ItemTooltip tooltip;

        private bool
            isNestedEquipment = false; // Flag to indicate if this is nested equipment (attached to another item)

        // For double-click detection
        private float lastClickTime = 0f;
        private const float doubleClickDelay = 0.3f; // Time window for double-click


        // ADD these fields to ItemCell class
        private AttachmentSlotType attachmentSlotType = AttachmentSlotType.None;
        private bool isAttachmentSlot = false;

        public ItemCellLocation GetCellLocation() => cellLocation;

        /// <summary>
        /// Initialize cell với inventory data
        /// </summary>
        public void Initialize(InventorySlot slotData, InventoryPanel panel, ItemCellLocation location, int index = -1,
            EquipmentSlotType equipType = EquipmentSlotType.Backpack, bool nested = false)
        {
            slot = slotData;
            inventoryPanel = panel;
            cellLocation = location;
            cellIndex = index;
            equipmentSlotType = equipType;
            isNestedEquipment = nested;

            // Find tooltip from inventory panel
            if (inventoryPanel != null)
            {
                tooltip = inventoryPanel.GetItemTooltip();
            }

            // Fallback: Find in scene
            if (tooltip == null)
            {
                tooltip = FindFirstObjectByType<ItemTooltip>();
            }

            // Setup slot label based on location
            SetupSlotLabel();

            // Initialize field visibility based on slot state
            InitializeFieldVisibility();

            UpdateDisplay();
        }


        /// <summary>
        /// Set attachment slot type (for validation)
        /// ADD this method to ItemCell.cs
        /// </summary>
        public void SetAttachmentSlotType(AttachmentSlotType slotType)
        {
            attachmentSlotType = slotType;
            isAttachmentSlot = true;
        }

        /// <summary>
        /// Get attachment slot type
        /// ADD this method to ItemCell.cs
        /// </summary>
        public AttachmentSlotType GetAttachmentSlotType()
        {
            return attachmentSlotType;
        }

        /// <summary>
        /// Check if this is an attachment slot
        /// ADD this method to ItemCell.cs
        /// </summary>
        public bool IsAttachmentSlot()
        {
            return isAttachmentSlot;
        }


        /// <summary>
        /// Setup slot label text based on cell location
        /// </summary>
        private void SetupSlotLabel()
        {
            if (slotLabelText == null) return;

            switch (cellLocation)
            {
                case ItemCellLocation.QuickSlot:
                    slotLabelText.text = (cellIndex + 1).ToString();
                    break;
                case ItemCellLocation.Weapon:
                    slotLabelText.text = cellIndex == 0 ? "Primary" : "Secondary";
                    break;
                case ItemCellLocation.Equipment:
                    slotLabelText.text = GetEquipmentSlotName(equipmentSlotType);
                    break;
                default:
                    slotLabelText.text = "";
                    break;
            }
        }

        private string GetEquipmentSlotName(EquipmentSlotType type)
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
        /// Initialize field visibility based on slot state (empty or not)
        /// Called during Initialize() to set initial state of UI elements
        /// </summary>
        private void InitializeFieldVisibility()
        {
            bool isEmpty = slot == null || slot.IsEmpty;

            // Item icon: enable only if slot has item
            if (itemIcon != null)
            {
                itemIcon.enabled = !isEmpty;
            }

            // Quantity text: active only if slot has item AND quantity > 1
            if (quantityText != null)
            {
                if (isEmpty)
                {
                    quantityText.gameObject.SetActive(false);
                    quantityText.text = "";
                }
                else
                {
                    // Will be set in UpdateDisplay() based on actual quantity
                    quantityText.gameObject
                        .SetActive(false); // Start as inactive, UpdateDisplay will activate if needed
                }
            }

            // Empty indicator
            if (emptyIndicator != null)
            {
                emptyIndicator.SetActive(isEmpty);
            }
        }

        /// <summary>
        /// Update cell display
        /// </summary>
        public void UpdateDisplay()
        {
            bool isEmpty = slot == null || slot.IsEmpty;

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
                    quantityText.gameObject.SetActive(false);
                }

                if (emptyIndicator != null)
                {
                    emptyIndicator.SetActive(true);
                }
            }
            else
            {
                // Slot with item
                if (itemIcon != null)
                {
                    itemIcon.enabled = true;

                    // Load icon from ItemDataBase (ItemDataRegistry)
                    if (slot.Item != null && !string.IsNullOrEmpty(slot.Item.ItemId))
                    {
                        var registry = NightHunt.InteractionSystem.Core.Abstractions.ItemDataRegistry.Load();
                        if (registry != null)
                        {
                            var itemData = registry.GetById(slot.Item.ItemId);
                            if (itemData != null && itemData.Icon != null)
                            {
                                itemIcon.sprite = itemData.Icon;
                            }
                            else
                            {
                                itemIcon.sprite = null;
                            }
                        }
                        else
                        {
                            itemIcon.sprite = null;
                        }
                    }
                    else
                    {
                        itemIcon.sprite = null;
                    }
                }

                if (quantityText != null)
                {
                    // Show quantity text only if quantity > 1
                    if (slot.Quantity > 1)
                    {
                        quantityText.text = slot.Quantity.ToString();
                        quantityText.gameObject.SetActive(true);
                    }
                    else
                    {
                        quantityText.text = "";
                        quantityText.gameObject.SetActive(false);
                    }
                }

                if (emptyIndicator != null)
                {
                    emptyIndicator.SetActive(false);
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
            bool wasSelected = isSelected;
            isSelected = selected;
            UpdateSelectedState();

            // Invoke events
            if (selected && !wasSelected)
            {
                onCellSelect?.Invoke(this);
            }
            else if (!selected && wasSelected)
            {
                onCellUnselect?.Invoke(this);
            }
        }

        /// <summary>
        /// Get slot data
        /// </summary>
        public InventorySlot GetSlot() => slot;

        /// <summary>
        /// Set slot data (dùng khi move/swap items)
        /// </summary>
        public void SetSlot(InventorySlot newSlot)
        {
            slot = newSlot;
            UpdateDisplay();
        }

        /// <summary>
        /// Clear slot data
        /// </summary>
        public void ClearSlot()
        {
            slot = new InventorySlot(); // Create empty slot
            UpdateDisplay();
        }

        /// <summary>
        /// Get cell location
        /// </summary>
        public ItemCellLocation GetLocation() => cellLocation;

        /// <summary>
        /// Get cell index (for quick slot, weapon slot)
        /// </summary>
        public int GetCellIndex() => cellIndex;

        /// <summary>
        /// Get equipment slot type (only valid when cellLocation == Equipment)
        /// </summary>
        public EquipmentSlotType GetEquipmentSlotType() => equipmentSlotType;

        /// <summary>
        /// Check if slot is empty
        /// </summary>
        public bool IsEmpty() => slot == null || slot.IsEmpty;

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

        // Pointer click handler
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                float currentTime = Time.time;
                bool isDoubleClick = (currentTime - lastClickTime) < doubleClickDelay;
                lastClickTime = currentTime;

                if (inventoryPanel != null)
                {
                    if (!slot.IsEmpty)
                    {
                        if (isDoubleClick && isSelected)
                        {
                            // Double-click on selected item → Use item
                            inventoryPanel.HandleSlotDoubleClick(this);
                        }
                        else
                        {
                            // Single click → Select item (show nested equipment if has)
                            inventoryPanel.SelectItem(this);
                        }
                    }
                    else
                    {
                        // Click on empty slot - deselect current item
                        inventoryPanel.SelectItem(null);
                    }
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

        // Drop handler - allows dropping items onto this cell
        public void OnDrop(PointerEventData eventData)
        {
            if (inventoryPanel == null)
                return;

            // Get the drag handler to check what's being dragged
            var dragHandler = inventoryPanel.GetComponentInChildren<DragDropHandler>();
            if (dragHandler != null && dragHandler.IsDragging())
            {
                // Let DragDropHandler handle the drop logic
                dragHandler.EndDragAny(eventData);
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
            // Show tooltip for items or empty equipment slots
            if (tooltip != null)
            {
                if (slot != null && !slot.IsEmpty)
                {
                    // Show tooltip for items
                    tooltip.ShowTooltip(slot, eventData.position);
                }
                else if (cellLocation == ItemCellLocation.Attachment && isAttachmentSlot)
                {
                    // ADDED: Show attachment slot tooltip
                    tooltip.ShowAttachmentSlotTooltip(attachmentSlotType, eventData.position);
                }
                else
                {
                    tooltip.ShowTooltip(slot, eventData.position);
                }
                // else if (cellLocation == ItemCellLocation.Equipment)
                // {
                //     tooltip.ShowEquipmentSlotTooltip(equipmentSlotType, eventData.position);
                // }
                // else if (cellLocation == ItemCellLocation.Weapon)
                // {
                //     tooltip.ShowWeaponSlotTooltip(cellIndex, eventData.position);
                // }
                // else if (cellLocation == ItemCellLocation.QuickSlot)
                // {
                //     tooltip.ShowQuickSlotTooltip(cellIndex, eventData.position);
                // }
            }

            // Show nested equipment panel when hovering over item that can have attachments
            if (inventoryPanel != null && slot != null && !slot.IsEmpty)
            {
                var slotData = GetSlot();
                if (slotData != null && !slotData.IsEmpty)
                {
                    // Check if item can have attachments (weapon, equipment with attachment slots, etc.)
                    var itemData = GetItemDataFromRegistry(slotData.Item.ItemId);
                    if (itemData != null)
                    {
                        bool canHaveAttachments = false;

                        // Check if item is EquipmentDataBase and has attachment slots
                        if (itemData is NightHunt.InteractionSystem.Core.Abstractions.EquipmentDataBase equipmentData)
                        {
                            if (equipmentData.AttachmentSlots != null && equipmentData.AttachmentSlots.Length > 0)
                            {
                                canHaveAttachments = true;
                            }
                        }

                        // Fallback: Check if item is a weapon
                        if (!canHaveAttachments && itemData.Category ==
                            NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Weapon)
                        {
                            canHaveAttachments = true;
                        }

                        if (canHaveAttachments)
                        {
                            // Show nested equipment panel for items with attachments
                            bool isEquippedItem = inventoryPanel.IsEquippedItem(this);
                            inventoryPanel.ShowNestedEquipmentPanelOnHover(slotData, isEquippedItem);
                        }
                    }
                }
            }

            // Invoke hover event
            onCellHover?.Invoke(this);
        }

        /// <summary>
        /// Get ItemDataBase from ItemDataRegistry (helper method)
        /// </summary>
        private NightHunt.InteractionSystem.Core.Abstractions.ItemDataBase GetItemDataFromRegistry(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return null;

            var registry = NightHunt.InteractionSystem.Core.Abstractions.ItemDataRegistry.Load();
            if (registry != null)
            {
                return registry.GetById(itemId);
            }

            return null;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (tooltip != null)
            {
                tooltip.HideTooltip();
            }

            // Hide nested equipment panel when unhovering (only if not selected)
            if (inventoryPanel != null && !isSelected)
            {
                inventoryPanel.HideNestedEquipmentPanelOnUnhover();
            }

            // Invoke unhover event
            onCellUnhover?.Invoke(this);
        }
    }
}