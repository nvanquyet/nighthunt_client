using System.Collections.Generic;
using NightHunt.Gameplay.Inventory;

namespace NightHunt.UI.Inventory
{
    /// <summary>
    /// Interface for inventory UI view
    /// Follows Interface Segregation Principle
    /// </summary>
    public interface IInventoryUIView
    {
        void DisplayItems(List<InventorySlot> items);
        void UpdateWeight(float current, float max);
        void ShowItemInfo(ItemInfoData info);
        void HideItemInfo();
        void ShowDropAmountSelector(string itemId, int maxQuantity, System.Action<int> onConfirm);
        void HideDropAmountSelector();
    }
}

