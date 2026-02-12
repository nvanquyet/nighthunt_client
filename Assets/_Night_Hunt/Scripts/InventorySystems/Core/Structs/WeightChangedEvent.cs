namespace NightHunt.Inventory.Core.Structs
{
    /// <summary>
    /// Event fired when total weight changes.
    /// </summary>
    public struct WeightChangedEvent
    {
        public int OwnerId;
        public bool IsLocalPlayer;
        public float CurrentWeight;
        public float MaxWeight;
        public bool IsOverweight;
    }


}