using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Config;
using System;
using TMPro;

namespace NightHunt.Inventory.UI.Slots
{
    /// <summary>
    /// Base class for all inventory slot UIs.
    /// Handles visual states, item display, and basic interactions.
    /// </summary>
    public abstract class ItemSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("Visual Components")]
        [SerializeField] protected Image backgroundImage;
        [SerializeField] protected Image itemIconImage;
        [SerializeField] protected Image emptySlotIcon;
        [SerializeField] protected TextMeshProUGUI stackCountText;
        [SerializeField] protected Slider durabilityBar;
        [SerializeField] protected Image durabilityFill;
        
        [Header("State Visuals")]
        [SerializeField] protected Image hoverHighlight;
        [SerializeField] protected Image selectedHighlight;
        [SerializeField] protected Image draggingOverlay;
        
        [Header("Colors")]
        [SerializeField] protected Color emptyColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        [SerializeField] protected Color filledColor = Color.white;
        [SerializeField] protected Color hoverColor = new Color(1f, 1f, 0.5f, 0.3f);
        [SerializeField] protected Color selectedColor = new Color(0.5f, 0.8f, 1f, 0.5f);
        [SerializeField] protected Color draggingColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);
        
        [Header("Settings")]
        [SerializeField] protected bool enableDoubleClick = true;
        [SerializeField] protected float doubleClickTime = 0.3f;
        
        [Header("Empty Slot Config")]
        [SerializeField] protected SlotLayoutConfig slotLayoutConfig;
        
        [Header("Debug")]
        [SerializeField] protected bool enableDebugLogs = false;
        
        // State
        protected ItemInstance currentItem;
        protected SlotVisualState currentState = SlotVisualState.Empty;
        protected bool isSelected = false;
        protected bool isHovered = false;
        protected bool isDragging = false;
        protected bool isEmptySlot = false;
        
        // Double click tracking
        private float lastClickTime = 0f;
        
        // Events
        public event Action<ItemSlotUI> OnSlotClicked;
        public event Action<ItemSlotUI> OnSlotDoubleClicked;
        public event Action<ItemSlotUI> OnSlotHovered;
        public event Action<ItemSlotUI> OnSlotUnhovered;
        public event Action<ItemSlotUI> OnSlotSelected;
        public event Action<ItemSlotUI> OnSlotDeselected;
        
        // === Public API ===
        
        /// <summary>
        /// Get current item in slot.
        /// </summary>
        public ItemInstance GetItem() => currentItem;
        
        /// <summary>
        /// Check if slot is empty.
        /// </summary>
        public bool IsEmpty() => currentItem == null;
        
        /// <summary>
        /// Check if slot is selected.
        /// </summary>
        public bool IsSelected() => isSelected;
        
        /// <summary>
        /// Check if slot is hovered.
        /// </summary>
        public bool IsHovered() => isHovered;
        
        /// <summary>
        /// Check if slot is being dragged.
        /// </summary>
        public bool IsDragging() => isDragging;
        
        /// <summary>
        /// Check if this is an empty slot (beyond actual inventory size).
        /// </summary>
        public bool IsEmptySlot() => isEmptySlot;
        
        /// <summary>
        /// Set empty slot state.
        /// </summary>
        public void SetIsEmptySlot(bool empty)
        {
            isEmptySlot = empty;
            if (empty)
            {
                ClearItem();
            }
            UpdateVisuals();
        }
        
        /// <summary>
        /// Set item in slot (updates visual).
        /// </summary>
        public virtual void SetItem(ItemInstance item)
        {
            currentItem = item;
            UpdateVisuals();
        }
        
        /// <summary>
        /// Clear item from slot.
        /// </summary>
        public virtual void ClearItem()
        {
            currentItem = null;
            UpdateVisuals();
        }
        
        /// <summary>
        /// Set selected state.
        /// </summary>
        public virtual void SetSelected(bool selected)
        {
            if (isSelected == selected)
                return;
            
            isSelected = selected;
            UpdateVisualState();
            
            if (selected)
                OnSlotSelected?.Invoke(this);
            else
                OnSlotDeselected?.Invoke(this);
        }
        
        /// <summary>
        /// Set hovered state.
        /// </summary>
        public virtual void SetHovered(bool hovered)
        {
            if (isHovered == hovered)
                return;
            
            isHovered = hovered;
            UpdateVisualState();
            
            if (hovered)
                OnSlotHovered?.Invoke(this);
            else
                OnSlotUnhovered?.Invoke(this);
        }
        
        /// <summary>
        /// Set dragging state.
        /// </summary>
        public virtual void SetDragging(bool dragging)
        {
            if (isDragging == dragging)
                return;
            
            isDragging = dragging;
            UpdateVisualState();
        }
        
        // === Unity Event Handlers ===
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            SetHovered(true);
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            SetHovered(false);
        }
        
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;
            
            // Block clicks on empty slots
            if (isEmptySlot)
                return;
            
            // Handle double click
            float currentTime = Time.time;
            if (enableDoubleClick && (currentTime - lastClickTime) < doubleClickTime)
            {
                OnSlotDoubleClicked?.Invoke(this);
                HandleDoubleClick();
                lastClickTime = 0f; // Reset to prevent triple click
            }
            else
            {
                OnSlotClicked?.Invoke(this);
                HandleClick();
                lastClickTime = currentTime;
            }
        }
        
        // === Virtual Methods (Override in derived classes) ===
        
        /// <summary>
        /// Handle click event (override in derived classes).
        /// </summary>
        protected virtual void HandleClick()
        {
            // Default: Select slot
            SetSelected(true);
        }
        
        /// <summary>
        /// Handle double click event (override in derived classes).
        /// </summary>
        protected virtual void HandleDoubleClick()
        {
            // Default: Auto-equip or use item
            if (currentItem != null)
            {
                Log($"Double clicked item: {currentItem.Definition.DisplayName}");
            }
        }
        
        // === Visual Updates ===
        
        protected virtual void UpdateVisuals()
        {
            UpdateVisualState();
            UpdateItemIcon();
            UpdateStackCount();
            UpdateDurabilityBar();
        }
        
        protected virtual void UpdateVisualState()
        {
            // Determine state
            if (isDragging)
                currentState = SlotVisualState.Dragging;
            else if (isSelected)
                currentState = SlotVisualState.Selected;
            else if (isHovered)
                currentState = SlotVisualState.Hovered;
            else if (currentItem != null)
                currentState = SlotVisualState.Filled;
            else
                currentState = SlotVisualState.Empty;
            
            // Update visuals based on state
            UpdateBackgroundColor();
            UpdateHighlights();
            UpdateEmptySlotIcon();
        }
        
        protected virtual void UpdateBackgroundColor()
        {
            if (backgroundImage == null)
                return;
            
            Color targetColor = currentState switch
            {
                SlotVisualState.Empty => emptyColor,
                SlotVisualState.Filled => filledColor,
                SlotVisualState.Hovered => hoverColor,
                SlotVisualState.Selected => selectedColor,
                SlotVisualState.Dragging => draggingColor,
                _ => emptyColor
            };
            
            backgroundImage.color = targetColor;
        }
        
        protected virtual void UpdateHighlights()
        {
            if (hoverHighlight != null)
                hoverHighlight.gameObject.SetActive(isHovered && !isSelected && !isDragging);
            
            if (selectedHighlight != null)
                selectedHighlight.gameObject.SetActive(isSelected && !isDragging);
            
            if (draggingOverlay != null)
                draggingOverlay.gameObject.SetActive(isDragging);
        }
        
        protected virtual void UpdateEmptySlotIcon()
        {
            if (emptySlotIcon == null)
                return;
            
            // Show empty icon when slot is empty (with or without item)
            bool shouldShow = currentItem == null;
            emptySlotIcon.gameObject.SetActive(shouldShow);
            
            if (shouldShow && slotLayoutConfig != null)
            {
                Sprite iconSprite = null;
                
                // Get icon based on slot type
                if (this is EquipmentSlotUI equipmentSlot)
                {
                    iconSprite = slotLayoutConfig.GetEquipmentEmptyIcon(equipmentSlot.GetSlotType());
                }
                else if (this is WeaponSlotUI weaponSlot)
                {
                    iconSprite = slotLayoutConfig.GetWeaponEmptyIcon(weaponSlot.GetSlotType());
                }
                else if (this is QuickSlotUI)
                {
                    iconSprite = slotLayoutConfig.quickSlotEmptyIcon;
                }
                else
                {
                    // Default to inventory icon
                    iconSprite = slotLayoutConfig.inventoryEmptyIcon;
                }
                
                if (iconSprite != null)
                {
                    emptySlotIcon.sprite = iconSprite;
                    emptySlotIcon.color = Color.white;
                }
                else
                {
                    emptySlotIcon.color = Color.clear;
                }
            }
        }
        
        protected virtual void UpdateItemIcon()
        {
            if (itemIconImage == null)
                return;
            
            if (currentItem == null || currentItem.Definition == null)
            {
                itemIconImage.gameObject.SetActive(false);
                return;
            }
            
            itemIconImage.gameObject.SetActive(true);
            
            // Set icon sprite
            if (currentItem.Definition.Icon != null)
            {
                itemIconImage.sprite = currentItem.Definition.Icon;
                itemIconImage.color = Color.white;
            }
            else
            {
                itemIconImage.color = Color.clear;
                LogWarning($"Item {currentItem.Definition.ItemId} has no icon");
            }
        }
        
        protected virtual void UpdateStackCount()
        {
            if (stackCountText == null)
                return;
            
            if (currentItem == null || currentItem.StackSize <= 1)
            {
                stackCountText.gameObject.SetActive(false);
                return;
            }
            
            stackCountText.gameObject.SetActive(true);
            stackCountText.text = currentItem.StackSize.ToString();
        }
        
        protected virtual void UpdateDurabilityBar()
        {
            if (durabilityBar == null || durabilityFill == null)
                return;
            
            if (currentItem == null || currentItem.Definition.MaxDurability <= 0)
            {
                durabilityBar.gameObject.SetActive(false);
                return;
            }
            
            durabilityBar.gameObject.SetActive(true);
            
            float durabilityPercent = currentItem.CurrentDurability / currentItem.Definition.MaxDurability;
            durabilityBar.value = durabilityPercent;
            
            // Color based on durability
            if (durabilityPercent > 0.5f)
                durabilityFill.color = Color.green;
            else if (durabilityPercent > 0.25f)
                durabilityFill.color = Color.yellow;
            else
                durabilityFill.color = Color.red;
        }
        
        // === Lifecycle ===
        
        protected virtual void Awake()
        {
            // Auto-find components if not assigned
            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();
            
            if (itemIconImage == null)
                itemIconImage = transform.Find("ItemIcon")?.GetComponent<Image>();
            
            if (emptySlotIcon == null)
                emptySlotIcon = transform.Find("EmptyIcon")?.GetComponent<Image>();
            
            if (stackCountText == null)
                stackCountText = transform.Find("StackCount")?.GetComponent<TextMeshProUGUI>();
            
            if (durabilityBar == null)
                durabilityBar = GetComponentInChildren<Slider>();
            
            if (hoverHighlight == null)
                hoverHighlight = transform.Find("HoverHighlight")?.GetComponent<Image>();
            
            if (selectedHighlight == null)
                selectedHighlight = transform.Find("SelectedHighlight")?.GetComponent<Image>();
            
            if (draggingOverlay == null)
                draggingOverlay = transform.Find("DraggingOverlay")?.GetComponent<Image>();
        }
        
        protected virtual void Start()
        {
            UpdateVisuals();
        }
        
        // === Debug ===
        
        protected void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[ItemSlotUI] {message}");
        }
        
        protected void LogWarning(string message)
        {
            Debug.LogWarning($"[ItemSlotUI] {message}");
        }
    }
    
    /// <summary>
    /// Visual states for item slots.
    /// </summary>
    public enum SlotVisualState
    {
        Empty,
        Filled,
        Hovered,
        Selected,
        Dragging
    }
}
