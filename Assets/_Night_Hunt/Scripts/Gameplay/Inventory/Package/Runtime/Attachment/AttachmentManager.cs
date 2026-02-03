using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NightHunt.Inventory.Core;
using NightHunt.Inventory.Attachment;

namespace NightHunt.Inventory.Attachment
{
    /// <summary>
    /// Manages attachment operations (attach/detach).
    /// </summary>
    public class AttachmentManager : MonoBehaviour
    {
        public event System.Action<ItemInstance, ItemInstance> OnAttachmentAdded;   // attachment, parent
        public event System.Action<ItemInstance, ItemInstance> OnAttachmentRemoved; // attachment, parent
        
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
            
            // Fire event
            OnAttachmentAdded?.Invoke(attachment, target);
            
            return AttachResult.Success();
        }
        
        public bool TryDetach(ItemInstance attachment, ItemInstance target)
        {
            if (!target.AttachedItems.Contains(attachment))
                return false;
            
            // Remove from target
            target.AttachedItems.Remove(attachment);
            
            // Remove stat modifiers
            RemoveAttachmentModifiers(attachment, target);
            
            // Fire event
            OnAttachmentRemoved?.Invoke(attachment, target);
            
            return true;
        }
        
        private void ApplyAttachmentModifiers(ItemInstance attachment, ItemInstance target)
        {
            string sourceId = $"Attach:{attachment.InstanceId}";
            
            // Apply weapon stat modifiers (if target is weapon)
            if (target.Definition.ItemType == ItemType.Weapon)
            {
                foreach (var mod in attachment.Definition.WeaponStatModifiers)
                {
                    // TODO: Fire event for weapon stats system
                    // WeaponStatsEvents.OnAddModifier?.Invoke(target, mod.WeaponStat, mod.Type, mod.Value, sourceId);
                }
            }
            
            // Apply character stat modifiers (if attachment affects character)
            foreach (var mod in attachment.Definition.CharacterStatModifiers)
            {
                // TODO: Fire event for character stats system
                // CharacterStatsEvents.OnAddModifier?.Invoke(mod.CharacterStat, mod.Type, mod.Value, sourceId);
            }
        }
        
        private void RemoveAttachmentModifiers(ItemInstance attachment, ItemInstance target)
        {
            string sourceId = $"Attach:{attachment.InstanceId}";
            
            if (target.Definition.ItemType == ItemType.Weapon)
            {
                // TODO: WeaponStatsEvents.OnRemoveModifier?.Invoke(target, sourceId);
            }
            
            // TODO: CharacterStatsEvents.OnRemoveModifier?.Invoke(sourceId);
        }
        
        /// <summary>
        /// Detach ALL attachments from an item (used when dropping item).
        /// Returns list of detached attachments.
        /// </summary>
        public List<ItemInstance> DetachAllAttachments(ItemInstance item)
        {
            var attachments = item.AttachedItems.ToList();
            
            foreach (var attachment in attachments)
            {
                TryDetach(attachment, item);
            }
            
            return attachments;
        }
    }
    
    /// <summary>
    /// Result of attach operation.
    /// </summary>
    public struct AttachResult
    {
        public bool IsSuccess;
        public string FailReason;
        
        public static AttachResult Success() => new AttachResult { IsSuccess = true };
        public static AttachResult Fail(string reason) => new AttachResult { IsSuccess = false, FailReason = reason };
    }
}
