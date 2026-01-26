using System.Collections.Generic;
using NightHunt.InteractionSystem.Core.Structs;

namespace NightHunt.InteractionSystem.Core.Interfaces
{
    /// <summary>
    /// Interface for containers that can hold loot items (chests, corpses, etc.).
    /// </summary>
    public interface ILootContainer
    {
        /// <summary>
        /// Get all items in the container.
        /// </summary>
        List<ItemInstance> GetItems();

        /// <summary>
        /// Get the maximum number of slots in the container.
        /// </summary>
        int GetMaxSlots();

        /// <summary>
        /// Check if the container is locked.
        /// </summary>
        bool IsLocked();

        /// <summary>
        /// Add an item to the container.
        /// </summary>
        bool AddItem(ItemInstance item);

        /// <summary>
        /// Remove an item from the container by ID.
        /// </summary>
        bool RemoveItem(string itemId, int quantity = 1);

        /// <summary>
        /// Check if the container is empty.
        /// </summary>
        bool IsEmpty();

        /// <summary>
        /// Get the container display name.
        /// </summary>
        string GetDisplayName();
    }
}
