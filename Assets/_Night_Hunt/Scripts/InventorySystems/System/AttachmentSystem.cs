using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Interfaces;

namespace NightHunt.Inventory.Systems
{
    /// <summary>
    /// Attachment system for attaching items to parent items.
    /// Examples:
    /// - Flashlight → Helmet (increases VisionRadius on Character)
    /// - Grip → Weapon (decreases Recoil on Weapon)
    /// - Scope → Weapon (increases Range/Accuracy on Weapon)
    /// - Pouch → Armor (increases WeightCapacity on Character)
    /// Implements IAttachmentSystem interface.
    /// </summary>
    public class AttachmentSystem : MonoBehaviour, IAttachmentSystem
    {
        [Header("References")]
        [SerializeField] private InventorySystem inventorySystem; // Injected
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        
        // === Dependency Injection ===
        
        public void SetInventorySystem(InventorySystem inventory)
        {
            inventorySystem = inventory;
        }
        
        // === IAttachmentSystem Implementation ===
        
        #region Query
        
        public ItemInstance[] GetAttachments(ItemInstance parentItem)
        {
            if (parentItem == null)
                return new ItemInstance[0];
            
            return parentItem.AttachedItems.ToArray();
        }
        
        public ItemInstance GetAttachment(ItemInstance parentItem, AttachmentSlotType slotType)
        {
            if (parentItem == null)
                return null;
            
            return parentItem.GetAttachment(slotType);
        }
        
        public bool HasAttachment(ItemInstance parentItem, AttachmentSlotType slotType)
        {
            return GetAttachment(parentItem, slotType) != null;
        }
        
        public bool CanAttach(ItemInstance parentItem, ItemInstance attachment)
        {
            if (parentItem == null || attachment == null)
                return false;
            
            if (parentItem.Definition == null || attachment.Definition == null)
                return false;
            
            return parentItem.CanAcceptAttachment(attachment);
        }
        
        public AttachmentSlotType[] GetAvailableSlots(ItemInstance parentItem)
        {
            if (parentItem == null || parentItem.Definition == null)
                return new AttachmentSlotType[0];
            
            return parentItem.Definition.AttachmentSlots;
        }
        
        #endregion
        
        #region Attach/Detach
        
        public OperationResult AttachItem(ItemInstance parentItem, ItemInstance attachment)
        {
            // Validate
            if (!CanAttach(parentItem, attachment))
            {
                AttachmentEvents.InvokeAttachmentOperationFailed(
                    OperationResult.IncompatibleAttachment, 
                    parentItem, 
                    attachment, 
                    "Cannot attach - incompatible or slot occupied"
                );
                return OperationResult.IncompatibleAttachment;
            }
            
            // Check if parent has this attachment slot type
            if (!parentItem.Definition.HasAttachmentSlot(attachment.Definition.AttachmentType))
            {
                AttachmentEvents.InvokeAttachmentOperationFailed(
                    OperationResult.AttachmentSlotNotAvailable, 
                    parentItem, 
                    attachment, 
                    "Parent item does not have this attachment slot"
                );
                return OperationResult.AttachmentSlotNotAvailable;
            }
            
            // Check if slot already occupied
            if (HasAttachment(parentItem, attachment.Definition.AttachmentType))
            {
                AttachmentEvents.InvokeAttachmentOperationFailed(
                    OperationResult.AttachmentSlotOccupied, 
                    parentItem, 
                    attachment, 
                    "Attachment slot already occupied"
                );
                return OperationResult.AttachmentSlotOccupied;
            }
            
            // Attach
            parentItem.AddAttachment(attachment);
            
            // Apply stat modifiers
            ApplyAttachmentModifiers(parentItem, attachment);
            
            // Fire event
            AttachmentEvents.InvokeAttachmentAttached(parentItem, attachment, attachment.Definition.AttachmentType);
            
            Log($"Attached {attachment.Definition.DisplayName} to {parentItem.Definition.DisplayName}");
            return OperationResult.Success;
        }
        
        public OperationResult DetachItem(ItemInstance parentItem, AttachmentSlotType slotType, out ItemInstance detachedItem)
        {
            detachedItem = null;
            
            // Validate
            if (parentItem == null)
            {
                return OperationResult.ItemNotFound;
            }
            
            // Get attachment
            var attachment = GetAttachment(parentItem, slotType);
            if (attachment == null)
            {
                AttachmentEvents.InvokeAttachmentOperationFailed(
                    OperationResult.ItemNotFound, 
                    parentItem, 
                    null, 
                    "No attachment found in this slot"
                );
                return OperationResult.ItemNotFound;
            }
            
            // Remove stat modifiers
            RemoveAttachmentModifiers(parentItem, attachment);
            
            // Detach
            parentItem.RemoveAttachment(slotType, out detachedItem);
            
            // Add back to inventory (optional - caller can handle this)
            if (inventorySystem != null)
            {
                inventorySystem.AddItem(detachedItem, out _);
            }
            
            // Fire event
            AttachmentEvents.InvokeAttachmentDetached(parentItem, detachedItem, slotType);
            
            Log($"Detached {detachedItem.Definition.DisplayName} from {parentItem.Definition.DisplayName}");
            return OperationResult.Success;
        }
        
