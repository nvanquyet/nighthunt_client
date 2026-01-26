using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using NightHunt.Gameplay.Inventory;

namespace NightHunt.Gameplay.UI
{
    /// <summary>
    /// UI component for weapon slot (2 slots for equipped weapons)
    /// </summary>
    public class WeaponSlotUI : MonoBehaviour, IDropHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI References")]
        [SerializeField] private Image slotBackground;
        [SerializeField] private Image weaponIcon;
        [SerializeField] private TextMeshProUGUI slotLabelText;
        [SerializeField] private GameObject selectedIndicator;
        [SerializeField] private GameObject emptyIndicator;

        private int slotIndex;
        private string equippedWeaponId;
        private PlayerHUD playerHUD;
        private bool isSelected = false;
        private InventorySlot weaponSlot; // Store weapon slot data
        private ItemTooltip tooltip;
        private InventoryPanel inventoryPanel;
        private bool isDragging = false;

        /// <summary>
        /// Initialize weapon slot
        /// </summary>
        public void Initialize(int index, PlayerHUD hud, InventoryPanel panel = null)
        {
            slotIndex = index;
            playerHUD = hud;
            inventoryPanel = panel;

            // Find tooltip
            tooltip = FindFirstObjectByType<ItemTooltip>();

            if (slotLabelText != null)
            {
                slotLabelText.text = index == 0 ? "Primary" : "Secondary";
            }

            UpdateDisplay();
        }

        /// <summary>
        /// Update slot display
        /// </summary>
        public void UpdateSlot(string weaponId)
        {
            equippedWeaponId = weaponId;
            UpdateDisplay();
        }

        /// <summary>
        /// Update visual display
        /// </summary>
        private void UpdateDisplay()
        {
            bool isEmpty = string.IsNullOrEmpty(equippedWeaponId);

            if (emptyIndicator != null)
            {
                emptyIndicator.SetActive(isEmpty);
            }

            if (weaponIcon != null)
            {
                weaponIcon.enabled = !isEmpty;
                // TODO: Load weapon icon from weapon config
                // if (!isEmpty) weaponIcon.sprite = GetWeaponIcon(equippedWeaponId);
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
        /// Get slot index
        /// </summary>
        public int GetSlotIndex() => slotIndex;

        /// <summary>
        /// Get equipped weapon ID
        /// </summary>
        public string GetEquippedWeaponId() => equippedWeaponId;

        /// <summary>
        /// Check if slot is empty
        /// </summary>
        public bool IsEmpty() => string.IsNullOrEmpty(equippedWeaponId);

        /// <summary>
        /// Update slot with weapon slot data
        /// </summary>
        public void UpdateSlot(InventorySlot slot)
        {
            weaponSlot = slot;
            if (slot != null && !slot.IsEmpty)
            {
                equippedWeaponId = slot.Item.ItemId;
            }
            else
            {
                equippedWeaponId = null;
            }
            UpdateDisplay();
        }

        /// <summary>
        /// Get weapon slot data
        /// </summary>
        public InventorySlot GetWeaponSlot() => weaponSlot;

        // Drop handler
        public void OnDrop(PointerEventData eventData)
        {
            // Handle drop from drag operation
        }

        // Hover handlers for tooltip
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (weaponSlot != null && !weaponSlot.IsEmpty && tooltip != null)
            {
                tooltip.ShowTooltip(weaponSlot, eventData.position);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (tooltip != null)
            {
                tooltip.HideTooltip();
            }
        }

        // Drag handlers - allow dragging from weapon slot
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (weaponSlot == null || weaponSlot.IsEmpty || inventoryPanel == null)
                return;

            isDragging = true;
            var dragHandler = inventoryPanel.GetComponentInChildren<DragDropHandler>();
            if (dragHandler != null)
            {
                dragHandler.StartDragFromWeaponSlot(this, eventData);
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
