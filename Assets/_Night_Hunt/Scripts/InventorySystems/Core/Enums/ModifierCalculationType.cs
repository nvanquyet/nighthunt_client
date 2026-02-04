namespace NightHunt.Inventory.Core.Enums
{
    /// <summary>
    /// Defines how stat modifiers are calculated.
    /// This is a GLOBAL setting that affects ALL items in the game.
    /// </summary>
    public enum ModifierCalculationType
    {
        /// <summary>
        /// Flat addition: Final = Base + Sum(Additions)
        /// Example: Base 100 HP + 50 HP = 150 HP
        /// </summary>
        FlatAddition,
        
        /// <summary>
        /// Percentage multiplier: Final = Base * (1 + Sum(Percentages))
        /// Example: Base 100 HP * (1 + 0.2) = 120 HP
        /// Value 0.2 means +20%
        /// </summary>
        PercentMultiplier
    }
}