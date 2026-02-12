using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using System;

namespace NightHunt.Inventory.Core.Interfaces
{
    /// <summary>
    /// Core inventory operations interface.
    /// Defines all possible actions on the inventory system.
    /// </summary>
    public interface IInventoryOperations
    {
        // ===== ITEM MANAGEMENT =====
        
        /// <summary>
        /// Add item to inventory at a specific index (or auto-find if -1).
        /// Handles stacking automatically.
        /// </summary>
        bool TryAddItem(ItemInstance item, int targetIndex = -1);
        
        /// <summary>
        /// Remove item from inventory completely.
        /// </summary>
        bool TryRemoveItem(string instanceId);
        
        /// <summary>
        /// Remove a specific quantity from a stack.
        /// </summary>
        bool TryRemoveQuantity(string instanceId, int quantity, out ItemInstance remainder);
        
        /// <summary>
        /// Drop item from inventory (logs for now, later spawns in world).
        /// </summary>
        bool TryDropItem(string instanceId, int quantity);
        
        // ===== MOVEMENT & TRANSFER =====
        
        /// <summary>
        /// Move item to a different inventory index.
        /// </summary>
        bool TryMoveItem(string instanceId, int newIndex);
        
        /// <summary>
        /// Equip item to equipment slot (swap if occupied).
        /// </summary>
        bool TryEquipToSlot(string instanceId, EquipmentSlotType slotType);
        
        /// <summary>
        /// Equip weapon to weapon slot (swap if occupied).
        /// </summary>
        bool TryEquipWeapon(string instanceId, int weaponSlotIndex);
        
        /// <summary>
        /// Assign item to quickslot (swap if occupied).
        /// </summary>
        bool TryAssignToQuickSlot(string instanceId, int quickSlotIndex);
        
        /// <summary>
        /// Unequip item from equipment slot back to inventory.
        /// </summary>
        bool TryUnequipFromSlot(EquipmentSlotType slotType);
        
        /// <summary>
        /// Unequip weapon from weapon slot back to inventory.
        /// </summary>
        bool TryUnequipWeapon(int weaponSlotIndex);
        
        /// <summary>
        /// Remove item from quickslot back to inventory.
        /// </summary>
        bool TryRemoveFromQuickSlot(int quickSlotIndex);
        
        // ===== ATTACHMENT SYSTEM =====
        
        /// <summary>
        /// Attach an item (attachment) to another item (host).
        /// </summary>
        bool TryAttachToItem(string attachmentInstanceId, string hostInstanceId, AttachmentSlotType slotType);
        
        /// <summary>
        /// Detach attachment from host item.
        /// </summary>
        bool TryDetachFromItem(string hostInstanceId, AttachmentSlotType slotType, out ItemInstance detachedAttachment);
        
        /// <summary>
        /// Swap attachments between two host items.
        /// </summary>
        bool TrySwapAttachments(string hostInstanceId1, string hostInstanceId2, AttachmentSlotType slotType);
        
        // ===== ITEM USAGE =====
        
        /// <summary>
        /// Use consumable item (starts usage timer if needed).
        /// </summary>
        bool TryUseItem(string instanceId);
        
        /// <summary>
        /// Cancel current item usage.
        /// </summary>
        void CancelItemUsage();
        
        // ===== RESOURCE MANAGEMENT =====
        
        /// <summary>
        /// Repair/refill item resource (durability, ammo, etc.).
        /// </summary>
        bool TryRefillResource(string instanceId, float amount);
        
        // ===== QUERIES =====
        
        /// <summary>
        /// Get item by instance ID from anywhere (inventory, equipment, weapons, quickslots).
        /// </summary>
        ItemInstance GetItem(string instanceId);
        
        /// <summary>
        /// Get current total weight of all items.
        /// </summary>
        float GetCurrentWeight();
        
        /// <summary>
        /// Check if can add item without exceeding weight limit.
        /// </summary>
        bool CanAddItem(ItemInstance item);
        
        /// <summary>
        /// Get item at specific inventory index.
        /// </summary>
        ItemInstance GetItemAtIndex(int index);
        
        /// <summary>
        /// Get equipped item in equipment slot.
        /// </summary>
        ItemInstance GetEquippedItem(EquipmentSlotType slotType);
        
        /// <summary>
        /// Get weapon in weapon slot.
        /// </summary>
        ItemInstance GetWeapon(int weaponSlotIndex);
        
        /// <summary>
        /// Get item in quickslot.
        /// </summary>
        ItemInstance GetQuickSlotItem(int quickSlotIndex);
        
        /// <summary>
        /// Find first available inventory index.
        /// </summary>
        int FindFirstAvailableIndex();
    }
}