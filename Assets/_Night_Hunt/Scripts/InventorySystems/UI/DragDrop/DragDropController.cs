using UnityEngine;
using UnityEngine.EventSystems;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.UI.Slots;
using NightHunt.Inventory.UI.Data;
using NightHunt.Inventory.UI.Trash;
using System;

namespace NightHunt.Inventory.UI.DragDrop
{
    /// <summary>
    /// Main drag & drop controller.
    /// Handles drag & drop operations, manages drag state, visual feedback.
    /// Coordinates between draggable and droppable items.
    /// </summary>
    public class DragDropController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DragDropVisual visualFeedback;
        [SerializeField] private DragDropValidator validator;
        [SerializeField] private InventoryUIDataProvider dataProvider;
        
        [Header("Settings")]
        [SerializeField] private float dragThreshold = 5f;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        // State
        private bool isDragging = false;
        private ItemInstance draggedItem;
        private ItemSlotUI sourceSlot;
        private ItemSlotUI currentHoveredSlot;
        private TrashSlotUI currentHoveredTrashSlot;
        private Vector2 dragStartPosition;
        
        // Events
        public event Action<ItemInstance, ItemSlotUI> OnDragStarted;
        public event Action<ItemInstance, ItemSlotUI, ItemSlotUI> OnDragEnded;
        public event Action<ItemInstance, ItemSlotUI> OnDropValidated;
        public event Action<ItemInstance, ItemSlotUI> OnDropRejected;
        
        // === Public API ===
        
        /// <summary>
        /// Start dragging item from slot.
        /// </summary>
        public void StartDrag(ItemInstance item, ItemSlotUI source)
        {
            if (item == null || source == null)
                return;
            
            if (!dataProvider.CanInteract())
            {
                Log("Cannot drag - not local player or spectating");
                return;
            }
            
            draggedItem = item;
            sourceSlot = source;
            isDragging = true;
            dragStartPosition = Input.mousePosition;
            
            // Set dragging state on source slot
            sourceSlot.SetDragging(true);
            
            // Show visual feedback
            if (visualFeedback != null)
            {
                visualFeedback.StartDrag(item, source);
            }
            
            OnDragStarted?.Invoke(item, source);
            
            Log($"Started dragging: {item.Definition.DisplayName}");
        }
        
        /// <summary>
        /// Update drag position (called during drag).
        /// </summary>
        public void UpdateDrag()
        {
            if (!isDragging)
                return;
            
            // Update visual feedback position
            if (visualFeedback != null)
            {
                visualFeedback.UpdateDragPosition(Input.mousePosition);
            }
            
            // Check for hovered slot
            CheckHoveredSlot();
        }
        
        /// <summary>
        /// End drag operation.
        /// </summary>
        public void EndDrag()
        {
            if (!isDragging)
                return;
            
            ItemSlotUI targetSlot = currentHoveredSlot;
            TrashSlotUI targetTrashSlot = currentHoveredTrashSlot;
            
            // Reset dragging state
            isDragging = false;
            
            // Reset source slot
            if (sourceSlot != null)
            {
                sourceSlot.SetDragging(false);
            }
            
            // Hide visual feedback
            if (visualFeedback != null)
            {
                visualFeedback.EndDrag();
            }
            
            // Clear hovered slots
            if (currentHoveredSlot != null)
            {
                currentHoveredSlot.SetHovered(false);
                currentHoveredSlot = null;
            }
            
            if (currentHoveredTrashSlot != null)
            {
                currentHoveredTrashSlot = null;
            }
            
            // Try to drop if valid target
            if (targetSlot != null && draggedItem != null)
            {
                TryDrop(draggedItem, sourceSlot, targetSlot);
            }
            else if (targetTrashSlot != null && draggedItem != null)
            {
                TryDropToTrash(draggedItem, sourceSlot, targetTrashSlot);
            }
            
            OnDragEnded?.Invoke(draggedItem, sourceSlot, targetSlot);
            
            // Clear state
            draggedItem = null;
            sourceSlot = null;
            
            Log("Ended drag");
        }
        
        /// <summary>
        /// Cancel drag operation.
        /// </summary>
        public void CancelDrag()
        {
            if (!isDragging)
                return;
            
            // Reset dragging state
            isDragging = false;
            
            if (sourceSlot != null)
            {
                sourceSlot.SetDragging(false);
            }
            
            if (visualFeedback != null)
            {
                visualFeedback.EndDrag();
            }
            
            if (currentHoveredSlot != null)
            {
                currentHoveredSlot.SetHovered(false);
                currentHoveredSlot = null;
            }
            
            if (currentHoveredTrashSlot != null)
            {
                currentHoveredTrashSlot = null;
            }
            
            draggedItem = null;
            sourceSlot = null;
            
            Log("Cancelled drag");
        }
        
