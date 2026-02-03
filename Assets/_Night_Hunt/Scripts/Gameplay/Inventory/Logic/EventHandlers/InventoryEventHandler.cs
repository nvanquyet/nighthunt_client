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
using NightHunt.InteractionSystem.Equipment;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Core.Interfaces;
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
        private NightHunt.Gameplay.Weapons.WeaponNetworkSync weaponNetworkSync;
        private EquipmentHandler equipmentHandler;
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
            // These should be on the same GameObject as InventoryService (player prefab)
            if (inventoryService != null && inventoryService is MonoBehaviour mb)
            {
                networkSync = mb.FindInHierarchy<InventoryNetworkSync>();
                containerNetworkSync = mb.FindInHierarchy<ContainerNetworkSync>();
                shopNetworkSync = mb.FindInHierarchy<ShopNetworkSync>();
            }
            
            // Fallback: Try to find ContainerNetworkSync from NetworkPlayer if not found above
            if (containerNetworkSync == null && networkPlayer != null)
            {
                containerNetworkSync = networkPlayer.FindInHierarchy<ContainerNetworkSync>();
                if (containerNetworkSync == null)
                {
                    containerNetworkSync = networkPlayer.GetComponent<ContainerNetworkSync>();
                    if (containerNetworkSync == null)
                    {
                        containerNetworkSync = networkPlayer.GetComponentInChildren<ContainerNetworkSync>();
                    }
                }
            }
            
            // Fallback: Try to find ShopNetworkSync from NetworkPlayer if not found above
            if (shopNetworkSync == null && networkPlayer != null)
            {
                shopNetworkSync = networkPlayer.FindInHierarchy<ShopNetworkSync>();
                if (shopNetworkSync == null)
                {
                    shopNetworkSync = networkPlayer.GetComponent<ShopNetworkSync>();
                    if (shopNetworkSync == null)
                    {
                        shopNetworkSync = networkPlayer.GetComponentInChildren<ShopNetworkSync>();
                    }
                }
            }

            // Get weapon network sync
            if (networkPlayer != null)
            {
                var weaponSystem = ComponentRegistry.GetWeaponSwitchingSystem(networkPlayer);
                if (weaponSystem != null && weaponSystem is MonoBehaviour weaponMB)
                {
                    weaponNetworkSync = weaponMB.FindInHierarchy<NightHunt.Gameplay.Weapons.WeaponNetworkSync>();
                }
                
                // Get EquipmentHandler for attachment operations
                equipmentHandler = networkPlayer.FindInHierarchy<EquipmentHandler>();
                if (equipmentHandler == null)
                {
                    equipmentHandler = networkPlayer.GetComponent<EquipmentHandler>();
                    if (equipmentHandler == null)
                    {
                        equipmentHandler = networkPlayer.GetComponentInChildren<EquipmentHandler>();
                    }
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
            Debug.Log("[InventoryEventHandler] OnEnable - Subscribing to events");
            // #region agent log
            try
            {
                long ts = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                System.IO.File.AppendAllText(
                    @"w:\Unity\Shotter\.cursor\debug.log",
                    $"{{\"id\":\"log_{ts}_evt\",\"timestamp\":{ts},\"location\":\"InventoryEventHandler.cs:OnEnable\",\"message\":\"InventoryEventHandler enabled - subscribing\",\"data\":{{\"go\":\"{gameObject.name}\",\"activeInHierarchy\":{gameObject.activeInHierarchy.ToString().ToLower()}}},\"sessionId\":\"debug-session\",\"runId\":\"run5\",\"hypothesisId\":\"SUB\"}}\n"
                );
            }
            catch { /* ignore */ }
            // #endregion
            // Subscribe to UI events
            InventoryUIEvents.OnMoveItemRequested += HandleMoveItemRequested;
            InventoryUIEvents.OnMoveItemToSlotRequested += HandleMoveItemToSlotRequested;
            InventoryUIEvents.OnSwapItemsRequested += HandleSwapItemsRequested;
            InventoryUIEvents.OnRemoveItemRequested += HandleRemoveItemRequested;
            InventoryUIEvents.OnEquipItemRequested += HandleEquipItemRequested;
            InventoryUIEvents.OnUnequipItemRequested += HandleUnequipItemRequested;
            InventoryUIEvents.OnEquipWeaponRequested += HandleEquipWeaponRequested;
            InventoryUIEvents.OnAttachItemRequested += HandleAttachItemRequested;
            InventoryUIEvents.OnDetachItemRequested += HandleDetachItemRequested;
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
            
            Debug.Log("[InventoryEventHandler] All events subscribed successfully");
            Debug.Log($"[InventoryEventHandler] containerNetworkSync: {containerNetworkSync != null}, networkSync: {networkSync != null}");
        }

        private void OnDisable()
        {
            // #region agent log
            try
            {
                long ts = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                System.IO.File.AppendAllText(
                    @"w:\Unity\Shotter\.cursor\debug.log",
                    $"{{\"id\":\"log_{ts}_evt\",\"timestamp\":{ts},\"location\":\"InventoryEventHandler.cs:OnDisable\",\"message\":\"InventoryEventHandler disabled - unsubscribing\",\"data\":{{\"go\":\"{gameObject.name}\",\"activeInHierarchy\":{gameObject.activeInHierarchy.ToString().ToLower()}}},\"sessionId\":\"debug-session\",\"runId\":\"run5\",\"hypothesisId\":\"SUB\"}}\n"
                );
            }
            catch { /* ignore */ }
            // #endregion
            // Unsubscribe from UI events
            InventoryUIEvents.OnMoveItemRequested -= HandleMoveItemRequested;
            InventoryUIEvents.OnMoveItemToSlotRequested -= HandleMoveItemToSlotRequested;
            InventoryUIEvents.OnSwapItemsRequested -= HandleSwapItemsRequested;
            InventoryUIEvents.OnRemoveItemRequested -= HandleRemoveItemRequested;
            InventoryUIEvents.OnEquipItemRequested -= HandleEquipItemRequested;
            InventoryUIEvents.OnUnequipItemRequested -= HandleUnequipItemRequested;
            InventoryUIEvents.OnEquipWeaponRequested -= HandleEquipWeaponRequested;
            InventoryUIEvents.OnAttachItemRequested -= HandleAttachItemRequested;
            InventoryUIEvents.OnDetachItemRequested -= HandleDetachItemRequested;
            InventoryUIEvents.OnAssignQuickSlotRequested -= HandleAssignQuickSlotRequested;
            InventoryUIEvents.OnClearQuickSlotRequested -= HandleClearQuickSlotRequested;
            InventoryUIEvents.OnUseItemRequested -= HandleUseItemRequested;
            InventoryUIEvents.OnCancelUseItemRequested -= HandleCancelUseItemRequested;
            InventoryUIEvents.OnOpenContainerRequested -= HandleOpenContainerRequested;
            InventoryUIEvents.OnCloseContainerRequested -= HandleCloseContainerRequested;
            InventoryUIEvents.OnMoveItemToContainerRequested -= HandleMoveItemToContainerRequested;
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
            // #region agent log
            try
            {
                long ts = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                System.IO.File.AppendAllText(
                    @"w:\Unity\Shotter\.cursor\debug.log",
                    $"{{\"id\":\"log_{ts}_evt\",\"timestamp\":{ts},\"location\":\"InventoryEventHandler.cs:HandleMoveItemToSlotRequested\",\"message\":\"Logic received RequestMoveItemToSlot (NOT IMPLEMENTED)\",\"data\":{{\"itemId\":\"{itemId}\",\"toX\":{toX},\"toY\":{toY},\"networkSync\":{(networkSync!=null).ToString().ToLower()},\"networkSyncSpawned\":{(networkSync!=null && networkSync.IsSpawned).ToString().ToLower()}}},\"sessionId\":\"debug-session\",\"runId\":\"run2\",\"hypothesisId\":\"H2\"}}\n"
                );
            }
            catch { /* ignore */ }
            // #endregion

            Debug.LogWarning("[InventoryEventHandler] MoveItemToSlot not yet implemented - need to find item position first");
        }

        private void HandleSwapItemsRequested(string itemId1, int x1, int y1, string itemId2, int x2, int y2)
        {
            // Use network sync if available (multiplayer)
            if (networkSync != null && networkSync.IsSpawned)
            {
                // Call ServerRpc - server will validate and sync to all clients
                networkSync.SwapItemsServerRpc(itemId1, x1, y1, itemId2, x2, y2, networkPlayer);
            }
            else if (inventoryService != null)
            {
                // Fallback: Direct call if not networked (single player or testing)
                // Swap = Move item1 to temp, move item2 to item1's position, move item1 to item2's position
                // For now, use two move operations
                bool success1 = inventoryService.MoveItem(itemId1, x1, y1, x2, y2);
                if (success1)
                {
                    bool success2 = inventoryService.MoveItem(itemId2, x2, y2, x1, y1);
                    if (!success2)
                    {
                        // Rollback: move item1 back
                        inventoryService.MoveItem(itemId1, x2, y2, x1, y1);
                    }
                }
            }
        }

        private void HandleRemoveItemRequested(string itemId, int quantity)
        {
            // Use network sync if available (multiplayer)
            if (networkSync != null && networkSync.IsSpawned)
            {
                // Call ServerRpc - server will validate and sync to all clients
                networkSync.RemoveItemServerRpc(itemId, quantity);
            }
            else if (inventoryService != null)
            {
                // Fallback: Direct call if not networked (single player or testing)
                inventoryService.RemoveItem(itemId, quantity);
            }
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

        private void HandleEquipWeaponRequested(string weaponId, int weaponSlotIndex)
        {
            if (weaponNetworkSync == null || networkPlayer == null) return;
            
            // Call network sync - server will validate and sync to all clients
            if (weaponNetworkSync.IsSpawned)
            {
                weaponNetworkSync.EquipWeaponServerRpc(weaponId, weaponSlotIndex, networkPlayer);
            }
            else if (inventoryService != null && inventoryService is InventoryService concreteService)
            {
                // Fallback: Direct call if not networked (single player or testing)
                // Cast to concrete type because EquipWeapon is not in IInventoryService interface
                concreteService.EquipWeapon(weaponSlotIndex, weaponId);
            }
        }

        /// <summary>
        /// Handle attach item request (attach attachment to equipment)
        /// </summary>
        private void HandleAttachItemRequested(string attachmentItemId, EquipmentSlotType equipmentSlotType, string attachmentSlotTypeString)
        {
            Debug.Log($"[InventoryEventHandler] HandleAttachItemRequested - attachmentItemId: {attachmentItemId}, equipmentSlotType: {equipmentSlotType}, attachmentSlotType: {attachmentSlotTypeString}");
            
            if (equipmentHandler == null || networkPlayer == null)
            {
                Debug.LogWarning($"[InventoryEventHandler] EquipmentHandler or NetworkPlayer is null - equipmentHandler: {equipmentHandler != null}, networkPlayer: {networkPlayer != null}");
                return;
            }

            // Convert EquipmentSlotType (UI) to EquipmentSlot (EquipmentDataBase)
            EquipmentSlot equipmentSlot = ConvertEquipmentSlotTypeToEquipmentSlot(equipmentSlotType);
            
            // Parse attachmentSlotType string to enum
            if (!System.Enum.TryParse<AttachmentSlotType>(attachmentSlotTypeString, out AttachmentSlotType attachmentSlotType))
            {
                Debug.LogWarning($"[InventoryEventHandler] Invalid attachmentSlotType: {attachmentSlotTypeString}");
                return;
            }

            // Call EquipmentHandler ServerRpc - server will validate and sync to all clients
            if (equipmentHandler.IsSpawned)
            {
                Debug.Log($"[InventoryEventHandler] Calling AttachToEquipmentServerRpc - equipmentSlot: {equipmentSlot}, attachmentSlotType: {attachmentSlotType}");
                equipmentHandler.AttachToEquipment(equipmentSlot, attachmentSlotType, attachmentItemId);
            }
            else
            {
                Debug.LogWarning($"[InventoryEventHandler] EquipmentHandler is not spawned - cannot attach item");
            }
        }

        /// <summary>
        /// Handle detach item request (detach attachment from equipment)
        /// </summary>
        private void HandleDetachItemRequested(EquipmentSlotType equipmentSlotType, string attachmentSlotTypeString)
        {
            Debug.Log($"[InventoryEventHandler] HandleDetachItemRequested - equipmentSlotType: {equipmentSlotType}, attachmentSlotType: {attachmentSlotTypeString}");
            
            if (equipmentHandler == null || networkPlayer == null)
            {
                Debug.LogWarning($"[InventoryEventHandler] EquipmentHandler or NetworkPlayer is null - equipmentHandler: {equipmentHandler != null}, networkPlayer: {networkPlayer != null}");
                return;
            }

            // Convert EquipmentSlotType (UI) to EquipmentSlot (EquipmentDataBase)
            EquipmentSlot equipmentSlot = ConvertEquipmentSlotTypeToEquipmentSlot(equipmentSlotType);
            
            // Parse attachmentSlotType string to enum
            if (!System.Enum.TryParse<AttachmentSlotType>(attachmentSlotTypeString, out AttachmentSlotType attachmentSlotType))
            {
                Debug.LogWarning($"[InventoryEventHandler] Invalid attachmentSlotType: {attachmentSlotTypeString}");
                return;
            }

            // Call EquipmentHandler ServerRpc - server will validate and sync to all clients
            if (equipmentHandler.IsSpawned)
            {
                Debug.Log($"[InventoryEventHandler] Calling DetachFromEquipmentServerRpc - equipmentSlot: {equipmentSlot}, attachmentSlotType: {attachmentSlotType}");
                equipmentHandler.DetachFromEquipment(equipmentSlot, attachmentSlotType);
            }
            else
            {
                Debug.LogWarning($"[InventoryEventHandler] EquipmentHandler is not spawned - cannot detach item");
            }
        }

        /// <summary>
        /// Convert UI EquipmentSlotType to EquipmentDataBase EquipmentSlot
        /// </summary>
        private EquipmentSlot ConvertEquipmentSlotTypeToEquipmentSlot(EquipmentSlotType slotType)
        {
            switch (slotType)
            {
                case EquipmentSlotType.Helmet:
                    return EquipmentSlot.Head;
                case EquipmentSlotType.Armor:
                case EquipmentSlotType.Vest:
                    return EquipmentSlot.Body;
                case EquipmentSlotType.Backpack:
                    return EquipmentSlot.Backpack;
                default:
                    Debug.LogWarning($"[InventoryEventHandler] Unknown EquipmentSlotType: {slotType}, defaulting to Body");
                    return EquipmentSlot.Body;
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

            // #region agent log
            try
            {
                long ts = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                System.IO.File.AppendAllText(
                    @"w:\Unity\Shotter\.cursor\debug.log",
                    $"{{\"id\":\"log_{ts}_evt\",\"timestamp\":{ts},\"location\":\"InventoryEventHandler.cs:HandleMoveItemToContainerRequested\",\"message\":\"Logic received RequestMoveItemToContainer\",\"data\":{{\"containerId\":\"{containerId}\",\"itemId\":\"{itemId}\",\"fromX\":{fromX},\"fromY\":{fromY},\"containerNetworkSync\":{(containerNetworkSync!=null).ToString().ToLower()},\"containerNetworkSyncSpawned\":{(containerNetworkSync!=null && containerNetworkSync.IsSpawned).ToString().ToLower()}}},\"sessionId\":\"debug-session\",\"runId\":\"run6\",\"hypothesisId\":\"INV_TO_CONTAINER\"}}\n"
                );
            }
            catch { /* ignore */ }
            // #endregion

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
                            // #region agent log
                            try
                            {
                                long ts = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                                System.IO.File.AppendAllText(
                                    @"w:\Unity\Shotter\.cursor\debug.log",
                                    $"{{\"id\":\"log_{ts}_evt\",\"timestamp\":{ts},\"location\":\"InventoryEventHandler.cs:HandleMoveItemToContainerRequested\",\"message\":\"Calling ContainerNetworkSync.MoveItemToContainerServerRpc\",\"data\":{{\"containerId\":\"{containerId}\",\"objectId\":{objectId},\"itemId\":\"{itemId}\",\"qty\":{itemAtPos.Value.quantity},\"alsoCallingNetworkRemove\":{(networkSync!=null && networkSync.IsSpawned).ToString().ToLower()}}},\"sessionId\":\"debug-session\",\"runId\":\"run2\",\"hypothesisId\":\"H1\"}}\n"
                                );
                            }
                            catch { /* ignore */ }
                            // #endregion
                            containerNetworkSync.MoveItemToContainerServerRpc(objectId, itemId, itemAtPos.Value.quantity);
                        }
                        // IMPORTANT: Do NOT remove from inventory here.
                        // ContainerNetworkSync.MoveItemToContainerServerRpc already removes inventory on server
                        // via InventoryNetworkSync.RemoveItemServerRpc. Removing again here causes double-remove.
                    }
                }
            }
        }

        private void HandleMoveItemFromContainerRequested(string containerId, string itemId, int toX, int toY)
        {
            Debug.Log($"[InventoryEventHandler] HandleMoveItemFromContainerRequested called - containerId: {containerId}, itemId: {itemId}, toX: {toX}, toY: {toY}");
            // #region agent log
            try
            {
                long ts = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                System.IO.File.AppendAllText(
                    @"w:\Unity\Shotter\.cursor\debug.log",
                    $"{{\"id\":\"log_{ts}_evt\",\"timestamp\":{ts},\"location\":\"InventoryEventHandler.cs:HandleMoveItemFromContainerRequested\",\"message\":\"Logic received RequestMoveItemFromContainer\",\"data\":{{\"containerId\":\"{containerId}\",\"itemId\":\"{itemId}\",\"toX\":{toX},\"toY\":{toY},\"containerNetworkSync\":{(containerNetworkSync!=null).ToString().ToLower()},\"containerNetworkSyncSpawned\":{(containerNetworkSync!=null && containerNetworkSync.IsSpawned).ToString().ToLower()}}},\"sessionId\":\"debug-session\",\"runId\":\"run5\",\"hypothesisId\":\"H_FROM_CONTAINER\"}}\n"
                );
            }
            catch { /* ignore */ }
            // #endregion
            
            if (string.IsNullOrEmpty(containerId) || string.IsNullOrEmpty(itemId))
            {
                Debug.LogWarning($"[InventoryEventHandler] HandleMoveItemFromContainerRequested: containerId or itemId is null/empty");
                return;
            }

            // Find container
            if (uint.TryParse(containerId, out uint objectId))
            {
                Debug.Log($"[InventoryEventHandler] Parsed containerId to objectId: {objectId}");
                
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
                
                if (networkObject == null && InstanceFinder.NetworkManager != null && InstanceFinder.NetworkManager.ClientManager != null)
                {
                    // Try client manager if server manager didn't find it
                    foreach (var kvp in InstanceFinder.NetworkManager.ClientManager.Objects.Spawned)
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
                    Debug.Log($"[InventoryEventHandler] Found NetworkObject: {networkObject.name}");
                    var container = networkObject.GetComponent<NetworkLootContainer>();
                    if (container != null)
                    {
                        Debug.Log($"[InventoryEventHandler] Found NetworkLootContainer: {container.GetDisplayName()}");
                        
                        // Get item from container
                        var items = container.GetItems();
                        Debug.Log($"[InventoryEventHandler] Container has {items?.Count ?? 0} items");
                        var item = items.FirstOrDefault(i => i.itemDataId == itemId);
                        if (item.itemDataId == itemId)
                        {
                            Debug.Log($"[InventoryEventHandler] Found item in container: {itemId}, quantity: {item.quantity}");
                            
                            // Remove from container and add to inventory via ServerRpc
                            if (containerNetworkSync != null && containerNetworkSync.IsSpawned)
                            {
                                Debug.Log($"[InventoryEventHandler] Calling MoveItemFromContainerServerRpc");
                                // #region agent log
                                try
                                {
                                    long ts = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                                    System.IO.File.AppendAllText(
                                        @"w:\Unity\Shotter\.cursor\debug.log",
                                        $"{{\"id\":\"log_{ts}_evt\",\"timestamp\":{ts},\"location\":\"InventoryEventHandler.cs:HandleMoveItemFromContainerRequested\",\"message\":\"Calling ContainerNetworkSync.MoveItemFromContainerServerRpc\",\"data\":{{\"objectId\":{objectId},\"itemId\":\"{itemId}\",\"qty\":{item.quantity}}},\"sessionId\":\"debug-session\",\"runId\":\"run5\",\"hypothesisId\":\"H_FROM_CONTAINER\"}}\n"
                                    );
                                }
                                catch { /* ignore */ }
                                // #endregion
                                containerNetworkSync.MoveItemFromContainerServerRpc(objectId, itemId, item.quantity);
                            }
                            else
                            {
                                Debug.LogWarning($"[InventoryEventHandler] ContainerNetworkSync is null or not spawned - containerNetworkSync: {containerNetworkSync != null}, IsSpawned: {containerNetworkSync?.IsSpawned ?? false}");
                            }
                            // IMPORTANT: Do NOT add to inventory here.
                            // ContainerNetworkSync.MoveItemFromContainerServerRpc removes from container and adds to inventory
                            // on the server (and syncs to clients). Adding again here causes duplicates/desync and loses ItemInstance state.
                        }
                        else
                        {
                            Debug.LogWarning($"[InventoryEventHandler] Item {itemId} not found in container");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[InventoryEventHandler] NetworkObject does not have NetworkLootContainer component");
                    }
                }
                else
                {
                    Debug.LogWarning($"[InventoryEventHandler] NetworkObject not found for objectId: {objectId}");
                }
            }
            else
            {
                Debug.LogWarning($"[InventoryEventHandler] Failed to parse containerId to uint: {containerId}");
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
