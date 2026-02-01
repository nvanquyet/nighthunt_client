using UnityEngine;
using FishNet.Object;
using NightHunt.Gameplay.UI;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Inventory;
using NightHunt.InteractionSystem.Utilities;
using NightHunt.Networking;

namespace NightHunt.Gameplay.Inventory.Logic.Sync
{
    /// <summary>
    /// Network sync component for inventory operations
    /// Handles ServerRpc (client -> server) and ObserversRpc (server -> all clients)
    /// Ensures all inventory changes are validated on server and synced to all remote clients
    /// </summary>
    [RequireComponent(typeof(InventoryComponentBase))]
    public class InventoryNetworkSync : NetworkBehaviour, IInventoryNetworkSync
    {
        private InventoryComponentBase inventoryComponent;
        private GridInventoryComponent gridInventory;
        private InventoryService inventoryService; 

        private void Awake()
        {
            // Try to find InventoryComponentBase in this object or parent
            inventoryComponent = gameObject.FindInHierarchy<InventoryComponentBase>();
            if (inventoryComponent == null)
            {
                inventoryComponent = gameObject.FindInHierarchy<InventoryComponentBase>();
            }
            
            gridInventory = inventoryComponent as GridInventoryComponent;
            
            // Find InventoryService
            inventoryService = gameObject.FindInHierarchy<InventoryService>();
            if (inventoryService == null)
            {
                inventoryService = gameObject.FindInHierarchy<InventoryService>();
            }
            
            if (inventoryComponent == null)
            {
                Debug.LogError("[InventoryNetworkSync] InventoryComponentBase not found! Please attach to GameObject with InventoryComponentBase or its parent.");
            }
        }

        #region Move Item (Network Sync)

        /// <summary>
        /// Client requests to move item - validated on server
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        public void MoveItemServerRpc(string itemId, int fromX, int fromY, int toX, int toY, NetworkPlayer player)
        {
            // Validate on server
            if (!ValidateMoveItem(itemId, fromX, fromY, toX, toY))
            {
                Debug.LogWarning($"[InventoryNetworkSync] MoveItem validation failed: {itemId} from ({fromX}, {fromY}) to ({toX}, {toY})");
                return;
            }

            // Process move on server using InventoryService
            bool success = false;
            if (inventoryService != null)
            {
                success = inventoryService.MoveItem(itemId, fromX, fromY, toX, toY);
            }
            
            if (success)
            {
                // Sync to all clients
                MoveItemObserversRpc(itemId, fromX, fromY, toX, toY);
            }
        }

        /// <summary>
        /// Sync item move to all clients
        /// </summary>
        [ObserversRpc]
        private void MoveItemObserversRpc(string itemId, int fromX, int fromY, int toX, int toY)
        {
            // Only process on clients (not on server again)
            if (IsServer)
                return;

            // Process move on client using InventoryService
            bool success = false;
            if (inventoryService != null)
            {
                success = inventoryService.MoveItem(itemId, fromX, fromY, toX, toY);
            }
            
            // Events will be fired by InventoryService automatically
        }

        /// <summary>
        /// Validate move item operation (server-side)
        /// </summary>
        private bool ValidateMoveItem(string itemId, int fromX, int fromY, int toX, int toY)
        {
            if (inventoryService == null)
                return false;

            // Check if source position has the item
            var sourceItem = inventoryService.GetItemAt(fromX, fromY);
            if (!sourceItem.HasValue || sourceItem.Value.itemDataId != itemId)
            {
                Debug.LogWarning($"[InventoryNetworkSync] ValidateMoveItem: Source item mismatch or empty");
                return false;
            }

            // Check if destination is valid (not out of bounds)
            (int width, int height) = inventoryService.GetGridSize();
            if (toX < 0 || toX >= width || toY < 0 || toY >= height)
            {
                Debug.LogWarning($"[InventoryNetworkSync] ValidateMoveItem: Destination out of bounds");
                return false;
            }

            // Check if destination is empty or can stack
            var destItem = inventoryService.GetItemAt(toX, toY);
            if (destItem.HasValue && destItem.Value.itemDataId != itemId)
            {
                // Destination has different item - cannot move
                Debug.LogWarning($"[InventoryNetworkSync] ValidateMoveItem: Destination occupied by different item");
                return false;
            }

            return true;
        }


