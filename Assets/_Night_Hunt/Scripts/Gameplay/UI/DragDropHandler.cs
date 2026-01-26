using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using NightHunt.Gameplay.Inventory;

namespace NightHunt.Gameplay.UI
{
    /// <summary>
    /// Handles drag and drop operations for inventory items
    /// Provides visual feedback and drop validation
    /// </summary>
    public class DragDropHandler : MonoBehaviour
    {
        [Header("Drag Visual")]
        [SerializeField] private GameObject dragIconPrefab;
        [SerializeField] private Canvas dragCanvas;

        private GameObject currentDragIcon;
        private InventorySlotUI draggedSlot;
        private QuickSlotUI draggedQuickSlot;
        private WeaponSlotUI draggedWeaponSlot;
        private EquipmentSlotUI draggedEquipmentSlot;
        private bool isDragging = false;
        private InventoryPanel inventoryPanel;

        /// <summary>
        /// Initialize drag drop handler
        /// </summary>
        public void Initialize(InventoryPanel panel, Canvas canvas)
        {
            inventoryPanel = panel;
            if (dragCanvas == null)
            {
                dragCanvas = canvas;
            }
        }

        /// <summary>
        /// Start drag operation from inventory slot
        /// </summary>
        public void StartDrag(InventorySlotUI slot, PointerEventData eventData)
        {
            if (slot == null || slot.IsEmpty())
                return;

            draggedSlot = slot;
            draggedQuickSlot = null;
            draggedWeaponSlot = null;
            draggedEquipmentSlot = null;
            isDragging = true;

            // Create drag icon
            CreateDragIcon(slot);
        }

        /// <summary>
        /// Start drag operation from quick slot
        /// </summary>
        public void StartDragFromQuickSlot(QuickSlotUI slot, PointerEventData eventData)
        {
            if (slot == null || slot.GetSlot() == null || slot.GetSlot().IsEmpty)
                return;

            draggedQuickSlot = slot;
            draggedSlot = null;
            draggedWeaponSlot = null;
            draggedEquipmentSlot = null;
            isDragging = true;

            // Create drag icon from quick slot
            CreateDragIconFromSlot(slot.GetSlot());
        }

        /// <summary>
        /// Start drag operation from weapon slot
        /// </summary>
        public void StartDragFromWeaponSlot(WeaponSlotUI slot, PointerEventData eventData)
        {
            if (slot == null || slot.GetWeaponSlot() == null || slot.GetWeaponSlot().IsEmpty)
                return;

            draggedWeaponSlot = slot;
            draggedSlot = null;
            draggedQuickSlot = null;
            draggedEquipmentSlot = null;
            isDragging = true;

            // Create drag icon from weapon slot
            CreateDragIconFromSlot(slot.GetWeaponSlot());
        }

        /// <summary>
        /// Start drag operation from equipment slot
        /// </summary>
        public void StartDragFromEquipmentSlot(EquipmentSlotUI slot, PointerEventData eventData)
        {
            if (slot == null || slot.GetSlot() == null || slot.GetSlot().IsEmpty)
                return;

            draggedEquipmentSlot = slot;
            draggedSlot = null;
            draggedQuickSlot = null;
            draggedWeaponSlot = null;
            isDragging = true;

            // Create drag icon from equipment slot
            CreateDragIconFromSlot(slot.GetSlot());
        }

        /// <summary>
        /// Update drag operation
        /// </summary>
        public void UpdateDrag(PointerEventData eventData)
        {
            if (!isDragging || currentDragIcon == null)
                return;

            // Update drag icon position to follow mouse
            RectTransform dragIconRect = currentDragIcon.GetComponent<RectTransform>();
            if (dragIconRect != null && dragCanvas != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dragCanvas.transform as RectTransform,
                    eventData.position,
                    dragCanvas.worldCamera,
                    out Vector2 localPoint);

                dragIconRect.anchoredPosition = localPoint;
            }

