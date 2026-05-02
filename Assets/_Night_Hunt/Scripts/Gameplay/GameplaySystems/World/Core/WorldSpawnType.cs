
namespace NightHunt.GameplaySystems.World
{
    /// <summary>
    /// The type of object spawned at a WorldItemSpawnPoint.
    ///   Item      → WorldItem (ground drop, scattered from SpawnTable)
    ///   Container → WorldContainer (crate / chest / loot box)
    /// </summary>
    public enum WorldSpawnType
    {
        /// <summary>Item dropped on the ground (WorldItem) — scattered from SpawnTable.</summary>
        Item,

        /// <summary>Loot container / Crate / Chest (WorldContainer).</summary>
        Container,
    }
}
