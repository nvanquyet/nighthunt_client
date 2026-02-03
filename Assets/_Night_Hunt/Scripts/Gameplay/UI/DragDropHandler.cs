using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using NightHunt.Gameplay.Core;
using NightHunt.Gameplay.Inventory;
using NightHunt.Gameplay.Inventory.Events;
using NightHunt.Gameplay.Inventory.Logic.Sync;
using NightHunt.InteractionSystem.Utilities;

namespace NightHunt.Gameplay.UI
{
    /// <summary>
    /// Handles drag and drop operations for inventory items
    /// Provides visual feedback and drop validation
    /// Unified to work with ItemCell for all slot types
    /// </summary>
    public class DragDropHandler : MonoBehaviour
    {
        [Header("Drag Visual")] [SerializeField]
        private ItemCell dragCell; // Reference to ItemCell for drag visual (set in Inspector)

        [SerializeField] private Canvas dragCanvas;

        private ItemCell draggedCell; // Unified: thay thế draggedSlot, draggedQuickSlot, etc.
        private InventorySlot draggedData; // Store data đang được drag
        private bool isDragging = false;
        private InventoryPanel inventoryPanel;
        private NightHunt.Gameplay.Inventory.Logic.Prediction.InventoryUIPrediction predictionSystem;

        // For improved UX: Store reference to original slot icon to restore after drag
        private Image originalSlotIcon; // Original slot's icon image (to disable/enable)
        private bool originalIconWasEnabled = false; // Track if original icon was enabled

        // For rollback: Store state before operation
        private InventorySlot sourceSlotBeforeOperation;
        private InventorySlot targetSlotBeforeOperation;
        private ItemCell sourceCellBeforeOperation;
        private ItemCell targetCellBeforeOperation;

        [Header("Events")] public UnityEngine.Events.UnityEvent<ItemCell, ItemCell, InventorySlot> OnItemMoved;
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

