using System.Collections.Generic;
using System.Linq;
using NightHunt.InteractionSystem.Core;
using UnityEngine;

namespace NightHunt.InteractionSystem.Items
{
    public static class ItemStackingService
    {
        public static bool TryAutoStack(
            InventoryComponentBase inventory,
            ItemInstance newItem,
            out ItemInstance mergedItem)
        {
            mergedItem = newItem;

            ItemDataBase itemData = ItemDatabaseManager.Instance.GetItemData(newItem.itemDataId);
            if (itemData == null) return false;

            // Cannot stack
            if (itemData.maxStack <= 1) return false;

            // Find existing stacks
            var existingItems = inventory.Items
                .Where(i => itemData.CanStack(i))
                .ToList();

            if (existingItems.Count == 0) return false;

            int remainingQuantity = newItem.quantity;

            foreach (var existing in existingItems)
            {
                if (remainingQuantity <= 0) break;

                int spaceAvailable = itemData.maxStack - existing.quantity;
                if (spaceAvailable <= 0) continue;

                int amountToAdd = Mathf.Min(spaceAvailable, remainingQuantity);

                // Update existing stack
                ItemInstance updated = existing;
                updated.quantity += amountToAdd;

                // Update in inventory (via server)
                // inventory.UpdateItem(updated);

                remainingQuantity -= amountToAdd;
            }

            // If fully stacked
            if (remainingQuantity <= 0)
            {
                mergedItem = default;
                return true;
            }

            // Partial stack
            mergedItem.quantity = remainingQuantity;
            return false;
        }

        public static ItemInstance[] SplitIntoStacks(ItemInstance item)
        {
            ItemDataBase data = ItemDatabaseManager.Instance.GetItemData(item.itemDataId);
            if (data == null) return new ItemInstance[] { item };

            if (item.quantity <= data.maxStack)
            {
                return new ItemInstance[] { item };
            }

            List<ItemInstance> stacks = new List<ItemInstance>();
            int remaining = item.quantity;

            while (remaining > 0)
            {
                int stackSize = Mathf.Min(remaining, data.maxStack);

                ItemInstance stack = ItemInstanceFactory.CreateInstance(item.itemDataId, stackSize);
                stack.durability = item.durability;
                stack.attachments = new List<AttachmentInstance>(item.attachments);

                stacks.Add(stack);
                remaining -= stackSize;
            }

            return stacks.ToArray();
        }
    }
}