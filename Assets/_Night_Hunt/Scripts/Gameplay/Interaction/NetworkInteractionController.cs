using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet;
using NightHunt.Gameplay.Loot;
using NightHunt.Gameplay.Inventory;
using NightHunt.Data;
using System;

namespace NightHunt.Gameplay.Interaction
{
    /// <summary>
    /// Network interaction controller
    /// Handles server-authoritative interaction requests from clients
    /// Follows Single Responsibility Principle - delegates to handlers
    /// </summary>
    public class NetworkInteractionController : NetworkBehaviour
    {
        private IInventoryProvider inventoryProvider;
        private IInteractionValidator validator;

        private void Awake()
        {
            inventoryProvider = GetComponent<IInventoryProvider>();
            validator = new InteractionValidator();
        }

        /// <summary>
        /// Client: Request interaction with target
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        public void ServerRpc_RequestInteract(uint targetNetId, string actionType, Vector3 playerPosition)
        {
            if (!IsServerStarted) return;

            // Validate using validator (Dependency Inversion Principle)
            if (!validator.ValidateInteraction(targetNetId, playerPosition, this))
            {
                return;
            }

            // Get handler from factory (Open/Closed Principle)
            IInteractionHandler handler = InteractionHandlerFactory.CreateHandler(actionType);
            if (handler == null)
            {
                Debug.LogWarning($"[NetworkInteractionController] No handler for action type: {actionType}");
                return;
            }

            // Delegate to handler
            if (handler.HandleInteraction(targetNetId, actionType, NetworkObject))
            {
                // Sync inventory if needed
                SyncInventoryToClient();
            }
        }

        /// <summary>
        /// Server: Sync inventory to client
        /// </summary>
        [Server]
        private void SyncInventoryToClient()
        {
            if (inventoryProvider == null) return;

            var inventorySync = GetComponent<InventorySync>();
            if (inventorySync != null)
            {
                var slots = inventoryProvider.GetItems();
                inventorySync.SyncInventory(slots);
            }
        }

        /// <summary>
        /// Get inventory provider (for handlers)
        /// </summary>
        public IInventoryProvider GetInventoryProvider() => inventoryProvider;
    }

    /// <summary>
    /// Interaction validator
    /// Follows Single Responsibility Principle
    /// </summary>
    public interface IInteractionValidator
    {
        bool ValidateInteraction(uint targetNetId, Vector3 playerPosition, NetworkInteractionController controller);
    }

    /// <summary>
    /// Default interaction validator implementation
    /// </summary>
    public class InteractionValidator : IInteractionValidator
    {
        private const float MAX_INTERACTION_DISTANCE = 5f;

        public bool ValidateInteraction(uint targetNetId, Vector3 playerPosition, NetworkInteractionController controller)
        {
            // FishNet version in this project uses ServerManager.Spawn/Despawn.
            // Lookup spawned objects via ServerManager.Objects.
            NetworkObject targetObj = null;
            var nm = InstanceFinder.NetworkManager;
            if (nm != null && nm.ServerManager != null && nm.ServerManager.Objects != null)
            {
                // Spawned dictionary key is int in this FishNet version.
                nm.ServerManager.Objects.Spawned.TryGetValue(unchecked((int)targetNetId), out targetObj);
            }
            if (targetObj == null)
            {
                Debug.LogWarning($"[InteractionValidator] Target not found: {targetNetId}");
                return false;
            }

            float distance = Vector3.Distance(playerPosition, targetObj.transform.position);
            if (distance > MAX_INTERACTION_DISTANCE)
            {
                Debug.LogWarning($"[InteractionValidator] Target too far: {distance}m");
                return false;
            }

            return true;
        }
    }
}

