using FishNet.Object;
using System;
using UnityEngine;
using GameplaySystems.Core.Interfaces;
using GameplaySystems.Core.Data;
using GameplaySystems.Core;

namespace GameplaySystems.Inventory
{
    /// <summary>
    /// Attachment system - NetworkBehaviour
    /// Manages attachments on items (scopes, grips, lights, pouches)
    /// 
    /// Design:
    /// - Attachments stored directly in ItemInstance.AttachedItems array
    /// - This system provides high-level operations
    /// - Triggers stat recalculation when attachments change
    /// </summary>
    public class AttachmentSystem : NetworkBehaviour, IAttachmentSystem
    {
        #region Serialized Fields
        
        [Header("References")]
        [SerializeField] private InventorySystem _inventorySystem;
        
        [Header("Debug")]
        [SerializeField] private bool _enableDebugLogs = false;
        
        #endregion
        
        #region Events
        
        public event Action<string, int, ItemInstance> OnAttachmentAttached;
        public event Action<string, int, ItemInstance> OnAttachmentDetached;
        
        #endregion
        
        #region Initialization
        
        private void Awake()
        {
#if UNITY_EDITOR
            if (_inventorySystem == null)
                _inventorySystem = GetComponent<InventorySystem>();
#endif
            
            if (_inventorySystem == null)
                Debug.LogError("[AttachmentSystem] InventorySystem is null!");
        }
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_inventorySystem == null)
                _inventorySystem = GetComponent<InventorySystem>();
        }
