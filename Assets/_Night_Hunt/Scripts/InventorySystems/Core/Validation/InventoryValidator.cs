using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Interfaces;
using NightHunt.Inventory.Core.Config;

namespace NightHunt.Inventory.Core.Validation
{
    /// <summary>
    /// Server-authoritative validation service.
    /// All operations MUST pass validation before execution.
    /// SOLID: Single Responsibility - only validates, doesn't execute.
    /// </summary>
    public class InventoryValidator : IInventoryValidator
    {
        private readonly InventoryConfig config;
        
        public InventoryValidator(InventoryConfig config)
        {
            this.config = config;
        }
        
        // ===== WEIGHT VALIDATION =====
        
        public bool ValidateWeight(ItemInstance item, float currentWeight, float maxWeight)
        {
            if (item == null || item.Definition == null)
            {
                LogWarning("ValidateWeight: Item or Definition is null");
                return false;
            }
            
            float itemWeight = item.GetTotalWeight();
            float newTotalWeight = currentWeight + itemWeight;
            
            if (newTotalWeight > maxWeight)
            {
                if (!config.AllowOverweight)
                {
                    LogWarning($"ValidateWeight: Would exceed weight limit ({newTotalWeight}/{maxWeight})");
                    return false;
                }
                else
                {
                    Log($"ValidateWeight: Allowing overweight ({newTotalWeight}/{maxWeight})");
                }
            }
            
            return true;
        }
        
        // ===== STACK VALIDATION =====
        
        public bool CanStack(ItemInstance item1, ItemInstance item2)
        {
            if (item1 == null || item2 == null)
                return false;
            
            if (item1.Definition == null || item2.Definition == null)
                return false;
            
            // Must be same item type
            if (item1.Definition.ItemId != item2.Definition.ItemId)
                return false;
            
            // Must be stackable
            if (!item1.Definition.IsStackable)
                return false;
            
            // Check if adding won't exceed max stack size
            int totalStack = item1.StackSize + item2.StackSize;
            if (totalStack > item1.Definition.MaxStackSize)
            {
                Log($"CanStack: Total stack ({totalStack}) would exceed max ({item1.Definition.MaxStackSize})");
                // Still can stack, but will need to split
            }
            
            return true;
        }
        
        public bool ValidateStackSize(ItemInstance item)
        {
            if (item == null || item.Definition == null)
                return false;
            
            if (item.StackSize <= 0)
            {
                LogWarning($"ValidateStackSize: StackSize is {item.StackSize}");
                return false;
            }
            
            if (!item.Definition.IsStackable && item.StackSize > 1)
            {
                LogWarning($"ValidateStackSize: Non-stackable item has StackSize > 1");
                return false;
            }
            
            if (item.Definition.IsStackable && item.StackSize > item.Definition.MaxStackSize)
            {
                LogWarning($"ValidateStackSize: StackSize ({item.StackSize}) exceeds max ({item.Definition.MaxStackSize})");
                return false;
            }
            
            return true;
        }
        
        // ===== SLOT VALIDATION =====
        
        public bool ValidateEquipmentSlot(ItemInstance item, EquipmentSlotType slotType)
        {
            if (item == null || item.Definition == null)
            {
                LogWarning("ValidateEquipmentSlot: Item or Definition is null");
                return false;
            }
            
            // Check if item's slot matches target slot
            if (item.Definition.EquipmentSlot != slotType)
            {
                LogWarning($"ValidateEquipmentSlot: Item slot {item.Definition.EquipmentSlot} doesn't match target {slotType}");
                return false;
            }
            
            return true;
        }
        
        public bool ValidateWeaponSlot(ItemInstance item)
        {
            if (item == null || item.Definition == null)
            {
                LogWarning("ValidateWeaponSlot: Item or Definition is null");
                return false;
            }
            
            // Check if item is weapon type
            if (item.Definition.ItemType != ItemType.Weapon)
            {
                LogWarning($"ValidateWeaponSlot: Item type {item.Definition.ItemType} is not weapon");
                return false;
            }
            
            return true;
        }
        