            // Find prediction system
            if (panel != null)
            {
                predictionSystem =
                    panel.GetComponent<NightHunt.Gameplay.Inventory.Logic.Prediction.InventoryUIPrediction>();
                if (predictionSystem == null)
                {
                    predictionSystem =
                        panel.GetComponentInParent<
                            NightHunt.Gameplay.Inventory.Logic.Prediction.InventoryUIPrediction>();
                }
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

            // #region agent log
            long timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var sourceLoc = cell != null ? cell.GetLocation().ToString() : "null";
            var sourceCellName = cell != null ? cell.name : "null";
            System.IO.File.AppendAllText(@"w:\Unity\Shotter\.cursor\debug.log", 
                $"{{\"id\":\"log_{timestamp}_drag\",\"timestamp\":{timestamp},\"location\":\"DragDropHandler.cs:StartDrag\",\"message\":\"StartDrag called\",\"data\":{{\"itemId\":\"{draggedData.Item.ItemId}\",\"sourceLocation\":\"{sourceLoc}\",\"sourceCellName\":\"{sourceCellName}\"}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"E\"}}\n");
            // #endregion

            // Clear tạm thời source cell (UI hiển thị empty)
            cell.ClearSlot();
            cell.UpdateDisplay(); // Ensure UI updates to show empty

            // Find item icon specifically (not background)
            originalSlotIcon = null;
            if (cell != null)
            {
                // Try to find item icon image component
                var images = cell.GetComponentsInChildren<Image>();
                foreach (var img in images)
                {
                    // Item icon is usually the one that shows the item sprite
                    if (img.name.ToLower().Contains("icon") || img.name.ToLower().Contains("item"))
                    {
                        originalSlotIcon = img;
                        break;
                    }
                }
                // Fallback: use first image that's not background
                if (originalSlotIcon == null && images.Length > 0)
                {
                    foreach (var img in images)
                    {
                        if (img.name.ToLower().Contains("background") == false)
                        {
                            originalSlotIcon = img;
                            break;
                        }
                    }
                }
            }

            if (originalSlotIcon != null)
            {
                originalIconWasEnabled = originalSlotIcon.enabled;
                originalSlotIcon.enabled = false; // Hide original icon while dragging
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

            // #region agent log
            long timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            System.IO.File.AppendAllText(@"w:\Unity\Shotter\.cursor\debug.log", 
                $"{{\"id\":\"log_{timestamp}_drag\",\"timestamp\":{timestamp},\"location\":\"DragDropHandler.cs:EndDrag\",\"message\":\"EndDrag called\",\"data\":{{\"draggedData\":{draggedData != null},\"isDragging\":{isDragging}}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"C\"}}\n");
            // #endregion

            GameObject dropTarget = FindDropTarget(eventData);
            bool dropHandled = false;
            
            if (dropTarget != null)
            {
                // #region agent log
                long timestamp2 = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                System.IO.File.AppendAllText(@"w:\Unity\Shotter\.cursor\debug.log", 
                    $"{{\"id\":\"log_{timestamp2}_drag\",\"timestamp\":{timestamp2},\"location\":\"DragDropHandler.cs:EndDrag\",\"message\":\"Drop target found\",\"data\":{{\"dropTarget\":\"{dropTarget.name}\"}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"C\"}}\n");
                // #endregion

                HandleDrop(cell, dropTarget);
                dropHandled = true;
            }
            else
            {
                // #region agent log
                long timestamp3 = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                System.IO.File.AppendAllText(@"w:\Unity\Shotter\.cursor\debug.log", 
                    $"{{\"id\":\"log_{timestamp3}_drag\",\"timestamp\":{timestamp3},\"location\":\"DragDropHandler.cs:EndDrag\",\"message\":\"No drop target found\",\"data\":{{}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"C\"}}\n");
                // #endregion
            }

            // Cleanup - always restore source cell if drop failed or wasn't handled
            CleanupDrag(dropHandled);
        }

        /// <summary>
        /// End drag operation from any cell type
        /// </summary>
        public void EndDragAny(PointerEventData eventData)
        {
            if (!isDragging || draggedCell == null)
                return;

            GameObject dropTarget = FindDropTarget(eventData);
            bool dropHandled = false;
            
            if (dropTarget != null)
            {
                HandleDrop(draggedCell, dropTarget);
                dropHandled = true;
            }

            // Cleanup
            CleanupDrag(dropHandled);
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

                // Prefer specific targets over generic inventory cells.
                // Priority: Trash > Attachment/Equipment/Weapon/QuickSlot > Container > Inventory > anything else
                int bestScore = int.MinValue;
                GameObject bestTarget = null;

                int debugCount = 0;
                foreach (var result in results)
                {
                    var go = result.gameObject;
                    if (go == null) continue;

                    // Skip if this is the drag cell itself
                    if (dragCell != null && dragCell.gameObject.activeSelf &&
                        (go == dragCell.gameObject || go.transform.IsChildOf(dragCell.transform)))
                    {
                        continue;
                    }

                    // Highest priority: Trash slot anywhere in parents
                    var trash = go.GetComponentInParent<TrashSlotUI>();
                    if (trash != null)
                    {
                        bestTarget = trash.gameObject;
                        bestScore = 1000;
                        break;
                    }

                    // Next: ItemCell (could be on self/parent/child)
                    ItemCell cell = go.GetComponent<ItemCell>();
                    if (cell == null) cell = go.GetComponentInParent<ItemCell>();
                    if (cell == null) cell = go.GetComponentInChildren<ItemCell>();

                    int score = 0;
                    string locStr = "None";
                    if (cell != null)
                    {
                        var loc = cell.GetLocation();
                        locStr = loc.ToString();
                        switch (loc)
                        {
                            case ItemCellLocation.Attachment: score = 900; break;
                            case ItemCellLocation.Equipment: score = 850; break;
                            case ItemCellLocation.Weapon: score = 800; break;
                            case ItemCellLocation.QuickSlot: score = 750; break;
                            case ItemCellLocation.Container: score = 600; break;
                            case ItemCellLocation.Inventory: score = 100; break;
                            default: score = 0; break;
                        }
                    }

                    // #region agent log
                    if (debugCount < 6)
                    {
                        debugCount++;
                        long ts = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        System.IO.File.AppendAllText(@"w:\Unity\Shotter\.cursor\debug.log",
                            $"{{\"id\":\"log_{ts}_ray\",\"timestamp\":{ts},\"location\":\"DragDropHandler.cs:FindDropTarget\",\"message\":\"Raycast candidate\",\"data\":{{\"name\":\"{go.name}\",\"hasItemCell\":{(cell!=null).ToString().ToLower()},\"loc\":\"{locStr}\",\"score\":{score}}},\"sessionId\":\"debug-session\",\"runId\":\"run3\",\"hypothesisId\":\"H3\"}}\n");
                    }
                    // #endregion

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestTarget = cell != null ? cell.gameObject : go;
                    }
                }

                dropTarget = bestTarget;
                if (dropTarget != null)
                {
                    // #region agent log
                    try
                    {
                        ItemCell chosenCell = dropTarget.GetComponent<ItemCell>();
                        if (chosenCell == null) chosenCell = dropTarget.GetComponentInParent<ItemCell>();
                        if (chosenCell == null) chosenCell = dropTarget.GetComponentInChildren<ItemCell>();
                        string chosenLoc = chosenCell != null ? chosenCell.GetLocation().ToString() : "None";
                        long ts = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        System.IO.File.AppendAllText(@"w:\Unity\Shotter\.cursor\debug.log",
                            $"{{\"id\":\"log_{ts}_ray\",\"timestamp\":{ts},\"location\":\"DragDropHandler.cs:FindDropTarget\",\"message\":\"Chosen dropTarget\",\"data\":{{\"dropTargetName\":\"{dropTarget.name}\",\"chosenHasItemCell\":{(chosenCell!=null).ToString().ToLower()},\"chosenLocation\":\"{chosenLoc}\"}},\"sessionId\":\"debug-session\",\"runId\":\"run7\",\"hypothesisId\":\"CELL_LOC\"}}\n");
                    }
                    catch { /* ignore */ }
                    // #endregion

                    Debug.Log($"[DragDropHandler] Found drop target: {dropTarget.name}");
                }
            }

