using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Domain.Attachment;
using System.Collections.Generic;

namespace NightHunt.Inventory.UI.Panels
{
    /// <summary>
    /// Attachment panel UI showing attachment slots for a specific item.
    /// Spawns slots dynamically from item.Definition.AttachmentSlots (data-driven).
    /// Works for both weapons and equipment (helmet, armor, etc.).
    /// </summary>
    public class AttachmentPanelUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject attachmentPanel; // Main panel GameObject to show/hide
        [SerializeField] private Transform slotContainer;
        [SerializeField] private GameObject slotPrefab;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        
        // Runtime-only fields (not shown in Inspector)
        private AttachmentManager attachmentManager; // Injected from local player
        private Dictionary<AttachmentSlotType, AttachmentSlotUI> slotMap;
        private ItemInstance currentItem; // Current item being viewed (weapon/equipment)
        
        #region Lifecycle
        
        void Awake()
        {
            slotMap = new Dictionary<AttachmentSlotType, AttachmentSlotUI>();
            
            // Ensure panel starts hidden
            if (attachmentPanel != null)
            {
                attachmentPanel.SetActive(false);
            }
        }
        
        void OnEnable()
        {
            AttachmentEvents.OnAttachmentAdded += HandleAttachmentAdded;
            AttachmentEvents.OnAttachmentRemoved += HandleAttachmentRemoved;
        }
        
        void OnDisable()
        {
            AttachmentEvents.OnAttachmentAdded -= HandleAttachmentAdded;
            AttachmentEvents.OnAttachmentRemoved -= HandleAttachmentRemoved;
        }
        
        #endregion
        
        #region Initialization
        
        /// <summary>
        /// Sets the AttachmentManager reference (from local player).
        /// Called by parent controller or player setup.
        /// </summary>
        public void SetAttachmentManager(AttachmentManager manager)
        {
            attachmentManager = manager;
            
            if (enableDebugLogs)
                Debug.Log("[AttachmentPanelUI] AttachmentManager injected");
        }
        
        /// <summary>
        /// Shows attachment slots for a specific item.
        /// Called when hovering/selecting an item that has attachment slots.
        /// </summary>
        public void ShowForItem(ItemInstance item)
        {
            if (item == null || item.Definition == null)
            {
                Hide();
                return;
            }
            
            // Check if item has attachment slots
            if (item.Definition.AttachmentSlots == null || item.Definition.AttachmentSlots.Length == 0)
            {
                Hide();
                return;
            }
            
            currentItem = item;
            
            // Clear existing slots
            ClearAllSlots();
            
            // Spawn slots from item data
            SpawnSlotsFromItemData(item);
            
            // Update attachment displays
            RefreshAttachments();
            
            // Show panel
            if (attachmentPanel != null)
            {
                attachmentPanel.SetActive(true);
            }
            
            if (enableDebugLogs)
                Debug.Log($"[AttachmentPanelUI] Showing attachment slots for {item.Definition.ItemId}");
        }
        
        /// <summary>
        /// Hides the attachment panel.
        /// Called when item has no attachments or item is deselected.
        /// </summary>
        public void Hide()
        {
            currentItem = null;
            ClearAllSlots();
            
            // Hide panel
            if (attachmentPanel != null)
            {
                attachmentPanel.SetActive(false);
            }
            
            if (enableDebugLogs)
                Debug.Log("[AttachmentPanelUI] Hiding attachment panel");
        }
        
