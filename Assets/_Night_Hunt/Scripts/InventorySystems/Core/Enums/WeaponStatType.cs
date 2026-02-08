namespace NightHunt.Inventory.Core.Enums
{
    /// <summary>
    /// Defines weapon stat types that can be modified by attachments.
    /// Example: Grip reduces Recoil, Scope increases Range/Accuracy
    /// </summary>
    public enum WeaponStatType
    {
        /// <summary>Damage per shot</summary>
        Damage,
        
        /// <summary>Fire rate (rounds per minute)</summary>
        FireRate,
        
        /// <summary>Recoil strength (lower is better)</summary>
        Recoil,
        
        /// <summary>Effective range in meters</summary>
        Range,
        
        /// <summary>Accuracy (0-1, where 1 is perfect accuracy)</summary>
        Accuracy,
        
        /// <summary>Magazine capacity</summary>
        MagazineSize,
        
        /// <summary>Reload speed multiplier (higher is faster)</summary>
        ReloadSpeed,
        
        /// <summary>Aim down sights speed</summary>
        ADSSpeed,
        
        /// <summary>Bullet velocity</summary>
        BulletVelocity,
        
        /// <summary>Penetration power</summary>
        Penetration
    }
}