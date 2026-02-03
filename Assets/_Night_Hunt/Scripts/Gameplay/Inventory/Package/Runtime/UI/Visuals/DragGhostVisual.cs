using UnityEngine;
using UnityEngine.UI;
using NightHunt.Inventory.Events;

namespace NightHunt.Inventory.UI
{
    /// <summary>
    /// Visual ghost image that follows mouse during drag operation.
    /// </summary>
    public class DragGhostVisual : MonoBehaviour
    {
        [SerializeField] private Image ghostImage;
        [SerializeField] private CanvasGroup canvasGroup;
        
        [Header("Settings")]
        [SerializeField] private float ghostAlpha = 0.6f;
        
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
        
        void ShowGhost(DragContext context)
        {
            ghostImage.sprite = context.ItemInstance.Definition.Icon;
            canvasGroup.alpha = ghostAlpha;
            gameObject.SetActive(true);
        }
        
        void UpdateGhostPosition(Vector2 screenPos)
        {
            transform.position = screenPos;
        }
        
        void HideGhost()
        {
            gameObject.SetActive(false);
        }
        
        void HideGhost(DragContext context)
        {
            gameObject.SetActive(false);
        }
    }
}
