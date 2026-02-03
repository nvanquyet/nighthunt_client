namespace NightHunt.Inventory.Core
{
    /// <summary>
    /// Type of interaction for world items and containers.
    /// </summary>
    public enum InteractionType
    {
        InstantPickup,   // Press F (items on ground)
        HoldToOpen       // Hold E (chests, containers, corpses)
    }
}
