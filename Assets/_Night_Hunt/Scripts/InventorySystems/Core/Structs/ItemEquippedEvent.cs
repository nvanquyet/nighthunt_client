using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;

namespace NightHunt.Inventory.Core.Structs
{
    /// <summary>
    /// Event fired when item is equipped.
    /// </summary>
    public struct ItemEquippedEvent
    {
        public int OwnerId;
        public bool IsLocalPlayer;
        public ItemInstance Item;
        public EquipmentSlotType SlotType;
        public ItemInstance SwappedItem; // null if slot was empty
    }


}