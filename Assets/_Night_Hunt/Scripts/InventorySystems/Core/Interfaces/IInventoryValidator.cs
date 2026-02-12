using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;

namespace NightHunt.Inventory.Core.Interfaces
{
    /// <summary>
    /// Server-authoritative validation service.
    /// All operations must pass validation before execution.
    /// </summary>
    public interface IInventoryValidator
    {
        // ===== WEIGHT VALIDATION =====
        
        /// <summary>
        /// Check if adding item would exceed weight capacity.
        /// </summary>
        bool ValidateWeight(ItemInstance item, float currentWeight, float maxWeight);
        
        // ===== STACK VALIDATION =====
        
        /// <summary>
        /// Check if two items can be stacked together.
        /// </summary>
        bool CanStack(ItemInstance item1, ItemInstance item2);
        
        /// <summary>
        /// Validate stack size doesn't exceed max.
        /// </summary>
        bool ValidateStackSize(ItemInstance item);
        
        // ===== SLOT VALIDATION =====
        
        /// <summary>
        /// Check if item can be equipped to specific equipment slot.
        /// </summary>
        bool ValidateEquipmentSlot(ItemInstance item, EquipmentSlotType slotType);
        
        /// <summary>
        /// Check if item can be equipped as weapon.
        /// </summary>
        bool ValidateWeaponSlot(ItemInstance item);
        
        /// <summary>
        /// Check if item can be assigned to quickslot.
        /// </summary>
        bool ValidateQuickSlot(ItemInstance item, int quickSlotIndex);
        
        // ===== ATTACHMENT VALIDATION =====
        
        /// <summary>
        /// Check if attachment can be attached to host item.
        /// </summary>
        bool ValidateAttachment(ItemInstance attachment, ItemInstance hostItem, AttachmentSlotType slotType);
        
        // ===== USAGE VALIDATION =====
        
        /// <summary>
        /// Check if item can be used (consumable, not depleted, etc.).
        /// </summary>
        bool ValidateItemUsage(ItemInstance item);
        
        // ===== RESOURCE VALIDATION =====
        
        /// <summary>
        /// Check if resource refill is valid.
        /// </summary>
        bool ValidateResourceRefill(ItemInstance item, float amount);
        
        // ===== INDEX VALIDATION =====
        
        /// <summary>
        /// Check if inventory index is valid (not negative).
        /// </summary>
        bool ValidateInventoryIndex(int index);
    }
}