using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using NightHunt.Inventory.Core.Events;

namespace NightHunt.Inventory.UI.Panels
{
    /// <summary>
    /// Trash slot UI for destroying items.
    /// No confirmation - direct deletion when dropped.
    /// </summary>
    public class TrashSlotUI : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI References")]
        [SerializeField] private Image trashIcon;
        
        [Header("Visual Feedback")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color hoverColor = Color.red;
        [SerializeField] private Color dropColor = new Color(1f, 0.5f, 0.5f, 1f);
        
        [Header("Animation")]
        [SerializeField] private bool useScaleAnimation = true;
        [SerializeField] private float hoverScale = 1.1f;
        [SerializeField] private float animationSpeed = 10f;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        private Vector3 originalScale;
        private Vector3 targetScale;
        
        #region Lifecycle
        
        void Awake()
        {
            originalScale = transform.localScale;
            targetScale = originalScale;
            
            if (trashIcon != null)
            {
                trashIcon.color = normalColor;
            }
        }
        
        void Update()
        {
            if (useScaleAnimation)
            {
                // Smooth scale animation
                transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * animationSpeed);
            }
        }
        
        #endregion
        
        #region IDropHandler Implementation
        
        public void OnDrop(PointerEventData eventData)
        {
            var draggedCell = eventData.pointerDrag?.GetComponent<UI.Cells.InventoryCellUI>();
            if (draggedCell == null || draggedCell.GetItemData() == null)
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[TrashSlotUI] Invalid drop - no item data");
                return;
            }
            
            var itemToTrash = draggedCell.GetItemData();
            
            // NO confirmation - direct trash
            InventoryEvents.InvokeRequestTrashItem(itemToTrash);
            
            // Visual feedback
            if (trashIcon != null)
            {
                trashIcon.color = dropColor;
                StartCoroutine(ResetColorAfterDelay(0.3f));
            }
            
            if (enableDebugLogs)
                Debug.Log($"[TrashSlotUI] Trashed item: {itemToTrash.Definition.ItemId}");
        }
        
        #endregion
        
        #region IPointerEnterHandler Implementation
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            // Change color
            if (trashIcon != null)
            {
                trashIcon.color = hoverColor;
            }
            
            // Scale up
            if (useScaleAnimation)
            {
                targetScale = originalScale * hoverScale;
            }
            
            if (enableDebugLogs)
                Debug.Log("[TrashSlotUI] Pointer enter");
        }
        
        #endregion
        
        #region IPointerExitHandler Implementation
        
        public void OnPointerExit(PointerEventData eventData)
        {
            // Reset color
            if (trashIcon != null)
            {
                trashIcon.color = normalColor;
            }
            
            // Reset scale
            if (useScaleAnimation)
            {
                targetScale = originalScale;
            }
            
            if (enableDebugLogs)
                Debug.Log("[TrashSlotUI] Pointer exit");
        }
        
        #endregion
        
        #region Helper Methods
        
        private System.Collections.IEnumerator ResetColorAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            
            if (trashIcon != null)
            {
                trashIcon.color = normalColor;
            }
        }
        
        #endregion
    }
}