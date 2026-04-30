using System;

namespace NightHunt.GameplaySystems.Core.Data
{
    /// <summary>
    /// Shared construction helpers for runtime item instances and serialized loot data.
    /// Keeps inventory, loot tables, containers, and stack splits on the same initialization rules.
    /// </summary>
    public static class ItemInstanceFactory
    {
        public static ItemInstance Create(ItemDefinition definition, int quantity = 1, int inventoryIndex = -1)
        {
            if (definition == null)
                return null;

            var item = new ItemInstance(definition.ItemID, quantity, inventoryIndex)
            {
                CurrentResource = definition.GetDefaultCurrentValue(),
                CurrentMagazine = 0
            };

            EnsureAttachmentSlots(item, definition);
            return item;
        }

        public static ItemInstanceData CreateData(ItemDefinition definition, int quantity = 1, int inventoryIndex = -1)
        {
            var item = Create(definition, quantity, inventoryIndex);
            return item != null ? item.ToData() : default;
        }

        public static ItemInstance CloneStackPortion(ItemInstance source, int quantity, int inventoryIndex)
        {
            if (source == null)
                return null;

            var item = new ItemInstance(source.DefinitionID, quantity, inventoryIndex)
            {
                CurrentResource = source.CurrentResource,
                CurrentMagazine = source.CurrentMagazine,
                CustomData = source.CustomData,
                AttachedItems = source.AttachedItems != null ? (string[])source.AttachedItems.Clone() : null
            };

            item.InstanceID = Guid.NewGuid().ToString();
            item.CreatedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return item;
        }

        public static ItemInstanceData CopyDataForQuantity(ItemInstanceData source, int quantity, bool newInstanceId)
        {
            var copy = source;
            copy.Quantity = quantity;

            if (newInstanceId || string.IsNullOrEmpty(copy.InstanceID))
                copy.InstanceID = Guid.NewGuid().ToString();

            if (copy.AttachedItems != null && !HasAnyAttachment(copy.AttachedItems))
                copy.AttachedItems = null;

            return copy;
        }

        public static void EnsureAttachmentSlots(ItemInstance item, ItemDefinition definition)
        {
            if (item == null || definition?.AttachmentSlots == null || definition.AttachmentSlots.Length == 0)
                return;

            int slotCount = definition.AttachmentSlots.Length;
            if (item.AttachedItems != null && item.AttachedItems.Length == slotCount)
                return;

            var slots = new string[slotCount];
            if (item.AttachedItems != null)
            {
                int copyCount = Math.Min(item.AttachedItems.Length, slotCount);
                for (int i = 0; i < copyCount; i++)
                    slots[i] = item.AttachedItems[i];
            }

            item.AttachedItems = slots;
        }

        public static void StripEmptyAttachmentSlots(ItemInstance item)
        {
            if (item?.AttachedItems != null && !HasAnyAttachment(item.AttachedItems))
                item.AttachedItems = null;
        }

        public static void StripEmptyAttachmentSlots(ref ItemInstanceData data)
        {
            if (data.AttachedItems != null && !HasAnyAttachment(data.AttachedItems))
                data.AttachedItems = null;
        }

        private static bool HasAnyAttachment(string[] attachments)
        {
            if (attachments == null)
                return false;

            for (int i = 0; i < attachments.Length; i++)
            {
                if (!string.IsNullOrEmpty(attachments[i]))
                    return true;
            }

            return false;
        }
    }
}
