using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using NightHunt.Gameplay.Inventory;

namespace NightHunt.Gameplay.UI
{
    /// <summary>
    /// Handles drag and drop operations for inventory items
    /// Provides visual feedback and drop validation
    /// Unified to work with ItemCell for all slot types
    /// </summary>
    public class DragDropHandler : MonoBehaviour
    {
        [Header("Drag Visual")]
        [SerializeField] private ItemCell dragCell; // Reference to ItemCell for drag visual (set in Inspector)
        [SerializeField] private Canvas dragCanvas;

        private ItemCell draggedCell; // Unified: thay thế draggedSlot, draggedQuickSlot, etc.
        private InventorySlot draggedData; // Store data đang được drag
        private bool isDragging = false;
        private InventoryPanel inventoryPanel;
        
        // For improved UX: Store reference to original slot icon to restore after drag
        private Image originalSlotIcon; // Original slot's icon image (to disable/enable)
        private bool originalIconWasEnabled = false; // Track if original icon was enabled

        [Header("Events")]
        public UnityEngine.Events.UnityEvent<ItemCell, ItemCell, InventorySlot> OnItemMoved;
        public UnityEngine.Events.UnityEvent<ItemCell, ItemCell, InventorySlot, InventorySlot> OnItemSwapped;
        public UnityEngine.Events.UnityEvent<ItemCell> OnDragStarted;
        public UnityEngine.Events.UnityEvent<ItemCell> OnDragEnded;
        public UnityEngine.Events.UnityEvent<ItemCell, bool> OnDropZoneHighlight; // cell, isValid

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
        /// Start drag operation từ bất kỳ ItemCell nào
        /// Store data và clear tạm thời source cell
        /// </summary>
        public void StartDrag(ItemCell cell, PointerEventData eventData)
        {
            if (cell == null || cell.IsEmpty())
                return;

            // Store data đang được drag
            draggedData = cell.GetSlot();
            if (draggedData == null || draggedData.IsEmpty)
                return;

            draggedCell = cell;
            isDragging = true;

            // Clear tạm thời source cell (UI hiển thị empty)
            cell.ClearSlot();

            // Store original slot icon reference and disable it for better UX
            originalSlotIcon = cell.GetComponentInChildren<Image>();
            if (originalSlotIcon != null)
            {
                originalIconWasEnabled = originalSlotIcon.enabled;
                originalSlotIcon.enabled = false; // Hide original icon while dragging
                Debug.Log("[DragDropHandler] Disabled original cell icon for drag");
            }

            // Setup drag cell với data từ source cell
            SetupDragCell(draggedData);

            // Fire event
            OnDragStarted?.Invoke(cell);
        }

        /// <summary>
        /// Update drag operation
        /// </summary>
        public void UpdateDrag(PointerEventData eventData)
        {
            if (!isDragging || dragCell == null || !dragCell.gameObject.activeSelf)
                return;

            // Update drag cell position to follow mouse
            RectTransform dragCellRect = dragCell.GetComponent<RectTransform>();
            if (dragCellRect != null && dragCanvas != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dragCanvas.transform as RectTransform,
                    eventData.position,
                    dragCanvas.worldCamera,
                    out Vector2 localPoint);

                dragCellRect.anchoredPosition = localPoint;
            }

            // Update highlight on valid drop zones
            UpdateDropZoneHighlight(eventData);
        }

        /// <summary>
        /// End drag operation
        /// </summary>
        public void EndDrag(ItemCell cell, PointerEventData eventData)
        {
            if (!isDragging)
                return;

            GameObject dropTarget = FindDropTarget(eventData);
            if (dropTarget != null)
            {
                HandleDrop(cell, dropTarget);
            }
            else
                    {
                Debug.LogWarning("[DragDropHandler] No drop target found!");
                    }
                    
            // Cleanup
            CleanupDrag();
        }

        /// <summary>
        /// End drag operation from any cell type
        /// </summary>
        public void EndDragAny(PointerEventData eventData)
        {
            if (!isDragging || draggedCell == null)
                return;

            GameObject dropTarget = FindDropTarget(eventData);
            if (dropTarget != null)
            {
                HandleDrop(draggedCell, dropTarget);
            }
            else
            {
                Debug.LogWarning("[DragDropHandler] No drop target found in EndDragAny!");
            }

            // Cleanup
            CleanupDrag();
        }

