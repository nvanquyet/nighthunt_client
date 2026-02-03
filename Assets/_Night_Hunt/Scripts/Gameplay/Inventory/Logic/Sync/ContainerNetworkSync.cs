using UnityEngine;
using FishNet.Object;
using FishNet;
using NightHunt.InteractionSystem.Loot;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Utilities;
using NightHunt.Gameplay.Inventory;
using NightHunt.InteractionSystem.Inventory;

namespace NightHunt.Gameplay.Inventory.Logic.Sync
{
    /// <summary>
    /// Network sync component for container operations
    /// Handles ServerRpc for moving items between inventory and containers
    /// 
    /// ATTACHMENT LOCATION:
    /// This component should be attached to the PLAYER PREFAB (same GameObject as InventoryService)
    /// NOT on container prefabs. Each player needs their own instance to call ServerRpc.
    /// </summary>
    public class ContainerNetworkSync : NetworkBehaviour
    {
        private InventoryNetworkSync inventoryNetworkSync;
        private InventoryComponentBase inventoryComponent;

        private void Awake()
        {
            // Find InventoryNetworkSync on the same GameObject or parent
            inventoryNetworkSync = gameObject.FindInHierarchy<InventoryNetworkSync>();
            if (inventoryNetworkSync == null)
            {
                Debug.LogWarning(
                    "[ContainerNetworkSync] InventoryNetworkSync not found - item won't be added to inventory after removing from container");
            }

            // Find InventoryComponentBase to get ItemInstance
            inventoryComponent = gameObject.FindInHierarchy<InventoryComponentBase>();
        }

