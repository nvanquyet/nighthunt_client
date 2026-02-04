using System;
using NightHunt.Inventory.Core.Data;

namespace NightHunt.Inventory.Core.Events
{
    
    /// <summary>
    /// Events for consumable effects system.
    /// </summary>
    public static class ConsumableEvents
    {
        public static event Action<ItemInstance> OnApplyEffect;
        
        public static void InvokeApplyEffect(ItemInstance item) => OnApplyEffect?.Invoke(item);
    }

}