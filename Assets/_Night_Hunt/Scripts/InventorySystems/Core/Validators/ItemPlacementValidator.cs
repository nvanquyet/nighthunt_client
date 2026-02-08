using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using System.Linq;

namespace NightHunt.Inventory.Core.Validators
{
    /// <summary>
    /// Validates item placement rules across the inventory system.
    /// Handles: Slot compatibility, requirements, restrictions.
    /// </summary>
    public static class ItemPlacementValidator
    {
        // === Slot Location Validation ===
        
        /// <summary>
        /// Check if item can be placed in specified slot location type.
        /// </summary>
        public static bool CanPlaceInLocation(ItemInstance item, SlotLocationType location)
        {
            if (item == null || item.Definition == null)
                return false;
            
            return item.Definition.IsAllowedInLocation(location);
        }
        
        /// <summary>
        /// Check if item can be equipped in specific equipment slot.
        /// </summary>
        public static bool CanEquipInSlot(ItemInstance item, EquipmentSlotType equipSlot)
        {
            if (item == null || item.Definition == null)
                return false;
            
            // Must be allowed in Equipment location
            if (!item.Definition.IsAllowedInLocation(SlotLocationType.Equipment))
                return false;
            
            // Must match equipment slot type
            return item.Definition.EquipmentSlot == equipSlot;
        }
        
        /// <summary>
        /// Check if weapon can be equipped in specific weapon slot.
        /// </summary>
        public static bool CanEquipWeaponInSlot(ItemInstance weapon, WeaponSlotType weaponSlot, ItemType[] allowedTypes = null)
        {
            if (weapon == null || weapon.Definition == null)
                return false;
            
            // Must be a weapon
            if (weapon.Definition.ItemType != ItemType.Weapon)
                return false;
            
            // Must be allowed in Weapon location
            if (!weapon.Definition.IsAllowedInLocation(SlotLocationType.Weapon))
                return false;
            
            // Check allowed types if specified
            if (allowedTypes != null && allowedTypes.Length > 0)
            {
                if (!allowedTypes.Contains(weapon.Definition.ItemType))
                    return false;
            }
            
            return true;
        }
        
        // === Requirement Validation ===
        