        #endregion

        #region Add Item (Network Sync)

        /// <summary>
        /// Server-only: Add item to inventory (called by server logic)
        /// </summary>
        [Server]
        public void AddItemServer(string itemId, int quantity)
        {
            Debug.Log($"[InventoryNetworkSync] ===== AddItemServer called (SERVER) =====");
            Debug.Log($"[InventoryNetworkSync] ItemId: {itemId}, Quantity: {quantity}");
            Debug.Log($"[InventoryNetworkSync] inventoryComponent: {inventoryComponent != null}, IsSpawned: {IsSpawned}");
            
            if (inventoryComponent == null)
            {
                Debug.LogError("[InventoryNetworkSync] AddItemServer: inventoryComponent is null!");
                return;
            }

            var itemInstance = new ItemInstance { itemDataId = itemId, quantity = quantity };
            Debug.Log($"[InventoryNetworkSync] Calling inventoryComponent.AddItem() on server...");
            bool success = inventoryComponent.AddItem(itemInstance);
            Debug.Log($"[InventoryNetworkSync] AddItem result: {success}");
            
            if (success)
            {
                Debug.Log($"[InventoryNetworkSync] Item added successfully on server, syncing to clients via ObserversRpc...");
                // Sync to all clients
                AddItemObserversRpc(itemId, quantity);
                Debug.Log($"[InventoryNetworkSync] AddItemObserversRpc called");
            }
            else
            {
                Debug.LogWarning($"[InventoryNetworkSync] AddItem failed on server! ItemId: {itemId}, Quantity: {quantity}");
            }
        }

        /// <summary>
        /// Sync item added to all clients
        /// </summary>
        [ObserversRpc]
        private void AddItemObserversRpc(string itemId, int quantity)
        {
            Debug.Log($"[InventoryNetworkSync] ===== AddItemObserversRpc called (CLIENT) =====");
            Debug.Log($"[InventoryNetworkSync] ItemId: {itemId}, Quantity: {quantity}");
            Debug.Log($"[InventoryNetworkSync] IsServer: {IsServer}, IsClient: {IsClient}");
            
            // Only process on clients (not on server again)
            if (IsServer)
            {
                Debug.Log("[InventoryNetworkSync] AddItemObserversRpc: Running on server, skipping");
                return;
            }

            // Process add on client
            if (inventoryComponent != null)
            {
                Debug.Log($"[InventoryNetworkSync] Adding item to client inventory component...");
                var itemInstance = new ItemInstance { itemDataId = itemId, quantity = quantity };
                bool success = inventoryComponent.AddItem(itemInstance);
                Debug.Log($"[InventoryNetworkSync] AddItem on client result: {success}");
                
                if (!success)
                {
                    Debug.LogError($"[InventoryNetworkSync] Failed to add item on client! ItemId: {itemId}, Quantity: {quantity}");
                }
            }
            else
            {
                Debug.LogError("[InventoryNetworkSync] AddItemObserversRpc: inventoryComponent is null on client!");
            }
        }

        #endregion

        #region Remove Item (Network Sync)

        /// <summary>
        /// Client requests to remove item - validated on server
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        public void RemoveItemServerRpc(string itemId, int quantity)
        {
            // Validate on server
            if (!ValidateRemoveItem(itemId, quantity))
            {
                Debug.LogWarning($"[InventoryNetworkSync] RemoveItem validation failed: {itemId}, quantity: {quantity}");
                return;
            }

            // Process remove on server using InventoryService
            bool success = false;
            if (inventoryService != null)
            {
                success = inventoryService.RemoveItem(itemId, quantity);
            }
            
            if (success)
            {
                // Sync to all clients
                RemoveItemObserversRpc(itemId, quantity);
            }
        }

