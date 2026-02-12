using NightHunt.Inventory.Core.Data;

namespace NightHunt.Inventory.Core.Structs
{
   
    /// <summary>
    /// Event fired when item usage completes.
    /// </summary>
    public struct ItemUsageCompletedEvent
    {
        public int OwnerId;
        public bool IsLocalPlayer;
        public ItemInstance Item;
    }


}