using System.Collections.Generic;
using System.Linq;
using NightHunt.Inventory.Core;

namespace NightHunt.Inventory.Domain
{
    /// <summary>
    /// Calculates total weight of inventory, equipment, weapons, and quickslots.
    /// </summary>
    public static class WeightCalculator
    {
        public static float CalculateTotalWeight(
            InventoryData inventory, 
            List<ItemInstance> equipment, 
            List<ItemInstance> weapons, 
            List<ItemInstance> quickSlots)
        {
            float total = 0f;
            
            // Inventory items
            foreach (var slot in inventory.GetAllSlots())
            {
                if (slot.Item != null)
                    total += CalculateItemWeight(slot.Item);
            }
            
            // Equipment items (including attachments)
            foreach (var item in equipment)
            {
                total += CalculateItemWeight(item);
            }
            
            // Weapon items (including attachments)
            foreach (var weapon in weapons)
            {
                total += CalculateItemWeight(weapon);
            }
            
            // QuickSlot items
            foreach (var item in quickSlots)
            {
                total += CalculateItemWeight(item);
            }
            
            return total;
        }
        
        public static float CalculateItemWeight(ItemInstance item)
        {
            float weight = item.Definition.Weight * item.StackSize;
            
            // Add attachment weights
            foreach (var attachment in item.AttachedItems)
            {
                weight += attachment.Definition.Weight;
            }
            
            return weight;
        }
    }
}
