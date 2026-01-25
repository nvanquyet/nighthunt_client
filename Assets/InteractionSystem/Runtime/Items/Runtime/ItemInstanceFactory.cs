using System;
using System.Collections.Generic;
using NightHunt.InteractionSystem.Core;
using UnityEngine;

namespace NightHunt.InteractionSystem.Items
{
    public static class ItemInstanceFactory
    {
        public static ItemInstance CreateInstance(string itemDataId, int quantity = 1)
        {
            ItemDataBase data = ItemDatabaseManager.Instance.GetItemData(itemDataId);
            if (data == null)
            {
                Debug.LogError($"Cannot create instance: Item {itemDataId} not found");
                return default;
            }

            return new ItemInstance
            {
                instanceId = Guid.NewGuid().ToString(),
                itemDataId = itemDataId,
                quantity = Mathf.Clamp(quantity, 1, data.maxStack),
                durability = 100f,
                metadata = new Dictionary<string, object>(),
                attachments = new List<AttachmentInstance>()
            };
        }

        public static ItemInstance CreateInstanceWithAttachments(
            string itemDataId,
            int quantity,
            AttachmentInstance[] attachments)
        {
            ItemInstance instance = CreateInstance(itemDataId, quantity);

            if (attachments != null)
            {
                instance.attachments = new List<AttachmentInstance>(attachments);
            }

            return instance;
        }

        public static ItemInstance Clone(ItemInstance original)
        {
            return new ItemInstance
            {
                instanceId = Guid.NewGuid().ToString(),
                itemDataId = original.itemDataId,
                quantity = original.quantity,
                durability = original.durability,
                metadata = new Dictionary<string, object>(original.metadata),
                attachments = new List<AttachmentInstance>(original.attachments)
            };
        }

        public static ItemInstance Split(ref ItemInstance original, int amountToSplit)
        {
            if (amountToSplit >= original.quantity)
            {
                Debug.LogError("Cannot split: amount exceeds quantity");
                return default;
            }

            ItemInstance split = Clone(original);
            split.quantity = amountToSplit;

            original.quantity -= amountToSplit;

            return split;
        }

        public static bool Merge(ref ItemInstance target, ItemInstance source)
        {
            ItemDataBase data = ItemDatabaseManager.Instance.GetItemData(target.itemDataId);
            if (data == null) return false;

            if (!data.CanStack(source)) return false;

            int totalQuantity = target.quantity + source.quantity;
            if (totalQuantity > data.maxStack)
            {
                return false;
            }

            target.quantity = totalQuantity;
            return true;
        }
    }
}