namespace NightHunt.Inventory.Core.Structs
{
    /// <summary>
    /// Event fired when stat modifiers change (equipment/attachments added/removed).
    /// </summary>
    public struct StatModifiersChangedEvent
    {
        public int OwnerId;
        public bool IsLocalPlayer;
        public string SourceId; // Item instance ID
    }
}