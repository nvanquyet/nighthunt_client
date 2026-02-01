using UnityEngine;
using FishNet.Object;
using FishNet;
using NightHunt.InteractionSystem.Shop;
using NightHunt.Networking;

namespace NightHunt.Gameplay.Inventory.Logic.Sync
{
    /// <summary>
    /// Network sync component for shop operations
    /// Handles ServerRpc for purchasing and selling items
    /// </summary>
    public class ShopNetworkSync : NetworkBehaviour
    {
        /// <summary>
        /// Purchase item from shop (client calls this)
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void PurchaseItemServerRpc(uint shopObjectId, string itemId, NetworkPlayer buyer)
        {
            // Get NetworkObject by ID - iterate through spawned objects
            NetworkObject networkObject = null;
            if (InstanceFinder.NetworkManager != null && InstanceFinder.NetworkManager.ServerManager != null)
            {
                foreach (var kvp in InstanceFinder.NetworkManager.ServerManager.Objects.Spawned)
                {
                    if (kvp.Key == shopObjectId)
                    {
                        networkObject = kvp.Value;
                        break;
                    }
                }
            }
            if (networkObject != null)
            {
                var shop = networkObject.GetComponent<ShopContainer>();
                if (shop != null && buyer != null)
                {
                    // Purchase item (adds to shop's purchased items container)
                    bool success = shop.PurchaseItem(itemId, buyer.Owner);
                    
                    if (success)
                    {
                        // Add item to player's inventory via InventoryNetworkSync
                        // Find InventoryNetworkSync on buyer's GameObject
                        var inventorySync = buyer.GetComponent<InventoryNetworkSync>();
                        if (inventorySync == null)
                        {
                            inventorySync = buyer.GetComponentInChildren<InventoryNetworkSync>();
                        }
                        if (inventorySync == null)
                        {
                            inventorySync = buyer.GetComponentInParent<InventoryNetworkSync>();
                        }
                        
                        if (inventorySync != null)
                        {
                            inventorySync.AddItemServer(itemId, 1);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Sell item to shop (client calls this)
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void SellItemServerRpc(uint shopObjectId, string itemId, int quantity, NetworkPlayer seller)
        {
            // Get NetworkObject by ID - iterate through spawned objects
            NetworkObject networkObject = null;
            if (InstanceFinder.NetworkManager != null && InstanceFinder.NetworkManager.ServerManager != null)
            {
                foreach (var kvp in InstanceFinder.NetworkManager.ServerManager.Objects.Spawned)
                {
                    if (kvp.Key == shopObjectId)
                    {
                        networkObject = kvp.Value;
                        break;
                    }
                }
            }
            if (networkObject != null)
            {
                var shop = networkObject.GetComponent<ShopContainer>();
                if (shop != null && seller != null)
                {
                    // Sell item (adds to shop's purchased items container)
                    bool success = shop.SellItem(itemId, quantity, seller.Owner);
                    
                    if (success)
                    {
                        // Remove item from inventory via InventoryNetworkSync
                        var inventorySync = seller.GetComponent<InventoryNetworkSync>();
                        if (inventorySync == null)
                        {
                            inventorySync = seller.GetComponentInChildren<InventoryNetworkSync>();
                        }
                        if (inventorySync == null)
                        {
                            inventorySync = seller.GetComponentInParent<InventoryNetworkSync>();
                        }
                        
                        if (inventorySync != null)
                        {
                            inventorySync.RemoveItemServerRpc(itemId, quantity);
                        }
                        
                        // TODO: Add money to player
                    }
                }
            }
        }
    }
}
