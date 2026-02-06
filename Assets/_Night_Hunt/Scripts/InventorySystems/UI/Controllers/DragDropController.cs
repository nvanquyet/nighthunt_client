using UnityEngine;
using UnityEngine.InputSystem;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Utilities;
using NightHunt.Inventory.Input;

namespace NightHunt.Inventory.UI.Controllers
{
    /// <summary>
    /// Manages drag and drop state using Unity New Input System.
    /// Handles cancellation with ESC/Right-click.
    /// </summary>
    public class DragDropController : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool allowDragCancel = true;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        private bool isDragging;
        private DragContext currentDragContext;
        private InventoryInputHandler inputActions;
        
        #region Lifecycle
        
        void Awake()
        {
            
            //inputActions = new NightHunt.Inventory.Input.InventoryInputActions();
        }
        
        void OnEnable()
        {
            // Subscribe to drag events
            DragDropEvents.OnBeginDrag += OnDragStarted;
            DragDropEvents.OnEndDrag += OnDragEnded;
            DragDropEvents.OnDrop += OnDropped;
        }
        
        void OnDisable()
        {
            // Unsubscribe from drag events
            DragDropEvents.OnBeginDrag -= OnDragStarted;
            DragDropEvents.OnEndDrag -= OnDragEnded;
            DragDropEvents.OnDrop -= OnDropped;
        }
        
        #endregion
        
        #region Input Callbacks
        
        private void OnCancelPerformed(InputAction.CallbackContext context)
        {
            if (isDragging && allowDragCancel)
            {
                CancelDrag("ESC key pressed");
            }
        }
        
        private void OnRightClickPerformed(InputAction.CallbackContext context)
        {
            if (isDragging && allowDragCancel)
            {
                CancelDrag("Right-click");
            }
        }
        
        #endregion
        
        #region Event Handlers
        
        private void OnDragStarted(DragContext context)
        {
            isDragging = true;
            currentDragContext = context;
            
            InventoryLogger.Log("DragDropController", $"Drag started: {context.ItemInstance?.Definition.ItemId}", enableDebugLogs);
        }
        
        private void OnDragEnded()
        {
            isDragging = false;
            
            InventoryLogger.Log("DragDropController", "Drag ended", enableDebugLogs);
        }
        
        private void OnDropped(DragContext context)
        {
            InventoryLogger.Log("DragDropController", $"Dropped from {context.SourceLocation} to {context.TargetLocation}", enableDebugLogs);
            
            // QuickSlot drops are handled by QuickSlotSlotUI.OnDrop()
            // Inventory-to-Inventory drops need handler (TODO: implement in InventoryManager)
            // Other location types are handled by their respective UI components
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Manually cancels the current drag operation.
        /// </summary>
        public void CancelDrag(string reason = "Manual cancel")
        {
            if (!isDragging) return;
            
            InventoryLogger.Log("DragDropController", $"Drag cancelled: {reason}", enableDebugLogs);
            isDragging = false;
            DragDropEvents.InvokeDragCancelled();
        }
        
        /// <summary>
        /// Checks if currently dragging.
        /// </summary>
        public bool IsDragging() => isDragging;
        
        /// <summary>
        /// Gets the current drag context.
        /// </summary>
        public DragContext GetCurrentContext() => currentDragContext;
        
        #endregion
    }
}