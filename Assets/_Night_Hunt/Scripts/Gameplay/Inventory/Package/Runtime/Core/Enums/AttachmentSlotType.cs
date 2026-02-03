namespace NightHunt.Inventory.Core
{
    /// <summary>
    /// Attachment slot types for weapons and equipment.
    /// </summary>
    public enum AttachmentSlotType
    {
        // Weapon attachments
        Scope,
        Grip,
        Muzzle,
        Magazine,
        
        // Equipment attachments (Helmet)
        Flashlight,
        NVG, // Night Vision Goggle
        
        // TODO: Stock, Camera
        None,
    }
}
