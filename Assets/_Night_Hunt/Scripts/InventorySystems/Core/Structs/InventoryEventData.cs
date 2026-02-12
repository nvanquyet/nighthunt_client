namespace NightHunt.Inventory.Core.Structs
{
    
    /// <summary>
    /// Base event data for inventory changes.
    /// Contains owner info for UI filtering.
    /// </summary>
    public struct InventoryEventData
    {
        public ulong OwnerId; // NetworkObject.ObjectId
        public bool IsLocalPlayer;
    }
}