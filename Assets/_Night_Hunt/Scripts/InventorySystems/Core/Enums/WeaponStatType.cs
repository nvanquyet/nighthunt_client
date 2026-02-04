namespace NightHunt.Inventory.Core.Enums
{
    /// <summary>
    /// Defines weapon stat types that can be modified by attachments.
    /// </summary>
    public enum WeaponStatType
    {
        /// <summary>Damage per shot</summary>
        Damage,
        
        /// <summary>Rate of fire (rounds per minute)</summary>
        FireRate,
        
        /// <summary>Recoil strength</summary>
        Recoil,
        
        /// <summary>Effective range in meters</summary>
        Range,
        
        /// <summary>Accuracy/spread</summary>
        Accuracy,
        
        /// <summary>Magazine capacity</summary>
        MagazineSize,
        
        /// <summary>Reload speed multiplier</summary>
        ReloadSpeed
        
        // TODO: Penetration, HeadshotMultiplier, etc.
    }
}