            // Fallback: Use pointerCurrentRaycast if RaycastAll didn't work
            if (dropTarget == null && eventData.pointerCurrentRaycast.gameObject != null)
            {
                var candidate = eventData.pointerCurrentRaycast.gameObject;
                if (dragCell == null || !dragCell.gameObject.activeSelf || (candidate != dragCell.gameObject &&
                                                                            !candidate.transform.IsChildOf(
                                                                                dragCell.transform)))
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
                Debug.LogWarning(
                    $"[DragDropHandler] HandleDrop: sourceCell={sourceCell != null}, dropTarget={dropTarget != null}, draggedData={draggedData != null}");
                // Restore data to source cell if drop failed
                if (sourceCell != null && draggedData != null)
                {
                    sourceCell.SetSlot(draggedData);
                }

                return;
            }


            // Find target cell - check dropTarget itself, then children, then parent hierarchy
            // All cells are ItemCell, only differ by ItemCellLocation
            ItemCell targetCell = dropTarget.GetComponent<ItemCell>();
            
            // If not found on dropTarget, check children (equipment/weapon cells might be children)
            if (targetCell == null)
            {
                targetCell = dropTarget.GetComponentInChildren<ItemCell>();
            }
            Debug.Log($"[DragDropHandler] HandleDrop: sourceCell={sourceCell.GetCellLocation()}, dropTarget={targetCell.GetCellLocation()}");
            
            // If still not found, check parent hierarchy
            if (targetCell == null)
            {
                Transform current = dropTarget.transform;
                for (int i = 0; i < 5 && current != null; i++)
                {
                    targetCell = current.GetComponent<ItemCell>();
                    if (targetCell != null) break; 
                    current = current.parent;
                }
            }
            
            // #region agent log
            if (targetCell != null)
            {
                long timestamp14 = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                System.IO.File.AppendAllText(@"w:\Unity\Shotter\.cursor\debug.log", 
                    $"{{\"id\":\"log_{timestamp14}_drag\",\"timestamp\":{timestamp14},\"location\":\"DragDropHandler.cs:HandleDrop\",\"message\":\"Found target cell\",\"data\":{{\"targetCellName\":\"{targetCell.name}\",\"targetLocation\":\"{targetCell.GetLocation()}\",\"sourceCellName\":\"{sourceCell.name}\",\"sourceLocation\":\"{sourceCell.GetLocation()}\",\"dropTargetName\":\"{dropTarget.name}\"}},\"sessionId\":\"debug-session\",\"runId\":\"run7\",\"hypothesisId\":\"CELL_LOC\"}}\n");
            }
            // #endregion

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
                // #region agent log
                long timestamp5 = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                System.IO.File.AppendAllText(@"w:\Unity\Shotter\.cursor\debug.log", 
                    $"{{\"id\":\"log_{timestamp5}_drag\",\"timestamp\":{timestamp5},\"location\":\"DragDropHandler.cs:HandleDrop\",\"message\":\"Dropping on trash slot\",\"data\":{{\"itemId\":\"{draggedData.Item.ItemId}\"}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"C\"}}\n");
                // #endregion

                // Drop on trash slot - restore data first, then drop
                sourceCell.SetSlot(draggedData);
                sourceCell.UpdateDisplay();
                trashSlot.DropItem(sourceCell);
                // Clear draggedData - drop handled
                draggedData = null;
                return;
            }

            if (targetCell == null)
            {
                // #region agent log
                long timestamp4 = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                System.IO.File.AppendAllText(@"w:\Unity\Shotter\.cursor\debug.log", 
                    $"{{\"id\":\"log_{timestamp4}_drag\",\"timestamp\":{timestamp4},\"location\":\"DragDropHandler.cs:HandleDrop\",\"message\":\"Target cell is null\",\"data\":{{}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"C\"}}\n");
                // #endregion

                // Restore data to source cell - invalid target
                sourceCell.SetSlot(draggedData);
                sourceCell.UpdateDisplay();
                // Don't clear draggedData - CleanupDrag will handle restore
                return;
            }

            var sourceLocation = sourceCell.GetLocation();
            var targetLocation = targetCell.GetLocation();

            // #region agent log
            long timestamp6 = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            System.IO.File.AppendAllText(@"w:\Unity\Shotter\.cursor\debug.log", 
                $"{{\"id\":\"log_{timestamp6}_drag\",\"timestamp\":{timestamp6},\"location\":\"DragDropHandler.cs:HandleDrop\",\"message\":\"Checking locations\",\"data\":{{\"sourceLocation\":\"{sourceLocation}\",\"targetLocation\":\"{targetLocation}\",\"itemId\":\"{draggedData.Item.ItemId}\",\"sourceCellName\":\"{sourceCell.name}\",\"targetCellName\":\"{targetCell.name}\"}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"E\"}}\n");
            // #endregion

