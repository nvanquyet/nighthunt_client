using NightHunt.Inventory.Core.Data;

namespace NightHunt.Inventory.Core.Structs
{
    /// <summary>
    /// Event fired when weapon is equipped.
    /// </summary>
    public struct WeaponEquippedEvent
    {
        public int OwnerId;
        public bool IsLocalPlayer;
        public ItemInstance Weapon;
        public int WeaponSlotIndex;
        public ItemInstance SwappedWeapon;
    }


}