#endif
        
        #endregion
        
        #region IAttachmentSystem - Getters
        
        public ItemInstance GetAttachment(string parentInstanceID, int slotIndex)
        {
            var parent = _inventorySystem.GetItemByInstanceID(parentInstanceID);
            if (parent == null)
                return null;
            
            string attachmentID = parent.GetAttachment(slotIndex);
            if (string.IsNullOrEmpty(attachmentID))
                return null;
            
            return _inventorySystem.GetItemByInstanceID(attachmentID);
        }
        
        public ItemInstance[] GetAllAttachments(string parentInstanceID)
        {
            var parent = _inventorySystem.GetItemByInstanceID(parentInstanceID);
            if (parent == null || parent.AttachedItems == null)
                return new ItemInstance[0];
            
            var attachments = new ItemInstance[parent.AttachedItems.Length];
            for (int i = 0; i < parent.AttachedItems.Length; i++)
            {
                attachments[i] = GetAttachment(parentInstanceID, i);
            }
            
            return attachments;
        }
        
        public bool IsSlotOccupied(string parentInstanceID, int slotIndex)
        {
            var parent = _inventorySystem.GetItemByInstanceID(parentInstanceID);
            return parent != null && parent.HasAttachment(slotIndex);
        }
        
        public bool CanAttach(string attachmentInstanceID, string parentInstanceID, int slotIndex)
        {
            var attachment = _inventorySystem.GetItemByInstanceID(attachmentInstanceID);
            var parent = _inventorySystem.GetItemByInstanceID(parentInstanceID);
            
            if (attachment == null || parent == null)
                return false;
            
            var attachmentDef = ItemDatabase.GetDefinition(attachment.DefinitionID);
            var parentDef = ItemDatabase.GetDefinition(parent.DefinitionID);
            
            if (attachmentDef == null || parentDef == null)
                return false;
            
            // Check slot index valid
            if (parentDef.AttachmentSlots == null || slotIndex < 0 || slotIndex >= parentDef.AttachmentSlots.Length)
                return false;
            
            // Check slot type compatibility
            var slotType = parentDef.AttachmentSlots[slotIndex];
            return attachmentDef.CanAttachToSlot(slotType);
        }
        
        public AttachmentSlotType GetSlotType(string parentInstanceID, int slotIndex)
        {
            var parent = _inventorySystem.GetItemByInstanceID(parentInstanceID);
            if (parent == null)
                return AttachmentSlotType.None;
            
            var parentDef = ItemDatabase.GetDefinition(parent.DefinitionID);
            if (parentDef == null || parentDef.AttachmentSlots == null)
                return AttachmentSlotType.None;
            
            if (slotIndex < 0 || slotIndex >= parentDef.AttachmentSlots.Length)
                return AttachmentSlotType.None;
            
            return parentDef.AttachmentSlots[slotIndex];
        }
        
        #endregion
        
        #region IAttachmentSystem - Attach/Detach
        
        public void AttachItem(string attachmentInstanceID, string parentInstanceID, int slotIndex)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[AttachmentSystem] AttachItem can only be called on server!");
                return;
            }
            
            AttachItemServer(attachmentInstanceID, parentInstanceID, slotIndex);
        }
        
        [Server]
        private void AttachItemServer(string attachmentInstanceID, string parentInstanceID, int slotIndex)
        {
            if (!CanAttach(attachmentInstanceID, parentInstanceID, slotIndex))
            {
                Debug.LogWarning("[AttachmentSystem] Cannot attach item");
                return;
            }
            
            var attachment = _inventorySystem.GetItemByInstanceID(attachmentInstanceID);
            var parent = _inventorySystem.GetItemByInstanceID(parentInstanceID);
            
            // If slot occupied, detach first
            if (parent.HasAttachment(slotIndex))
            {
                DetachItemServer(parentInstanceID, slotIndex);
            }
            
            // Attach
            parent.SetAttachment(slotIndex, attachmentInstanceID);
            
            // Remove attachment from inventory (it's now on the item)
            _inventorySystem.RemoveItem(attachmentInstanceID, 1);
            
            // Invalidate item stat cache (attachments changed)
            ItemStatSystem.InvalidateCache(parentInstanceID);
            
            OnAttachmentAttached?.Invoke(parentInstanceID, slotIndex, attachment);
            
            // TODO: Trigger stat recalculation
            
            if (_enableDebugLogs)
            {
                var attachDef = ItemDatabase.GetDefinition(attachment.DefinitionID);
                var parentDef = ItemDatabase.GetDefinition(parent.DefinitionID);
                Debug.Log($"[AttachmentSystem] Attached {attachDef?.DisplayName} to {parentDef?.DisplayName} slot {slotIndex}");
            }
        }
        
        public void DetachItem(string parentInstanceID, int slotIndex)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[AttachmentSystem] DetachItem can only be called on server!");
                return;
            }
            
            DetachItemServer(parentInstanceID, slotIndex);
        }
        
        [Server]
        private void DetachItemServer(string parentInstanceID, int slotIndex)
        {
            var parent = _inventorySystem.GetItemByInstanceID(parentInstanceID);
            if (parent == null || !parent.HasAttachment(slotIndex))
            {
                Debug.LogWarning("[AttachmentSystem] No attachment to detach");
                return;
            }
            
            string attachmentID = parent.GetAttachment(slotIndex);
            var attachment = _inventorySystem.GetItemByInstanceID(attachmentID);
            
            if (attachment != null)
            {
                // Return to inventory
                var attachDef = ItemDatabase.GetDefinition(attachment.DefinitionID);
                _inventorySystem.AddItem(attachment.DefinitionID, 1);
                
                OnAttachmentDetached?.Invoke(parentInstanceID, slotIndex, attachment);
            }
            
            // Clear slot
            parent.ClearAttachment(slotIndex);
            
            // Invalidate item stat cache (attachments changed)
            ItemStatSystem.InvalidateCache(parentInstanceID);
            
            // TODO: Trigger stat recalculation
            
            if (_enableDebugLogs)
            {
                Debug.Log($"[AttachmentSystem] Detached attachment from slot {slotIndex}");
            }
        }
        
        public void SwapAttachments(string parentID1, int slotIndex1, string parentID2, int slotIndex2)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[AttachmentSystem] SwapAttachments can only be called on server!");
                return;
            }
            
            SwapAttachmentsServer(parentID1, slotIndex1, parentID2, slotIndex2);
        }
        
        [Server]
        private void SwapAttachmentsServer(string parentID1, int slotIndex1, string parentID2, int slotIndex2)
        {
            var parent1 = _inventorySystem.GetItemByInstanceID(parentID1);
            var parent2 = _inventorySystem.GetItemByInstanceID(parentID2);
            
            if (parent1 == null || parent2 == null)
            {
                Debug.LogWarning("[AttachmentSystem] Invalid parent items");
                return;
            }
            
            string attach1 = parent1.GetAttachment(slotIndex1);
            string attach2 = parent2.GetAttachment(slotIndex2);
            
            parent1.SetAttachment(slotIndex1, attach2);
            parent2.SetAttachment(slotIndex2, attach1);
            
            // Invalidate item stat cache for both items
            ItemStatSystem.InvalidateCache(parentID1);
            ItemStatSystem.InvalidateCache(parentID2);
        }
        
        public void DetachAllFromItem(string parentInstanceID)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[AttachmentSystem] DetachAllFromItem can only be called on server!");
                return;
            }
            
            DetachAllFromItemServer(parentInstanceID);
        }
        
        [Server]
        private void DetachAllFromItemServer(string parentInstanceID)
        {
            var parent = _inventorySystem.GetItemByInstanceID(parentInstanceID);
            if (parent == null || parent.AttachedItems == null)
                return;
            
            for (int i = 0; i < parent.AttachedItems.Length; i++)
            {
                if (parent.HasAttachment(i))
                {
                    DetachItemServer(parentInstanceID, i);
                }
            }
        }
        
        #endregion
    }
}