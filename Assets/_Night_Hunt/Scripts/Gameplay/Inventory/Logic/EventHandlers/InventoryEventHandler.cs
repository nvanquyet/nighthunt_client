using UnityEngine;
using NightHunt.Gameplay.Inventory.Events;
using NightHunt.Gameplay.Inventory.Logic.Services;
using NightHunt.Gameplay.Inventory.Logic.Sync;
using NightHunt.Gameplay.Inventory;
using NightHunt.Gameplay.UI;
using NightHunt.Gameplay.Core;
using NightHunt.Networking;
using NightHunt.InteractionSystem.Utilities;
using NightHunt.InteractionSystem.Loot;
using NightHunt.InteractionSystem.Core.Structs;
using FishNet.Object;
using FishNet;
using System.Linq;

namespace NightHunt.Gameplay.Inventory.Logic.EventHandlers
{
    /// <summary>
    /// Event handler that bridges UI events to Logic services
    /// Subscribes to InventoryUIEvents and calls appropriate services
    /// This ensures separation between UI and Logic layers
    /// Uses network sync for multiplayer operations
    /// </summary>
    public class InventoryEventHandler : MonoBehaviour
    {
        private IInventoryService inventoryService;
        private IItemUsageService itemUsageService;
        private InventoryNetworkSync networkSync;
        private ContainerNetworkSync containerNetworkSync;
        private ShopNetworkSync shopNetworkSync;
        private NetworkPlayer networkPlayer;

        private void Awake()
        {
            // Get NetworkPlayer
            networkPlayer = gameObject.FindInHierarchy<NetworkPlayer>();
            if (networkPlayer == null)
            {
                Debug.LogError("[InventoryEventHandler] NetworkPlayer not found!");
                enabled = false;
                return;
            }

            // Get services from ComponentRegistry
            inventoryService = ComponentRegistry.GetInventoryService(networkPlayer) as IInventoryService;
            itemUsageService = ComponentRegistry.GetItemUsageSystem(networkPlayer) as IItemUsageService;

            // Get network sync components
            if (inventoryService != null && inventoryService is MonoBehaviour mb)
            {
                networkSync = mb.FindInHierarchy<InventoryNetworkSync>();
                containerNetworkSync = mb.FindInHierarchy<ContainerNetworkSync>();
                if (containerNetworkSync == null)
                {
                    containerNetworkSync = mb.FindInHierarchy<ContainerNetworkSync>();
                }
                shopNetworkSync = mb.FindInHierarchy<ShopNetworkSync>();
                if (shopNetworkSync == null)
                {
                    shopNetworkSync = mb.FindInHierarchy<ShopNetworkSync>();
                }
            }

            if (inventoryService == null)
            {
                Debug.LogWarning("[InventoryEventHandler] IInventoryService not found - some features may not work");
            }

            if (itemUsageService == null)
            {
                Debug.LogWarning("[InventoryEventHandler] IItemUsageService not found - item usage may not work");
            }

            if (networkSync == null)
            {
                Debug.LogWarning("[InventoryEventHandler] InventoryNetworkSync not found - network sync may not work");
            }
        }

        private void OnEnable()
        {
            // Subscribe to UI events
            InventoryUIEvents.OnMoveItemRequested += HandleMoveItemRequested;
            InventoryUIEvents.OnMoveItemToSlotRequested += HandleMoveItemToSlotRequested;
            InventoryUIEvents.OnEquipItemRequested += HandleEquipItemRequested;
            InventoryUIEvents.OnUnequipItemRequested += HandleUnequipItemRequested;
            InventoryUIEvents.OnAssignQuickSlotRequested += HandleAssignQuickSlotRequested;
            InventoryUIEvents.OnClearQuickSlotRequested += HandleClearQuickSlotRequested;
            InventoryUIEvents.OnUseItemRequested += HandleUseItemRequested;
            InventoryUIEvents.OnCancelUseItemRequested += HandleCancelUseItemRequested;
            InventoryUIEvents.OnOpenContainerRequested += HandleOpenContainerRequested;
            InventoryUIEvents.OnCloseContainerRequested += HandleCloseContainerRequested;
            InventoryUIEvents.OnMoveItemToContainerRequested += HandleMoveItemToContainerRequested;
            InventoryUIEvents.OnMoveItemFromContainerRequested += HandleMoveItemFromContainerRequested;
            InventoryUIEvents.OnPurchaseItemRequested += HandlePurchaseItemRequested;
            InventoryUIEvents.OnSellItemRequested += HandleSellItemRequested;
        }

