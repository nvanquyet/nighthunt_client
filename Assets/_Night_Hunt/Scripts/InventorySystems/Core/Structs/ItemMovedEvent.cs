namespace NightHunt.Inventory.Core.Structs
{
    /// <summary>
    /// Event fired when item is moved to new index.
    /// </summary>
    public struct ItemMovedEvent
    {
        public ulong OwnerId;
        public bool IsLocalPlayer;
        public string InstanceId;
        public int OldIndex;
        public int NewIndex;
    }

}