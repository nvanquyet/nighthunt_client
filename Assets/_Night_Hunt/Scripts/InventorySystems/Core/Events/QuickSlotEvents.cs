using System;
using NightHunt.Inventory.Core.Data;

namespace NightHunt.Inventory.Core.Events
{
    /// <summary>
    /// Global events for QuickSlotSystem.
    /// Subscribe for UI updates and quick slot management.
    /// </summary>
    public static class QuickSlotEvents
    {
        // === Quick Slot Operations ===
        /// <summary>Fired when item is assigned to quick slot. Args: (item, quickSlotIndex)</summary>
        public static event Action<ItemInstance, int> OnQuickSlotAssigned;
        
        /// <summary>Fired when quick slot is cleared. Args: (quickSlotIndex)</summary>
        public static event Action<int> OnQuickSlotCleared;
        
        /// <summary>Fired when quick slot item is used. Args: (item, quickSlotIndex)</summary>
        public static event Action<ItemInstance, int> OnQuickSlotUsed;
        
        /// <summary>Fired when quick slot is updated (item changed). Args: (item, quickSlotIndex)</summary>
        public static event Action<ItemInstance, int> OnQuickSlotUpdated;
        
        // === Validation ===
        /// <summary>Fired when quick slot operation fails. Args: (result, quickSlotIndex, errorMessage)</summary>
        public static event Action<Core.Enums.OperationResult, int, string> OnQuickSlotOperationFailed;
        
        // === Invoke Methods ===
        public static void InvokeQuickSlotAssigned(ItemInstance item, int quickSlotIndex) 
            => OnQuickSlotAssigned?.Invoke(item, quickSlotIndex);
        
        public static void InvokeQuickSlotCleared(int quickSlotIndex) 
            => OnQuickSlotCleared?.Invoke(quickSlotIndex);
        
        public static void InvokeQuickSlotUsed(ItemInstance item, int quickSlotIndex) 
            => OnQuickSlotUsed?.Invoke(item, quickSlotIndex);
        
        public static void InvokeQuickSlotUpdated(ItemInstance item, int quickSlotIndex) 
            => OnQuickSlotUpdated?.Invoke(item, quickSlotIndex);
        
        public static void InvokeQuickSlotOperationFailed(Core.Enums.OperationResult result, int quickSlotIndex, string message) 
            => OnQuickSlotOperationFailed?.Invoke(result, quickSlotIndex, message);
    }
}
