namespace GameplaySystems.Stat
{
    /// <summary>
    /// Defines all player stat types
    /// These are stats that belong to the player character
    /// Items can modify these stats through PlayerStatModifier
    /// </summary>
    public enum PlayerStatType
    {
        // === Core Vitals ===
        Health,
        MaxHealth,
        Stamina,
        MaxStamina,
        
        // === Movement ===
        MovementSpeed,      // Base movement speed (m/s)
        SprintSpeed,        // Sprint speed (m/s)
        
        // === Weight System ===
        WeightCapacity,     // Maximum weight can carry (kg)
        CurrentWeight,      // Current total weight (kg)
        
        // === Combat - Defense ===
        Armor,              // Physical armor value (tổng từ equipment)
        MagicResist,        // Magic resistance value (tổng từ equipment)
        
        // === Vision ===
        VisionRange,        // Vision range (meters)
        NightVision         // Night vision effectiveness (0-100)
    }
}