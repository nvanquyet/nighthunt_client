using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.UI.Slots;

namespace NightHunt.Inventory.UI.Trash
{
    /// <summary>
    /// Trash slot UI - allows players to drop items here to delete them.
    /// </summary>
    public class TrashSlotUI : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Visual Components")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image trashIcon;
        [SerializeField] private Image hoverHighlight;
        
        [Header("Colors")]
        [SerializeField] private Color normalColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        [SerializeField] private Color hoverColor = new Color(0.5f, 0.2f, 0.2f, 0.8f);
        [SerializeField] private Color dragOverColor = new Color(0.8f, 0.2f, 0.2f, 0.8f);
        
        [Header("Settings")]
        [SerializeField] private bool requireConfirmation = true;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        // State
        private bool isHovered = false;
        private bool isDragOver = false;
        
        // Events
        public event System.Action<ItemInstance> OnItemTrashed;
        
        // === Public API ===
        
        /// <summary>
        /// Check if item can be trashed.
        /// </summary>
        public bool CanTrash(ItemInstance item)
        {
            return item != null;
        }
        
        /// <summary>
        /// Trash item (delete from inventory).
        /// </summary>
        public void TrashItem(ItemInstance item)
        {
            if (item == null)
                return;
            
            if (requireConfirmation)
            {
                // TODO: Show confirmation dialog
                // For now, just trash immediately
            }
            
            // Call NetworkSync to remove item
            var dataProvider = FindObjectOfType<Data.InventoryUIDataProvider>();
            if (dataProvider != null)
            {
                var inventorySync = dataProvider.GetInventoryNetworkSync();
                if (inventorySync != null && dataProvider.CanInteract())
                {
                    inventorySync.RequestDrop(item.InstanceId);
                    OnItemTrashed?.Invoke(item);
                    Log($"Trashed item: {item.Definition?.DisplayName}");
                }
            }
        }
        
        // === Unity Event Handlers ===
        
        public void OnDrop(PointerEventData eventData)
        {
            // Get dragged item from DragDropController
            var dragController = FindObjectOfType<DragDrop.DragDropController>();
            if (dragController == null)
                return;
            
            // Get dragged item (via reflection or public method)
            var draggedItemField = typeof(DragDrop.DragDropController).GetField("draggedItem",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (draggedItemField != null)
            {
                var draggedItem = draggedItemField.GetValue(dragController) as ItemInstance;
                if (draggedItem != null)
                {
                    TrashItem(draggedItem);
                }
            }
            
            isDragOver = false;
            UpdateVisuals();
        }
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            UpdateVisuals();
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            isDragOver = false;
            UpdateVisuals();
        }
        
        // === Visual Updates ===
        
        private void UpdateVisuals()
        {
            if (backgroundImage == null)
                return;
            
            Color targetColor = isDragOver ? dragOverColor : (isHovered ? hoverColor : normalColor);
            backgroundImage.color = targetColor;
            
            if (hoverHighlight != null)
                hoverHighlight.gameObject.SetActive(isHovered || isDragOver);
        }
        
        // === Lifecycle ===
        
        void Awake()
        {
            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();
            
            if (trashIcon == null)
                trashIcon = transform.Find("TrashIcon")?.GetComponent<Image>();
            
            if (hoverHighlight == null)
                hoverHighlight = transform.Find("HoverHighlight")?.GetComponent<Image>();
        }
        
        void Start()
        {
            UpdateVisuals();
        }
        
        // === Debug ===
        
        void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[TrashSlotUI] {message}");
        }
    }
}