        private void OnDisable()
        {
            // Unsubscribe from UI events
            InventoryUIEvents.OnMoveItemRequested -= HandleMoveItemRequested;
            InventoryUIEvents.OnMoveItemToSlotRequested -= HandleMoveItemToSlotRequested;
            InventoryUIEvents.OnEquipItemRequested -= HandleEquipItemRequested;
            InventoryUIEvents.OnUnequipItemRequested -= HandleUnequipItemRequested;
            InventoryUIEvents.OnAssignQuickSlotRequested -= HandleAssignQuickSlotRequested;
            InventoryUIEvents.OnClearQuickSlotRequested -= HandleClearQuickSlotRequested;
            InventoryUIEvents.OnUseItemRequested -= HandleUseItemRequested;
            InventoryUIEvents.OnCancelUseItemRequested -= HandleCancelUseItemRequested;
            InventoryUIEvents.OnOpenContainerRequested -= HandleOpenContainerRequested;
            InventoryUIEvents.OnCloseContainerRequested -= HandleCloseContainerRequested;
            InventoryUIEvents.OnMoveItemToContainerRequested -= HandleMoveItemFromContainerRequested;
            InventoryUIEvents.OnMoveItemFromContainerRequested -= HandleMoveItemFromContainerRequested;
            InventoryUIEvents.OnPurchaseItemRequested -= HandlePurchaseItemRequested;
            InventoryUIEvents.OnSellItemRequested -= HandleSellItemRequested;
        }

        // UI Event Handlers - All operations go through network sync for multiplayer
        private void HandleMoveItemRequested(string itemId, int fromX, int fromY, int toX, int toY)
        {
            // Use network sync if available (multiplayer)
            if (networkSync != null && networkSync.IsSpawned)
            {
                // Call ServerRpc - server will validate and sync to all clients
                networkSync.MoveItemServerRpc(itemId, fromX, fromY, toX, toY, networkPlayer);
            }
            else if (inventoryService != null)
            {
                // Fallback: Direct call if not networked (single player or testing)
                bool success = inventoryService.MoveItem(itemId, fromX, fromY, toX, toY);
                if (success)
                {
                    // Event will be fired by InventoryService
                }
            }
        }

        private void HandleMoveItemToSlotRequested(string itemId, int toX, int toY)
        {
            // Similar to HandleMoveItemRequested but for moving to specific slot
            // This would need to find the item's current position first
            Debug.LogWarning("[InventoryEventHandler] MoveItemToSlot not yet implemented - need to find item position first");
        }

        private void HandleEquipItemRequested(string itemId, EquipmentSlotType slotType)
        {
            if (networkSync == null || networkPlayer == null) return;
            
            // Call network sync - server will validate and sync to all clients
            if (networkSync.IsSpawned)
            {
                networkSync.EquipItemServerRpc(itemId, slotType, networkPlayer);
            }
        }

        private void HandleUnequipItemRequested(string itemId, EquipmentSlotType slotType)
        {
            if (networkSync == null || networkPlayer == null) return;
            
            // Call network sync - server will validate and sync to all clients
            if (networkSync.IsSpawned)
            {
                networkSync.UnequipItemServerRpc(slotType, networkPlayer);
            }
        }

        private void HandleAssignQuickSlotRequested(string itemId, int slotIndex)
        {
            if (networkSync == null || networkPlayer == null) return;
            
            // Call network sync - server will validate and sync to all clients
            if (networkSync.IsSpawned)
            {
                networkSync.AssignQuickSlotServerRpc(slotIndex, itemId, networkPlayer);
            }
        }

