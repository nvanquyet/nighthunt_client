namespace NightHunt.Inventory.Core.Enums
{
    /// <summary>
    /// Defines weapon slot types.
    /// Default: Primary + Secondary
    /// Can be expanded to include Melee slot
    /// </summary>
    public enum WeaponSlotType
    {
        /// <summary>Primary weapon slot (rifle, shotgun, etc.)</summary>
        Primary,
        
        /// <summary>Secondary weapon slot (pistol, SMG)</summary>
        Secondary,
        
        // /// <summary>Melee weapon slot (knife, axe) - future expansion</summary>
        //Melee
    }
}