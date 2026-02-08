using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using System.Collections.Generic;

namespace NightHunt.Inventory.Core.Interfaces
{
    /// <summary>
    /// Generic interface for any container that holds items in slots.
    /// Implemented by: InventoryData, EquipmentData, WeaponData, QuickSlotData, ContainerData
    /// </summary>
    public interface ISlotContainer
    {
        /// <summary>Total number of slots</summary>
        int GetSlotCount();
        
        /// <summary>Get item at specific slot</summary>
        ItemInstance GetItemAtSlot(int slotIndex);
        
        /// <summary>Set item at specific slot (null to clear)</summary>
        bool SetItemAtSlot(int slotIndex, ItemInstance item);
        
        /// <summary>Check if slot is empty</summary>
        bool IsSlotEmpty(int slotIndex);
        
        /// <summary>Get all items in container</summary>
        List<ItemInstance> GetAllItems();
        
        /// <summary>Get number of empty slots</summary>
        int GetEmptySlotCount();
        
        /// <summary>Clear all slots</summary>
        void Clear();
        
        /// <summary>Find first empty slot index (-1 if none)</summary>
        int FindFirstEmptySlot();
        
        /// <summary>Check if container can accept this item type</summary>
        bool CanAcceptItem(ItemInstance item);
    }
}