            // Update highlight on valid drop zones
            UpdateDropZoneHighlight(eventData);
        }

        /// <summary>
        /// End drag operation
        /// </summary>
        public void EndDrag(InventorySlotUI slot, PointerEventData eventData)
        {
            if (!isDragging)
                return;

            // Find drop target
            GameObject dropTarget = eventData.pointerCurrentRaycast.gameObject;
            if (dropTarget != null)
            {
                HandleDrop(slot, dropTarget);
            }

            // Cleanup
            CleanupDrag();
        }

        /// <summary>
        /// End drag operation from any slot type
        /// </summary>
        public void EndDragAny(PointerEventData eventData)
        {
            if (!isDragging)
                return;

            // Find drop target
            GameObject dropTarget = eventData.pointerCurrentRaycast.gameObject;
            if (dropTarget != null)
            {
                // Determine source type
                if (draggedSlot != null)
                {
                    HandleDrop(draggedSlot, dropTarget);
                }
                else if (draggedQuickSlot != null)
                {
                    HandleDropFromQuickSlot(draggedQuickSlot, dropTarget);
                }
                else if (draggedWeaponSlot != null)
                {
                    HandleDropFromWeaponSlot(draggedWeaponSlot, dropTarget);
                }
                else if (draggedEquipmentSlot != null)
                {
                    HandleDropFromEquipmentSlot(draggedEquipmentSlot, dropTarget);
                }
            }

            // Cleanup
            CleanupDrag();
        }

        /// <summary>
        /// Handle drop operation
        /// </summary>
        private void HandleDrop(InventorySlotUI sourceSlot, GameObject dropTarget)
        {
            if (sourceSlot == null || dropTarget == null)
                return;

            // Check drop target type
            InventorySlotUI targetSlot = dropTarget.GetComponent<InventorySlotUI>();
            QuickSlotUI quickSlot = dropTarget.GetComponent<QuickSlotUI>();
            WeaponSlotUI weaponSlot = dropTarget.GetComponent<WeaponSlotUI>();
            EquipmentSlotUI equipmentSlot = dropTarget.GetComponent<EquipmentSlotUI>();
            TrashSlotUI trashSlot = dropTarget.GetComponent<TrashSlotUI>();

            if (targetSlot != null)
            {
                // Drop on inventory slot
                inventoryPanel?.MoveItem(sourceSlot, targetSlot);
            }
            else if (quickSlot != null)
            {
                // Drop on quick slot
                inventoryPanel?.AssignQuickSlot(sourceSlot, quickSlot.GetSlotIndex());
            }
            else if (weaponSlot != null)
            {
                // Drop on weapon slot
                inventoryPanel?.EquipWeapon(sourceSlot, weaponSlot.GetSlotIndex());
            }
            else if (equipmentSlot != null)
            {
                // Drop on equipment slot
                inventoryPanel?.EquipItem(sourceSlot, equipmentSlot);
            }
            else if (trashSlot != null)
            {
                // Drop on trash slot
                trashSlot.DropItem(sourceSlot);
            }
        }

        /// <summary>
        /// Handle drop from quick slot
        /// </summary>
        private void HandleDropFromQuickSlot(QuickSlotUI sourceSlot, GameObject dropTarget)
        {
            if (sourceSlot == null || dropTarget == null)
                return;

            var slot = sourceSlot.GetSlot();
            if (slot == null || slot.IsEmpty)
                return;

            InventorySlotUI targetSlot = dropTarget.GetComponent<InventorySlotUI>();
            TrashSlotUI trashSlot = dropTarget.GetComponent<TrashSlotUI>();

            if (targetSlot != null)
            {
                // Move from quick slot to inventory
                inventoryPanel?.MoveFromQuickSlotToInventory(sourceSlot.GetSlotIndex(), targetSlot);
            }
            else if (trashSlot != null)
            {
                // Drop from quick slot
                trashSlot.DropItemFromQuickSlot(sourceSlot);
            }
        }