            // Special handling for container <-> inventory moves (check BEFORE validation)
            // This ensures container moves are handled even if CanDropItemToCell would reject them
            if ((sourceLocation == ItemCellLocation.Container && targetLocation == ItemCellLocation.Inventory) ||
                (sourceLocation == ItemCellLocation.Inventory && targetLocation == ItemCellLocation.Container))
            {
                // #region agent log
                long timestamp7 = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                System.IO.File.AppendAllText(@"w:\Unity\Shotter\.cursor\debug.log", 
                    $"{{\"id\":\"log_{timestamp7}_drag\",\"timestamp\":{timestamp7},\"location\":\"DragDropHandler.cs:HandleDrop\",\"message\":\"Calling HandleContainerInventoryMove\",\"data\":{{\"sourceLocation\":\"{sourceLocation}\",\"targetLocation\":\"{targetLocation}\"}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"D\"}}\n");
                // #endregion
                HandleContainerInventoryMove(sourceCell, targetCell);
                return;
            }

            // Special handling for weapon/equipment/quick slot moves (check BEFORE validation)
            // This ensures equipment moves are handled even if CanDropItemToCell would reject them
            if (targetLocation == ItemCellLocation.Weapon || targetLocation == ItemCellLocation.Equipment ||
                targetLocation == ItemCellLocation.QuickSlot || targetLocation == ItemCellLocation.Attachment)
            {
                // #region agent log
                long timestamp13 = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                System.IO.File.AppendAllText(@"w:\Unity\Shotter\.cursor\debug.log", 
                    $"{{\"id\":\"log_{timestamp13}_drag\",\"timestamp\":{timestamp13},\"location\":\"DragDropHandler.cs:HandleDrop\",\"message\":\"Calling HandleEquipmentMove\",\"data\":{{\"sourceLocation\":\"{sourceLocation}\",\"targetLocation\":\"{targetLocation}\",\"itemId\":\"{draggedData.Item.ItemId}\"}},\"sessionId\":\"debug-session\",\"runId\":\"run2\",\"hypothesisId\":\"G\"}}\n");
                // #endregion
                HandleEquipmentMove(sourceCell, targetCell);
                return;
            }

            // Validate: Check if item can be dropped to target cell (for inventory-to-inventory moves only)
            if (!CanDropItemToCell(draggedData, targetCell))
            {
                // #region agent log
                long timestamp3 = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                System.IO.File.AppendAllText(@"w:\Unity\Shotter\.cursor\debug.log", 
                    $"{{\"id\":\"log_{timestamp3}_drag\",\"timestamp\":{timestamp3},\"location\":\"DragDropHandler.cs:HandleDrop\",\"message\":\"Validation failed\",\"data\":{{\"itemId\":\"{draggedData.Item.ItemId}\",\"sourceLocation\":\"{sourceLocation}\",\"targetLocation\":\"{targetLocation}\"}},\"sessionId\":\"debug-session\",\"runId\":\"run2\",\"hypothesisId\":\"G\"}}\n");
                // #endregion

                // Restore data to source cell - drop failed
                sourceCell.SetSlot(draggedData);
                sourceCell.UpdateDisplay();
                // Don't clear draggedData - CleanupDrag will handle restore
                return;
            }

            // #region agent log
            long timestamp8 = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            System.IO.File.AppendAllText(@"w:\Unity\Shotter\.cursor\debug.log", 
                $"{{\"id\":\"log_{timestamp8}_drag\",\"timestamp\":{timestamp8},\"location\":\"DragDropHandler.cs:HandleDrop\",\"message\":\"Proceeding with inventory-to-inventory drop after validation\",\"data\":{{\"sourceLocation\":\"{sourceLocation}\",\"targetLocation\":\"{targetLocation}\",\"itemId\":\"{draggedData.Item.ItemId}\"}},\"sessionId\":\"debug-session\",\"runId\":\"run2\",\"hypothesisId\":\"G\"}}\n");
            // #endregion

            // At this point, only inventory-to-inventory moves should reach here
            // Container and equipment moves are already handled above

            // Store state before operation for rollback
            sourceSlotBeforeOperation = new InventorySlot();
            if (sourceCell.GetSlot() != null)
            {
                sourceSlotBeforeOperation.SetItem(sourceCell.GetSlot().Item, sourceCell.GetSlot().Quantity);
            }

            targetSlotBeforeOperation = new InventorySlot();
            if (targetCell.GetSlot() != null)
            {
                targetSlotBeforeOperation.SetItem(targetCell.GetSlot().Item, targetCell.GetSlot().Quantity);
            }

            sourceCellBeforeOperation = sourceCell;
            targetCellBeforeOperation = targetCell;

