namespace NightHunt.Inventory.Core.Enums
{
    /// <summary>
    /// Defines how stat modifiers are calculated.
    /// Flat: Add/subtract exact value
    /// Percentage: Multiply by percentage (0.15 = +15%)
    /// </summary>
    public enum ModifierCalculationType
    {
        /// <summary>
        /// Flat addition/subtraction.
        /// Example: +10 HP, -5 Noise Level
        /// </summary>
        Flat,
        
        /// <summary>
        /// Percentage modification.
        /// Example: +0.15 = +15% increase, -0.1 = -10% decrease
        /// Formula: BaseStat × (1 + PercentageValue)
        /// </summary>
        Percentage
    }
}