using NightHunt.Inventory.Core.Data;

namespace NightHunt.Inventory.Core.Structs
{
     
    /// <summary>
    /// Event fired when item removed from quickslot.
    /// </summary>
    public struct QuickSlotRemovedEvent
    {
        public ulong OwnerId;
        public bool IsLocalPlayer;
        public ItemInstance Item;
        public int QuickSlotIndex;
    }
}