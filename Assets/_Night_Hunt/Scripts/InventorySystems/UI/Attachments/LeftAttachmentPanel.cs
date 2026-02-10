using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Config;
using NightHunt.Inventory.UI.Data;
using System.Collections.Generic;
using System.Linq;

namespace NightHunt.Inventory.UI.Attachments
{
    /// <summary>
    /// Left attachment panel - shows attachments for selected inventory item.
    /// Spawns AttachmentSlotUI dynamically based on item.Definition.AttachmentSlots.
    /// </summary>
    public class LeftAttachmentPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform slotContainer;
        [SerializeField] private GameObject attachmentSlotPrefab;
        [SerializeField] private InventoryUIDataProvider dataProvider;
        [SerializeField] private SlotLayoutConfig slotLayoutConfig;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        // State
        private ItemInstance currentItem;
        private Dictionary<AttachmentSlotType, AttachmentSlotUI> slotUIs = new Dictionary<AttachmentSlotType, AttachmentSlotUI>();
        
        // === Public API ===
        
        /// <summary>
        /// Show attachments for inventory item.
        /// </summary>
        public void ShowAttachmentsForItem(ItemInstance item)
        {
            if (item == null || item.Definition == null)
            {
                HideAttachments();
                return;
            }
            
            currentItem = item;
            
            // Check if item has attachment slots
            var attachmentSlots = dataProvider?.GetAvailableAttachmentSlots(item);
            if (attachmentSlots == null || attachmentSlots.Length == 0)
            {
                HideAttachments();
                return;
            }
            
            // Show panel
            gameObject.SetActive(true);
            
            // Spawn slots for each attachment type
            foreach (var slotType in attachmentSlots)
            {
                if (!slotUIs.ContainsKey(slotType))
                {
                    SpawnAttachmentSlot(slotType);
                }
                
                // Refresh slot with current item
                if (slotUIs.ContainsKey(slotType))
                {
                    slotUIs[slotType].SetParentItem(item);
                }
            }
            
            // Remove slots that are no longer needed
            var slotsToRemove = slotUIs.Keys.Where(k => !attachmentSlots.Contains(k)).ToList();
            foreach (var slotType in slotsToRemove)
            {
                RemoveAttachmentSlot(slotType);
            }
            
            Log($"Showing attachments for: {item.Definition.DisplayName}");
        }
        
        /// <summary>
        /// Hide attachments panel.
        /// </summary>
        public void HideAttachments()
        {
            gameObject.SetActive(false);
            currentItem = null;
        }
        
        /// <summary>
        /// Refresh attachments display.
        /// </summary>
        public void RefreshAttachments()
        {
            if (currentItem != null)
            {
                ShowAttachmentsForItem(currentItem);
            }
        }
        
        // === Slot Management ===
        
        private void SpawnAttachmentSlot(AttachmentSlotType slotType)
        {
            if (slotContainer == null || attachmentSlotPrefab == null)
                return;
            
            GameObject slotObj = Instantiate(attachmentSlotPrefab, slotContainer);
            AttachmentSlotUI slotUI = slotObj.GetComponent<AttachmentSlotUI>();
            
            if (slotUI != null)
            {
                slotUI.SetSlotType(slotType);
                
                // Pass SlotLayoutConfig to slot for empty icon
                if (slotLayoutConfig != null)
                {
                    slotUI.SetSlotLayoutConfig(slotLayoutConfig);
                }
                
                slotUIs[slotType] = slotUI;
            }
        }
        
        private void RemoveAttachmentSlot(AttachmentSlotType slotType)
        {
            if (!slotUIs.ContainsKey(slotType))
                return;
            
            var slotUI = slotUIs[slotType];
            if (slotUI != null)
            {
                Destroy(slotUI.gameObject);
            }
            
            slotUIs.Remove(slotType);
        }
        
        // === Lifecycle ===
        
        void Awake()
        {
            if (dataProvider == null)
                dataProvider = FindObjectOfType<InventoryUIDataProvider>();
            
            if (slotContainer == null)
                slotContainer = transform;
        }
        
        void Start()
        {
            HideAttachments();
        }
        
        // === Debug ===
        
        void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[LeftAttachmentPanel] {message}");
        }
    }
}
