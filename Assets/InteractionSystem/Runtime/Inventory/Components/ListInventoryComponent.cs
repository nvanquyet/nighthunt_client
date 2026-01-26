using FishNet.Object;
using NightHunt.InteractionSystem.Core;
using UnityEngine;

namespace NightHunt.InteractionSystem.Inventory
{
    public class ListInventoryComponent : InventoryComponentBase
    {
        [Header("List Settings")]
        [SerializeField] private int maxCapacity = 30;
    
        public override int Capacity => maxCapacity;
    
        [Server]
        public override bool TryAddItem(ItemInstance item)
        {
            // Simple capacity check
            if (items.Count >= maxCapacity)
            {
                return false;
            }
        
            return base.TryAddItem(item);
        }
    }
}