        /// <summary>
        /// Find drop target, skipping drag icon
        /// </summary>
        private GameObject FindDropTarget(PointerEventData eventData)
        {
            GameObject dropTarget = null;
            
            // Use RaycastAll to get all objects under mouse, then filter out drag icon
            var eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                PointerEventData pointerData = new PointerEventData(eventSystem)
                {
                    position = eventData.position
                };
                var results = new System.Collections.Generic.List<RaycastResult>();
                eventSystem.RaycastAll(pointerData, results);
                
                // Find first object that is NOT the drag icon
                foreach (var result in results)
                {
                    // Skip if this is the drag cell itself
                    if (dragCell != null && dragCell.gameObject.activeSelf && (result.gameObject == dragCell.gameObject || result.gameObject.transform.IsChildOf(dragCell.transform)))
                    {
                        continue;
                    }
                    
                    // Found valid drop target
                    dropTarget = result.gameObject;
                    Debug.Log($"[DragDropHandler] Found drop target: {dropTarget.name}");
                    break;
                }
            }
            
            // Fallback: Use pointerCurrentRaycast if RaycastAll didn't work
            if (dropTarget == null && eventData.pointerCurrentRaycast.gameObject != null)
            {
                var candidate = eventData.pointerCurrentRaycast.gameObject;
                if (dragCell == null || !dragCell.gameObject.activeSelf || (candidate != dragCell.gameObject && !candidate.transform.IsChildOf(dragCell.transform)))
                {
                    dropTarget = candidate;
                }
            }