        /// <summary>
        /// Check if player meets all requirements to use this item.
        /// </summary>
        public static bool MeetsRequirements(ItemInstance item, PlayerData playerData)
        {
            if (item == null || item.Definition == null)
                return false;
            
            if (playerData == null)
            {
                Debug.LogWarning("[ItemPlacementValidator] PlayerData is null - cannot validate requirements");
                return true; // Allow if no player data (for testing)
            }
            
            if (item.Definition.Requirements == null || item.Definition.Requirements.Length == 0)
                return true; // No requirements
            
            foreach (var requirement in item.Definition.Requirements)
            {
                if (!MeetsRequirement(requirement, playerData))
                    return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Check if player meets a specific requirement.
        /// </summary>
        private static bool MeetsRequirement(ItemRequirement requirement, PlayerData playerData)
        {
            switch (requirement.Type)
            {
                case RequirementType.None:
                    return true;
                
                case RequirementType.PlayerLevel:
                    return playerData.Level >= requirement.RequiredAmount;
                
                case RequirementType.EventCompletion:
                    return playerData.HasCompletedEvent(requirement.RequirementId);
                
                case RequirementType.Achievement:
                    return playerData.HasAchievement(requirement.RequirementId);
                
                case RequirementType.Currency:
                    return playerData.GetCurrency(requirement.RequirementId) >= requirement.RequiredAmount;
                
                default:
                    Debug.LogWarning($"[ItemPlacementValidator] Unknown requirement type: {requirement.Type}");
                    return false;
            }
        }
        
        /// <summary>
        /// Get list of unmet requirements for display.
        /// </summary>
        public static ItemRequirement[] GetUnmetRequirements(ItemInstance item, PlayerData playerData)
        {
            if (item == null || item.Definition == null || playerData == null)
                return new ItemRequirement[0];
            
            if (item.Definition.Requirements == null || item.Definition.Requirements.Length == 0)
                return new ItemRequirement[0];
            
            return item.Definition.Requirements.Where(req => !MeetsRequirement(req, playerData)).ToArray();
        }
        
        // === Attachment Validation ===
        
        /// <summary>
        /// Check if attachment can be attached to parent item.
        /// </summary>
        public static bool CanAttach(ItemInstance parentItem, ItemInstance attachment)
        {
            if (parentItem == null || attachment == null)
                return false;
            
            if (parentItem.Definition == null || attachment.Definition == null)
                return false;
            
            return parentItem.CanAcceptAttachment(attachment);
        }
        
        /// <summary>
        /// Validate attachment with detailed reason.
        /// </summary>
        public static (bool canAttach, string reason) ValidateAttachment(ItemInstance parentItem, ItemInstance attachment)
        {
            if (parentItem == null)
                return (false, "Parent item is null");
            
            if (attachment == null)
                return (false, "Attachment is null");
            
            if (parentItem.Definition == null)
                return (false, "Parent item has no definition");
            
            if (attachment.Definition == null)
                return (false, "Attachment has no definition");
            
            // Check if parent has this attachment slot type
            if (!parentItem.Definition.HasAttachmentSlot(attachment.Definition.AttachmentType))
                return (false, $"Parent item does not have {attachment.Definition.AttachmentType} slot");
            
            // Check if slot already occupied
            if (parentItem.HasAttachment(attachment.Definition.AttachmentType))
                return (false, $"{attachment.Definition.AttachmentType} slot already occupied");
            
            return (true, "Can attach");
        }
        
        // === Stack Validation ===
        
        /// <summary>
        /// Check if two items can be stacked together.
        /// </summary>
        public static bool CanStack(ItemInstance item1, ItemInstance item2)
        {
            if (item1 == null || item2 == null)
                return false;
            
            if (item1.Definition == null || item2.Definition == null)
                return false;
            
            // Must be same item
            if (item1.Definition.ItemId != item2.Definition.ItemId)
                return false;
            
            // Must be stackable
            if (!item1.Definition.IsStackable || !item2.Definition.IsStackable)
                return false;
            
            // Check if either stack has room
            int maxStack = item1.Definition.MaxStackSize;
            if (item1.StackSize >= maxStack && item2.StackSize >= maxStack)
                return false;
            
            return true;
        }
        
        /// <summary>
        /// Check if item can be split.
        /// </summary>
        public static bool CanSplit(ItemInstance item, int splitAmount)
        {
            if (item == null || item.Definition == null)
                return false;
            
            if (!item.Definition.IsStackable)
                return false;
            
            if (splitAmount <= 0 || splitAmount >= item.StackSize)
                return false;
            
            return true;
        }
        
        // === Durability Validation ===
        
        /// <summary>
        /// Check if item is usable (not broken).
        /// </summary>
        public static bool IsUsable(ItemInstance item)
        {
            if (item == null || item.Definition == null)
                return false;
            
            // Check durability
            if (item.IsBroken())
                return false;
            
            return true;
        }
        
        /// <summary>
        /// Check if item can be repaired.
        /// </summary>
        public static bool CanRepair(ItemInstance item, bool allowRepairConfig)
        {
            if (item == null || item.Definition == null)
                return false;
            
            // Check config allows repair
            if (!allowRepairConfig)
                return false;
            
            // Check if item is damaged
            if (item.CurrentDurability >= item.Definition.MaxDurability)
                return false;
            
            return true;
        }
        
        // === Compatibility Validation ===
        
        /// <summary>
        /// Check if drag-drop operation is valid.
        /// </summary>
        public static (bool isValid, string reason) ValidateDragDrop(
            ItemInstance draggedItem,
            SlotLocationType sourceLocation,
            SlotLocationType targetLocation,
            EquipmentSlotType? targetEquipSlot = null,
            WeaponSlotType? targetWeaponSlot = null)
        {
            if (draggedItem == null || draggedItem.Definition == null)
                return (false, "Invalid item");
            
            // Check if item allowed in target location
            if (!draggedItem.Definition.IsAllowedInLocation(targetLocation))
                return (false, $"Item cannot be placed in {targetLocation}");
            
            // Validate equipment slot if applicable
            if (targetLocation == SlotLocationType.Equipment && targetEquipSlot.HasValue)
            {
                if (!CanEquipInSlot(draggedItem, targetEquipSlot.Value))
                    return (false, $"Item cannot be equipped in {targetEquipSlot.Value} slot");
            }
            
            // Validate weapon slot if applicable
            if (targetLocation == SlotLocationType.Weapon && targetWeaponSlot.HasValue)
            {
                if (!CanEquipWeaponInSlot(draggedItem, targetWeaponSlot.Value))
                    return (false, $"Item cannot be equipped in {targetWeaponSlot.Value} slot");
            }
            
            return (true, "Valid");
        }
    }
    
    /// <summary>
    /// Temporary player data structure for requirement validation.
    /// TODO: Replace with actual PlayerData class from game.
    /// </summary>
    public class PlayerData
    {
        public int Level { get; set; } = 1;
        
        private System.Collections.Generic.HashSet<string> completedEvents = new System.Collections.Generic.HashSet<string>();
        private System.Collections.Generic.HashSet<string> achievements = new System.Collections.Generic.HashSet<string>();
        private System.Collections.Generic.Dictionary<string, int> currencies = new System.Collections.Generic.Dictionary<string, int>();
        
        public bool HasCompletedEvent(string eventId)
        {
            return completedEvents.Contains(eventId);
        }
        
        public void CompleteEvent(string eventId)
        {
            completedEvents.Add(eventId);
        }
        
        public bool HasAchievement(string achievementId)
        {
            return achievements.Contains(achievementId);
        }
        
        public void UnlockAchievement(string achievementId)
        {
            achievements.Add(achievementId);
        }
        
        public int GetCurrency(string currencyId)
        {
            return currencies.ContainsKey(currencyId) ? currencies[currencyId] : 0;
        }
        
        public void SetCurrency(string currencyId, int amount)
        {
            currencies[currencyId] = amount;
        }
    }
}