using UnityEngine;
using FishNet.Object;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Loot;
using NightHunt.InteractionSystem.Inventory;
using NightHunt.InteractionSystem.Events;

namespace NightHunt.InteractionSystem.Loot
{
    /// <summary>
    /// Handles item transfer between containers and inventories (server-side).
    /// </summary>
    public class LootTransferHandler : NetworkBehaviour
    {
        /// <summary>
        /// Transfer item from source to destination.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void TransferItem(NetworkObject sourceContainer, NetworkObject destinationInventory, ItemInstance item)
        {
            if (sourceContainer == null || destinationInventory == null)
                return;

            LootContainer source = sourceContainer.GetComponent<LootContainer>();
            GridInventoryComponent destination = destinationInventory.GetComponent<GridInventoryComponent>();
            
            if (source == null || destination == null)
                return;

            // Remove from source
            if (source.RemoveItem(item.itemDataId, item.quantity))
            {
                // Add to destination
                if (destination.AddItem(item))
                {
                    // Success - invoke loot event (looting FROM container)
                    InventoryEvents.InvokeItemLooted(item, source);
                    return;
                }
                else
                {
                    // Failed to add - return item to source
                    source.AddItem(item);
                }
            }
        }

        /// <summary>
        /// Transfer item from inventory to container.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void TransferItemFromInventory(NetworkObject sourceInventory, NetworkObject destinationContainer, ItemInstance item)
        {
            if (sourceInventory == null || destinationContainer == null)
                return;

            GridInventoryComponent source = sourceInventory.GetComponent<GridInventoryComponent>();
            LootContainer destination = destinationContainer.GetComponent<LootContainer>();
            
            if (source == null || destination == null)
                return;

            // Remove from source
            if (source.RemoveItem(item.itemDataId, item.quantity))
            {
                // Add to destination
                if (destination.AddItem(item))
                {
                    // Success - invoke loot event (transferring TO container)
                    InventoryEvents.InvokeItemLooted(item, destination);
                    return;
                }
                else
                {
                    // Failed to add - return item to source
                    source.AddItem(item);
                }
            }
        }
    }
}
