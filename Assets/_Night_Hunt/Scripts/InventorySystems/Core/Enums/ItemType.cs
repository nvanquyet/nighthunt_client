namespace NightHunt.Inventory.Core.Enums
{
    /// <summary>
    /// Defines all item types in the game.
    /// Used for categorization and validation.
    /// </summary>
    public enum ItemType
    {
        /// <summary>Weapons (guns, melee weapons)</summary>
        Weapon,
        
        /// <summary>Equipment (helmet, armor, backpack, boots)</summary>
        Equipment,
        
        /// <summary>Consumable items (health potions, food, buffs)</summary>
        Consumable,
        
        /// <summary>Throwable items (grenades, smoke bombs)</summary>
        Throwable,
        
        /// <summary>Ammunition</summary>
        Ammo,
        
        /// <summary>Crafting materials</summary>
        Material,
        
        /// <summary>Quest items</summary>
        Quest,
        
        /// <summary>Attachments for weapons/equipment</summary>
        Attachment,
        
        /// <summary>Key items (permanent unlocks)</summary>
        Key,
        
        /// <summary>Miscellaneous items</summary>
        Miscellaneous
    }
}