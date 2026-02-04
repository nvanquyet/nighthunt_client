namespace NightHunt.Inventory.Core.Enums
{
    /// <summary>
    /// Defines the type of item.
    /// Used for sorting, filtering, and validation.
    /// </summary>
    public enum ItemType
    {
        /// <summary>Weapons (rifles, pistols, etc.)</summary>
        Weapon,
        
        /// <summary>Armor and protective equipment</summary>
        Armor,
        
        /// <summary>Consumable items (medkits, food, etc.)</summary>
        Consumable,
        
        /// <summary>Weapon/equipment attachments</summary>
        Attachment,
        
        /// <summary>Throwable items (grenades, etc.)</summary>
        Throwable,
        
        /// <summary>Miscellaneous items</summary>
        Misc
    }
}