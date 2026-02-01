using FishNet.Object;

namespace NightHunt.InteractionSystem.Inventory
{
    /// <summary>
    /// Interface for network-synchronized inventory operations.
    /// Implemented by Gameplay layer (e.g., InventoryNetworkSync) to provide network sync functionality.
    /// This allows package components (e.g., PickupHandler) to use network sync without depending on Gameplay layer.
    /// </summary>
    public interface IInventoryNetworkSync
    {
        /// <summary>
        /// Whether this component is spawned on the network.
        /// </summary>
        bool IsSpawned { get; }

        /// <summary>
        /// Server-only: Add item to inventory and sync to all clients.
        /// </summary>
        /// <param name="itemId">Item data ID</param>
        /// <param name="quantity">Quantity to add</param>
        void AddItemServer(string itemId, int quantity);
    }
}
