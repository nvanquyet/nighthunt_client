using UnityEngine;
using UnityEngine.InputSystem;
using NightHunt.Inventory.Core.Events;
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
            
            if (enableDebugLogs)
            {
                Debug.Log($"[DragDropController] Drag started: {context.ItemInstance?.Definition.ItemId}");
            }
        }
        
        private void OnDragEnded()
        {
            isDragging = false;
            
            if (enableDebugLogs)
            {
                Debug.Log("[DragDropController] Drag ended");
            }
        }
        
        private void OnDropped(DragContext context)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[DragDropController] Dropped from {context.SourceLocation} to {context.TargetLocation}");
            }
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Manually cancels the current drag operation.
        /// </summary>
        public void CancelDrag(string reason = "Manual cancel")
        {
            if (!isDragging) return;
            
            Debug.Log($"[DragDropController] Drag cancelled: {reason}");
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