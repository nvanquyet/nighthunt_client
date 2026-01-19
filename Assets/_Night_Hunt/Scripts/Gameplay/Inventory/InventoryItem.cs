using NightHunt.Data;

namespace NightHunt.Gameplay.Inventory
{
    /// <summary>
    /// Wrapper class for inventory items (used by UI)
    /// </summary>
    public class InventoryItem
    {
        public string ItemId { get; private set; }
        public ItemConfigData Config { get; private set; }
        public int Quantity { get; private set; }

        public InventoryItem(InventorySlot slot)
        {
            if (slot != null && !slot.IsEmpty)
            {
                ItemId = slot.Item.ItemId;
                Config = slot.Item;
                Quantity = slot.Quantity;
            }
        }

        public InventoryItem(ItemConfigData config, int quantity)
        {
            ItemId = config.ItemId;
            Config = config;
            Quantity = quantity;
        }
    }
}

