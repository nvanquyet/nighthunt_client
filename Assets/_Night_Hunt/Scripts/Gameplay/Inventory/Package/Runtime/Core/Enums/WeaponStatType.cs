namespace NightHunt.Inventory.Core
{
    /// <summary>
    /// Weapon stat types that can be modified by attachments.
    /// </summary>
    public enum WeaponStatType
    {
        Damage,
        FireRate,
        Recoil,
        Range,
        Accuracy,
        MagazineSize,
        ReloadSpeed
        
        // Future expansion (TODO)
        // Penetration,
        // HeadshotMultiplier,
    }
}
