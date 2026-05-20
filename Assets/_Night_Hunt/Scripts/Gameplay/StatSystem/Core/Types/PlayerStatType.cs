namespace NightHunt.Gameplay.StatSystem.Core.Types
{
    /// <summary>
    /// Defines all player character stat keys used by runtime stat systems.
    /// Append only: values are serialized in ScriptableObject assets.
    /// </summary>
    public enum PlayerStatType
    {
        // Core vitals
        Health,
        MaxHealth,
        Stamina,
        MaxStamina,

        // Movement
        MovementSpeed,

        // Weight
        WeightCapacity,
        CurrentWeight,

        // Combat defense
        Armor,

        // Vision
        VisionRange,

        // Passive regeneration / drain rates
        HealthRegenRate,
        StaminaRegenRate,
        StaminaDrainRate
    }
}