        public OperationResult SwapAttachment(ItemInstance parentItem, ItemInstance newAttachment, out ItemInstance oldAttachment)
        {
            oldAttachment = null;
            
            // Validate
            if (!CanAttach(parentItem, newAttachment))
            {
                return OperationResult.IncompatibleAttachment;
            }
            
            AttachmentSlotType slotType = newAttachment.Definition.AttachmentType;
            
            // Get old attachment
            oldAttachment = GetAttachment(parentItem, slotType);
            
            // If no old attachment, just attach new one
            if (oldAttachment == null)
            {
                return AttachItem(parentItem, newAttachment);
            }
            
            // Remove old modifiers
            RemoveAttachmentModifiers(parentItem, oldAttachment);
            
            // Swap
            parentItem.RemoveAttachment(slotType, out _);
            parentItem.AddAttachment(newAttachment);
            
            // Apply new modifiers
            ApplyAttachmentModifiers(parentItem, newAttachment);
            
            // Add old attachment back to inventory
            if (inventorySystem != null)
            {
                inventorySystem.AddItem(oldAttachment, out _);
            }
            
            // Fire event
            AttachmentEvents.InvokeAttachmentSwapped(parentItem, oldAttachment, newAttachment, slotType);
            
            Log($"Swapped {oldAttachment.Definition.DisplayName} with {newAttachment.Definition.DisplayName} on {parentItem.Definition.DisplayName}");
            return OperationResult.Success;
        }
        
        public void DetachAll(ItemInstance parentItem)
        {
            if (parentItem == null)
                return;
            
            var attachments = parentItem.AttachedItems.ToArray(); // Copy to avoid modification during iteration
            
            foreach (var attachment in attachments)
            {
                DetachItem(parentItem, attachment.Definition.AttachmentType, out _);
            }
            
            AttachmentEvents.InvokeAllAttachmentsDetached(parentItem);
            
            Log($"Detached all attachments from {parentItem.Definition.DisplayName}");
        }
        
        #endregion
        
        // === Stat Modifier Management ===
        
        /// <summary>
        /// Apply stat modifiers based on attachment target and parent item type.
        /// CRITICAL LOGIC:
        /// - If parent is WEAPON → apply WEAPON stat modifiers (Recoil, Accuracy, etc.)
        /// - If parent is EQUIPMENT → apply CHARACTER stat modifiers (VisionRadius, etc.)
        /// </summary>
        private void ApplyAttachmentModifiers(ItemInstance parentItem, ItemInstance attachment)
        {
            var modifiers = attachment.GetStatModifiers();
            string sourceId = attachment.GetModifierSourceId();
            
            foreach (var mod in modifiers)
            {
                if (mod.Target == StatModifierTarget.Character)
                {
                    // Apply to CharacterStats
                    // Example: Flashlight on helmet → VisionRadius increased
                    CharacterStatsEvents.InvokeAddModifier(
                        mod.CharacterStat,
                        mod.CalculationType,
                        mod.Value,
                        sourceId
                    );
                    
                    Log($"Applied CHARACTER modifier: {mod.CharacterStat} {mod.CalculationType} {mod.Value:F2} from {sourceId}");
                }
                else if (mod.Target == StatModifierTarget.Weapon)
                {
                    // WeaponStats listens to AttachmentEvents and applies automatically
                    // Example: Grip on weapon → Recoil decreased
                    
                    // Note: We fire the AttachmentAttached event, and WeaponStats
                    // subscribes to this event to apply weapon modifiers.
                    // This keeps the attachment system decoupled from WeaponStats.
                    
                    Log($"WEAPON modifier will be applied by WeaponStats: {mod.WeaponStat} {mod.CalculationType} {mod.Value:F2}");
                }
            }
        }
        
        /// <summary>
        /// Remove stat modifiers when attachment is detached.
        /// </summary>
        private void RemoveAttachmentModifiers(ItemInstance parentItem, ItemInstance attachment)
        {
            string sourceId = attachment.GetModifierSourceId();
            
            var modifiers = attachment.GetStatModifiers();
            
            foreach (var mod in modifiers)
            {
                if (mod.Target == StatModifierTarget.Character)
                {
                    // Remove from CharacterStats
                    CharacterStatsEvents.InvokeRemoveModifier(sourceId);
                    
                    Log($"Removed CHARACTER modifier from {sourceId}");
                }
                else if (mod.Target == StatModifierTarget.Weapon)
                {
                    // WeaponStats listens to AttachmentDetached event and removes automatically
                    Log($"WEAPON modifier will be removed by WeaponStats from {sourceId}");
                }
            }
        }
        
        // === Public API - Additional ===
        
        /// <summary>
        /// Get all attachments of a specific type across all items.
        /// Useful for UI/inventory management.
        /// </summary>
        public ItemInstance[] FindAllAttachments(AttachmentSlotType slotType)
        {
            var result = new System.Collections.Generic.List<ItemInstance>();
            
            if (inventorySystem == null)
                return result.ToArray();
            
            var allItems = inventorySystem.GetContainerData().GetAllItems();
            
            foreach (var item in allItems)
            {
                if (item.Definition.AttachmentType == slotType)
                {
                    result.Add(item);
                }
            }
            
            return result.ToArray();
        }
        
        /// <summary>
        /// Check if item is currently attached to any parent.
        /// </summary>
        public bool IsAttached(ItemInstance item)
        {
            if (item == null)
                return false;
            
            return item.IsEquipped && item.EquippedLocation == SlotLocationType.Attachment;
        }
        
        /// <summary>
        /// Get parent item that this attachment is attached to.
        /// </summary>
        public ItemInstance GetParentItem(ItemInstance attachment)
        {
            if (!IsAttached(attachment))
                return null;
            
            // Search all items to find parent
            if (inventorySystem == null)
                return null;
            
            var allItems = inventorySystem.GetContainerData().GetAllItems();
            
            foreach (var item in allItems)
            {
                if (item.AttachedItems.Contains(attachment))
                {
                    return item;
                }
            }
            
            return null;
        }
        
        // === Debug ===
        
        void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[AttachmentSystem] {message}");
        }
    }
}