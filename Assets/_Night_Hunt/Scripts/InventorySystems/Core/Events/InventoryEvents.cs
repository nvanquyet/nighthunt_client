using System;
using NightHunt.Inventory.Core.Structs;

namespace NightHunt.Inventory.Core.Events
{
    /// <summary>
    /// Centralized event dispatcher for inventory system.
    /// UI subscribes to these events to update.
    /// </summary>
    public static class InventoryEvents
    {
        public static event Action<ItemAddedEvent> OnItemAdded;
        public static event Action<ItemRemovedEvent> OnItemRemoved;
        public static event Action<ItemMovedEvent> OnItemMoved;
        public static event Action<ItemEquippedEvent> OnItemEquipped;
        public static event Action<ItemUnequippedEvent> OnItemUnequipped;
        public static event Action<WeaponEquippedEvent> OnWeaponEquipped;
        public static event Action<WeaponUnequippedEvent> OnWeaponUnequipped;
        public static event Action<QuickSlotAssignedEvent> OnQuickSlotAssigned;
        public static event Action<QuickSlotRemovedEvent> OnQuickSlotRemoved;
        public static event Action<AttachmentAttachedEvent> OnAttachmentAttached;
        public static event Action<AttachmentDetachedEvent> OnAttachmentDetached;
        public static event Action<ItemUsageStartedEvent> OnItemUsageStarted;
        public static event Action<ItemUsageCompletedEvent> OnItemUsageCompleted;
        public static event Action<ItemUsageCancelledEvent> OnItemUsageCancelled;
        public static event Action<ItemResourceChangedEvent> OnItemResourceChanged;
        public static event Action<WeightChangedEvent> OnWeightChanged;
        public static event Action<StatModifiersChangedEvent> OnStatModifiersChanged;
        public static event Action<OperationFailedEvent> OnOperationFailed;
        
        // ===== RAISE METHODS =====
        
        public static void RaiseItemAdded(ItemAddedEvent eventData) => OnItemAdded?.Invoke(eventData);
        public static void RaiseItemRemoved(ItemRemovedEvent eventData) => OnItemRemoved?.Invoke(eventData);
        public static void RaiseItemMoved(ItemMovedEvent eventData) => OnItemMoved?.Invoke(eventData);
        public static void RaiseItemEquipped(ItemEquippedEvent eventData) => OnItemEquipped?.Invoke(eventData);
        public static void RaiseItemUnequipped(ItemUnequippedEvent eventData) => OnItemUnequipped?.Invoke(eventData);
        public static void RaiseWeaponEquipped(WeaponEquippedEvent eventData) => OnWeaponEquipped?.Invoke(eventData);
        public static void RaiseWeaponUnequipped(WeaponUnequippedEvent eventData) => OnWeaponUnequipped?.Invoke(eventData);
        public static void RaiseQuickSlotAssigned(QuickSlotAssignedEvent eventData) => OnQuickSlotAssigned?.Invoke(eventData);
        public static void RaiseQuickSlotRemoved(QuickSlotRemovedEvent eventData) => OnQuickSlotRemoved?.Invoke(eventData);
        public static void RaiseAttachmentAttached(AttachmentAttachedEvent eventData) => OnAttachmentAttached?.Invoke(eventData);
        public static void RaiseAttachmentDetached(AttachmentDetachedEvent eventData) => OnAttachmentDetached?.Invoke(eventData);
        public static void RaiseItemUsageStarted(ItemUsageStartedEvent eventData) => OnItemUsageStarted?.Invoke(eventData);
        public static void RaiseItemUsageCompleted(ItemUsageCompletedEvent eventData) => OnItemUsageCompleted?.Invoke(eventData);
        public static void RaiseItemUsageCancelled(ItemUsageCancelledEvent eventData) => OnItemUsageCancelled?.Invoke(eventData);
        public static void RaiseItemResourceChanged(ItemResourceChangedEvent eventData) => OnItemResourceChanged?.Invoke(eventData);
        public static void RaiseWeightChanged(WeightChangedEvent eventData) => OnWeightChanged?.Invoke(eventData);
        public static void RaiseStatModifiersChanged(StatModifiersChangedEvent eventData) => OnStatModifiersChanged?.Invoke(eventData);
        public static void RaiseOperationFailed(OperationFailedEvent eventData) => OnOperationFailed?.Invoke(eventData);
        
        /// <summary>
        /// Clear all event subscriptions (useful for cleanup).
        /// </summary>
        public static void ClearAllEvents()
        {
            OnItemAdded = null;
            OnItemRemoved = null;
            OnItemMoved = null;
            OnItemEquipped = null;
            OnItemUnequipped = null;
            OnWeaponEquipped = null;
            OnWeaponUnequipped = null;
            OnQuickSlotAssigned = null;
            OnQuickSlotRemoved = null;
            OnAttachmentAttached = null;
            OnAttachmentDetached = null;
            OnItemUsageStarted = null;
            OnItemUsageCompleted = null;
            OnItemUsageCancelled = null;
            OnItemResourceChanged = null;
            OnWeightChanged = null;
            OnStatModifiersChanged = null;
            OnOperationFailed = null;
        }
    }
}