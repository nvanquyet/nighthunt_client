using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NightHunt.Inventory.Core;
using NightHunt.Inventory.Domain;

namespace NightHunt.Inventory.Networking
{
    /// <summary>
    /// Anti-cheat validation for inventory operations.
    /// </summary>
    public static class InventoryAntiCheat
    {
        // 1. Item duplication check
        public static bool ValidateNoDuplicateInstances(InventorySnapshot snapshot)
        {
            var allInstances = snapshot.Items;
            var uniqueIds = new HashSet<string>();
            
            foreach (var item in allInstances)
            {
                if (!uniqueIds.Add(item.InstanceId))
                {
                    Debug.LogError($"[ANTI-CHEAT] Duplicate item instance detected: {item.InstanceId}");
                    return false;
                }
            }
            return true;
        }
        
        // 2. Weight limit check
        public static bool ValidateWeightLimit(float totalWeight, float maxCapacity, WeightPenaltyConfig config)
        {
            float maxAllowed = maxCapacity * (config.maxCapacityPercent / 100f);
            
            if (totalWeight > maxAllowed)
            {
                Debug.LogError($"[ANTI-CHEAT] Weight limit exceeded: {totalWeight} > {maxAllowed}");
                return false;
            }
            return true;
        }
        
        // 3. Slot compatibility
        public static bool ValidateSlotRules(ItemInstance item, SlotLocationType targetSlot)
        {
            if (item.Definition.AllowedSlotLocations == null) return false;
            
            if (!item.Definition.AllowedSlotLocations.Contains(targetSlot))
            {
                Debug.LogError($"[ANTI-CHEAT] Invalid slot: {item.Definition.ItemId} cannot go in {targetSlot}");
                return false;
            }
            return true;
        }
        
        // 4. Stack size validation
        public static bool ValidateStackSize(ItemInstance item)
        {
            if (item.StackSize > item.Definition.MaxStackSize)
            {
                Debug.LogError($"[ANTI-CHEAT] Stack overflow: {item.StackSize} > {item.Definition.MaxStackSize}");
                return false;
            }
            return true;
        }
    }
}
