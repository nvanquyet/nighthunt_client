using FishNet.Object;
using System;
using UnityEngine;
using NightHunt.GameplaySystems.Core.Interfaces;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.GameplaySystems.Stat;
using NightHunt.Utilities;

namespace NightHunt.GameplaySystems.Attachment
{
    /// <summary>
    /// Manages item attachment slots (scopes, grips, etc.) for a networked player.
    /// Automatically recovers attachments to inventory when the parent item is dropped.
    /// </summary>
    public class AttachmentSystem : NetworkBehaviour, IAttachmentSystem, IDisposable
    {
        #region Serialized Fields
        
        [Header("References")]
        [SerializeField] private InventorySystem _inventorySystem;
        
        [Header("Configuration")]
        [SerializeField] private InventoryConfig _inventoryConfig;
        
        [Tooltip("Auto-recover attachments when parent item dropped/destroyed")]
        [SerializeField] private bool _autoRecoverAttachments = true;
        
        [Header("Debug")]
        [SerializeField] private bool _enableDebugLogs = false;
        
        #endregion
        
        #region Events
        
        public event Action<string, int, ItemInstance> OnAttachmentAttached;
        public event Action<string, int, ItemInstance> OnAttachmentDetached;
        
        #endregion
        
        #region Lifecycle
        
        private void Awake()
        {
            ValidateReferences();
        }
        
