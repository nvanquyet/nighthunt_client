using NightHunt.Inventory.Core.Data;

namespace NightHunt.Inventory.Core.Structs
{
    /// <summary>
    /// Event fired when weapon is unequipped.
    /// </summary>
    public struct WeaponUnequippedEvent
    {
        public ulong OwnerId;
        public bool IsLocalPlayer;
        public ItemInstance Weapon;
        public int WeaponSlotIndex;
    }
}