namespace NightHunt.Inventory.Core.Enums
{
    /// <summary>
    /// Defines where an item can be located in the inventory system.
    /// Used for drag & drop validation and slot compatibility.
    /// </summary>
    public enum SlotLocationType
    {
        /// <summary>Main player inventory (list-based scrollable)</summary>
        Inventory,
        
        /// <summary>Equipment slots (Helmet, Armor, Backpack)</summary>
        Equipment,
        
        /// <summary>Weapon slots (Primary, Secondary)</summary>
        Weapon,
        
        /// <summary>Quick access slots (default: 4 slots)</summary>
        QuickSlot,
        
        /// <summary>Attachment slots on weapons/equipment</summary>
        Attachment,
        
        /// <summary>Container storage (chests, corpses, etc.)</summary>
        Container,
        
        /// <summary>Trash slot (destroys items)</summary>
        Trash,
    }
}