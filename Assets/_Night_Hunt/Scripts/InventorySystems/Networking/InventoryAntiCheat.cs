using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Domain.Stats;
using System.Collections.Generic;
using System.Linq;

namespace NightHunt.Inventory.Networking
{
    /// <summary>
    /// Server-side anti-cheat validation for inventory operations.
    /// Validates all client requests to prevent cheating.
    /// </summary>
    public static class InventoryAntiCheat
    {
        /// <summary>
        /// Validates that no duplicate item instances exist.
        /// </summary>
        public static bool ValidateNoDuplicateInstances(List<ItemInstance> allItems)
        {
            var uniqueIds = new HashSet<string>();
            
            foreach (var item in allItems)
            {
                if (!uniqueIds.Add(item.InstanceId))
                {
                    Debug.LogError($"[ANTI-CHEAT] Duplicate item instance detected: {item.InstanceId}");
                    return false;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Validates that total weight is within limits.
        /// </summary>
        public static bool ValidateWeightLimit(float totalWeight, float maxCapacity, WeightPenaltyConfig config)
        {
            if (config == null) return true;
            
            float maxAllowed = maxCapacity * (config.maxCapacityPercent / 100f);
            
            if (totalWeight > maxAllowed)
            {
                Debug.LogError($"[ANTI-CHEAT] Weight limit exceeded: {totalWeight:F2} > {maxAllowed:F2}");
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Validates item instance ownership.
        /// </summary>
        public static bool ValidateOwnership(ItemInstance item, uint playerNetworkId, ItemOwnershipRegistry registry)
        {
            if (registry == null) return true;
            
            var owner = registry.GetOwner(item.InstanceId);
            if (owner != playerNetworkId)
            {
                Debug.LogError($"[ANTI-CHEAT] Item ownership mismatch: {item.InstanceId} (owner: {owner}, claimer: {playerNetworkId})");
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Validates that item can be placed in target slot.
        /// </summary>
        public static bool ValidateSlotRules(ItemInstance item, Core.Enums.SlotLocationType targetSlot)
        {
            if (!item.Definition.AllowedSlotLocations.Contains(targetSlot))
            {
                Debug.LogError($"[ANTI-CHEAT] Invalid slot: {item.Definition.ItemId} cannot go in {targetSlot}");
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Validates stack size is within limits.
        /// </summary>
        public static bool ValidateStackSize(ItemInstance item)
        {
            if (item.StackSize > item.Definition.MaxStackSize)
            {
                Debug.LogError($"[ANTI-CHEAT] Stack overflow: {item.StackSize} > {item.Definition.MaxStackSize}");
                return false;
            }
            
            if (item.StackSize < 1)
            {
                Debug.LogError($"[ANTI-CHEAT] Invalid stack size: {item.StackSize}");
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Validates durability is within limits.
        /// </summary>
        public static bool ValidateDurability(ItemInstance item)
        {
            if (item.CurrentDurability < 0 || item.CurrentDurability > item.Definition.MaxDurability)
            {
                Debug.LogError($"[ANTI-CHEAT] Invalid durability: {item.CurrentDurability} (max: {item.Definition.MaxDurability})");
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Validates ammo count is reasonable.
        /// </summary>
        public static bool ValidateAmmo(ItemInstance item, int maxAmmoAllowed = 9999)
        {
            if (item.CurrentAmmo < 0 || item.CurrentAmmo > maxAmmoAllowed)
            {
                Debug.LogError($"[ANTI-CHEAT] Invalid ammo: {item.CurrentAmmo}");
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Validates attachment compatibility.
        /// </summary>
        public static bool ValidateAttachments(ItemInstance item)
        {
            foreach (var attachment in item.AttachedItems)
            {
                // Check if attachment type is in allowed slots
                if (!item.Definition.AttachmentSlots.Contains(attachment.Definition.AttachmentType))
                {
                    Debug.LogError($"[ANTI-CHEAT] Invalid attachment: {attachment.Definition.ItemId} on {item.Definition.ItemId}");
                    return false;
                }
                
                // Check for duplicate attachment types
                var duplicates = item.AttachedItems.Count(a => a.Definition.AttachmentType == attachment.Definition.AttachmentType);
                if (duplicates > 1)
                {
                    Debug.LogError($"[ANTI-CHEAT] Duplicate attachment type: {attachment.Definition.AttachmentType}");
                    return false;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Comprehensive validation of an item instance.
        /// </summary>
        public static bool ValidateItemInstance(ItemInstance item)
        {
            if (item == null)
            {
                Debug.LogError("[ANTI-CHEAT] Item is null");
                return false;
            }
            
            if (item.Definition == null)
            {
                Debug.LogError("[ANTI-CHEAT] Item definition is null");
                return false;
            }
            
            if (string.IsNullOrEmpty(item.InstanceId))
            {
                Debug.LogError("[ANTI-CHEAT] Item instance ID is empty");
                return false;
            }
            
            // Validate all properties
            if (!ValidateStackSize(item)) return false;
            if (!ValidateDurability(item)) return false;
            if (!ValidateAmmo(item)) return false;
            if (!ValidateAttachments(item)) return false;
            
            return true;
        }
        
        /// <summary>
        /// Validates entire inventory snapshot.
        /// </summary>
        public static bool ValidateInventorySnapshot(
            List<ItemInstance> allItems,
            float totalWeight,
            float maxCapacity,
            WeightPenaltyConfig weightConfig,
            ItemOwnershipRegistry ownershipRegistry,
            uint playerNetworkId)
        {
            // Check for duplicates
            if (!ValidateNoDuplicateInstances(allItems))
                return false;
            
            // Check weight
            if (!ValidateWeightLimit(totalWeight, maxCapacity, weightConfig))
                return false;
            
            // Validate each item
            foreach (var item in allItems)
            {
                if (!ValidateItemInstance(item))
                    return false;
                
                if (ownershipRegistry != null)
                {
                    if (!ValidateOwnership(item, playerNetworkId, ownershipRegistry))
                        return false;
                }
            }
            
            return true;
        }
    }
    
}