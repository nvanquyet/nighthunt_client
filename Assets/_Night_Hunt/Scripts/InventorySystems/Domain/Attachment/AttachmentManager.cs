using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Enums;
using System;
using System.Collections.Generic;

namespace NightHunt.Inventory.Domain.Attachment
{
    
    /// <summary>
    /// Manages attachment operations on weapons and equipment.
    /// </summary>
    public class AttachmentManager : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        
        #region Events
        
        public event Action<ItemInstance, ItemInstance> OnAttachmentAdded;   // attachment, parent
        public event Action<ItemInstance, ItemInstance> OnAttachmentRemoved; // attachment, parent
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Tries to attach an attachment to a target item.
        /// </summary>
        public AttachResult TryAttach(ItemInstance attachment, ItemInstance target)
        {
            if (!AttachmentValidator.CanAttach(attachment, target))
            {
                return AttachResult.Fail("Cannot attach to this item");
            }
            
            // Add to target's attached items
            target.AttachedItems.Add(attachment);
            
            // Apply stat modifiers
            ApplyAttachmentModifiers(attachment, target);
            
            // Fire events
            OnAttachmentAdded?.Invoke(attachment, target);
            AttachmentEvents.InvokeAttachmentAdded(attachment, target);
            
            Log($"Attached {attachment.Definition.ItemId} to {target.Definition.ItemId}");
            
            return AttachResult.Success();
        }
        
        /// <summary>
        /// Tries to detach an attachment from a target item.
        /// </summary>
        public bool TryDetach(ItemInstance attachment, ItemInstance target)
        {
            if (attachment == null || target == null) return false;
            
            if (!target.AttachedItems.Contains(attachment))
            {
                LogWarning("Attachment not found on target");
                return false;
            }
            
            // Remove from target
            target.AttachedItems.Remove(attachment);
            
            // Remove stat modifiers
            RemoveAttachmentModifiers(attachment, target);
            
            // Fire events
            OnAttachmentRemoved?.Invoke(attachment, target);
            AttachmentEvents.InvokeAttachmentRemoved(attachment, target);
            
            Log($"Detached {attachment.Definition.ItemId} from {target.Definition.ItemId}");
            
            return true;
        }
        
        /// <summary>
        /// Detaches ALL attachments from an item.
        /// Used when dropping item to world.
        /// Returns list of detached attachments.
        /// </summary>
        public List<ItemInstance> DetachAllAttachments(ItemInstance item)
        {
            if (item == null) return new List<ItemInstance>();
            
            var attachments = new List<ItemInstance>(item.AttachedItems);
            
            foreach (var attachment in attachments)
            {
                TryDetach(attachment, item);
            }
            
            Log($"Detached all attachments from {item.Definition.ItemId}");
            
            return attachments;
        }
        
        /// <summary>
        /// Gets all attachments on an item.
        /// </summary>
        public List<ItemInstance> GetAttachments(ItemInstance item)
        {
            return item?.AttachedItems ?? new List<ItemInstance>();
        }
        
        /// <summary>
        /// Gets attachment in a specific slot.
        /// </summary>
        public ItemInstance GetAttachmentInSlot(ItemInstance item, AttachmentSlotType slotType)
        {
            if (item == null) return null;
            
            return item.AttachedItems.Find(a => a.Definition.AttachmentType == slotType);
        }
        
        /// <summary>
        /// Checks if an item has attachments.
        /// </summary>
        public bool HasAttachments(ItemInstance item)
        {
            return item != null && item.AttachedItems.Count > 0;
        }
        
        #endregion
        
        #region Stat Modifiers
        
        private void ApplyAttachmentModifiers(ItemInstance attachment, ItemInstance target)
        {
            string sourceId = $"Attach:{attachment.InstanceId}";
            
            // Apply weapon stat modifiers (if target is weapon)
            if (target.Definition.ItemType == ItemType.Weapon)
            {
                foreach (var mod in attachment.Definition.WeaponStatModifiers)
                {
                    WeaponStatsEvents.InvokeAddModifier(
                        target,
                        mod.WeaponStat,
                        mod.Type,
                        mod.Value,
                        sourceId
                    );
                }
            }
            
            // Apply character stat modifiers (if attachment affects character)
            foreach (var mod in attachment.Definition.CharacterStatModifiers)
            {
                CharacterStatsEvents.InvokeAddModifier(
                    mod.CharacterStat,
                    mod.Type,
                    mod.Value,
                    sourceId
                );
            }
            
            Log($"Applied stat modifiers for {attachment.Definition.ItemId}");
        }
        
        private void RemoveAttachmentModifiers(ItemInstance attachment, ItemInstance target)
        {
            string sourceId = $"Attach:{attachment.InstanceId}";
            
            if (target.Definition.ItemType == ItemType.Weapon)
            {
                WeaponStatsEvents.InvokeRemoveModifier(target, sourceId);
            }
            
            CharacterStatsEvents.InvokeRemoveModifier(sourceId);
            
            Log($"Removed stat modifiers for {attachment.Definition.ItemId}");
        }
        
        #endregion
        
        #region Helper Methods
        
        private void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[AttachmentManager] {message}");
        }
        
        private void LogWarning(string message)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[AttachmentManager] {message}");
        }
        
        #endregion
    }
    
}