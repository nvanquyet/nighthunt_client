using System.Collections.Generic;
using NightHunt.Gameplay.Inventory;
using NightHunt.Data;

namespace NightHunt.UI.Inventory
{
    /// <summary>
    /// Presenter for inventory UI
    /// Follows MVP pattern and Single Responsibility Principle
    /// Separates UI logic from business logic
    /// </summary>
    public class InventoryUIPresenter
    {
        private readonly IInventoryProvider inventoryProvider;
        private readonly IInventoryUIView view;

        public InventoryUIPresenter(IInventoryProvider provider, IInventoryUIView uiView)
        {
            inventoryProvider = provider ?? throw new System.ArgumentNullException(nameof(provider));
            view = uiView ?? throw new System.ArgumentNullException(nameof(uiView));
        }

        /// <summary>
        /// Refresh inventory display
        /// </summary>
        public void RefreshInventory()
        {
            if (inventoryProvider == null || view == null) return;

            var items = inventoryProvider.GetItems();
            view.DisplayItems(items);
            view.UpdateWeight(inventoryProvider.GetCurrentWeight(), inventoryProvider.GetWeightCapacity());
        }

        /// <summary>
        /// Handle item selection
        /// </summary>
        public void OnItemSelected(InventorySlot slot)
        {
            if (slot == null || slot.IsEmpty)
            {
                view.HideItemInfo();
                return;
            }

            var itemConfig = GameConfigLoader.Instance?.GetItemConfig(slot.Item.ItemId);
            if (itemConfig == null) return;

            view.ShowItemInfo(new ItemInfoData
            {
                ItemId = slot.Item.ItemId,
                DisplayName = itemConfig.DisplayName,
                Quantity = slot.Quantity,
                Weight = itemConfig.Weight,
                CanDrop = itemConfig.CanDrop,
                // ItemConfigData.UseType is a string in this project.
                CanUse = itemConfig.IsConsumable || (!string.IsNullOrEmpty(itemConfig.UseType) && itemConfig.UseType != "None"),
                Description = GetItemDescription(itemConfig)
            });
        }

        /// <summary>
        /// Get item description from config
        /// </summary>
        private string GetItemDescription(ItemConfigData config)
        {
            if (config == null) return "";
            if (config.IsConsumable)
                return $"Effect: {config.EffectType}, Value: {config.EffectValue}";
            return config.Category ?? "";
        }
    }

    /// <summary>
    /// Item info data structure
    /// </summary>
    public struct ItemInfoData
    {
        public string ItemId;
        public string DisplayName;
        public int Quantity;
        public float Weight;
        public bool CanDrop;
        public bool CanUse;
        public string Description;
    }
}