        /// <summary>
        /// Get ItemInstance from inventory by itemId (preserves state: durability, attachments, etc.)
        /// </summary>
        private ItemInstance? GetItemInstanceFromInventory(string itemId)
        {
            if (inventoryComponent == null)
                return null;

            // Try to find item in inventory
            var items = inventoryComponent.Items;
            if (items != null)
            {
                foreach (var item in items)
                {
                    if (item.itemDataId == itemId)
                    {
                        return item;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Get ItemInstance from container by itemId (preserves state)
        /// </summary>
        private ItemInstance? GetItemInstanceFromContainer(NetworkLootContainer container, string itemId)
        {
            if (container == null)
                return null;

            var items = container.GetItems();
            if (items != null)
            {
                foreach (var item in items)
                {
                    if (item.itemDataId == itemId)
                    {
                        return item;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Get NetworkObject by ID
        /// </summary>
        private NetworkObject GetNetworkObjectById(uint objectId)
        {
            if (InstanceFinder.NetworkManager != null && InstanceFinder.NetworkManager.ServerManager != null)
            {
                foreach (var kvp in InstanceFinder.NetworkManager.ServerManager.Objects.Spawned)
                {
                    if (kvp.Key == objectId)
                    {
                        return kvp.Value;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Move item from inventory to container (client calls this)
        /// Flow: Get ItemInstance from inventory → Add to container → Remove from inventory
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void MoveItemToContainerServerRpc(uint containerObjectId, string itemId, int quantity)
        {
            // #region agent log
            try
            {
                long ts = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                System.IO.File.AppendAllText(
                    @"w:\Unity\Shotter\.cursor\debug.log",
                    $"{{\"id\":\"log_{ts}_cns\",\"timestamp\":{ts},\"location\":\"ContainerNetworkSync.cs:MoveItemToContainerServerRpc\",\"message\":\"SERVER MoveItemToContainerServerRpc called\",\"data\":{{\"containerObjectId\":{containerObjectId},\"itemId\":\"{itemId}\",\"quantity\":{quantity},\"inventorySync\":{(inventoryNetworkSync!=null).ToString().ToLower()}}},\"sessionId\":\"debug-session\",\"runId\":\"run2\",\"hypothesisId\":\"H1\"}}\n"
                );
            }
            catch { /* ignore */ }
            // #endregion

            ItemInstance? itemInstance = GetItemInstanceFromInventory(itemId);
            if (!itemInstance.HasValue) return;
            NetworkObject networkObject = GetNetworkObjectById(containerObjectId);
            var container = networkObject.FindInHierarchy<NetworkLootContainer>();
            // 2. Add to container (server-side)
            bool added = container.AddItem(itemInstance.Value);
            // #region agent log
            try
            {
                long ts = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                System.IO.File.AppendAllText(
                    @"w:\Unity\Shotter\.cursor\debug.log",
                    $"{{\"id\":\"log_{ts}_cns\",\"timestamp\":{ts},\"location\":\"ContainerNetworkSync.cs:MoveItemToContainerServerRpc\",\"message\":\"SERVER container.AddItem result\",\"data\":{{\"itemId\":\"{itemId}\",\"added\":{added.ToString().ToLower()},\"qty\":{quantity}}},\"sessionId\":\"debug-session\",\"runId\":\"run6\",\"hypothesisId\":\"INV_TO_CONTAINER\"}}\n"
                );
            }
            catch { /* ignore */ }
            // #endregion
            if (!added) return;

            // 3. Remove from inventory (will sync via ObserversRpc)
            inventoryNetworkSync.RemoveItemServerRpc(itemId, quantity);

            // #region agent log
            try
            {
                long ts = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                System.IO.File.AppendAllText(
                    @"w:\Unity\Shotter\.cursor\debug.log",
                    $"{{\"id\":\"log_{ts}_cns\",\"timestamp\":{ts},\"location\":\"ContainerNetworkSync.cs:MoveItemToContainerServerRpc\",\"message\":\"SERVER called inventory remove\",\"data\":{{\"itemId\":\"{itemId}\",\"qty\":{quantity}}},\"sessionId\":\"debug-session\",\"runId\":\"run6\",\"hypothesisId\":\"INV_TO_CONTAINER\"}}\n"
                );
            }
            catch { /* ignore */ }
            // #endregion
        }

        /// <summary>
        /// Move item from container to inventory (client calls this)
        /// Flow: Get ItemInstance from container → Remove from container → Add to inventory
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void MoveItemFromContainerServerRpc(uint containerObjectId, string itemId, int quantity)
        {
            // #region agent log
            try
            {
                long ts = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                System.IO.File.AppendAllText(
                    @"w:\Unity\Shotter\.cursor\debug.log",
                    $"{{\"id\":\"log_{ts}_cns\",\"timestamp\":{ts},\"location\":\"ContainerNetworkSync.cs:MoveItemFromContainerServerRpc\",\"message\":\"SERVER MoveItemFromContainerServerRpc called\",\"data\":{{\"containerObjectId\":{containerObjectId},\"itemId\":\"{itemId}\",\"quantity\":{quantity}}},\"sessionId\":\"debug-session\",\"runId\":\"run5\",\"hypothesisId\":\"H_FROM_CONTAINER\"}}\n"
                );
            }
            catch { /* ignore */ }
            // #endregion

            NetworkObject networkObject = GetNetworkObjectById(containerObjectId);
            if (networkObject == null)
            {
                return;
            }

            var container = networkObject.FindInHierarchy<NetworkLootContainer>();
            if (container == null || !container.GetAllowRemoveItems())
            {
                return;
            }

            // Get actual ItemInstance from container (preserves state: durability, attachments, etc.)
            ItemInstance? itemInstance = GetItemInstanceFromContainer(container, itemId);
            if (!itemInstance.HasValue)
            {
                return;
            }

            // Use actual quantity from container (may be less than requested)
            int actualQuantity = Mathf.Min(itemInstance.Value.quantity, quantity);

            // Remove from container
            bool removed = container.RemoveItem(itemId, actualQuantity);
            if (!removed)
            {
                return;
            }

            // Add to inventory (will sync via ObserversRpc)
            // IMPORTANT: Preserve full ItemInstance state (durability, attachments, customData)
            if (inventoryNetworkSync != null && inventoryNetworkSync.IsSpawned)
            {
                // Use the actual ItemInstance from container to preserve all state
                inventoryNetworkSync.AddItemInstanceServer(itemInstance.Value);
            }
        }
    }
}