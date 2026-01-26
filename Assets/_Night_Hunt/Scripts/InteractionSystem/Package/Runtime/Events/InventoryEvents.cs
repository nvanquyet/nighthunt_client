using System;
using UnityEngine;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Core.Interfaces;
using NightHunt.InteractionSystem.Items.Data;

namespace NightHunt.InteractionSystem.Events
{
    /// <summary>
    /// Event system for inventory and equipment changes.
    /// Uses observer pattern to decouple UI from logic.
    /// </summary>
    public static class InventoryEvents
    {
        // Item Events
        public static event Action<ItemInstance> OnItemAdded;
        public static event Action<ItemInstance, int> OnItemRemoved;
        public static event Action<ItemInstance, int> OnItemQuantityChanged;
        public static event Action OnInventoryChanged;

        // Weight Events
        public static event Action<float, float> OnWeightChanged; // currentWeight, maxWeight
        public static event Action<float> OnWeightLimitReached; // currentWeight
        public static event Action<float> OnWeightLimitExceeded; // currentWeight (over limit)
        public static event Action<float> OnWeightWarning; // currentWeight (near limit, e.g., >80%)

        // Slot Events
        public static event Action<int, int> OnSlotCountChanged; // currentSlots, maxSlots
        public static event Action OnInventoryFull;
        public static event Action OnInventorySpaceAvailable;

        // Equipment Events
        public static event Action<EquipmentSlot, ItemInstance> OnItemEquipped;
        public static event Action<EquipmentSlot, ItemInstance> OnItemUnequipped;
        public static event Action OnEquipmentChanged;

        // Loot Events
        public static event Action<ILootContainer> OnLootContainerOpened;
        public static event Action<ILootContainer> OnLootContainerClosed;
        public static event Action<ItemInstance, ILootContainer> OnItemLooted;

        // Pickup Events
        public static event Action<ItemInstance, string> OnItemPickedUp; // item, pickupableName
        public static event Action<string> OnPickupFailed; // reason

        // Attachment Events
        public static event Action<EquipmentSlot, Core.Interfaces.AttachmentSlotType, AttachmentData> OnAttachmentAttached;
        public static event Action<EquipmentSlot, Core.Interfaces.AttachmentSlotType, AttachmentData> OnAttachmentDetached;

        /// <summary>
        /// Invoke when item is added.
        /// </summary>
        public static void InvokeItemAdded(ItemInstance item)
        {
            OnItemAdded?.Invoke(item);
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Invoke when item is removed.
        /// </summary>
        public static void InvokeItemRemoved(ItemInstance item, int quantityRemoved)
        {
            OnItemRemoved?.Invoke(item, quantityRemoved);
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Invoke when item quantity changes.
        /// </summary>
        public static void InvokeItemQuantityChanged(ItemInstance item, int newQuantity)
        {
            OnItemQuantityChanged?.Invoke(item, newQuantity);
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Invoke when weight changes.
        /// </summary>
        public static void InvokeWeightChanged(float currentWeight, float maxWeight)
        {
            OnWeightChanged?.Invoke(currentWeight, maxWeight);

            float percentage = maxWeight > 0 ? currentWeight / maxWeight : 0f;

            // Check weight warnings
            if (currentWeight >= maxWeight)
            {
                OnWeightLimitExceeded?.Invoke(currentWeight);
            }
            else if (currentWeight >= maxWeight * 0.95f)
            {
                OnWeightLimitReached?.Invoke(currentWeight);
            }
            else if (percentage >= 0.8f)
            {
                OnWeightWarning?.Invoke(currentWeight);
            }
        }

        /// <summary>
        /// Invoke when slot count changes.
        /// </summary>
        public static void InvokeSlotCountChanged(int currentSlots, int maxSlots)
        {
            OnSlotCountChanged?.Invoke(currentSlots, maxSlots);

            if (currentSlots >= maxSlots)
            {
                OnInventoryFull?.Invoke();
            }
            else
            {
                OnInventorySpaceAvailable?.Invoke();
            }
        }

        /// <summary>
        /// Invoke when item is equipped.
        /// </summary>
        public static void InvokeItemEquipped(EquipmentSlot slot, ItemInstance item)
        {
            OnItemEquipped?.Invoke(slot, item);
            OnEquipmentChanged?.Invoke();
        }

        /// <summary>
        /// Invoke when item is unequipped.
        /// </summary>
        public static void InvokeItemUnequipped(EquipmentSlot slot, ItemInstance item)
        {
            OnItemUnequipped?.Invoke(slot, item);
            OnEquipmentChanged?.Invoke();
        }

        /// <summary>
        /// Invoke when loot container is opened.
        /// </summary>
        public static void InvokeLootContainerOpened(ILootContainer container)
        {
            OnLootContainerOpened?.Invoke(container);
        }

        /// <summary>
        /// Invoke when loot container is closed.
        /// </summary>
        public static void InvokeLootContainerClosed(ILootContainer container)
        {
            OnLootContainerClosed?.Invoke(container);
        }

        /// <summary>
        /// Invoke when item is looted.
        /// </summary>
        public static void InvokeItemLooted(ItemInstance item, ILootContainer source)
        {
            OnItemLooted?.Invoke(item, source);
        }

        /// <summary>
        /// Invoke when item is picked up.
        /// </summary>
        public static void InvokeItemPickedUp(ItemInstance item, string pickupableName)
        {
            OnItemPickedUp?.Invoke(item, pickupableName);
        }

        /// <summary>
        /// Invoke when pickup fails.
        /// </summary>
        public static void InvokePickupFailed(string reason)
        {
            OnPickupFailed?.Invoke(reason);
        }

        /// <summary>
        /// Invoke when inventory changes (for UI toggle, etc.).
        /// </summary>
        public static void InvokeInventoryChanged()
        {
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Invoke when attachment is attached to equipment.
        /// </summary>
        public static void InvokeAttachmentAttached(EquipmentSlot equipmentSlot, Core.Interfaces.AttachmentSlotType attachmentSlot, AttachmentData attachment)
        {
            OnAttachmentAttached?.Invoke(equipmentSlot, attachmentSlot, attachment);
            OnEquipmentChanged?.Invoke();
        }

        /// <summary>
        /// Invoke when attachment is detached from equipment.
        /// </summary>
        public static void InvokeAttachmentDetached(EquipmentSlot equipmentSlot, Core.Interfaces.AttachmentSlotType attachmentSlot, AttachmentData attachment)
        {
            OnAttachmentDetached?.Invoke(equipmentSlot, attachmentSlot, attachment);
            OnEquipmentChanged?.Invoke();
        }
    }
}
