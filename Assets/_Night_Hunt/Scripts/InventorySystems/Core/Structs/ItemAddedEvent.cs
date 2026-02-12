using NightHunt.Inventory.Core.Data;

namespace NightHunt.Inventory.Core.Structs
{
    
    /// <summary>
    /// Event fired when item is added to inventory.
    /// </summary>
    public struct ItemAddedEvent
    {
        public int OwnerId;
        public bool IsLocalPlayer;
        public ItemInstance Item;
        public int InventoryIndex;
    }
}