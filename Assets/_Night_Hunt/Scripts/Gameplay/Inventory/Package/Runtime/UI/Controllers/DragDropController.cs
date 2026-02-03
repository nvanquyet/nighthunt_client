using UnityEngine;
using NightHunt.Inventory.Events;

namespace NightHunt.Inventory.UI
{
    /// <summary>
    /// Handles drag & drop cancellation (ESC + Right-Click).
    /// </summary>
    public class DragDropController : MonoBehaviour
    {
        private bool isDragging;
        private DragContext currentDragContext;
        
        void OnEnable()
        {
            DragDropEvents.OnBeginDrag += OnDragStarted;
            DragDropEvents.OnEndDrag += OnDragEnded;
        }
        
        void OnDisable()
        {
            DragDropEvents.OnBeginDrag -= OnDragStarted;
            DragDropEvents.OnEndDrag -= OnDragEnded;
        }
        
        void OnDragStarted(DragContext context)
        {
            isDragging = true;
            currentDragContext = context;
        }
        
        void OnDragEnded()
        {
            isDragging = false;
        }
        
        void Update()
        {
            if (!isDragging) return;
            
            // Cancel with ESC
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelDrag();
            }
            
            // Cancel with Right-Click
            if (Input.GetMouseButtonDown(1))
            {
                CancelDrag();
            }
        }
        
        void CancelDrag()
        {
            Debug.Log("[DragDrop] Drag cancelled by user");
            isDragging = false;
            DragDropEvents.FireDragCancelled();
        }
    }
}
