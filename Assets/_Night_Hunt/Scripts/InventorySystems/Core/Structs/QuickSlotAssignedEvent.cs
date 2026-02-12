using NightHunt.Inventory.Core.Data;

namespace NightHunt.Inventory.Core.Structs
{
    /// <summary>
    /// Event fired when item assigned to quickslot.
    /// </summary>
    public struct QuickSlotAssignedEvent
    {
        public int OwnerId;
        public bool IsLocalPlayer;
        public ItemInstance Item;
        public int QuickSlotIndex;
        public ItemInstance SwappedItem;
    }

}