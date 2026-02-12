namespace NightHunt.Inventory.Core.Enums
{
    /// <summary>
    /// Defines what happens when double-clicking an item in inventory UI.
    /// </summary>
    public enum DoubleClickBehavior
    {
        /// <summary>
        /// Auto-determine based on item type:
        /// - Consumables → Use
        /// - Equipment/Armor → Quick equip
        /// - Weapons → Auto-equip to first available slot
        /// </summary>
        AutoDetermine,
        
        /// <summary>
        /// Use the item (for consumables).
        /// </summary>
        Use,
        
        /// <summary>
        /// Quick equip to appropriate slot.
        /// </summary>
        QuickEquip,
        
        /// <summary>
        /// Do nothing on double-click.
        /// </summary>
        None
    }

}