        private void HandleClearQuickSlotRequested(int slotIndex)
        {
            if (networkSync == null || networkPlayer == null) return;
            
            // Call ServerRpc - server will validate and sync to all clients
            if (networkSync.IsSpawned)
            {
                networkSync.ClearQuickSlotServerRpc(slotIndex, networkPlayer);
            }
        }

        private void HandleUseItemRequested(string itemId)
        {
            if (networkSync == null || networkPlayer == null) return;
            
            // Call ServerRpc - server will validate and sync to all clients
            if (networkSync.IsSpawned)
            {
                networkSync.UseItemServerRpc(itemId, networkPlayer);
            }
        }

        private void HandleCancelUseItemRequested(string itemId)
        {
            if (itemUsageService == null) return;
            
            bool success = itemUsageService.CancelUseItem(itemId);
            if (success)
            {
                // Event will be fired by ItemUsageService
            }
        }

        private void HandleOpenContainerRequested(string containerId)
        {
            if (string.IsNullOrEmpty(containerId))
                return;

            // Try to parse as NetworkObject ID
            if (uint.TryParse(containerId, out uint objectId))
            {
                // Find NetworkObject by ID
                // Get NetworkObject by ID - iterate through spawned objects
                NetworkObject networkObject = null;
                if (InstanceFinder.NetworkManager != null && InstanceFinder.NetworkManager.ServerManager != null)
                {
                    foreach (var kvp in InstanceFinder.NetworkManager.ServerManager.Objects.Spawned)
                    {
                        if (kvp.Key == objectId)
                        {
                            networkObject = kvp.Value;
                            break;
                        }
                    }
                }
                if (networkObject != null)
                {
                    var container = networkObject.GetComponent<NetworkLootContainer>();
                    if (container != null && networkPlayer != null)
                    {
                        // Call ServerRpc to open container
                        container.RequestOpenContainer(networkPlayer.Owner);
                    }
                }
            }
        }

        private void HandleCloseContainerRequested(string containerId)
        {
            // Fire event to close container in UI
            // UI layer will handle closing the panel
            Debug.Log($"[InventoryEventHandler] CloseContainer requested: {containerId}");
        }

        private void HandleMoveItemToContainerRequested(string containerId, string itemId, int fromX, int fromY)
        {
            if (string.IsNullOrEmpty(containerId) || string.IsNullOrEmpty(itemId))
                return;

            // Get item from inventory
            if (inventoryService == null)
                return;

            var itemAtPos = inventoryService.GetItemAt(fromX, fromY);
            if (!itemAtPos.HasValue || itemAtPos.Value.itemDataId != itemId)
            {
                Debug.LogWarning($"[InventoryEventHandler] MoveItemToContainer: Item {itemId} not found at ({fromX}, {fromY})");
                return;
            }

            // Find container
            if (uint.TryParse(containerId, out uint objectId))
            {
                // Get NetworkObject by ID - iterate through spawned objects
                NetworkObject networkObject = null;
                if (InstanceFinder.NetworkManager != null && InstanceFinder.NetworkManager.ServerManager != null)
                {
                    foreach (var kvp in InstanceFinder.NetworkManager.ServerManager.Objects.Spawned)
                    {
                        if (kvp.Key == objectId)
                        {
                            networkObject = kvp.Value;
                            break;
                        }
                    }
                }
                if (networkObject != null)
                {
                    var container = networkObject.GetComponent<NetworkLootContainer>();
                    if (container != null)
                    {
                        // Remove from inventory and add to container via ServerRpc
                        if (containerNetworkSync != null && containerNetworkSync.IsSpawned)
                        {
                            containerNetworkSync.MoveItemToContainerServerRpc(objectId, itemId, itemAtPos.Value.quantity);
                        }
                        
                        // Remove from inventory (will be synced via network)
                        if (networkSync != null && networkSync.IsSpawned)
                        {
                            networkSync.RemoveItemServerRpc(itemId, itemAtPos.Value.quantity);
                        }
                        else
                        {
                            inventoryService.RemoveItem(itemId, itemAtPos.Value.quantity);
                        }
                    }
                }
            }
        }

