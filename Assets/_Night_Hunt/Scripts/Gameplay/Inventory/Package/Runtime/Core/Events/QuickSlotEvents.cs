using System;
using NightHunt.Inventory.Core;

namespace NightHunt.Inventory.Events
{
    /// <summary>
    /// Events for quickslot system.
    /// </summary>
    public static class QuickSlotEvents
    {
        public static event Action<ItemInstance> OnRequestConsume;
        public static event Action<ItemInstance> OnRequestEquipThrowable;
        public static event Action<ItemInstance, int> OnQuickSlotDoubleClicked;
        public static event Action<ItemInstance> OnConsumeComplete;
        public static event Action<ItemInstance, string> OnConsumeCancelled;
        
        public static void FireRequestConsume(ItemInstance item) => OnRequestConsume?.Invoke(item);
        public static void FireRequestEquipThrowable(ItemInstance item) => OnRequestEquipThrowable?.Invoke(item);
        public static void FireQuickSlotDoubleClicked(ItemInstance item, int slotIndex) => OnQuickSlotDoubleClicked?.Invoke(item, slotIndex);
        public static void FireConsumeComplete(ItemInstance item) => OnConsumeComplete?.Invoke(item);
        public static void FireConsumeCancelled(ItemInstance item, string reason) => OnConsumeCancelled?.Invoke(item, reason);
    }
}