        private void SpawnSlotsFromItemData(ItemInstance item)
        {
            if (slotContainer == null)
            {
                Debug.LogError("[AttachmentPanelUI] SlotContainer not assigned!");
                return;
            }
            
            if (slotPrefab == null)
            {
                Debug.LogError("[AttachmentPanelUI] SlotPrefab not assigned!");
                return;
            }
            
            // Spawn slots from item's AttachmentSlots data
            foreach (var slotType in item.Definition.AttachmentSlots)
            {
                var slotObj = Instantiate(slotPrefab, slotContainer);
                var slotUI = slotObj.GetComponent<AttachmentSlotUI>();
                
                if (slotUI != null)
                {
                    slotUI.Initialize(slotType, this);
                    slotMap[slotType] = slotUI;
                    
                    if (enableDebugLogs)
                        Debug.Log($"[AttachmentPanelUI] Spawned {slotType} slot for {item.Definition.ItemId}");
                }
                else
                {
                    Debug.LogError($"[AttachmentPanelUI] Slot prefab doesn't have AttachmentSlotUI component!");
                    Destroy(slotObj);
                }
            }
        }
        
        private void ClearAllSlots()
        {
            foreach (var kvp in slotMap)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value.gameObject);
            }
            slotMap.Clear();
        }
        
        private void RefreshAttachments()
        {
            if (currentItem == null || attachmentManager == null) return;
            
            foreach (var kvp in slotMap)
            {
                var attachment = attachmentManager.GetAttachmentInSlot(currentItem, kvp.Key);
                kvp.Value.SetAttachment(attachment);
            }
        }
        
        #endregion
        
        #region Public API
        
        public void OnAttachmentDropped(ItemInstance attachment, AttachmentSlotType targetSlot)
        {
            if (attachmentManager == null || currentItem == null)
            {
                Debug.LogError("[AttachmentPanelUI] Manager not assigned or no item selected!");
                return;
            }
            
            // Validate attachment type
            if (attachment.Definition.ItemType != ItemType.Attachment)
            {
                UIEvents.InvokeShowError("Only attachments can be placed here");
                return;
            }
            
            // Validate slot compatibility
            if (attachment.Definition.AttachmentType != targetSlot)
            {
                UIEvents.InvokeShowError($"Wrong attachment type for this slot");
                return;
            }
            
            // Validate item accepts this attachment type
            if (!System.Array.Exists(currentItem.Definition.AttachmentSlots, 
                slot => slot == targetSlot))
            {
                UIEvents.InvokeShowError($"This item doesn't support {targetSlot} attachments");
                return;
            }
            
            // Try attach
            var result = attachmentManager.TryAttach(attachment, currentItem);
            
            if (result.IsSuccess)
            {
                InventoryEvents.InvokeRequestRemoveItem(attachment.InstanceId);
                
                if (enableDebugLogs)
                    Debug.Log($"[AttachmentPanelUI] Attached {attachment.Definition.ItemId} to {currentItem.Definition.ItemId}");
            }
            else
            {
                UIEvents.InvokeShowError(result.FailReason);
            }
        }
        
        public void OnDetachRequested(AttachmentSlotType slotType)
        {
            if (attachmentManager == null || currentItem == null) return;
            
            var attachment = attachmentManager.GetAttachmentInSlot(currentItem, slotType);
            if (attachment != null)
            {
                if (attachmentManager.TryDetach(attachment, currentItem))
                {
                    InventoryEvents.InvokeRequestAddItem(attachment);
                    
                    if (enableDebugLogs)
                        Debug.Log($"[AttachmentPanelUI] Detached {attachment.Definition.ItemId}");
                }
            }
        }
        
        #endregion
        
        #region Event Handlers
        
        private void HandleAttachmentAdded(ItemInstance attachment, ItemInstance parentItem)
        {
            if (parentItem == currentItem)
            {
                var slotType = attachment.Definition.AttachmentType;
                if (slotMap.TryGetValue(slotType, out var slot))
                {
                    slot.SetAttachment(attachment);
                }
            }
        }
        
        private void HandleAttachmentRemoved(ItemInstance attachment, ItemInstance parentItem)
        {
            if (parentItem == currentItem)
            {
                var slotType = attachment.Definition.AttachmentType;
                if (slotMap.TryGetValue(slotType, out var slot))
                {
                    slot.SetAttachment(null);
                }
            }
        }
        
        #endregion
    }
}
