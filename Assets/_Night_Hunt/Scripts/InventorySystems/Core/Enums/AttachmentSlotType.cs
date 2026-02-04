namespace NightHunt.Inventory.Core.Enums
{
    /// <summary>
    /// Defines attachment slot types for weapons and equipment.
    /// </summary>
    public enum AttachmentSlotType
    {
        // === Weapon Attachments ===
        
        /// <summary>Optical sight attachment</summary>
        Scope,
        
        /// <summary>Grip attachment for stability</summary>
        Grip,
        
        /// <summary>Muzzle attachment (suppressor, compensator, etc.)</summary>
        Muzzle,
        
        /// <summary>Magazine attachment (extended, fast, etc.)</summary>
        Magazine,
        
        // === Equipment Attachments (Helmet) ===
        
        /// <summary>Flashlight attachment for helmet</summary>
        Flashlight,
        
        /// <summary>Night Vision Goggle attachment</summary>
        NVG
        
        // TODO: Stock, Camera, Laser, etc.
    }
}