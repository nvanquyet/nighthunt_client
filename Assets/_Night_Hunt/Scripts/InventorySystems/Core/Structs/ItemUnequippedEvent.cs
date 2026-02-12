using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;

namespace NightHunt.Inventory.Core.Structs
{
    /// <summary>
    /// Event fired when item is unequipped.
    /// </summary>
    public struct ItemUnequippedEvent
    {
        public ulong OwnerId;
        public bool IsLocalPlayer;
        public ItemInstance Item;
        public EquipmentSlotType SlotType;
    }
}