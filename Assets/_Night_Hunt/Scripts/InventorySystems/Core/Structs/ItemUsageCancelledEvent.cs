using NightHunt.Inventory.Core.Data;

namespace NightHunt.Inventory.Core.Structs
{
    /// <summary>
    /// Event fired when item usage is cancelled.
    /// </summary>
    public struct ItemUsageCancelledEvent
    {
        public int OwnerId;
        public bool IsLocalPlayer;
        public ItemInstance Item;
    }


}