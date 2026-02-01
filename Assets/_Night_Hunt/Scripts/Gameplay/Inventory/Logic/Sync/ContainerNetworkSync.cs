using UnityEngine;
using FishNet.Object;
using FishNet;
using NightHunt.InteractionSystem.Loot;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Utilities;

namespace NightHunt.Gameplay.Inventory.Logic.Sync
{
    /// <summary>
    /// Network sync component for container operations
    /// Handles ServerRpc for moving items between inventory and containers
    /// </summary>
    public class ContainerNetworkSync : NetworkBehaviour
    {
        /// <summary>
        /// Move item from inventory to container (client calls this)
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void MoveItemToContainerServerRpc(uint containerObjectId, string itemId, int quantity)
        {
            // Get NetworkObject by ID - iterate through spawned objects
            NetworkObject networkObject = null;
            if (InstanceFinder.NetworkManager != null && InstanceFinder.NetworkManager.ServerManager != null)
            {
                foreach (var kvp in InstanceFinder.NetworkManager.ServerManager.Objects.Spawned)
                {
                    if (kvp.Key == containerObjectId)
                    {
                        networkObject = kvp.Value;
                        break;
                    }
                }
            }
            if (networkObject != null)
            {
                var container = networkObject.FindInHierarchy<NetworkLootContainer>();
                if (container != null)
                {
                    Debug.Log($"[ContainerNetworkSync] MoveItemToContainer - Container: {container.GetDisplayName()}, allowAddItems: {container.GetAllowAddItems()}");
                    
                    if (!container.GetAllowAddItems())
                    {
                        Debug.LogWarning($"[ContainerNetworkSync] MoveItemToContainer failed: Container '{container.GetDisplayName()}' does not allow adding items");
                        return;
                    }
                    
                    var item = new ItemInstance { itemDataId = itemId, quantity = quantity };
                    container.AddItem(item);
                }
            }
        }

        /// <summary>
        /// Move item from container to inventory (client calls this)
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void MoveItemFromContainerServerRpc(uint containerObjectId, string itemId, int quantity)
        {
            // Get NetworkObject by ID - iterate through spawned objects
            NetworkObject networkObject = null;
            if (InstanceFinder.NetworkManager != null && InstanceFinder.NetworkManager.ServerManager != null)
            {
                foreach (var kvp in InstanceFinder.NetworkManager.ServerManager.Objects.Spawned)
                {
                    if (kvp.Key == containerObjectId)
                    {
                        networkObject = kvp.Value;
                        break;
                    }
                }
            }
            if (networkObject != null)
            {
                var container = networkObject.FindInHierarchy<NetworkLootContainer>();
                if (container != null)
                {
                    Debug.Log($"[ContainerNetworkSync] MoveItemFromContainer - Container: {container.GetDisplayName()}, allowRemoveItems: {container.GetAllowRemoveItems()}");
                    
                    if (!container.GetAllowRemoveItems())
                    {
                        Debug.LogWarning($"[ContainerNetworkSync] MoveItemFromContainer failed: Container '{container.GetDisplayName()}' does not allow removing items");
                        return;
                    }
                    
                    container.RemoveItem(itemId, quantity);
                }
            }
        }
    }
}
