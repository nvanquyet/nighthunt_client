using UnityEngine;
using NightHunt.Inventory.Core.Enums;

namespace NightHunt.Inventory.Core.Config
{
    /// <summary>
    /// Configuration for inventory system behavior.
    /// Centralized settings for all inventory rules.
    /// </summary>
    [CreateAssetMenu(fileName = "InventoryConfig", menuName = "NightHunt/Inventory/Inventory Config")]
    public class InventoryConfig : ScriptableObject
    {
        [Header("Capacity Settings")]
        [Tooltip("Number of weapon slots (default: 2 - Primary & Secondary)")]
        public int WeaponSlotCount = 2;
        
        [Tooltip("Number of quickslots (default: 4)")]
        public int QuickSlotCount = 4;
        
        [Header("Weight Rules")]
        [Tooltip("Does equipped equipment count towards total weight?")]
        public bool EquipmentAddsWeight = false;
        
        [Tooltip("Do weapons count towards total weight when equipped?")]
        public bool WeaponsAddWeight = true;
        
        [Tooltip("Do items in quickslots count towards total weight?")]
        public bool QuickSlotItemsAddWeight = true;
        
        [Header("Equip Behavior")]
        [Tooltip("When equipping stackable items (e.g., health potions), equip full stack or just 1?")]
        public EquipStackBehavior StackableEquipBehavior = EquipStackBehavior.EquipFullStack;
        
        [Tooltip("When equipping non-stackable items (e.g., weapons, armor), what happens to rest of stack?")]
        public EquipStackBehavior NonStackableEquipBehavior = EquipStackBehavior.EquipOneReturnRest;
        
        [Header("Item Usage")]
        [Tooltip("Can player use items while moving?")]
        public bool CanUseItemsWhileMoving = true;
        
        [Tooltip("Does using item interrupt other actions?")]
        public bool ItemUsageInterruptsActions = true;
        
        [Tooltip("Default usage time for consumables (seconds)")]
        public float DefaultConsumableUsageTime = 3f;
        
        [Header("Validation")]
        [Tooltip("Allow adding items beyond weight capacity?")]
        public bool AllowOverweight = false;
        
        [Tooltip("Warning threshold for weight (0.0 - 1.0, e.g., 0.9 = 90%)")]
        [Range(0f, 1f)]
        public float WeightWarningThreshold = 0.9f;
        
        [Header("Stacking")]
        [Tooltip("Auto-stack items when adding to inventory?")]
        public bool AutoStackOnAdd = true;
        
        [Header("Debug")]
        [Tooltip("Enable verbose logging?")]
        public bool EnableDebugLogs = false;
        
        // ===== HELPER METHODS =====
        
        /// <summary>
        /// Get equip behavior for a specific item type.
        /// </summary>
        public EquipStackBehavior GetEquipBehavior(ItemType itemType)
        {
            // Consumables and stackable items use StackableEquipBehavior
            if (itemType == ItemType.Consumable)
            {
                return StackableEquipBehavior;
            }
            
            // Weapons, armor, etc. use NonStackableEquipBehavior
            return NonStackableEquipBehavior;
        }
        
        /// <summary>
        /// Check if item type should add weight when equipped.
        /// </summary>
        public bool DoesEquippedItemAddWeight(SlotLocationType locationType)
        {
            switch (locationType)
            {
                case SlotLocationType.Equipment:
                    return EquipmentAddsWeight;
                
                case SlotLocationType.Weapon:
                    return WeaponsAddWeight;
                
                case SlotLocationType.QuickSlot:
                    return QuickSlotItemsAddWeight;
                
                default:
                    return true; // Inventory items always add weight
            }
        }
    }
    
    /// <summary>
    /// Defines how stacks behave when equipping.
    /// </summary>
    public enum EquipStackBehavior
    {
        /// <summary>
        /// Equip entire stack (e.g., health potions in quickslot).
        /// </summary>
        EquipFullStack,
        
        /// <summary>
        /// Equip only 1, return rest to inventory (e.g., weapons).
        /// </summary>
        EquipOneReturnRest,
    }
}