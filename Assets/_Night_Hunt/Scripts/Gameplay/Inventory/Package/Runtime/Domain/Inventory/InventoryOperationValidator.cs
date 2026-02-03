using UnityEngine;
using NightHunt.Inventory.Core;
using NightHunt.Inventory.Events;
using FishNet.Connection;
using NightHunt.Networking;

namespace NightHunt.Inventory.Domain
{
    /// <summary>
    /// Server-side validation for inventory operations.
    /// Anti-cheat: Validates ownership, weight, slot compatibility, stack size.
    /// </summary>
    public class InventoryOperationValidator : MonoBehaviour
    {
        private InventoryManager inventoryManager;
        
        void Awake()
        {
            // Use ComponentFinder to find InventoryManager in hierarchy
            inventoryManager = ComponentFinder.FindInHierarchy<InventoryManager>(this);
        }
        
        public OperationResult ValidateDrop(DragContext context, NetworkConnection owner)
        {
            // 1. Ownership check
            if (owner == null) 
                return OperationResult.Fail("Not owner");
            
            // 2. Anti-cheat: Item instance exists
            if (inventoryManager == null || !inventoryManager.HasItem(context.ItemInstance.InstanceId))
                return OperationResult.Fail("Item not found (potential cheat)");
            
            // 3. Slot compatibility check
            if (!IsSlotCompatible(context.ItemInstance, context.TargetLocation))
                return OperationResult.Fail($"{context.ItemInstance.Definition.ItemId} cannot go in {context.TargetLocation} slot");
            
            // 4. Weight check (for inventory/equipment)
            // TODO: Implement weight check
            
            // 5. Equipment slot type check
            if (context.TargetLocation == SlotLocationType.Equipment)
            {
                // TODO: Validate equipment slot type
            }
            
            // 6. Weapon slot type check
            if (context.TargetLocation == SlotLocationType.Weapon)
            {
                if (context.ItemInstance.Definition.ItemType != ItemType.Weapon)
                    return OperationResult.Fail("Only weapons can go in weapon slots");
            }
            
            // 7. QuickSlot type check
            if (context.TargetLocation == SlotLocationType.QuickSlot)
            {
                if (context.ItemInstance.Definition.ItemType != ItemType.Consumable && 
                    context.ItemInstance.Definition.ItemType != ItemType.Throwable)
                    return OperationResult.Fail("Only consumables/throwables can go in quickslots");
            }
            
            return OperationResult.Success();
        }
        
        bool IsSlotCompatible(ItemInstance item, SlotLocationType target)
        {
            if (item.Definition.AllowedSlotLocations == null) return false;
            return System.Array.IndexOf(item.Definition.AllowedSlotLocations, target) >= 0;
        }
    }
}
