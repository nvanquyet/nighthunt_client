namespace NightHunt.Inventory.Core.Enums
{
   
    /// <summary>
    /// Defines types of inventory operations.
    /// </summary>
    public enum OperationType
    {
        /// <summary>Move item to empty slot</summary>
        Move,
        
        /// <summary>Swap items between occupied slots</summary>
        Swap,
        
        /// <summary>Equip item from inventory</summary>
        Equip,
        
        /// <summary>Unequip item to inventory</summary>
        Unequip,
        
        /// <summary>Attach attachment to weapon/equipment</summary>
        Attach,
        
        /// <summary>Detach attachment to inventory</summary>
        Detach,
        
        /// <summary>Merge stack A into stack B</summary>
        StackMerge,
        
        /// <summary>Transfer between inventory and container</summary>
        ContainerTransfer,
        
        /// <summary>Drop item to world</summary>
        WorldDrop,
        
        /// <summary>Destroy item (trash)</summary>
        Trash
    }
}