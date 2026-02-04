using System.Collections.Generic;
using System.Linq;

namespace NightHunt.Inventory.Networking
{
    /// <summary>
    /// Tracks item ownership for anti-cheat.
    /// </summary>
    public class ItemOwnershipRegistry
    {
        private Dictionary<string, uint> itemOwners; // instanceId -> networkId

        public ItemOwnershipRegistry()
        {
            itemOwners = new Dictionary<string, uint>();
        }

        /// <summary>
        /// Registers an item to a player.
        /// </summary>
        public void RegisterItem(string instanceId, uint ownerNetworkId)
        {
            itemOwners[instanceId] = ownerNetworkId;
        }

        /// <summary>
        /// Unregisters an item (when destroyed).
        /// </summary>
        public void UnregisterItem(string instanceId)
        {
            itemOwners.Remove(instanceId);
        }

        /// <summary>
        /// Transfers ownership of an item.
        /// </summary>
        public void TransferOwnership(string instanceId, uint newOwnerNetworkId)
        {
            itemOwners[instanceId] = newOwnerNetworkId;
        }

        /// <summary>
        /// Gets the owner of an item.
        /// </summary>
        public uint GetOwner(string instanceId)
        {
            if (itemOwners.TryGetValue(instanceId, out uint owner))
                return owner;

            return 0; // No owner
        }

        /// <summary>
        /// Checks if a player owns an item.
        /// </summary>
        public bool IsOwner(string instanceId, uint networkId)
        {
            return GetOwner(instanceId) == networkId;
        }

        /// <summary>
        /// Gets all items owned by a player.
        /// </summary>
        public List<string> GetOwnedItems(uint networkId)
        {
            return itemOwners
                .Where(kvp => kvp.Value == networkId)
                .Select(kvp => kvp.Key)
                .ToList();
        }

        /// <summary>
        /// Clears all ownership data.
        /// </summary>
        public void Clear()
        {
            itemOwners.Clear();
        }
    }
}