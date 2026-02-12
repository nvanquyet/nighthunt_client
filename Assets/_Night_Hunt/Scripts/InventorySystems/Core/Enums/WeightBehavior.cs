namespace NightHunt.Inventory.Core.Enums
{
    /// <summary>
    /// Defines weight behavior when item is equipped.
    /// </summary>
    public enum WeightBehavior
    {
        /// <summary>
        /// Use global config setting from InventoryConfig.
        /// </summary>
        UseGlobalConfig,
        
        /// <summary>
        /// Always add weight even when equipped.
        /// </summary>
        AlwaysAddWeight,
        
        /// <summary>
        /// Never add weight when equipped.
        /// </summary>
        NeverAddWeight
    }
}