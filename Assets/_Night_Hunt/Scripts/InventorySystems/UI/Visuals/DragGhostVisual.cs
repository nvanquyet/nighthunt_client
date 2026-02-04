using UnityEngine;
using UnityEngine.UI;
using NightHunt.Inventory.Core.Events;

namespace NightHunt.Inventory.UI.Visuals
{
    /// <summary>
    /// Displays a ghost image that follows the cursor during drag operations.
    /// </summary>
    public class DragGhostVisual : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image ghostImage;
        [SerializeField] private CanvasGroup canvasGroup;
        
        [Header("Settings")]
        [SerializeField] private float ghostAlpha = 0.6f;
        [SerializeField] private Vector2 offset = new Vector2(10f, -10f);
        
        private RectTransform rectTransform;
        
        #region Lifecycle
        
        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            gameObject.SetActive(false);
        }
        
        void OnEnable()
        {
            DragDropEvents.OnBeginDrag += ShowGhost;
            DragDropEvents.OnDragging += UpdateGhostPosition;
            DragDropEvents.OnDrop += HideGhost;
            DragDropEvents.OnDragCancelled += HideGhost;
            DragDropEvents.OnEndDrag += HideGhost;
        }
        
        void OnDisable()
        {
            DragDropEvents.OnBeginDrag -= ShowGhost;
            DragDropEvents.OnDragging -= UpdateGhostPosition;
            DragDropEvents.OnDrop -= HideGhost;
            DragDropEvents.OnDragCancelled -= HideGhost;
            DragDropEvents.OnEndDrag -= HideGhost;
        }
        
        #endregion
        
        #region Event Handlers
        
        private void ShowGhost(DragContext context)
        {
            if (context.ItemInstance == null) return;
            
            ghostImage.sprite = context.ItemInstance.Definition.Icon;
            canvasGroup.alpha = ghostAlpha;
            gameObject.SetActive(true);
        }
        
        private void UpdateGhostPosition(Vector2 screenPos)
        {
            rectTransform.position = screenPos + offset;
        }
        
        private void HideGhost()
        {
            gameObject.SetActive(false);
        }
        
        private void HideGhost(DragContext context)
        {
            HideGhost();
        }
        
        #endregion
    }
}