namespace NightHunt.Inventory.Core.Enums
{
    /// <summary>
    /// Defines equipment slot types.
    /// Can be expanded at runtime for flexible equipment systems.
    /// </summary>
    public enum EquipmentSlotType
    {
        /// <summary>Head protection slot</summary>
        Helmet,
        
        /// <summary>Body protection slot</summary>
        Armor,
        
        /// <summary>Storage capacity expansion slot</summary>
        Backpack
        
        // TODO: Accessories (rings, necklaces) - runtime flexible expansion
    }
}