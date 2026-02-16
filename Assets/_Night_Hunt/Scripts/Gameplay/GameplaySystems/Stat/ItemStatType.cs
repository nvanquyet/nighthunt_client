namespace GameplaySystems.Stat
{
    /// <summary>
    /// Defines all item stat types
    /// Used for item stats that contribute to player stats
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