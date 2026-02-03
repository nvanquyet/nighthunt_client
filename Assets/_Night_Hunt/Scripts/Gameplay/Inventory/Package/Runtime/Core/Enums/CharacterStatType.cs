namespace NightHunt.Inventory.Core
{
    /// <summary>
    /// Character stat types that can be modified by equipment and attachments.
    /// </summary>
    public enum CharacterStatType
    {
        MaxHP,
        Armor,
        Stamina,
        MoveSpeed,
        WeightCapacity,
        VisionRadius,
        NoiseLevel
        
        // Future expansion (TODO)
        // CritChance,
        // DodgeChance,
        // Resistance,
    }
}
