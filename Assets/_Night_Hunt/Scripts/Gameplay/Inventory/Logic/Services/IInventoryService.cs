using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.Gameplay.Inventory;
using NightHunt.Gameplay.UI;

namespace NightHunt.Gameplay.Inventory.Logic.Services
{
    /// <summary>
    /// Interface for inventory service
    /// Separates UI from business logic
    /// </summary>
    public interface IInventoryService
    {
        // Inventory operations
        bool AddItem(string itemId, int quantity);
        bool RemoveItem(string itemId, int quantity);
        bool MoveItem(string itemId, int fromX, int fromY, int toX, int toY);
        bool HasItem(string itemId, int quantity = 1);
        int GetItemQuantity(string itemId);

        // Equipment operations
        bool EquipItem(string itemId, EquipmentSlotType slotType);
        bool UnequipItem(EquipmentSlotType slotType);
        string GetEquippedItem(EquipmentSlotType slotType);

        // Quick slot operations
        bool AssignQuickSlot(int slotIndex, string itemId);
        bool ClearQuickSlot(int slotIndex);
        string GetQuickSlotItem(int slotIndex);

        // Grid operations
        InteractionSystem.Inventory.GridInventoryComponent GetGrid();
        (int width, int height) GetGridSize();
        ItemInstance? GetItemAt(int x, int y);
        
        // Get all items
        System.Collections.Generic.List<ItemInstance> GetItems();
    }
}