        private void ValidateReferences()
        {
            _inventorySystem = ComponentResolver.Find<InventorySystem>(this)
                .UseExisting(_inventorySystem)
                .OnSelf().InChildren().InParent()
                .OrLogError("[AttachmentSystem] InventorySystem not found!")
                .Resolve();
        }

#if UNITY_EDITOR
        [ContextMenu("Validate References")]
        private void OnValidate() => ValidateReferences();
#endif
        
        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            
            // Subscribe to inventory events for auto-recovery
            if (_autoRecoverAttachments && _inventorySystem != null)
            {
                _inventorySystem.OnItemRemoved += OnParentItemRemoved;
            }
        }
        
        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            
            if (_inventorySystem != null)
            {
                _inventorySystem.OnItemRemoved -= OnParentItemRemoved;
            }
        }
        
        #endregion
        
        #region IDisposable Implementation
        
        public void Dispose()
        {
            // Unsubscribe from inventory events
            if (_inventorySystem != null)
            {
                _inventorySystem.OnItemRemoved -= OnParentItemRemoved;
            }
        }
        
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
            if (parentDef.AttachmentSlots == null || 
                slotIndex < 0 || 
                slotIndex >= parentDef.AttachmentSlots.Length)
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
                Debug.LogWarning("[AttachmentSystem] AttachItem: server-only!");
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
                DetachItemServer(parentInstanceID, slotIndex);
            
            // Attach
            parent.SetAttachment(slotIndex, attachmentInstanceID);
            
            // Remove from inventory slots but keep in ItemDatabase (for ItemStatSystem modifier lookup)
            _inventorySystem.RemoveItemFromSlotsOnly(attachmentInstanceID);
            
            // Recompute item stats so attachment modifiers are immediately reflected.
            RecomputeItemStats(parentInstanceID);

            OnAttachmentAttached?.Invoke(parentInstanceID, slotIndex, attachment);
            
            if (_enableDebugLogs)
            {
                var attachDef = ItemDatabase.GetDefinition(attachment.DefinitionID);
                var parentDef = ItemDatabase.GetDefinition(parent.DefinitionID);
                Debug.Log($"[AttachmentSystem] Attached {attachDef?.DisplayName} → {parentDef?.DisplayName}[{slotIndex}]");
            }
        }
        
        public void DetachItem(string parentInstanceID, int slotIndex)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[AttachmentSystem] DetachItem: server-only!");
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
                Debug.LogWarning("[AttachmentSystem] DetachItemServer: no attachment to detach");
                return;
            }

            string attachmentID = parent.GetAttachment(slotIndex);

            // Get instance: may not be in inventory slots (was moved out via RemoveItemFromSlotsOnly)
            // Fall back to ItemDatabase which keeps all registered instances regardless of slot state.
            var attachment = _inventorySystem.GetItemByInstanceID(attachmentID)
                          ?? ItemDatabase.GetInstance(attachmentID);

            // Clear the attachment slot BEFORE restoring to inventory to avoid any circular reference
            parent.ClearAttachment(slotIndex);

            if (attachment != null)
            {
                _inventorySystem.RestoreItemToSlots(attachment);
                OnAttachmentDetached?.Invoke(parentInstanceID, slotIndex, attachment);
            }
            else
            {
                Debug.LogWarning($"[AttachmentSystem] DetachItemServer: attachment instance not found: {attachmentID}");
            }

            // Recompute item stats so removed attachment modifiers are no longer applied.
            RecomputeItemStats(parentInstanceID);

            if (_enableDebugLogs)
                Debug.Log($"[AttachmentSystem] Detached attachment from {parentInstanceID}[{slotIndex}]");
        }
        
        public void SwapAttachments(string parentID1, int slotIndex1, string parentID2, int slotIndex2)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[AttachmentSystem] SwapAttachments: server-only!");
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
                Debug.LogWarning("[AttachmentSystem] SwapAttachments: invalid parent items");
                return;
            }

            string attach1 = parent1.GetAttachment(slotIndex1);
            string attach2 = parent2.GetAttachment(slotIndex2);

            // Validate cross-compatibility before swapping.
            if (!string.IsNullOrEmpty(attach1) && !string.IsNullOrEmpty(attach2))
            {
                // Both slots occupied – validate cross-compatibility
                if (!CanAttach(attach1, parentID2, slotIndex2) || !CanAttach(attach2, parentID1, slotIndex1))
                {
                    Debug.LogWarning("[AttachmentSystem] SwapAttachments: incompatible attachment types for swap");
                    return;
                }
            }
            else if (!string.IsNullOrEmpty(attach1))
            {
                if (!CanAttach(attach1, parentID2, slotIndex2))
                {
                    Debug.LogWarning("[AttachmentSystem] SwapAttachments: attach1 incompatible with target slot");
                    return;
                }
            }
            else if (!string.IsNullOrEmpty(attach2))
            {
                if (!CanAttach(attach2, parentID1, slotIndex1))
                {
                    Debug.LogWarning("[AttachmentSystem] SwapAttachments: attach2 incompatible with target slot");
                    return;
                }
            }

            parent1.SetAttachment(slotIndex1, attach2);
            parent2.SetAttachment(slotIndex2, attach1);

            // Recompute stat caches for both parents after swap.
            RecomputeItemStats(parentID1);
            RecomputeItemStats(parentID2);

            if (_enableDebugLogs)
                Debug.Log($"[AttachmentSystem] Swapped {parentID1}[{slotIndex1}] ↔ {parentID2}[{slotIndex2}]");
        }
        
        public void DetachAllFromItem(string parentInstanceID)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[AttachmentSystem] DetachAllFromItem: server-only!");
                return;
            }
            
            DetachAllFromItemServer(parentInstanceID, false);
        }
        
        [Server]
        private void DetachAllFromItemServer(string parentInstanceID, bool dropWithItem = false)
        {
            var parent = _inventorySystem.GetItemByInstanceID(parentInstanceID);
            if (parent == null || parent.AttachedItems == null)
                return;
            
            bool returnToInventory = !dropWithItem && 
                (_inventoryConfig == null || _inventoryConfig.ReturnAttachmentsToInventoryOnDrop);
            
            for (int i = 0; i < parent.AttachedItems.Length; i++)
            {
                if (parent.HasAttachment(i))
                {
                    if (returnToInventory)
                        DetachItemServer(parentInstanceID, i);
                }
            }
        }
        
        #endregion
        
        #region Auto-Recovery

        /// <summary>Recovers attached items into inventory when the parent item is removed.</summary>
        [Server]
        private void OnParentItemRemoved(ItemInstance parentItem, int quantity)
        {
            if (!_autoRecoverAttachments)
                return;
            
            if (parentItem == null || parentItem.AttachedItems == null)
                return;
            
            // Only recover if item fully removed
            var remaining = _inventorySystem.GetItemByInstanceID(parentItem.InstanceID);
            if (remaining != null)
                return;
            
            // Chỉ recover nếu config cho phép return vào inventory
            // Nếu config = false, attachments sẽ drop cùng item (không recover)
            bool shouldRecover = _inventoryConfig == null || _inventoryConfig.ReturnAttachmentsToInventoryOnDrop;
            if (!shouldRecover)
                return;
            
            // Recover all attachments
            int recoveredCount = 0;
            for (int i = 0; i < parentItem.AttachedItems.Length; i++)
            {
                string attachmentID = parentItem.AttachedItems[i];
                if (string.IsNullOrEmpty(attachmentID))
                    continue;
                
                // Get attachment definition to add back to inventory
                var attachment = ItemDatabase.GetInstance(attachmentID);
                if (attachment != null)
                {
                    _inventorySystem.AddItem(attachment.DefinitionID, 1);
                    ItemDatabase.UnregisterInstance(attachmentID);
                    recoveredCount++;
                    
                    if (_enableDebugLogs)
                    {
                        var def = ItemDatabase.GetDefinition(attachment.DefinitionID);
                        Debug.Log($"[AttachmentSystem] Recovered attachment: {def?.DisplayName}");
                    }
                }
            }
            
            if (recoveredCount > 0 && _enableDebugLogs)
            {
                var parentDef = ItemDatabase.GetDefinition(parentItem.DefinitionID);
                Debug.Log($"[AttachmentSystem] Recovered {recoveredCount} attachments from {parentDef?.DisplayName}");
            }
        }
        
        #endregion

        #region Helpers

        private void RecomputeItemStats(string instanceID)
        {
            var item = _inventorySystem?.GetItemByInstanceID(instanceID)
                    ?? ItemDatabase.GetInstance(instanceID);
            if (item != null) ItemStatComputer.Compute(item);
        }

        #endregion
    }
}
