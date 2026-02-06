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
        public static event Action<int, ItemInstance> OnQuickSlotChanged; // slotIndex, item (null if cleared)
        public static event Action<int> OnQuickSlotSelected; // slotIndex
        public static event Action<int> OnQuickSlotUnselected; // slotIndex
        
        // Cooldown events
        public static event Action<int> OnCooldownStarted; // slotIndex
        public static event Action<int> OnCooldownEnded; // slotIndex
        
        // Progress bar events
        public static event Action<float> OnConsumeProgress; // progress (0-1)
        public static event Action OnConsumeStarted;
        public static event Action OnConsumeCompleted;
        
        public static void InvokeRequestConsume(ItemInstance item) => OnRequestConsume?.Invoke(item);
        public static void InvokeRequestEquipThrowable(ItemInstance item) => OnRequestEquipThrowable?.Invoke(item);
        public static void InvokeQuickSlotDoubleClicked(ItemInstance item, int slotIndex) => OnQuickSlotDoubleClicked?.Invoke(item, slotIndex);
        public static void InvokeConsumeComplete(ItemInstance item) => OnConsumeComplete?.Invoke(item);
        public static void InvokeConsumeCancelled(ItemInstance item, string reason) => OnConsumeCancelled?.Invoke(item, reason);
        public static void InvokeQuickSlotChanged(int slotIndex, ItemInstance item) => OnQuickSlotChanged?.Invoke(slotIndex, item);
        public static void InvokeQuickSlotSelected(int slotIndex) => OnQuickSlotSelected?.Invoke(slotIndex);
        public static void InvokeQuickSlotUnselected(int slotIndex) => OnQuickSlotUnselected?.Invoke(slotIndex);
        public static void InvokeCooldownStarted(int slotIndex) => OnCooldownStarted?.Invoke(slotIndex);
        public static void InvokeCooldownEnded(int slotIndex) => OnCooldownEnded?.Invoke(slotIndex);
        public static void InvokeConsumeProgress(float progress) => OnConsumeProgress?.Invoke(progress);
        public static void InvokeConsumeStarted() => OnConsumeStarted?.Invoke();
        public static void InvokeConsumeCompleted() => OnConsumeCompleted?.Invoke();
    }
}