            // Handle drop within same location (inventory to inventory, etc.): Swap if target has item, Move if target is empty
            // NOTE: For inventory-to-inventory moves, we do LOCAL UI update only (optimistic update)
            // Server will sync the actual position later, but local UI can be rearranged freely
            if (targetCell.IsEmpty())
            {
                // MOVE: Gán data vào target
                targetCell.SetSlot(draggedData);
                // sourceCell đã empty rồi (từ StartDrag)

                // For inventory-to-inventory moves, update local UI state and fire network event
                if (sourceLocation == ItemCellLocation.Inventory && targetLocation == ItemCellLocation.Inventory)
                {
                    // Get grid positions
                    int sourceIndex = inventoryPanel?.GetSlotUIs()?.IndexOf(sourceCell) ?? -1;
                    int targetIndex = inventoryPanel?.GetSlotUIs()?.IndexOf(targetCell) ?? -1;
                    if (sourceIndex >= 0 && targetIndex >= 0)
                    {
                        // Update local UI state (per-instance)
                        var instanceId = draggedData.Item.InstanceId;
                        if (!string.IsNullOrEmpty(instanceId))
                        {
                            inventoryPanel?.UpdateLocalItemPosition(instanceId, targetIndex);
                        }

                        var (fromX, fromY) = inventoryPanel?.GetGridPositionFromIndex(sourceIndex) ?? (0, 0);
                        var (toX, toY) = inventoryPanel?.GetGridPositionFromIndex(targetIndex) ?? (0, 0);

                        // Fire network event to sync move (optional - local UI is primary)
                        InventoryUIEvents.RequestMoveItem(draggedData.Item.ItemId, fromX, fromY, toX, toY);
                    }
                }

                OnItemMoved?.Invoke(sourceCell, targetCell, draggedData);
                
                // Clear draggedData - move handled successfully
                draggedData = null;
            }
            else
            {
                // SWAP: Swap data
                var targetData = targetCell.GetSlot();
                targetCell.SetSlot(draggedData);
                targetCell.UpdateDisplay();
                sourceCell.SetSlot(targetData);
                sourceCell.UpdateDisplay();

                // For inventory-to-inventory swaps, update local UI state and fire network event
                if (sourceLocation == ItemCellLocation.Inventory && targetLocation == ItemCellLocation.Inventory)
                {
                    // Get grid positions
                    int sourceIndex = inventoryPanel?.GetSlotUIs()?.IndexOf(sourceCell) ?? -1;
                    int targetIndex = inventoryPanel?.GetSlotUIs()?.IndexOf(targetCell) ?? -1;
                    if (sourceIndex >= 0 && targetIndex >= 0)
                    {
                        // Update local UI state for both items (per-instance)
                        var draggedInstanceId = draggedData.Item.InstanceId;
                        var targetInstanceId = targetData.Item.InstanceId;
                        if (!string.IsNullOrEmpty(draggedInstanceId))
                        {
                            inventoryPanel?.UpdateLocalItemPosition(draggedInstanceId, targetIndex);
                        }
                        if (!string.IsNullOrEmpty(targetInstanceId))
                        {
                            inventoryPanel?.UpdateLocalItemPosition(targetInstanceId, sourceIndex);
                        }

                        var (fromX, fromY) = inventoryPanel?.GetGridPositionFromIndex(sourceIndex) ?? (0, 0);
                        var (toX, toY) = inventoryPanel?.GetGridPositionFromIndex(targetIndex) ?? (0, 0);

                        // Fire network event to sync swap
                        InventoryUIEvents.RequestSwapItems(
                            draggedData.Item.ItemId, fromX, fromY,
                            targetData.Item.ItemId, toX, toY);
                    }
                }

                OnItemSwapped?.Invoke(sourceCell, targetCell, draggedData, targetData);
                
                // Clear draggedData - swap handled successfully
                draggedData = null;
            }