        /// <summary>
        /// Sync item removed to all clients
        /// </summary>
        [ObserversRpc]
        private void RemoveItemObserversRpc(string itemId, int quantity)
        {
            // Only process on clients (not on server again)
            if (IsServer)
                return;

            // Process remove on client using InventoryService
            if (inventoryService != null)
            {
                inventoryService.RemoveItem(itemId, quantity);
            }
            
            // Events will be fired by InventoryService
        }

        /// <summary>
        /// Validate remove item operation (server-side)
        /// </summary>
        private bool ValidateRemoveItem(string itemId, int quantity)
        {
            if (inventoryService == null)
                return false;

            // Check if item exists and has enough quantity
            int currentQuantity = inventoryService.GetItemQuantity(itemId);
            if (currentQuantity < quantity)
            {
                Debug.LogWarning($"[InventoryNetworkSync] ValidateRemoveItem: Not enough quantity. Have: {currentQuantity}, Need: {quantity}");
                return false;
            }

            return true;
        }

        #endregion

        #region Equip Item (Network Sync)

        /// <summary>
        /// Client requests to equip item - validated on server
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        public void EquipItemServerRpc(string itemId, EquipmentSlotType slotType, NetworkPlayer player)
        {
            // Validate on server
            if (!ValidateEquipItem(itemId, slotType))
            {
                Debug.LogWarning($"[InventoryNetworkSync] EquipItem validation failed: {itemId}, slot: {slotType}");
                return;
            }

            // Process equip on server using InventoryService
            bool success = false;
            if (inventoryService != null)
            {
                success = inventoryService.EquipItem(itemId, slotType);
            }
            
            if (success)
            {
                // Sync to all clients
                EquipItemObserversRpc(itemId, slotType);
            }
        }

        /// <summary>
        /// Sync item equipped to all clients
        /// </summary>
        [ObserversRpc]
        private void EquipItemObserversRpc(string itemId, EquipmentSlotType slotType)
        {
            // Only process on clients (not on server again)
            if (IsServer)
                return;

            // Process equip on client using InventoryService
            if (inventoryService != null)
            {
                inventoryService.EquipItem(itemId, slotType);
            }
            
            // Events will be fired by InventoryService
        }

        /// <summary>
        /// Validate equip item operation (server-side)
        /// </summary>
        private bool ValidateEquipItem(string itemId, EquipmentSlotType slotType)
        {
            if (inventoryService == null)
                return false;

            // Check if item exists in inventory
            if (!inventoryService.HasItem(itemId))
            {
                Debug.LogWarning($"[InventoryNetworkSync] ValidateEquipItem: Item {itemId} not in inventory");
                return false;
            }

            // TODO: Add more validation (item type matches slot type, etc.)
            return true;
        }


        #endregion

        #region Quick Slot (Network Sync)

        /// <summary>
        /// Client requests to assign quick slot - validated on server
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        public void AssignQuickSlotServerRpc(int slotIndex, string itemId, NetworkPlayer player)
        {
            // Validate on server
            if (!ValidateAssignQuickSlot(slotIndex, itemId))
            {
                Debug.LogWarning($"[InventoryNetworkSync] AssignQuickSlot validation failed: slot {slotIndex}, item: {itemId}");
                return;
            }

            // Process assign on server using InventoryService
            bool success = false;
            if (inventoryService != null)
            {
                success = inventoryService.AssignQuickSlot(slotIndex, itemId);
            }
            
            if (success)
            {
                // Sync to all clients
                AssignQuickSlotObserversRpc(slotIndex, itemId);
            }
        }

        /// <summary>
        /// Sync quick slot assigned to all clients
        /// </summary>
        [ObserversRpc]
        private void AssignQuickSlotObserversRpc(int slotIndex, string itemId)
        {
            // Only process on clients (not on server again)
            if (IsServer)
                return;

            // Process assign on client using InventoryService
            if (inventoryService != null)
            {
                inventoryService.AssignQuickSlot(slotIndex, itemId);
            }
            
            // Events will be fired by InventoryService
        }

