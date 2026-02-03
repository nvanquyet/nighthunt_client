namespace NightHunt.Inventory.Core
{
    /// <summary>
    /// Defines where an item can be placed in the inventory system.
    /// Used for slot compatibility validation.
    /// </summary>
    public enum SlotLocationType
    {
        Inventory,
        Equipment,
        Weapon,
        QuickSlot,
        Attachment,
        Container,
        Trash,
        WorldDrop
    }
}
