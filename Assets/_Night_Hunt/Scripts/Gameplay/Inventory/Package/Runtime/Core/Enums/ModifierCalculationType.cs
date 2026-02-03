namespace NightHunt.Inventory.Core
{
    /// <summary>
    /// Global calculation type for all modifiers in the system.
    /// Configured in ModifierSystemConfig ScriptableObject.
    /// </summary>
    public enum ModifierCalculationType
    {
        FlatAddition,       // Final = Base + Sum(Additions)
        PercentMultiplier   // Final = Base * (1 + Sum(Percentages))
    }
}
