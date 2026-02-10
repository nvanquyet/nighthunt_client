using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.UI;

namespace NightHunt.Inventory.UI.Attachments
{
    /// <summary>
    /// Manages both left and right attachment panels.
    /// Handles showing attachments for selected/hovered items.
    /// Coordinates panel visibility.
    /// </summary>
    public class AttachmentPanelController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LeftAttachmentPanel leftPanel;
        [SerializeField] private RightAttachmentPanel rightPanel;
        [SerializeField] private MainInventoryUIManager uiManager;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        // === Lifecycle ===
        
        void Awake()
        {
            if (uiManager == null)
                uiManager = FindObjectOfType<MainInventoryUIManager>();
            
            if (leftPanel == null)
                leftPanel = GetComponentInChildren<LeftAttachmentPanel>();
            
            if (rightPanel == null)
                rightPanel = GetComponentInChildren<RightAttachmentPanel>();
        }
        
        void Start()
        {
            // Subscribe to UI manager events
            if (uiManager != null)
            {
                uiManager.OnInventoryItemSelected += OnInventoryItemSelected;
                uiManager.OnInventoryItemDeselected += OnInventoryItemDeselected;
                uiManager.OnEquippedItemHovered += OnEquippedItemHovered;
                uiManager.OnEquippedItemUnhovered += OnEquippedItemUnhovered;
            }
        }
        
        void OnDestroy()
        {
            if (uiManager != null)
            {
                uiManager.OnInventoryItemSelected -= OnInventoryItemSelected;
                uiManager.OnInventoryItemDeselected -= OnInventoryItemDeselected;
                uiManager.OnEquippedItemHovered -= OnEquippedItemHovered;
                uiManager.OnEquippedItemUnhovered -= OnEquippedItemUnhovered;
            }
        }
        
        // === Event Handlers ===
        
        private void OnInventoryItemSelected(ItemInstance item)
        {
            if (leftPanel != null)
            {
                leftPanel.ShowAttachmentsForItem(item);
            }
            
            Log($"Left panel showing attachments for: {item?.Definition?.DisplayName ?? "null"}");
        }
        
        private void OnInventoryItemDeselected()
        {
            if (leftPanel != null)
            {
                leftPanel.HideAttachments();
            }
        }
        
        private void OnEquippedItemHovered(ItemInstance item)
        {
            if (rightPanel != null)
            {
                rightPanel.ShowAttachmentsForItem(item);
            }
            
            Log($"Right panel showing attachments for: {item?.Definition?.DisplayName ?? "null"}");
        }
        
        private void OnEquippedItemUnhovered()
        {
            if (rightPanel != null)
            {
                rightPanel.HideAttachments();
            }
        }
        
        // === Debug ===
        
        void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[AttachmentPanelController] {message}");
        }
    }
}