        /// <summary>
        /// Check if currently dragging.
        /// </summary>
        public bool IsDragging() => isDragging;
        
        /// <summary>
        /// Get currently dragged item.
        /// </summary>
        public ItemInstance GetDraggedItem() => draggedItem;
        
        // === Private Methods ===
        
        private void CheckHoveredSlot()
        {
            // Raycast to find hovered slot
            PointerEventData pointerData = new PointerEventData(EventSystem.current)
            {
                position = Input.mousePosition
            };
            
            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);
            
            ItemSlotUI newHoveredSlot = null;
            TrashSlotUI newHoveredTrashSlot = null;
            
            foreach (var result in results)
            {
                // Check for ItemSlotUI first
                ItemSlotUI slot = result.gameObject.GetComponent<ItemSlotUI>();
                if (slot != null && slot != sourceSlot)
                {
                    newHoveredSlot = slot;
                    break;
                }
                
                // Check for TrashSlotUI
                TrashSlotUI trashSlot = result.gameObject.GetComponent<TrashSlotUI>();
                if (trashSlot != null)
                {
                    newHoveredTrashSlot = trashSlot;
                    break;
                }
            }
            
            // Update hovered ItemSlotUI
            if (newHoveredSlot != currentHoveredSlot)
            {
                if (currentHoveredSlot != null)
                {
                    currentHoveredSlot.SetHovered(false);
                }
                
                currentHoveredSlot = newHoveredSlot;
                
                if (currentHoveredSlot != null)
                {
                    currentHoveredSlot.SetHovered(true);
                    
                    // Update visual feedback
                    if (visualFeedback != null && validator != null)
                    {
                        bool canDrop = validator.CanDrop(draggedItem, sourceSlot, currentHoveredSlot);
                        visualFeedback.SetDropZoneValid(currentHoveredSlot, canDrop);
                    }
                }
            }
            
            // Update hovered TrashSlotUI
            if (newHoveredTrashSlot != currentHoveredTrashSlot)
            {
                // Clear ItemSlotUI hover if switching to trash
                if (newHoveredTrashSlot != null && currentHoveredSlot != null)
                {
                    currentHoveredSlot.SetHovered(false);
                    currentHoveredSlot = null;
                }
                
                currentHoveredTrashSlot = newHoveredTrashSlot;
                
                // Update visual feedback for trash slot
                if (currentHoveredTrashSlot != null && visualFeedback != null && validator != null)
                {
                    bool canDrop = validator.CanDrop(draggedItem, sourceSlot, currentHoveredTrashSlot);
                    // Note: visualFeedback might need update to support TrashSlotUI
                }
            }
        }
        
        private void TryDrop(ItemInstance item, ItemSlotUI source, ItemSlotUI target)
        {
            if (validator == null)
            {
                LogError("DragDropValidator not assigned!");
                return;
            }
            
            // Validate drop
            bool canDrop = validator.CanDrop(item, source, target);
            
            if (canDrop)
            {
                // Execute drop via validator (calls NetworkSync)
                validator.ExecuteDrop(item, source, target);
                OnDropValidated?.Invoke(item, target);
                Log($"Dropped {item.Definition.DisplayName} to {target.GetType().Name}");
            }
            else
            {
                OnDropRejected?.Invoke(item, target);
                Log($"Drop rejected: {item.Definition.DisplayName} to {target.GetType().Name}");
            }
        }
        
        private void TryDropToTrash(ItemInstance item, ItemSlotUI source, TrashSlotUI target)
        {
            if (validator == null)
            {
                LogError("DragDropValidator not assigned!");
                return;
            }
            
            // Validate drop
            bool canDrop = validator.CanDrop(item, source, target);
            
            if (canDrop)
            {
                // Execute drop via validator (calls NetworkSync)
                validator.ExecuteDrop(item, source, target);
                Log($"Dropped {item.Definition.DisplayName} to Trash");
            }
            else
            {
                Log($"Drop rejected: {item.Definition.DisplayName} to Trash");
            }
        }
        
        // === Lifecycle ===
        
        void Awake()
        {
            if (dataProvider == null)
                dataProvider = FindObjectOfType<InventoryUIDataProvider>();
            
            if (visualFeedback == null)
                visualFeedback = GetComponent<DragDropVisual>();
            
            if (validator == null)
                validator = GetComponent<DragDropValidator>();
        }
        
        void Update()
        {
            if (isDragging)
            {
                UpdateDrag();
            }
        }
        
        // === Debug ===
        
        void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[DragDropController] {message}");
        }
        
        void LogError(string message)
        {
            Debug.LogError($"[DragDropController] {message}");
        }
    }
}
