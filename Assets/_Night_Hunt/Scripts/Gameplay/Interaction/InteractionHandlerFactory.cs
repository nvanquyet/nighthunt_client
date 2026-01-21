using System.Collections.Generic;
using FishNet.Object;
using FishNet;
using NightHunt.Gameplay.Inventory;
using NightHunt.Gameplay.Loot;
using NightHunt.Data;
using UnityEngine;

namespace NightHunt.Gameplay.Interaction
{
    /// <summary>
    /// Factory for creating interaction handlers
    /// Follows Open/Closed Principle - can extend without modifying existing code
    /// </summary>
    public class InteractionHandlerFactory
    {
        private static Dictionary<string, System.Func<IInteractionHandler>> handlers = new Dictionary<string, System.Func<IInteractionHandler>>();

        static InteractionHandlerFactory()
        {
            RegisterHandler("Pickup", () => new PickupInteractionHandler());
            RegisterHandler("OpenChest", () => new ChestInteractionHandler());
            // Can easily add more handlers without modifying existing code
        }

        /// <summary>
        /// Register a new interaction handler type
        /// </summary>
        public static void RegisterHandler(string actionType, System.Func<IInteractionHandler> handlerFactory)
        {
            handlers[actionType] = handlerFactory;
        }

        /// <summary>
        /// Create handler for action type
        /// </summary>
        public static IInteractionHandler CreateHandler(string actionType)
        {
            if (handlers.ContainsKey(actionType))
            {
                return handlers[actionType]();
            }
            return null;
        }
    }

    /// <summary>
    /// Pickup interaction handler
    /// Follows Single Responsibility Principle
    /// </summary>
    public class PickupInteractionHandler : IInteractionHandler
    {
        private readonly IPickupCalculator pickupCalculator;

        public PickupInteractionHandler()
        {
            pickupCalculator = new PickupCalculator();
        }

        public PickupInteractionHandler(IPickupCalculator calculator)
        {
            pickupCalculator = calculator; // Dependency Injection
        }

        public bool HandleInteraction(uint targetNetId, string actionType, NetworkObject interactor)
        {
            NetworkObject targetObj = null;
            var nm = InstanceFinder.NetworkManager;
            if (nm != null && nm.ServerManager != null && nm.ServerManager.Objects != null)
            {
                // Spawned dictionary key is int in this FishNet version.
                nm.ServerManager.Objects.Spawned.TryGetValue(unchecked((int)targetNetId), out targetObj);
            }
            if (targetObj == null) return false;

            NetworkLootItem lootItem = targetObj.GetComponent<NetworkLootItem>();
            if (lootItem == null || lootItem.IsLooted) return false;

            var inventoryProvider = interactor.GetComponent<IInventoryProvider>();
            if (inventoryProvider == null) return false;

            // Calculate how many can be taken
            int requestedQty = lootItem.Quantity;
            int takenQty = pickupCalculator.CalculatePickupAmount(lootItem.ItemId, requestedQty, inventoryProvider);

            if (takenQty > 0)
            {
                // Try to add items
                int actuallyTaken = 0;
                for (int i = 0; i < takenQty; i++)
                {
                    if (inventoryProvider.AddItem(lootItem.ItemId, 1))
                    {
                        actuallyTaken++;
                    }
                    else
                    {
                        break;
                    }
                }

                if (actuallyTaken > 0)
                {
                    int remainingQty = requestedQty - actuallyTaken;
                    if (remainingQty > 0)
                    {
                        lootItem.UpdateQuantity(remainingQty);
                    }
                    else
                    {
                        lootItem.MarkAsLooted();
                    }
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Interface for pickup calculation logic
    /// Follows Dependency Inversion Principle
    /// </summary>
    public interface IPickupCalculator
    {
        int CalculatePickupAmount(string itemId, int requestedQty, IInventoryProvider inventory);
    }

    /// <summary>
    /// Pickup calculator implementation
    /// Follows Single Responsibility Principle
    /// </summary>
    public class PickupCalculator : IPickupCalculator
    {
        public int CalculatePickupAmount(string itemId, int requestedQty, IInventoryProvider inventory)
        {
            var itemConfig = GameConfigLoader.Instance?.GetItemConfig(itemId);
            if (itemConfig == null) return 0;

            float itemWeight = itemConfig.Weight;
            float totalWeight = itemWeight * requestedQty;
            float currentWeight = inventory.GetCurrentWeight();
            float maxWeight = inventory.GetWeightCapacity();

            // Calculate how many can fit based on weight
            if (currentWeight + totalWeight <= maxWeight)
            {
                return requestedQty; // Can take all
            }

            float availableWeight = maxWeight - currentWeight;
            if (availableWeight <= 0) return 0;

            return Mathf.FloorToInt(availableWeight / itemWeight);
        }
    }

    /// <summary>
    /// Chest interaction handler
    /// Follows Single Responsibility Principle
    /// </summary>
    public class ChestInteractionHandler : IInteractionHandler
    {
        public bool HandleInteraction(uint targetNetId, string actionType, NetworkObject interactor)
        {
            // TODO: Implement chest/container interaction
            return false;
        }
    }
}