        /// <summary>
        /// Validate assign quick slot operation (server-side)
        /// </summary>
        private bool ValidateAssignQuickSlot(int slotIndex, string itemId)
        {
            if (inventoryService == null)
                return false;

            // Validate slot index
            if (slotIndex < 0 || slotIndex >= 10) // Max 10 quick slots
            {
                Debug.LogWarning($"[InventoryNetworkSync] ValidateAssignQuickSlot: Invalid slot index {slotIndex}");
                return false;
            }

            // Check if item exists in inventory
            if (!inventoryService.HasItem(itemId))
            {
                Debug.LogWarning($"[InventoryNetworkSync] ValidateAssignQuickSlot: Item {itemId} not in inventory");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Client requests to clear quick slot - validated on server
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        public void ClearQuickSlotServerRpc(int slotIndex, NetworkPlayer player)
        {
            // Process clear on server using InventoryService
            bool success = false;
            if (inventoryService != null)
            {
                success = inventoryService.ClearQuickSlot(slotIndex);
            }
            
            if (success)
            {
                // Sync to all clients
                ClearQuickSlotObserversRpc(slotIndex);
            }
        }

        /// <summary>
        /// Sync quick slot cleared to all clients
        /// </summary>
        [ObserversRpc]
        private void ClearQuickSlotObserversRpc(int slotIndex)
        {
            // Only process on clients (not on server again)
            if (IsServer)
                return;

            // Process clear on client using InventoryService
            if (inventoryService != null)
            {
                inventoryService.ClearQuickSlot(slotIndex);
            }
            
            // Events will be fired by InventoryService
        }

        #endregion

        #region Unequip Item (Network Sync)

        /// <summary>
        /// Client requests to unequip item - validated on server
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        public void UnequipItemServerRpc(EquipmentSlotType slotType, NetworkPlayer player)
        {
            if (inventoryService == null)
                return;

            // Get equipped item first
            string equippedItemId = inventoryService.GetEquippedItem(slotType);
            if (string.IsNullOrEmpty(equippedItemId))
            {
                Debug.LogWarning($"[InventoryNetworkSync] UnequipItemServerRpc: No item equipped in slot {slotType}");
                return;
            }

            // Process unequip on server
            bool success = inventoryService.UnequipItem(slotType);
            
            if (success)
            {
                // Sync to all clients
                UnequipItemObserversRpc(slotType);
            }
        }

        /// <summary>
        /// Sync item unequipped to all clients
        /// </summary>
        [ObserversRpc]
        private void UnequipItemObserversRpc(EquipmentSlotType slotType)
        {
            // Only process on clients (not on server again)
            if (IsServer)
                return;

            // Process unequip on client using InventoryService
            if (inventoryService != null)
            {
                inventoryService.UnequipItem(slotType);
            }
            
            // Events will be fired by InventoryService
        }

        #endregion

        #region Use Item (Network Sync)

        /// <summary>
        /// Client requests to use item - validated on server
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        public void UseItemServerRpc(string itemId, NetworkPlayer player)
        {
            if (inventoryService == null)
                return;

            // Validate on server
            if (!ValidateUseItem(itemId))
            {
                Debug.LogWarning($"[InventoryNetworkSync] UseItemServerRpc: Validation failed for {itemId}");
                return;
            }

            // Process use on server
            bool success = inventoryService.UseItem(itemId);
            
            if (success)
            {
                // Sync to all clients
                UseItemObserversRpc(itemId);
            }
        }

        /// <summary>
        /// Sync item use to all clients
        /// </summary>
        [ObserversRpc]
        private void UseItemObserversRpc(string itemId)
        {
            // Only process on clients (not on server again)
            if (IsServer)
                return;

            // Client-side use (for effects, animations, etc.)
            // Actual item removal is handled by server
            // Events will be fired by ItemUsageSystem
        }

        /// <summary>
        /// Validate use item operation (server-side)
        /// </summary>
        private bool ValidateUseItem(string itemId)
        {
            if (inventoryService == null)
                return false;

            // Check if item exists in inventory
            if (!inventoryService.HasItem(itemId))
            {
                Debug.LogWarning($"[InventoryNetworkSync] ValidateUseItem: Item {itemId} not in inventory");
                return false;
            }

            // TODO: Check cooldown, usage conditions, etc.

            return true;
        }

        #endregion
    }
}
