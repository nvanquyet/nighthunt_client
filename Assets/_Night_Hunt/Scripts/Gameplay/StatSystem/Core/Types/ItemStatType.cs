namespace NightHunt.StatSystem.Core.Types
{
    /// <summary>
    /// Defines all item stat types (intrinsic to item).
    /// Stats that affect player (Armor, MovementSpeed, VisionRange) use PlayerModifier, NOT here.
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
        AimSpeed,
        ReloadSpeed,
        MagazineSize,
        
        // General Item Stats
        Weight,           // Trọng lượng (âm = bonus capacity, dương = penalty)
        MaxDurability,
        Durability
    }
}
