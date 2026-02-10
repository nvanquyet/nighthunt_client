using UnityEngine;
using UnityEngine.UI;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.UI.Slots;

namespace NightHunt.Inventory.UI.DragDrop
{
    /// <summary>
    /// Visual feedback during drag.
    /// Handles ghost image, drop zone highlighting, invalid drop indication.
    /// </summary>
    public class DragDropVisual : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private GameObject dragGhost;
        [SerializeField] private Image dragGhostImage;
        [SerializeField] private Canvas dragCanvas;
        
        [Header("Colors")]
        [SerializeField] private Color validDropColor = new Color(0f, 1f, 0f, 0.3f);
        [SerializeField] private Color invalidDropColor = new Color(1f, 0f, 0f, 0.3f);
        
        [Header("Settings")]
        [SerializeField] private float ghostOffset = 10f;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        // State
        private bool isDragging = false;
        private ItemSlotUI currentDropZone;
        private Image dropZoneHighlight;
        
        // === Public API ===
        
        /// <summary>
        /// Start drag visual feedback.
        /// </summary>
        public void StartDrag(ItemInstance item, ItemSlotUI source)
        {
            if (item == null || item.Definition == null)
                return;
            
            isDragging = true;
            
            // Create or show drag ghost
            if (dragGhost == null)
            {
                CreateDragGhost();
            }
            
            if (dragGhost != null)
            {
                dragGhost.SetActive(true);
                
                // Set icon
                if (dragGhostImage != null && item.Definition.Icon != null)
                {
                    dragGhostImage.sprite = item.Definition.Icon;
                    dragGhostImage.color = new Color(1f, 1f, 1f, 0.7f); // Semi-transparent
                }
            }
            
            Log($"Started drag visual for: {item.Definition.DisplayName}");
        }
        
        /// <summary>
        /// Update drag position.
        /// </summary>
        public void UpdateDragPosition(Vector2 mousePosition)
        {
            if (!isDragging || dragGhost == null)
                return;
            
            // Update ghost position
            if (dragCanvas != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dragCanvas.transform as RectTransform,
                    mousePosition,
                    dragCanvas.worldCamera,
                    out Vector2 localPoint
                );
                
                dragGhost.transform.localPosition = localPoint + new Vector2(ghostOffset, ghostOffset);
            }
            else
            {
                dragGhost.transform.position = mousePosition;
            }
        }
        
        /// <summary>
        /// Set drop zone highlight (valid/invalid).
        /// </summary>
        public void SetDropZoneValid(ItemSlotUI slot, bool isValid)
        {
            // Remove old highlight
            if (currentDropZone != null && currentDropZone != slot)
            {
                RemoveDropZoneHighlight();
            }
            
            currentDropZone = slot;
            
            if (slot == null)
            {
                RemoveDropZoneHighlight();
                return;
            }
            
            // Add highlight to slot
            if (dropZoneHighlight == null)
            {
                CreateDropZoneHighlight(slot);
            }
            
            if (dropZoneHighlight != null)
            {
                dropZoneHighlight.color = isValid ? validDropColor : invalidDropColor;
                dropZoneHighlight.gameObject.SetActive(true);
            }
        }
        
        /// <summary>
        /// End drag visual feedback.
        /// </summary>
        public void EndDrag()
        {
            isDragging = false;
            
            if (dragGhost != null)
            {
                dragGhost.SetActive(false);
            }
            
            RemoveDropZoneHighlight();
            
            Log("Ended drag visual");
        }
        
        // === Private Methods ===
        
        private void CreateDragGhost()
        {
            if (dragCanvas == null)
            {
                dragCanvas = FindObjectOfType<Canvas>();
                if (dragCanvas == null)
                {
                    LogError("No Canvas found for drag ghost!");
                    return;
                }
            }
            
            GameObject ghost = new GameObject("DragGhost");
            ghost.transform.SetParent(dragCanvas.transform, false);
            
            RectTransform rectTransform = ghost.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(64f, 64f);
            
            dragGhostImage = ghost.AddComponent<Image>();
            dragGhostImage.raycastTarget = false; // Don't block raycasts
            
            dragGhost = ghost;
            dragGhost.SetActive(false);
        }
        
        private void CreateDropZoneHighlight(ItemSlotUI slot)
        {
            if (slot == null)
                return;
            
            GameObject highlight = new GameObject("DropZoneHighlight");
            highlight.transform.SetParent(slot.transform, false);
            
            RectTransform rectTransform = highlight.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;
            
            dropZoneHighlight = highlight.AddComponent<Image>();
            dropZoneHighlight.raycastTarget = false;
            
            // Make it appear on top
            highlight.transform.SetAsLastSibling();
        }
        
        private void RemoveDropZoneHighlight()
        {
            if (dropZoneHighlight != null)
            {
                dropZoneHighlight.gameObject.SetActive(false);
                dropZoneHighlight = null;
            }
            
            currentDropZone = null;
        }
        
        // === Lifecycle ===
        
        void Awake()
        {
            if (dragCanvas == null)
                dragCanvas = GetComponentInParent<Canvas>();
        }
        
        // === Debug ===
        
        void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[DragDropVisual] {message}");
        }
        
        void LogError(string message)
        {
            Debug.LogError($"[DragDropVisual] {message}");
        }
    }
}
