namespace NightHunt.Gameplay.Character.Stats
{
    /// <summary>
    /// Enum defining all character stats that can be modified by equipment, status effects, zones, etc.
    /// This is game-specific and separate from InteractionSystem's StatType (which is for weapons/attachments).
    /// </summary>
    public enum CharacterStatType
    {
        // Core stats
        MaxHP,
        Stamina,
        MoveSpeed,
        WeightCapacity,
        VisionRadius,
        NoiseLevel,

        // Combat stats (can be extended)
        DamageReduction,
        CritChance,
        CritMultiplier,

        // Utility stats (can be extended)
        ReviveTime,
        RevivedHP,
        BleedoutTime,
        ADSSlow,

        // Add more stats as needed...
    }
}
