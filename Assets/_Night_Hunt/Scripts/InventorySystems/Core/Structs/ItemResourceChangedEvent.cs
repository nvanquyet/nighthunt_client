namespace NightHunt.Inventory.Core.Structs
{
    /// <summary>
    /// Event fired when item resource changes (durability, ammo, etc.).
    /// </summary>
    public struct ItemResourceChangedEvent
    {
        public int OwnerId;
        public bool IsLocalPlayer;
        public string InstanceId;
        public float OldValue;
        public float NewValue;
        public float MaxValue;
    }


}