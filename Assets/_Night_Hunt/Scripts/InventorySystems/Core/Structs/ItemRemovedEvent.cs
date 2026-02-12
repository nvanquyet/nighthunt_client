namespace NightHunt.Inventory.Core.Structs
{
      
    /// <summary>
    /// Event fired when item is removed from inventory.
    /// </summary>
    public struct ItemRemovedEvent
    {
        public int OwnerId;
        public bool IsLocalPlayer;
        public string InstanceId;
        public int InventoryIndex;
    }


}