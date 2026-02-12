namespace NightHunt.Inventory.Core.Structs
{
    /// <summary>
    /// Event fired when operation fails validation.
    /// </summary>
    public struct OperationFailedEvent
    {
        public int OwnerId;
        public bool IsLocalPlayer;
        public string Operation;
        public string Reason;
    }
}