        private void HandleMoveItemFromContainerRequested(string containerId, string itemId, int toX, int toY)
        {
            if (string.IsNullOrEmpty(containerId) || string.IsNullOrEmpty(itemId))
                return;

            // Find container
            if (uint.TryParse(containerId, out uint objectId))
            {
                // Get NetworkObject by ID - iterate through spawned objects
                NetworkObject networkObject = null;
                if (InstanceFinder.NetworkManager != null && InstanceFinder.NetworkManager.ServerManager != null)
                {
                    foreach (var kvp in InstanceFinder.NetworkManager.ServerManager.Objects.Spawned)
                    {
                        if (kvp.Key == objectId)
                        {
                            networkObject = kvp.Value;
                            break;
                        }
                    }
                }
                if (networkObject != null)
                {
                    var container = networkObject.GetComponent<NetworkLootContainer>();
                    if (container != null)
                    {
                        // Get item from container
                        var items = container.GetItems();
                        var item = items.FirstOrDefault(i => i.itemDataId == itemId);
                        if (item.itemDataId == itemId)
                        {
                            // Remove from container and add to inventory via ServerRpc
                            if (containerNetworkSync != null && containerNetworkSync.IsSpawned)
                            {
                                containerNetworkSync.MoveItemFromContainerServerRpc(objectId, itemId, item.quantity);
                            }
                            
                            // Add to inventory (will be synced via network)
                            if (networkSync != null && networkSync.IsSpawned)
                            {
                                networkSync.AddItemServer(itemId, item.quantity);
                            }
                            else
                            {
                                inventoryService.AddItem(itemId, item.quantity);
                            }
                        }
                    }
                }
            }
        }


        private void HandlePurchaseItemRequested(string shopId, string itemId)
        {
            if (string.IsNullOrEmpty(shopId) || string.IsNullOrEmpty(itemId))
                return;

            if (shopNetworkSync == null || networkPlayer == null)
            {
                Debug.LogWarning("[InventoryEventHandler] ShopNetworkSync or NetworkPlayer is null!");
                return;
            }

            // Find shop container
            if (uint.TryParse(shopId, out uint objectId))
            {
                // Call ServerRpc to purchase item
                if (shopNetworkSync.IsSpawned)
                {
                    shopNetworkSync.PurchaseItemServerRpc(objectId, itemId, networkPlayer);
                }
                else
                {
                    Debug.LogWarning("[InventoryEventHandler] ShopNetworkSync is not spawned!");
                }
            }
        }

        private void HandleSellItemRequested(string shopId, string itemId)
        {
            if (string.IsNullOrEmpty(shopId) || string.IsNullOrEmpty(itemId))
                return;

            // Get item from inventory
            if (inventoryService == null)
                return;

            // Find item in inventory
            var items = inventoryService.GetItems();
            var item = items.Find(i => i.itemDataId == itemId);
            if (item.itemDataId != itemId)
            {
                Debug.LogWarning($"[InventoryEventHandler] SellItem: Item {itemId} not found in inventory");
                return;
            }

            if (shopNetworkSync == null || networkPlayer == null)
            {
                Debug.LogWarning("[InventoryEventHandler] ShopNetworkSync or NetworkPlayer is null!");
                return;
            }

            // Find shop container
            if (uint.TryParse(shopId, out uint objectId))
            {
                // Call ServerRpc to sell item
                if (shopNetworkSync.IsSpawned)
                {
                    shopNetworkSync.SellItemServerRpc(objectId, itemId, item.quantity, networkPlayer);
                }
                else
                {
                    Debug.LogWarning("[InventoryEventHandler] ShopNetworkSync is not spawned!");
                }
            }
        }
    }
}
