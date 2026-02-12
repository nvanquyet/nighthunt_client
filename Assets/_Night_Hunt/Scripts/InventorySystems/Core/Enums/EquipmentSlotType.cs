namespace NightHunt.Inventory.Core.Enums
{
    /// <summary>
    /// Defines equipment slot types.
    /// Can be expanded at runtime for flexible equipment systems.
    /// </summary>
    public enum EquipmentSlotType
    {
        None,
        
        /// <summary>Head protection slot</summary>
        Helmet,
        
        /// <summary>Body protection slot</summary>
        Armor,
        
        /// <summary>Storage capacity expansion slot</summary>
        Backpack,
        
        /// <summary>Boot raise speed for player</summary>
        Boots,
        
        // TODO: Accessories (rings, necklaces) - runtime flexible expansion
    }
}