            return dropTarget;
        }

        /// <summary>
        /// Handle drop operation - unified logic cho tất cả cell types
        /// Swap data nếu target có item, move nếu target empty
        /// </summary>
        private void HandleDrop(ItemCell sourceCell, GameObject dropTarget)
        {
            if (sourceCell == null || dropTarget == null || draggedData == null || draggedData.IsEmpty)
            {
                Debug.LogWarning($"[DragDropHandler] HandleDrop: sourceCell={sourceCell != null}, dropTarget={dropTarget != null}, draggedData={draggedData != null}");
                // Restore data to source cell if drop failed
                if (sourceCell != null && draggedData != null)
                {
                    sourceCell.SetSlot(draggedData);
                }
                return;
            }

            Debug.Log($"[DragDropHandler] HandleDrop: sourceCell={sourceCell.name}, dropTarget={dropTarget.name}");

            // Find target cell
            ItemCell targetCell = dropTarget.GetComponent<ItemCell>();
            if (targetCell == null)
            {
                // Check parent hierarchy
                Transform current = dropTarget.transform;
                for (int i = 0; i < 5 && current != null; i++)
                {
                    targetCell = current.GetComponent<ItemCell>();
                    if (targetCell != null) break;
                    current = current.parent;
                }
            }

            // Check for TrashSlotUI
            TrashSlotUI trashSlot = dropTarget.GetComponent<TrashSlotUI>();
            if (trashSlot == null)
            {
                Transform current = dropTarget.transform;
                for (int i = 0; i < 5 && current != null; i++)
                {
                        trashSlot = current.GetComponent<TrashSlotUI>();
                    if (trashSlot != null) break;
                    current = current.parent;
                }
            }

            if (trashSlot != null)
            {
                // Drop on trash slot - restore data first
                sourceCell.SetSlot(draggedData);
                Debug.Log($"[DragDropHandler] Dropping on trash slot");
                trashSlot.DropItem(sourceCell);
                return;
            }

            if (targetCell == null)
                                {
                Debug.LogWarning($"[DragDropHandler] Drop target is not a valid ItemCell or TrashSlot!");
                // Restore data to source cell
                sourceCell.SetSlot(draggedData);
                return;
            }

            // Validate: Check if item can be dropped to target cell
            if (!CanDropItemToCell(draggedData, targetCell))
                            {
                Debug.LogWarning($"[DragDropHandler] Cannot drop item to target cell - validation failed");
                // Restore data to source cell
                sourceCell.SetSlot(draggedData);
                return;
            }

            // Handle drop: Swap if target has item, Move if target is empty
            if (targetCell.IsEmpty())
            {
                // MOVE: Gán data vào target
                targetCell.SetSlot(draggedData);
                // sourceCell đã empty rồi (từ StartDrag)
                OnItemMoved?.Invoke(sourceCell, targetCell, draggedData);
                Debug.Log($"[DragDropHandler] Moved item from {sourceCell.GetLocation()} to {targetCell.GetLocation()}");
                    }
                    else
                    {
                // SWAP: Swap data
                var targetData = targetCell.GetSlot();
                targetCell.SetSlot(draggedData);
                sourceCell.SetSlot(targetData);
                OnItemSwapped?.Invoke(sourceCell, targetCell, draggedData, targetData);
                Debug.Log($"[DragDropHandler] Swapped items between {sourceCell.GetLocation()} and {targetCell.GetLocation()}");
            }

            // Refresh UI
            sourceCell.UpdateDisplay();
            targetCell.UpdateDisplay();

            // Fire network events
            FireNetworkEvents(sourceCell, targetCell, draggedData);

            // Clear dragged data
            draggedData = null;
        }

        /// <summary>
        /// Check if can drop from source location to target location
        /// </summary>
        private bool CanDropToLocation(ItemCellLocation source, ItemCellLocation target)
        {
            // Define compatibility rules
            switch (target)
            {
                case ItemCellLocation.Inventory:
                    return source == ItemCellLocation.Container || source == ItemCellLocation.Inventory || 
                           source == ItemCellLocation.QuickSlot || source == ItemCellLocation.Weapon || 
                           source == ItemCellLocation.Equipment || source == ItemCellLocation.Attachment;
                case ItemCellLocation.Container:
                    return source == ItemCellLocation.Inventory;
                case ItemCellLocation.QuickSlot:
                    return source == ItemCellLocation.Inventory || source == ItemCellLocation.QuickSlot;
                case ItemCellLocation.Weapon:
                    return source == ItemCellLocation.Inventory || source == ItemCellLocation.Weapon;
                case ItemCellLocation.Equipment:
                    return source == ItemCellLocation.Inventory || source == ItemCellLocation.Equipment;
                case ItemCellLocation.Attachment:
                    return source == ItemCellLocation.Inventory || source == ItemCellLocation.Attachment;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Check if item can be dropped to target cell (validation với item data)
        /// </summary>
        private bool CanDropItemToCell(InventorySlot itemSlot, ItemCell targetCell)
        {
            if (itemSlot == null || itemSlot.IsEmpty || targetCell == null)
                return false;

            // Get item data from registry
            var itemData = GetItemDataFromRegistry(itemSlot.Item.ItemId);
            if (itemData == null)
                return false;

            // Event items: chỉ vào Inventory hoặc Container
            if (IsEventItem(itemData))
            {
                var location = targetCell.GetLocation();
                return location == ItemCellLocation.Inventory || location == ItemCellLocation.Container;
            }

            // Validation theo location
            var targetLocation = targetCell.GetLocation();
            switch (targetLocation)
            {
                case ItemCellLocation.Inventory:
                case ItemCellLocation.Container:
                    return true; // Tất cả items có thể vào inventory/container
                
                case ItemCellLocation.QuickSlot:
                    return itemData.Category == NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Consumable || 
                           itemData.Category == NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Ammo;
                
                case ItemCellLocation.Weapon:
                    return itemData.Category == NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Weapon;
                
                case ItemCellLocation.Equipment:
                    return CanEquipToSlotType(itemData.Category, targetCell.GetEquipmentSlotType());
                
                case ItemCellLocation.Attachment:
                    return itemData.Category == NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Attachment;
                
                default:
                    return false;
            }
        }

        /// <summary>
        /// Check if item is an event item (chỉ có thể vào inventory/container)
        /// </summary>
        private bool IsEventItem(NightHunt.InteractionSystem.Core.Abstractions.ItemDataBase itemData)
        {
            if (itemData == null)
                return false;

            // Dùng category: Misc category có thể là event items
            // Có thể mở rộng sau với pattern hoặc flag riêng
            return itemData.Category == NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Misc && 
                   (itemData.ItemId.StartsWith("event_") || itemData.ItemId.Contains("_event"));
                }

        /// <summary>
        /// Check if item category can be equipped to equipment slot type
        /// </summary>
        private bool CanEquipToSlotType(NightHunt.InteractionSystem.Core.Abstractions.ItemCategory category, EquipmentSlotType slotType)
        {
            return (slotType == EquipmentSlotType.Armor && category == NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Armor) ||
                   (slotType == EquipmentSlotType.Helmet && category == NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Helmet) ||
                   (slotType == EquipmentSlotType.Vest && category == NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Armor) || // TODO: Check if Vest is separate category
                   (slotType == EquipmentSlotType.Backpack && category == NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Backpack);
        }

        /// <summary>
        /// Get ItemDataBase from ItemDataRegistry
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

        /// <summary>
        /// Handle swap or move within same location type
        /// </summary>
        private void HandleSwapOrMove(ItemCell sourceCell, ItemCell targetCell)
        {
            var sourceSlot = sourceCell.GetSlot();
            var targetSlot = targetCell.GetSlot();

            if (targetSlot == null || targetSlot.IsEmpty)
            {
                // Target is empty - move item
                targetCell.SetSlot(sourceSlot);
                sourceCell.ClearSlot();
            }
            else
            {
                // Both have items - swap
                var temp = targetSlot;
                targetCell.SetSlot(sourceSlot);
                sourceCell.SetSlot(temp);
            }

            // Refresh UI
            sourceCell.UpdateDisplay();
            targetCell.UpdateDisplay();

            // Fire events based on location
            var location = sourceCell.GetLocation();
            FireMoveEvents(location, location, sourceSlot);
        }

        /// <summary>
        /// Handle move between different locations
        /// </summary>
        private void HandleMoveBetweenLocations(ItemCell sourceCell, ItemCell targetCell)
        {
            var sourceSlot = sourceCell.GetSlot();
            var sourceLocation = sourceCell.GetLocation();
            var targetLocation = targetCell.GetLocation();

            // Special handling for container <-> inventory
            if ((sourceLocation == ItemCellLocation.Container && targetLocation == ItemCellLocation.Inventory) ||
                (sourceLocation == ItemCellLocation.Inventory && targetLocation == ItemCellLocation.Container))
            {
                HandleContainerInventoryMove(sourceCell, targetCell);
                return;
            }

            // Special handling for weapon/equipment slots
            if (targetLocation == ItemCellLocation.Weapon || targetLocation == ItemCellLocation.Equipment)
            {
                HandleEquipmentMove(sourceCell, targetCell);
                return;
            }

            // Default: Clear source and set target (if empty) or swap
            if (targetCell.IsEmpty())
            {
                targetCell.SetSlot(sourceSlot);
                sourceCell.ClearSlot();
                }
                else
                {
                // Swap: move target item to source location
                var targetSlot = targetCell.GetSlot();
                targetCell.SetSlot(sourceSlot);
                sourceCell.SetSlot(targetSlot);
                }

            // Refresh UI
            sourceCell.UpdateDisplay();
            targetCell.UpdateDisplay();

            // Fire events
            FireMoveEvents(sourceLocation, targetLocation, sourceSlot);
        }

        /// <summary>
        /// Handle move between container and inventory
        /// </summary>
        private void HandleContainerInventoryMove(ItemCell sourceCell, ItemCell targetCell)
        {
            var sourceSlot = sourceCell.GetSlot();
            var sourceLocation = sourceCell.GetLocation();
            var targetLocation = targetCell.GetLocation();
            var lootPanel = inventoryPanel?.GetLootContainerPanel();

            if (lootPanel == null || !lootPanel.IsContainerLoaded())
            {
                Debug.LogWarning("[DragDropHandler] Cannot move between container and inventory - container not loaded");
                return;
            }

            if (sourceLocation == ItemCellLocation.Inventory && targetLocation == ItemCellLocation.Container)
            {
                // Move from inventory to container
                if (lootPanel.CanAddItems())
                {
                    Debug.Log($"[DragDropHandler] Moving item from inventory to container");
                    // Note: We need grid position for container, but ItemCell doesn't have it
                    // This will need to be handled by InventoryPanel/LootContainerPanel
                    inventoryPanel?.MoveItemToContainer(sourceCell, targetCell);
                }
            }
            else if (sourceLocation == ItemCellLocation.Container && targetLocation == ItemCellLocation.Inventory)
            {
                // Move from container to inventory
                if (lootPanel.CanRemoveItems())
                {
                    Debug.Log($"[DragDropHandler] Moving item from container to inventory");
                    inventoryPanel?.MoveItemFromContainer(sourceCell, targetCell);
                }
            }

            // Refresh UI after move (ServerRpc is async)
            StartCoroutine(RefreshUIAfterMove());
        }

        /// <summary>
        /// Handle move to/from equipment/weapon slots
        /// </summary>
        private void HandleEquipmentMove(ItemCell sourceCell, ItemCell targetCell)
        {
            var sourceSlot = sourceCell.GetSlot();
            var targetLocation = targetCell.GetLocation();
                    
            if (targetLocation == ItemCellLocation.Weapon)
                    {
                // Equip weapon
                int weaponSlotIndex = targetCell.GetCellIndex();
                if (targetCell.IsEmpty())
                {
                    // Target is empty - equip weapon
                    inventoryPanel?.EquipWeapon(sourceCell, weaponSlotIndex);
                    }
                    else
                    {
                    // Target has weapon - swap
                    inventoryPanel?.SwapWeapon(sourceCell, weaponSlotIndex);
                }
            }
            else if (targetLocation == ItemCellLocation.Equipment)
            {
                // Equip equipment
                EquipmentSlotType slotType = targetCell.GetEquipmentSlotType();
                if (targetCell.IsEmpty())
                {
                    // Target is empty - equip item
                    inventoryPanel?.EquipItem(sourceCell, targetCell);
                }
                else
                {
                    // Target has item - swap
                    inventoryPanel?.SwapEquipment(sourceCell, targetCell);
                }
            }
        }

        /// <summary>
        /// Fire move events based on locations
        /// </summary>
        private void FireMoveEvents(ItemCellLocation sourceLocation, ItemCellLocation targetLocation, InventorySlot sourceSlot)
            {
            // TODO: Fire appropriate events based on locations
            // This will be handled by InventoryPanel methods
        }

        /// <summary>
        /// Fire network events sau khi swap/move thành công
        /// </summary>
        private void FireNetworkEvents(ItemCell sourceCell, ItemCell targetCell, InventorySlot movedData)
        {
            if (movedData == null || movedData.IsEmpty)
                return;

            var sourceLocation = sourceCell.GetLocation();
            var targetLocation = targetCell.GetLocation();

            // Fire appropriate InventoryUIEvents based on locations
            // Note: Network events will be fired by InventoryPanel methods for equipment/weapon slots
            // For inventory-to-inventory moves, events are already fired in HandleDrop
            // This method is mainly for logging/debugging
            Debug.Log($"[DragDropHandler] FireNetworkEvents: {sourceLocation} -> {targetLocation}, Item: {movedData.Item.ItemId}");
        }

        /// <summary>
        /// Setup drag cell với data từ InventorySlot
        /// Copy data và hiển thị drag cell
        /// </summary>
        private void SetupDragCell(InventorySlot slot)
        {
            if (slot == null || slot.IsEmpty || dragCell == null || dragCanvas == null)
            {
                Debug.LogWarning("[DragDropHandler] Cannot setup drag cell - slot is empty or dragCell is null");
                return;
            }

            // Set data vào drag cell (copy từ source slot)
            dragCell.SetSlot(slot);
            dragCell.UpdateDisplay();

            // Ensure drag cell is in correct canvas and active
            if (dragCell.transform.parent != dragCanvas.transform)
            {
                dragCell.transform.SetParent(dragCanvas.transform, false);
            }
            
            dragCell.gameObject.SetActive(true);
            dragCell.transform.SetAsLastSibling(); // Show on top

            // Set initial position (will be updated by UpdateDrag)
            RectTransform dragCellRect = dragCell.GetComponent<RectTransform>();
            if (dragCellRect != null)
            {
                dragCellRect.anchoredPosition = Vector2.zero;
            }

            Debug.Log($"[DragDropHandler] Setup drag cell with item: {slot.Item.ItemId}");
        }

        /// <summary>
        /// Update highlight on valid drop zones
        /// </summary>
        private void UpdateDropZoneHighlight(PointerEventData eventData)
        {
            if (!isDragging || dragCell == null || !dragCell.gameObject.activeSelf || draggedData == null || draggedData.IsEmpty)
                return;

            // Find all ItemCells under mouse
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
                return;

            PointerEventData pointerData = new PointerEventData(eventSystem)
            {
                position = eventData.position
            };
            var results = new System.Collections.Generic.List<RaycastResult>();
            eventSystem.RaycastAll(pointerData, results);

            // Clear previous highlights
            ClearDropZoneHighlights();

            // Highlight valid/invalid drop zones
            foreach (var result in results)
            {
                // Skip if this is the drag cell itself
                if (dragCell != null && dragCell.gameObject.activeSelf && (result.gameObject == dragCell.gameObject || result.gameObject.transform.IsChildOf(dragCell.transform)))
                {
                    continue;
                }

                ItemCell targetCell = result.gameObject.GetComponent<ItemCell>();
                if (targetCell == null)
                {
                    // Check parent hierarchy
                    Transform current = result.gameObject.transform;
                    for (int i = 0; i < 5 && current != null; i++)
                {
                        targetCell = current.GetComponent<ItemCell>();
                        if (targetCell != null) break;
                        current = current.parent;
                    }
                }

                if (targetCell != null && targetCell != draggedCell)
                    {
                    // Check if can drop to this cell
                    bool isValid = CanDropItemToCell(draggedData, targetCell);
                    
                    // Visual feedback: Change background color or add outline
                    Image cellBackground = targetCell.GetComponent<Image>();
                    if (cellBackground == null)
                        {
                        cellBackground = targetCell.GetComponentInChildren<Image>();
                    }

                    if (cellBackground != null)
                    {
                        // Set color based on validity (green for valid, red for invalid)
                        cellBackground.color = isValid ? new Color(0.5f, 1f, 0.5f, 0.5f) : new Color(1f, 0.5f, 0.5f, 0.5f);
                    }

                    // Fire event
                    OnDropZoneHighlight?.Invoke(targetCell, isValid);
                }
            }
        }

        /// <summary>
        /// Cleanup drag operation
        /// </summary>
        private void CleanupDrag()
        {
            // Restore original slot icon (re-enable it)
            if (originalSlotIcon != null)
            {
                originalSlotIcon.enabled = originalIconWasEnabled;
                originalSlotIcon = null;
                originalIconWasEnabled = false;
                Debug.Log("[DragDropHandler] Restored original cell icon after drag");
            }

            // Hide drag cell instead of destroying
            if (dragCell != null)
            {
                dragCell.ClearSlot();
                dragCell.gameObject.SetActive(false);
                Debug.Log("[DragDropHandler] Hidden drag cell after drag");
            }

            // Restore data to source cell if drag was cancelled (no valid drop)
            if (draggedCell != null && draggedData != null && !draggedData.IsEmpty)
            {
                draggedCell.SetSlot(draggedData);
                draggedCell.UpdateDisplay();
                Debug.Log("[DragDropHandler] Restored data to source cell after drag cancelled");
            }

            // Fire event
            if (draggedCell != null)
            {
                OnDragEnded?.Invoke(draggedCell);
            }

            draggedCell = null;
            draggedData = null;
            isDragging = false;

            // Clear all highlights
            ClearDropZoneHighlights();
        }

        /// <summary>
        /// Clear all drop zone highlights
        /// </summary>
        private void ClearDropZoneHighlights()
        {
            // Find all ItemCells in scene and reset their colors
            ItemCell[] allCells = FindObjectsByType<ItemCell>(FindObjectsSortMode.None);
            foreach (var cell in allCells)
            {
                if (cell != null)
                {
                    Image cellBackground = cell.GetComponent<Image>();
                    if (cellBackground == null)
                    {
                        cellBackground = cell.GetComponentInChildren<Image>();
                    }

                    if (cellBackground != null)
                    {
                        // Reset to default color (white/transparent)
                        cellBackground.color = Color.white;
                    }
                }
            }
        }

        /// <summary>
        /// Check if currently dragging
        /// </summary>
        public bool IsDragging() => isDragging;

        /// <summary>
        /// Refresh UI after moving items between container and inventory
        /// ServerRpc is async, so we need to wait a bit for server to process and sync
        /// </summary>
        private IEnumerator RefreshUIAfterMove()
        {
            // Wait a bit for ServerRpc to process and sync
            yield return new WaitForSeconds(0.1f);
            
            // Refresh inventory panel
            if (inventoryPanel != null)
            {
                inventoryPanel.RefreshInventoryGrid();
            }
            
            // Refresh loot container panel
            var lootPanel = inventoryPanel?.GetLootContainerPanel();
            if (lootPanel != null && lootPanel.IsContainerLoaded())
            {
                lootPanel.RefreshLootGrid();
            }
            
            Debug.Log("[DragDropHandler] Refreshed UI after move operation");
        }
    }
}
