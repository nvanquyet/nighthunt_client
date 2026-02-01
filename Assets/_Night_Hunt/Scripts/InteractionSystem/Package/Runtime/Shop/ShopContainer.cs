using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Events;
using NightHunt.InteractionSystem.Inventory;
using NightHunt.InteractionSystem.Utilities;

namespace NightHunt.InteractionSystem.Shop
{
    /// <summary>
    /// Shop container that manages available items for purchase and purchased items storage
    /// Similar to LootContainer but with purchase/sell functionality
    /// </summary>
    [RequireComponent(typeof(InventoryComponentBase))]
    public class ShopContainer : NetworkBehaviour
    {
        [Header("Shop Settings")]
        [SerializeField] private string shopName = "Shop";
        [SerializeField] private int maxPurchasedItems = 12;

        [Header("Available Items")]
        [SerializeField] private List<ShopItemData> availableItems = new List<ShopItemData>();

        private readonly SyncList<ItemInstance> purchasedItems = new SyncList<ItemInstance>();
        private InventoryComponentBase purchasedItemsContainer;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            purchasedItems.OnChange += OnPurchasedItemsChanged;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (purchasedItems != null)
                purchasedItems.OnChange -= OnPurchasedItemsChanged;
        }

        private void Awake()
        {
            // Use ComponentFinder to search in hierarchy (component might be in child)
            purchasedItemsContainer = ComponentFinder.FindComponentInHierarchy<InventoryComponentBase>(gameObject, includeInactive: false);
            if (purchasedItemsContainer == null)
            {
                Debug.LogError("[ShopContainer] InventoryComponentBase not found in hierarchy!");
            }
        }

        /// <summary>
        /// Get shop display name
        /// </summary>
        public string GetDisplayName()
        {
            return shopName;
        }

        /// <summary>
        /// Get available items for purchase
        /// </summary>
        public List<ShopItemData> GetAvailableItems()
        {
            return new List<ShopItemData>(availableItems);
        }

        /// <summary>
        /// Get purchased items (in Items tab)
        /// </summary>
        public List<ItemInstance> GetPurchasedItems()
        {
            List<ItemInstance> items = new List<ItemInstance>();
            foreach (var item in purchasedItems)
            {
                items.Add(item);
            }
            return items;
        }

        /// <summary>
        /// Request to open shop (client calls this)
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestOpenShop(NetworkConnection conn)
        {
            // Send shop data to requesting player
            OpenShopForPlayer(conn);
        }

        /// <summary>
        /// Open shop for specific player
        /// </summary>
        [TargetRpc]
        private void OpenShopForPlayer(NetworkConnection conn)
        {
            // Invoke event for UI to listen
            InventoryEvents.InvokeShopOpened(this);
        }

        /// <summary>
        /// Purchase item from shop (server-side)
        /// </summary>
        [Server]
        public bool PurchaseItem(string itemId, NetworkConnection buyer)
        {
            // Find item in available items
            var shopItem = availableItems.Find(item => item.itemId == itemId);
            if (shopItem == null)
            {
                Debug.LogWarning($"[ShopContainer] Item {itemId} not available in shop");
                return false;
            }

            // Check stock
            if (shopItem.stock >= 0 && shopItem.currentStock <= 0)
            {
                Debug.LogWarning($"[ShopContainer] Item {itemId} is out of stock");
                return false;
            }

            // TODO: Check player money
            // For now, just proceed with purchase

            // Create item instance
            var itemInstance = new ItemInstance
            {
                itemDataId = itemId,
                quantity = 1
            };

            // Add to purchased items container
            if (purchasedItemsContainer != null)
            {
                if (purchasedItemsContainer.AddItem(itemInstance))
                {
                    // Update stock
                    if (shopItem.stock >= 0)
                    {
                        shopItem.currentStock--;
                    }

                    // Sync to all clients
                    PurchaseItemObserversRpc(itemId);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Sell item to shop (server-side)
        /// </summary>
        [Server]
        public bool SellItem(string itemId, int quantity, NetworkConnection seller)
        {
            // TODO: Check if item is sellable and get sell price
            // For now, just add to purchased items container

            var itemInstance = new ItemInstance
            {
                itemDataId = itemId,
                quantity = quantity
            };

            if (purchasedItemsContainer != null)
            {
                if (purchasedItemsContainer.AddItem(itemInstance))
                {
                    // Sync to all clients
                    SellItemObserversRpc(itemId, quantity);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Sync item purchased to all clients
        /// </summary>
        [ObserversRpc]
        private void PurchaseItemObserversRpc(string itemId)
        {
            // Fire event for UI layer
            // Events will be fired by InventoryService when item is added
        }

        /// <summary>
        /// Sync item sold to all clients
        /// </summary>
        [ObserversRpc]
        private void SellItemObserversRpc(string itemId, int quantity)
        {
            // Fire event for UI layer
            // Events will be fired by InventoryService when item is added
        }

        private void OnPurchasedItemsChanged(SyncListOperation op, int index, ItemInstance oldItem, ItemInstance newItem, bool asServer)
        {
            // Handle item changes
        }
    }

    /// <summary>
    /// Shop item data (item available for purchase)
    /// </summary>
    [System.Serializable]
    public class ShopItemData
    {
        public string itemId;
        public float price;
        public int stock = -1; // -1 for unlimited
        public int currentStock = -1;
    }
}