        /// <summary>
        /// Handle drop from weapon slot
        /// </summary>
        private void HandleDropFromWeaponSlot(WeaponSlotUI sourceSlot, GameObject dropTarget)
        {
            if (sourceSlot == null || dropTarget == null)
                return;

            var slot = sourceSlot.GetWeaponSlot();
            if (slot == null || slot.IsEmpty)
                return;

            InventorySlotUI targetSlot = dropTarget.GetComponent<InventorySlotUI>();
            TrashSlotUI trashSlot = dropTarget.GetComponent<TrashSlotUI>();

            if (targetSlot != null)
            {
                // Unequip weapon to inventory
                inventoryPanel?.UnequipWeaponToInventory(sourceSlot.GetSlotIndex(), targetSlot);
            }
            else if (trashSlot != null)
            {
                // Drop weapon
                trashSlot.DropItemFromWeaponSlot(sourceSlot);
            }
        }

        /// <summary>
        /// Handle drop from equipment slot
        /// </summary>
        private void HandleDropFromEquipmentSlot(EquipmentSlotUI sourceSlot, GameObject dropTarget)
        {
            if (sourceSlot == null || dropTarget == null)
                return;

            var slot = sourceSlot.GetSlot();
            if (slot == null || slot.IsEmpty)
                return;

            InventorySlotUI targetSlot = dropTarget.GetComponent<InventorySlotUI>();
            TrashSlotUI trashSlot = dropTarget.GetComponent<TrashSlotUI>();

            if (targetSlot != null)
            {
                // Unequip item to inventory
                inventoryPanel?.UnequipItemToInventory(sourceSlot, targetSlot);
            }
            else if (trashSlot != null)
            {
                // Drop equipment
                trashSlot.DropItemFromEquipmentSlot(sourceSlot);
            }
        }

        /// <summary>
        /// Create drag icon visual
        /// </summary>
        private void CreateDragIcon(InventorySlotUI slot)
        {
            if (slot == null || slot.IsEmpty())
                return;

            CreateDragIconFromSlot(slot.GetSlot());
        }

        /// <summary>
        /// Create drag icon from inventory slot
        /// </summary>
        private void CreateDragIconFromSlot(InventorySlot slot)
        {
            if (slot == null || slot.IsEmpty || dragIconPrefab == null || dragCanvas == null)
                return;

            currentDragIcon = Instantiate(dragIconPrefab, dragCanvas.transform);
            currentDragIcon.transform.SetAsLastSibling();

            // Set icon image
            Image iconImage = currentDragIcon.GetComponent<Image>();
            if (iconImage != null)
            {
                // TODO: Set icon from item data
                // iconImage.sprite = slot.Item.Icon;
            }

            // Make it follow mouse
            currentDragIcon.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        }

        /// <summary>
        /// Update highlight on valid drop zones
        /// </summary>
        private void UpdateDropZoneHighlight(PointerEventData eventData)
        {
            // TODO: Implement highlight logic for valid drop zones
            // This would highlight slots that can accept the dragged item
        }

        /// <summary>
        /// Cleanup drag operation
        /// </summary>
        private void CleanupDrag()
        {
            if (currentDragIcon != null)
            {
                Destroy(currentDragIcon);
                currentDragIcon = null;
            }

            draggedSlot = null;
            draggedQuickSlot = null;
            draggedWeaponSlot = null;
            draggedEquipmentSlot = null;
            isDragging = false;

            // Clear all highlights
            ClearDropZoneHighlights();
        }

        /// <summary>
        /// Clear all drop zone highlights
        /// </summary>
        private void ClearDropZoneHighlights()
        {
            // TODO: Clear highlights on all slots
        }

        /// <summary>
        /// Check if currently dragging
        /// </summary>
        public bool IsDragging() => isDragging;
    }
}