            // Register pending operation for rollback (if prediction system available)
            if (predictionSystem != null)
            {
                var parameters = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "sourceLocation", sourceCell.GetLocation().ToString() },
                    { "targetLocation", targetCell.GetLocation().ToString() }
                };

                System.Action rollback = () =>
                {
                    // Rollback UI state
                    if (sourceCellBeforeOperation != null)
                    {
                        sourceCellBeforeOperation.SetSlot(sourceSlotBeforeOperation);
                        sourceCellBeforeOperation.UpdateDisplay();
                    }

                    if (targetCellBeforeOperation != null)
                    {
                        targetCellBeforeOperation.SetSlot(targetSlotBeforeOperation);
                        targetCellBeforeOperation.UpdateDisplay();
                    }

                    Debug.LogWarning("[DragDropHandler] Rolled back operation due to server rejection");
                };

                var operationType = targetCell.IsEmpty()
                    ? NightHunt.Gameplay.Inventory.Logic.Prediction.InventoryUIPrediction.OperationType.MoveItem
                    : NightHunt.Gameplay.Inventory.Logic.Prediction.InventoryUIPrediction.OperationType.SwapItems;

                predictionSystem.RegisterPendingOperation(operationType, parameters, rollback);
            }

            // Fire network events for inventory-to-inventory moves (if draggedData still exists)
            if (draggedData != null)
            {
                FireNetworkEvents(sourceCell, targetCell, draggedData);
            }
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
        private bool CanEquipToSlotType(NightHunt.InteractionSystem.Core.Abstractions.ItemCategory category,
            EquipmentSlotType slotType)
        {
            return (slotType == EquipmentSlotType.Armor &&
                    category == NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Armor) ||
                   (slotType == EquipmentSlotType.Helmet &&
                    category == NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Helmet) ||
                   (slotType == EquipmentSlotType.Vest &&
                    category == NightHunt.InteractionSystem.Core.Abstractions.ItemCategory
                        .Armor) || // TODO: Check if Vest is separate category
                   (slotType == EquipmentSlotType.Backpack &&
                    category == NightHunt.InteractionSystem.Core.Abstractions.ItemCategory.Backpack);
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
            // #region agent log
            long timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string itemId = draggedData?.Item?.ItemId ?? "null";
            System.IO.File.AppendAllText(@"w:\Unity\Shotter\.cursor\debug.log", 
                $"{{\"id\":\"log_{timestamp}_drag\",\"timestamp\":{timestamp},\"location\":\"DragDropHandler.cs:HandleContainerInventoryMove\",\"message\":\"HandleContainerInventoryMove called\",\"data\":{{\"sourceLocation\":\"{sourceCell.GetLocation()}\",\"targetLocation\":\"{targetCell.GetLocation()}\",\"itemId\":\"{itemId}\"}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"C\"}}\n");
            // #endregion

            if (sourceCell == null || targetCell == null || draggedData == null || draggedData.IsEmpty)
            {
                // #region agent log
                long timestamp2 = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                System.IO.File.AppendAllText(@"w:\Unity\Shotter\.cursor\debug.log", 
                    $"{{\"id\":\"log_{timestamp2}_drag\",\"timestamp\":{timestamp2},\"location\":\"DragDropHandler.cs:HandleContainerInventoryMove\",\"message\":\"Invalid parameters\",\"data\":{{\"sourceCell\":{sourceCell != null},\"targetCell\":{targetCell != null},\"draggedData\":{draggedData != null}}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"C\"}}\n");
                // #endregion
                return;
            }

            var sourceLocation = sourceCell.GetLocation();
            var targetLocation = targetCell.GetLocation();

            // For container moves: Support both MOVE and SWAP
            // IMPORTANT: sourceCell may have been cleared in StartDrag(), so DO NOT rely on sourceCell.GetSlot().
            // Always use draggedData as the source payload.
            if (sourceLocation == ItemCellLocation.Inventory && targetLocation == ItemCellLocation.Container)
            {
                // Inventory → Container: Check if target has item for swap
                if (targetCell.IsEmpty())
                {
                    // MOVE: Inventory → Container (empty slot)
                    // #region agent log
                    long timestamp10 = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    System.IO.File.AppendAllText(@"w:\Unity\Shotter\.cursor\debug.log", 
                        $"{{\"id\":\"log_{timestamp10}_drag\",\"timestamp\":{timestamp10},\"location\":\"DragDropHandler.cs:HandleContainerInventoryMove\",\"message\":\"Calling MoveItemToContainer\",\"data\":{{\"itemId\":\"{draggedData.Item.ItemId}\",\"inventoryPanel\":{inventoryPanel != null}}},\"sessionId\":\"debug-session\",\"runId\":\"run2\",\"hypothesisId\":\"F\"}}\n");
                    // #endregion
                    if (inventoryPanel != null)
                    {
                        var lootPanel = inventoryPanel.GetLootContainerPanel();
                        if (lootPanel != null && lootPanel.IsContainerLoaded())
                        {
                            int sourceIndex = inventoryPanel.GetSlotUIs().IndexOf(sourceCell);
                            var (fromX, fromY) = inventoryPanel.GetGridPositionFromIndex(sourceIndex);
                            var instanceId = draggedData.Item.InstanceId;
                            if (!string.IsNullOrEmpty(instanceId))
                            {
                                inventoryPanel.ClearLocalItemPosition(instanceId);
                            }
                            lootPanel.MoveItemToContainer(draggedData.Item.ItemId, fromX, fromY);
                        }
                    }
                }
                else
                {
                    // SWAP: Inventory ↔ Container (target has item)
                    // Swap: Move dragged item to container, move target item to inventory
                    var targetData = targetCell.GetSlot();
                    if (targetData != null && !targetData.IsEmpty)
                    {
                        // First, move target item from container to inventory (at source position)
                        if (inventoryPanel != null)
                        {
                            var lootPanel = inventoryPanel.GetLootContainerPanel();
                            if (lootPanel != null)
                            {
                                // Get source grid position
                                int sourceIndex = inventoryPanel.GetSlotUIs().IndexOf(sourceCell);
                                var (toX, toY) = inventoryPanel.GetGridPositionFromIndex(sourceIndex);
                                
                                // Save target position for target item BEFORE moving
                                inventoryPanel.UpdateLocalItemPosition(targetData.Item.ItemId, sourceIndex);
                                
                                // Move target item from container to inventory
                                lootPanel.MoveItemFromContainer(targetData.Item.ItemId, toX, toY);
                            }
                        }
                        
                        // Then, move dragged item from inventory to container
                        if (inventoryPanel != null)
                        {
                            var lootPanel = inventoryPanel.GetLootContainerPanel();
                            if (lootPanel != null && lootPanel.IsContainerLoaded())
                            {
                                int sourceIndex = inventoryPanel.GetSlotUIs().IndexOf(sourceCell);
                                var (fromX, fromY) = inventoryPanel.GetGridPositionFromIndex(sourceIndex);
                                var instanceId = draggedData.Item.InstanceId;
                                if (!string.IsNullOrEmpty(instanceId))
                                {
                                    inventoryPanel.ClearLocalItemPosition(instanceId);
                                }
                                lootPanel.MoveItemToContainer(draggedData.Item.ItemId, fromX, fromY);
                            }
                        }
                    }
                }
                // Clear draggedData so CleanupDrag doesn't restore
                draggedData = null;
            }
            else if (sourceLocation == ItemCellLocation.Container && targetLocation == ItemCellLocation.Inventory)
            {
                // Container → Inventory: Check if target has item for swap
                if (targetCell.IsEmpty())
                {
                    // MOVE: Container → Inventory (empty slot)
                    // #region agent log
                    long timestamp9 = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    System.IO.File.AppendAllText(@"w:\Unity\Shotter\.cursor\debug.log", 
                        $"{{\"id\":\"log_{timestamp9}_drag\",\"timestamp\":{timestamp9},\"location\":\"DragDropHandler.cs:HandleContainerInventoryMove\",\"message\":\"Calling MoveItemFromContainer\",\"data\":{{\"itemId\":\"{draggedData.Item.ItemId}\",\"inventoryPanel\":{inventoryPanel != null}}},\"sessionId\":\"debug-session\",\"runId\":\"run2\",\"hypothesisId\":\"F\"}}\n");
                    // #endregion
                    if (inventoryPanel != null)
                    {
                        var lootPanel = inventoryPanel.GetLootContainerPanel();
                        if (lootPanel != null && lootPanel.IsContainerLoaded())
                        {
                            int targetIndex = inventoryPanel.GetSlotUIs().IndexOf(targetCell);
                            var (toX, toY) = inventoryPanel.GetGridPositionFromIndex(targetIndex);
                            var instanceId = draggedData.Item.InstanceId;
                            if (!string.IsNullOrEmpty(instanceId))
                            {
                                inventoryPanel.UpdateLocalItemPosition(instanceId, targetIndex);
                            }
                            lootPanel.MoveItemFromContainer(draggedData.Item.ItemId, toX, toY);
                        }
                    }
                }
                else
                {
                    // SWAP: Container ↔ Inventory (target has item)
                    // Swap: Move dragged item to inventory, move target item to container
                    var targetData = targetCell.GetSlot();
                    if (targetData != null && !targetData.IsEmpty)
                    {
                        // First, move target item from inventory to container
                        if (inventoryPanel != null)
                        {
                            // Get target grid position for moving target item to container
                            int targetIndex = inventoryPanel.GetSlotUIs().IndexOf(targetCell);
                            var (fromX, fromY) = inventoryPanel.GetGridPositionFromIndex(targetIndex);
                            
                            // Clear local position for target item (moving to container)
                            var targetInstanceId = targetData.Item.InstanceId;
                            if (!string.IsNullOrEmpty(targetInstanceId))
                            {
                                inventoryPanel.ClearLocalItemPosition(targetInstanceId);
                            }
                            
                            // Move target item to container
                            var lootPanel = inventoryPanel.GetLootContainerPanel();
                            if (lootPanel != null)
                            {
                                lootPanel.MoveItemToContainer(targetData.Item.ItemId, fromX, fromY);
                            }
                        }
                        
                        // Then, move dragged item from container to inventory
                        if (inventoryPanel != null)
                        {
                            var lootPanel = inventoryPanel.GetLootContainerPanel();
                            if (lootPanel != null && lootPanel.IsContainerLoaded())
                            {
                                int targetIndex = inventoryPanel.GetSlotUIs().IndexOf(targetCell);
                                var (toX, toY) = inventoryPanel.GetGridPositionFromIndex(targetIndex);
                                var instanceId = draggedData.Item.InstanceId;
                                if (!string.IsNullOrEmpty(instanceId))
                                {
                                    inventoryPanel.UpdateLocalItemPosition(instanceId, targetIndex);
                                }
                                lootPanel.MoveItemFromContainer(draggedData.Item.ItemId, toX, toY);
                            }
                        }
                    }
                }
                // Clear draggedData so CleanupDrag doesn't restore
                draggedData = null;
            }
            else
            {
                // Fallback: restore if unknown move type
                sourceCell.SetSlot(draggedData);
                sourceCell.UpdateDisplay();
            }
        }

        /// <summary>
        /// Handle move to/from equipment/weapon/quick/attachment slots
        /// </summary>
        private void HandleEquipmentMove(ItemCell sourceCell, ItemCell targetCell)
        {
            if (sourceCell == null || targetCell == null || draggedData == null || draggedData.IsEmpty)
            {
                // Restore if invalid
                if (sourceCell != null && draggedData != null)
                {
                    sourceCell.SetSlot(draggedData);
                    sourceCell.UpdateDisplay();
                }
                return;
            }

            var targetLocation = targetCell.GetLocation();

            // For equipment moves: Keep source empty, wait for server sync
            // Server will remove item from inventory via ObserversRpc
            if (targetLocation == ItemCellLocation.Weapon)
            {
                int weaponSlotIndex = targetCell.GetCellIndex();
                if (targetCell.IsEmpty())
                {
                    inventoryPanel?.EquipWeapon(sourceCell, weaponSlotIndex);
                }
                else
                {
                    inventoryPanel?.SwapWeapon(sourceCell, weaponSlotIndex);
                }
            }
            else if (targetLocation == ItemCellLocation.Equipment)
            {
                EquipmentSlotType slotType = targetCell.GetEquipmentSlotType();
                if (targetCell.IsEmpty())
                {
                    inventoryPanel?.EquipItem(sourceCell, targetCell);
                }
                else
                {
                    inventoryPanel?.SwapEquipment(sourceCell, targetCell);
                }
            }
            else if (targetLocation == ItemCellLocation.QuickSlot)
            {
                int quickSlotIndex = targetCell.GetCellIndex();
                inventoryPanel?.AssignQuickSlot(sourceCell, quickSlotIndex);
            }
            else if (targetLocation == ItemCellLocation.Attachment)
            {
                // TODO: Implement attachment logic
                // For now, restore data to source
                sourceCell.SetSlot(draggedData);
                sourceCell.UpdateDisplay();
                return; // Don't clear draggedData if not handled
            }

            // Clear draggedData so CleanupDrag doesn't restore (server will sync)
            draggedData = null;

            // Refresh UI after equipment move (ServerRpc is async)
            StartCoroutine(RefreshUIAfterMove());
        }

        /// <summary>
        /// Fire move events based on locations
        /// </summary>
        private void FireMoveEvents(ItemCellLocation sourceLocation, ItemCellLocation targetLocation,
            InventorySlot sourceSlot)
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
            Debug.Log(
                $"[DragDropHandler] FireNetworkEvents: {sourceLocation} -> {targetLocation}, Item: {movedData.Item.ItemId}");
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
            if (!isDragging || dragCell == null || !dragCell.gameObject.activeSelf || draggedData == null ||
                draggedData.IsEmpty)
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
                if (dragCell != null && dragCell.gameObject.activeSelf && (result.gameObject == dragCell.gameObject ||
                                                                           result.gameObject.transform.IsChildOf(
                                                                               dragCell.transform)))
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
                        cellBackground.color =
                            isValid ? new Color(0.5f, 1f, 0.5f, 0.5f) : new Color(1f, 0.5f, 0.5f, 0.5f);
                    }

                    // Fire event
                    OnDropZoneHighlight?.Invoke(targetCell, isValid);
                }
            }
        }

        /// <summary>
        /// Cleanup drag operation
        /// </summary>
        private void CleanupDrag(bool dropHandled = false)
        {
            // #region agent log
            long timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            System.IO.File.AppendAllText(@"w:\Unity\Shotter\.cursor\debug.log", 
                $"{{\"id\":\"log_{timestamp}_drag\",\"timestamp\":{timestamp},\"location\":\"DragDropHandler.cs:CleanupDrag\",\"message\":\"CleanupDrag called\",\"data\":{{\"dropHandled\":{dropHandled},\"draggedData\":{draggedData != null},\"draggedCell\":{draggedCell != null}}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"C\"}}\n");
            // #endregion

            // Hide drag cell FIRST (before restoring source)
            if (dragCell != null)
            {
                dragCell.ClearSlot();
                dragCell.gameObject.SetActive(false);
            }

            // Restore original slot icon (re-enable it)
            if (originalSlotIcon != null)
            {
                originalSlotIcon.enabled = originalIconWasEnabled;
                originalSlotIcon = null;
                originalIconWasEnabled = false;
            }

            // Restore data to source cell if drop was NOT handled or failed
            // For successful drops (container move, equipment move), draggedData may be null or source cell already updated
            if (!dropHandled && draggedCell != null && draggedData != null && !draggedData.IsEmpty)
            {
                // Drop failed or cancelled - restore source cell
                draggedCell.SetSlot(draggedData);
                draggedCell.UpdateDisplay();
                
                // #region agent log
                long timestamp2 = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                System.IO.File.AppendAllText(@"w:\Unity\Shotter\.cursor\debug.log", 
                    $"{{\"id\":\"log_{timestamp2}_drag\",\"timestamp\":{timestamp2},\"location\":\"DragDropHandler.cs:CleanupDrag\",\"message\":\"Restored data to source cell\",\"data\":{{\"itemId\":\"{draggedData.Item.ItemId}\"}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"C\"}}\n");
                // #endregion
            }
            else if (draggedCell != null && draggedData == null)
            {
                // draggedData was cleared (e.g., by container move) - source cell should be empty
                // Ensure it's properly cleared
                if (draggedCell.GetSlot() == null || draggedCell.GetSlot().IsEmpty)
                {
                    draggedCell.ClearSlot();
                    draggedCell.UpdateDisplay();
                }
            }

            // Fire event
            if (draggedCell != null)
            {
                OnDragEnded?.Invoke(draggedCell);
            }

            // Clear state
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