namespace NightHunt.Inventory.Core
{
    /// <summary>
    /// Type of container in the world.
    /// </summary>
    public enum ContainerType
    {
        StaticChest,    // World chest (no respawn, loot once)
        BossLoot,       // Boss drop (despawn after delay)
        PlayerCorpse    // Dead player loot (permanent)
    }
}
