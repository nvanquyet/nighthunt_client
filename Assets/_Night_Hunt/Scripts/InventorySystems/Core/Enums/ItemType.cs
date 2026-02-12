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

        /// <summary>Attachments for weapons/equipment</summary>
        Attachment,
        
        //Event Items could be added in future expansions
        Event,
    }
}