        public bool ValidateQuickSlot(ItemInstance item, int quickSlotIndex)
        {
            if (item == null || item.Definition == null)
            {
                LogWarning("ValidateQuickSlot: Item or Definition is null");
                return false;
            }
            
            // Validate index
            if (quickSlotIndex < 0 || quickSlotIndex >= config.QuickSlotCount)
            {
                LogWarning($"ValidateQuickSlot: Invalid index {quickSlotIndex}");
                return false;
            }
            
            return true;
        }
        
        // ===== ATTACHMENT VALIDATION =====
        
        public bool ValidateAttachment(ItemInstance attachment, ItemInstance hostItem, AttachmentSlotType slotType)
        {
            if (attachment == null || attachment.Definition == null)
            {
                LogWarning("ValidateAttachment: Attachment or Definition is null");
                return false;
            }
            
            if (hostItem == null || hostItem.Definition == null)
            {
                LogWarning("ValidateAttachment: Host item or Definition is null");
                return false;
            }
            
            // Check if attachment is actually an attachment type
            if (attachment.Definition.ItemType != ItemType.Attachment)
            {
                LogWarning($"ValidateAttachment: Item type {attachment.Definition.ItemType} is not attachment");
                return false;
            }
            
            // Check if attachment's type matches requested slot
            if (attachment.Definition.AttachmentType != slotType)
            {
                LogWarning($"ValidateAttachment: Attachment type {attachment.Definition.AttachmentType} doesn't match slot {slotType}");
                return false;
            }
            
            // Check if host item has this slot type
            if (hostItem.Definition.AttachmentSlots == null || 
                !System.Array.Exists(hostItem.Definition.AttachmentSlots, slot => slot == slotType))
            {
                LogWarning($"ValidateAttachment: Host item doesn't have slot {slotType}");
                return false;
            }
            
            // Check if slot is already occupied
            if (hostItem.HasAttachment(slotType))
            {
                LogWarning($"ValidateAttachment: Slot {slotType} is already occupied");
                return false;
            }
            
            return true;
        }
        
        // ===== USAGE VALIDATION =====
        
        public bool ValidateItemUsage(ItemInstance item)
        {
            if (item == null || item.Definition == null)
            {
                LogWarning("ValidateItemUsage: Item or Definition is null");
                return false;
            }
            
            // Check if item is consumable
            if (item.Definition.ItemType != ItemType.Consumable)
            {
                LogWarning($"ValidateItemUsage: Item type {item.Definition.ItemType} is not consumable");
                return false;
            }
            
            // Check if item has resource (if applicable)
            if (item.Definition.ResourceType != ItemResourceType.None)
            {
                if (item.IsResourceDepleted())
                {
                    LogWarning($"ValidateItemUsage: Item resource is depleted");
                    return false;
                }
            }
            
            // Check stack size
            if (item.StackSize <= 0)
            {
                LogWarning($"ValidateItemUsage: Item stack is empty");
                return false;
            }
            
            return true;
        }
        
        // ===== RESOURCE VALIDATION =====
        
        public bool ValidateResourceRefill(ItemInstance item, float amount)
        {
            if (item == null || item.Definition == null)
            {
                LogWarning("ValidateResourceRefill: Item or Definition is null");
                return false;
            }
            
            if (item.Definition.ResourceType == ItemResourceType.None)
            {
                LogWarning("ValidateResourceRefill: Item has no resource type");
                return false;
            }
            
            if (amount < 0)
            {
                LogWarning($"ValidateResourceRefill: Amount cannot be negative ({amount})");
                return false;
            }
            
            return true;
        }
        
        // ===== INDEX VALIDATION =====
        
        public bool ValidateInventoryIndex(int index)
        {
            if (index < -1) // -1 is valid (means auto-find)
            {
                LogWarning($"ValidateInventoryIndex: Index {index} is invalid");
                return false;
            }
            
            return true;
        }
        
        // ===== LOGGING =====
        
        private void Log(string message)
        {
            if (config != null && config.EnableDebugLogs)
                UnityEngine.Debug.Log($"[InventoryValidator] {message}");
        }
        
        private void LogWarning(string message)
        {
            UnityEngine.Debug.LogWarning($"[InventoryValidator] {message}");
        }
    }
}