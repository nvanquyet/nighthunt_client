using System;
using UnityEngine;

namespace NightHunt.Inventory.Core
{
    /// <summary>
    /// Individual inventory slot in list-based inventory.
    /// </summary>
    [Serializable]
    public class InventorySlot
    {
        public int Index;
        public ItemInstance Item; // Null if empty
        
        public bool IsEmpty => Item == null;
        
        public InventorySlot()
        {
            Index = -1;
            Item = null;
        }
        
        public InventorySlot(int index)
        {
            Index = index;
            Item = null;
        }
    }
}
