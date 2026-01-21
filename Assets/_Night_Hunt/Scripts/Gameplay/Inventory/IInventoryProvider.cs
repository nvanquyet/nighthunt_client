using System.Collections.Generic;
using NightHunt.Gameplay.Inventory;

namespace NightHunt.Gameplay.Inventory
{
    /// <summary>
    /// Interface for inventory data provider
    /// Follows Interface Segregation Principle
    /// </summary>
    public interface IInventoryProvider
    {
        List<InventorySlot> GetItems();
        InventorySlot FindSlotWithItem(string itemId);
        bool AddItem(string itemId, int quantity);
        bool RemoveItem(string itemId, int quantity);
        float GetCurrentWeight();
        float GetWeightCapacity();
    }
}

