using UnityEngine;
using FishNet.Object;
using NightHunt.Gameplay.Inventory.Events;

namespace NightHunt.Gameplay.Inventory.Logic.Sync
{
    /// <summary>
    /// Network sync component for item usage operations
    /// Handles ServerRpc for usage requests and ObserversRpc for progress updates
    /// Ensures all usage state changes are synced to all remote clients
    /// </summary>
    public class ItemUsageNetworkSync : NetworkBehaviour
    {
        #region Use Item (Network Sync)

        /// <summary>
        /// Client requests to use item - validated on server
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        public void UseItemServerRpc(string itemId)
        {
            // Validate on server
            if (!ValidateUseItem(itemId))
            {
                Debug.LogWarning($"[ItemUsageNetworkSync] UseItem validation failed: {itemId}");
                return;
            }

            // Fire event - ItemUsageSystem will handle the actual usage
            // Sync to all clients
            UseItemStartedObserversRpc(itemId);
        }

        /// <summary>
        /// Sync item usage started to all clients
        /// </summary>
        [ObserversRpc]
        private void UseItemStartedObserversRpc(string itemId)
        {
            // Fire event for UI layer (on all clients)
            InventoryLogicEvents.FireItemUseStarted(itemId);
        }

        /// <summary>
        /// Sync item usage progress to all clients
        /// </summary>
        [ObserversRpc]
        public void UseItemProgressObserversRpc(string itemId, float progress)
        {
            // Fire event for UI layer (on all clients)
            InventoryLogicEvents.FireItemUseProgress(itemId, progress);
        }

        /// <summary>
        /// Sync item usage completed to all clients
        /// </summary>
        [ObserversRpc]
        public void UseItemCompletedObserversRpc(string itemId)
        {
            // Fire event for UI layer (on all clients)
            InventoryLogicEvents.FireItemUseCompleted(itemId);
        }

        /// <summary>
        /// Sync item usage cancelled to all clients
        /// </summary>
        [ObserversRpc]
        public void UseItemCancelledObserversRpc(string itemId)
        {
            // Fire event for UI layer (on all clients)
            InventoryLogicEvents.FireItemUseCancelled(itemId);
        }

        /// <summary>
        /// Validate use item operation (server-side)
        /// </summary>
        private bool ValidateUseItem(string itemId)
        {
            // TODO: Add validation (item exists, cooldown, etc.)
            return true;
        }

        #endregion
    }
}
