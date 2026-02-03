namespace NightHunt.Inventory.Core
{
    /// <summary>
    /// Type of inventory operation being performed.
    /// </summary>
    public enum OperationType
    {
        Move,           // Empty target → move item
        Swap,           // Occupied target → swap items
        Equip,          // Inventory → Equipment/Weapon
        Unequip,        // Equipment/Weapon → Inventory
        Attach,         // Attachment → Weapon/Equipment
        Detach,         // Attachment → Inventory
        StackMerge,     // Stack A → Stack B
        ContainerTransfer, // Inventory ↔ Container
        WorldDrop,      // Inventory → World
        Trash           // Any → Trash (destroy)
    }
}
