namespace NightHunt.Inventory.Core.Enums
{
    /// <summary>
    /// Defines attachment slot types for weapons and equipment.
    /// Examples: Scope on weapon, Flashlight on helmet
    /// </summary>
    public enum AttachmentSlotType
    {
        /// <summary>No attachment slot</summary>
        None,
        
        // === WEAPON ATTACHMENTS ===
        
        /// <summary>Optical sight slot (scopes, red dots)</summary>
        Scope,
        
        /// <summary>Barrel attachment (suppressor, compensator)</summary>
        Barrel,
        
        /// <summary>Grip/foregrip slot (reduces recoil)</summary>
        Grip,
        
        /// <summary>Magazine slot (extended mags)</summary>
        Magazine,
        
        /// <summary>Stock attachment (stability)</summary>
        Stock,
        
        /// <summary>Laser/flashlight rail</summary>
        Rail,
        
        /// <summary>Underbarrel attachment (grenade launcher)</summary>
        Underbarrel,
        
        // === EQUIPMENT ATTACHMENTS ===
        
        /// <summary>Flashlight (can attach to helmet or weapon)</summary>
        Flashlight,
        
        /// <summary>Night vision device (helmet)</summary>
        NightVision,
        
        /// <summary>Armor plate (armor)</summary>
        ArmorPlate,
        
        /// <summary>Pouch/bag attachment (armor/backpack - increases capacity)</summary>
        Pouch,
        
        /// <summary>Trinket/charm slot (cosmetic or minor stat boost)</summary>
        Trinket
    }
}