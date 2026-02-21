using System;
using System.Collections.Generic;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.Core.Interfaces
{
    /// <summary>
    /// Interface for equipment management system
    /// 
    /// RESPONSIBILITIES:
    /// - Manages equipped items (head, chest, back, etc.)
    /// - Applies stat modifiers from equipment
    /// - Handles equipment slot management
    /// - Implemented by EquipmentSystem (NetworkBehaviour)
    /// 
    /// NETWORK ARCHITECTURE:
    /// - Server-authoritative: All operations on server
    /// - Client receives updates via SyncDictionary
    /// </summary>
    public interface IEquipmentSystem
    {
        #region Getters
        
        /// <summary>
        /// Get equipped item in specific slot
        /// Returns null if slot empty
        /// </summary>
        ItemInstance GetEquippedItem(EquipmentSlotType slotType);
        
        /// <summary>
        /// Get all equipped items as dictionary
        /// Key: SlotType, Value: ItemInstance
        /// </summary>
        Dictionary<EquipmentSlotType, ItemInstance> GetAllEquippedItems();
        
        /// <summary>
        /// Check if slot is occupied
        /// </summary>
        bool IsSlotOccupied(EquipmentSlotType slotType);
        
        /// <summary>
        /// Check if item can be equipped in slot
        /// Validates item type and slot compatibility
        /// </summary>
        bool CanEquipInSlot(string itemDefinitionID, EquipmentSlotType slotType);
        
        #endregion
        
        #region Equip/Unequip
        
        /// <summary>
        /// Equip item from inventory to equipment slot
        /// Auto-swaps if slot occupied
        /// Server-side only
        /// </summary>
        void EquipItem(string instanceID);
        
        /// <summary>
        /// Unequip item from slot back to inventory
        /// Server-side only
        /// </summary>
        void UnequipItem(EquipmentSlotType slotType);
        
        /// <summary>
        /// Swap equipped items between two slots
        /// </summary>
        void SwapEquipment(EquipmentSlotType slot1, EquipmentSlotType slot2);
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Event fired when item equipped
        /// Parameters: (slotType, item)
        /// </summary>
        event Action<EquipmentSlotType, ItemInstance> OnItemEquipped;
        
        /// <summary>
        /// Event fired when item unequipped
        /// Parameters: (slotType, item)
        /// </summary>
        event Action<EquipmentSlotType, ItemInstance> OnItemUnequipped;
        
        #endregion
    }
}
