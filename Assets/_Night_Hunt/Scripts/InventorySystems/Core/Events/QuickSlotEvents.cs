using System;
using NightHunt.Inventory.Core.Data;

namespace NightHunt.Inventory.Core.Events
{
    /// <summary>
    /// Events for quick slot system.
    /// </summary>
    public static class QuickSlotEvents
    {
        public static event Action<ItemInstance> OnRequestConsume; // Start consume with progress
        public static event Action<ItemInstance> OnRequestEquipThrowable;
        public static event Action<ItemInstance, int> OnQuickSlotDoubleClicked; // item, slotIndex
        public static event Action<ItemInstance> OnConsumeComplete;
        public static event Action<ItemInstance, string> OnConsumeCancelled; // item, reason
        
        public static void InvokeRequestConsume(ItemInstance item) => OnRequestConsume?.Invoke(item);
        public static void InvokeRequestEquipThrowable(ItemInstance item) => OnRequestEquipThrowable?.Invoke(item);
        public static void InvokeQuickSlotDoubleClicked(ItemInstance item, int slotIndex) => OnQuickSlotDoubleClicked?.Invoke(item, slotIndex);
        public static void InvokeConsumeComplete(ItemInstance item) => OnConsumeComplete?.Invoke(item);
        public static void InvokeConsumeCancelled(ItemInstance item, string reason) => OnConsumeCancelled?.Invoke(item, reason);
    }
}