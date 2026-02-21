using System;
using System.Collections.Generic;
using NightHunt.GameplaySystems.Inventory;
using NightHunt.GameplaySystems.Core.Data;

namespace NightHunt.GameplaySystems.Core.Interfaces
{
    /// <summary>
    /// Interface for quick slot management system
    /// 
    /// RESPONSIBILITIES:
    /// - Manages quick access slots (hotkeys 1-4)
    /// - Only allows consumables and throwables
    /// - Coordinates with ItemUseSystem for item usage
    /// - Implemented by QuickSlotSystem (NetworkBehaviour)
    /// 
    /// NETWORK ARCHITECTURE:
    /// - Server-authoritative: All operations on server
    /// - Client receives updates via SyncList
    /// </summary>
    public interface IQuickSlotSystem
    {
        #region Getters
        
        /// <summary>
        /// Get item in specific quick slot index (0-3)
        /// Returns null if slot empty
        /// </summary>
        ItemInstance GetQuickSlotItem(int slotIndex);
        
        /// <summary>
        /// Get all quick slot items
        /// Array length = QuickSlotCount from config
        /// null = empty slot
        /// </summary>
        ItemInstance[] GetAllQuickSlots();
        
        /// <summary>
        /// Check if slot is occupied
        /// </summary>
        bool IsSlotOccupied(int slotIndex);
        
        /// <summary>
        /// Check if item can be placed in quick slot
        /// Validates item type (consumable/throwable only)
        /// </summary>
        bool CanPlaceInQuickSlot(string itemDefinitionID);
        
        /// <summary>
        /// Get quick slot count from config
        /// </summary>
        int GetQuickSlotCount();
        
        #endregion
        
        #region Assign/Remove
        
        /// <summary>
        /// Assign item from inventory to quick slot
        /// Item stays in inventory, just referenced
        /// Server-side only
        /// </summary>
        void AssignToQuickSlot(string instanceID, int slotIndex);
        
        /// <summary>
        /// Remove item from quick slot
        /// Item stays in inventory
        /// Server-side only
        /// </summary>
        void RemoveFromQuickSlot(int slotIndex);
        
        /// <summary>
        /// Swap items between two quick slots
        /// </summary>
        void SwapQuickSlots(int slotIndex1, int slotIndex2);
        
        /// <summary>
        /// Clear all quick slots
        /// </summary>
        void ClearAllQuickSlots();
        
        #endregion
        
        #region Usage
        
        /// <summary>
        /// Use item in quick slot
        /// Consumes item if successful
        /// Server-side only
        /// </summary>
        void UseQuickSlot(int slotIndex);
        
        /// <summary>
        /// Check if can use quick slot
        /// Validates item exists, has quantity, can be used
        /// </summary>
        bool CanUseQuickSlot(int slotIndex);
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Event fired when item assigned to quick slot
        /// Parameters: (slotIndex, item)
        /// </summary>
        event Action<int, ItemInstance> OnQuickSlotAssigned;
        
        /// <summary>
        /// Event fired when item removed from quick slot
        /// Parameters: (slotIndex)
        /// </summary>
        event Action<int> OnQuickSlotRemoved;
        
        /// <summary>
        /// Event fired when quick slot used
        /// Parameters: (slotIndex, item)
        /// </summary>
        event Action<int, ItemInstance> OnQuickSlotUsed;
        
        #endregion
    }
}
