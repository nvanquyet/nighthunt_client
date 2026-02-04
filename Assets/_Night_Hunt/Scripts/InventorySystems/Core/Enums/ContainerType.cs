namespace NightHunt.Inventory.Core.Enums
{
    /// <summary>
    /// Defines container types.
    /// </summary>
    public enum ContainerType
    {
        /// <summary>World chest (no respawn, loot once)</summary>
        StaticChest,
        
        /// <summary>Boss drop (despawn after configured delay)</summary>
        BossLoot,
        
        /// <summary>Dead player loot (permanent)</summary>
        PlayerCorpse
    }
}