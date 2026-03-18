namespace NightHunt.Gameplay.StatSystem.Core.Types
{
    /// <summary>
    /// Defines all player stat types
    /// 
    /// RESPONSIBILITIES:
    /// - Enumeration of all player character stats
    /// - Used by PlayerStatSystem for stat management
    /// - Items can modify these stats through PlayerStatModifier
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
        
        // === Weight System ===
        WeightCapacity,     // Maximum weight can carry (kg)
        CurrentWeight,      // Current total weight (kg)
        
        // === Combat - Defense ===
        Armor,              // Physical armor value (tổng từ equipment)
        
        // === Vision ===
        VisionRange         // Vision range (meters) - cộng dồn từ LightRange items
    }
}
