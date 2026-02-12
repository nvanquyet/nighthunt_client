using NightHunt.Inventory.Core.Data;

namespace NightHunt.Inventory.Core.Structs
{
    /// <summary>
    /// Event fired when item usage starts.
    /// </summary>
    public struct ItemUsageStartedEvent
    {
        public int OwnerId;
        public bool IsLocalPlayer;
        public ItemInstance Item;
        public float UsageDuration;
    }

}