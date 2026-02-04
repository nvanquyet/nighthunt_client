namespace NightHunt.Inventory.Core.Enums
{
    /// <summary>
    /// Defines character stat types that can be modified by equipment and items.
    /// </summary>
    public enum CharacterStatType
    {
        /// <summary>Maximum health points</summary>
        MaxHP,
        
        /// <summary>Armor/damage reduction</summary>
        Armor,
        
        /// <summary>Stamina for sprinting/actions</summary>
        Stamina,
        
        /// <summary>Movement speed</summary>
        MoveSpeed,
        
        /// <summary>Maximum weight capacity in kg</summary>
        WeightCapacity,
        
        /// <summary>Vision/detection radius</summary>
        VisionRadius,
        
        /// <summary>Noise level (stealth)</summary>
        NoiseLevel
        
        // TODO: CritChance, DodgeChance, Resistance, etc.
    }
}