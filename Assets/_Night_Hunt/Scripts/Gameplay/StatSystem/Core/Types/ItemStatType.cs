namespace NightHunt.StatSystem.Core.Types
{
    /// <summary>
    /// Defines all item stat types
    /// 
    /// RESPONSIBILITIES:
    /// - Enumeration of item stats that contribute to player stats
    /// - Used by ItemStatSystem for item stat calculations
    /// - Used in item definitions (WeaponDefinition, EquipmentDefinition)
    /// </summary>
    public enum ItemStatType
    {
        // Weapon Stats
        Damage,
        FireRate,
        Recoil,
        Spread,
        Accuracy,
        Range,
        
        // Armor Stats
        ArmorValue,
        // Weight
        Weight,
        StaminaPenalty,
        MovementSpeedPenalty,
        WeightCapacityBonus,
        LightRange,
        Brightness,
        AimSpeed,
        ReloadSpeed,
        MagazineSize,
        MaxDurability,
        Durability
    }
}
