using System;
using NightHunt.Gameplay.Inventory;
using NightHunt.Gameplay.UI;

namespace NightHunt.Gameplay.Inventory.Events
{
    /// <summary>
    /// Events fired by UI Layer to request actions from Logic Layer
    /// Logic Layer subscribes to these events and processes requests
    /// </summary>
    public static class InventoryUIEvents
    {
        // Item movement requests
        public static event Action<string, int, int, int, int> OnMoveItemRequested; // itemId, fromX, fromY, toX, toY
        public static event Action<string, int, int> OnMoveItemToSlotRequested; // itemId, toX, toY
        public static event Action<string, int, int, string, int, int> OnSwapItemsRequested; // itemId1, x1, y1, itemId2, x2, y2

        // Equipment requests
        public static event Action<string, EquipmentSlotType> OnEquipItemRequested; // itemId, slotType
        public static event Action<string, EquipmentSlotType> OnUnequipItemRequested; // itemId, slotType

        // Quick slot requests
        public static event Action<string, int> OnAssignQuickSlotRequested; // itemId, slotIndex
        public static event Action<int> OnClearQuickSlotRequested; // slotIndex

        // Usage requests
        public static event Action<string> OnUseItemRequested; // itemId
        public static event Action<string> OnCancelUseItemRequested; // itemId

        // Container/Shop requests
        public static event Action<string> OnOpenContainerRequested; // containerId
        public static event Action<string> OnCloseContainerRequested; // containerId
        public static event Action<string, string, int, int> OnMoveItemToContainerRequested; // containerId, itemId, fromX, fromY
        public static event Action<string, string, int, int> OnMoveItemFromContainerRequested; // containerId, itemId, toX, toY

        // Shop requests
        public static event Action<string, string> OnPurchaseItemRequested; // shopId, itemId
        public static event Action<string, string> OnSellItemRequested; // shopId, itemId

        // Fire methods (called by UI Layer)
        public static void RequestMoveItem(string itemId, int fromX, int fromY, int toX, int toY) => OnMoveItemRequested?.Invoke(itemId, fromX, fromY, toX, toY);
        public static void RequestMoveItemToSlot(string itemId, int toX, int toY) => OnMoveItemToSlotRequested?.Invoke(itemId, toX, toY);
        public static void RequestSwapItems(string itemId1, int x1, int y1, string itemId2, int x2, int y2) => OnSwapItemsRequested?.Invoke(itemId1, x1, y1, itemId2, x2, y2);
        public static void RequestEquipItem(string itemId, EquipmentSlotType slotType) => OnEquipItemRequested?.Invoke(itemId, slotType);
        public static void RequestUnequipItem(string itemId, EquipmentSlotType slotType) => OnUnequipItemRequested?.Invoke(itemId, slotType);
        public static void RequestAssignQuickSlot(string itemId, int slotIndex) => OnAssignQuickSlotRequested?.Invoke(itemId, slotIndex);
        public static void RequestClearQuickSlot(int slotIndex) => OnClearQuickSlotRequested?.Invoke(slotIndex);
        public static void RequestUseItem(string itemId) => OnUseItemRequested?.Invoke(itemId);
        public static void RequestCancelUseItem(string itemId) => OnCancelUseItemRequested?.Invoke(itemId);
        public static void RequestOpenContainer(string containerId) => OnOpenContainerRequested?.Invoke(containerId);
        public static void RequestCloseContainer(string containerId) => OnCloseContainerRequested?.Invoke(containerId);
        public static void RequestMoveItemToContainer(string containerId, string itemId, int fromX, int fromY) => OnMoveItemToContainerRequested?.Invoke(containerId, itemId, fromX, fromY);
        public static void RequestMoveItemFromContainer(string containerId, string itemId, int toX, int toY) => OnMoveItemFromContainerRequested?.Invoke(containerId, itemId, toX, toY);
        public static void RequestPurchaseItem(string shopId, string itemId) => OnPurchaseItemRequested?.Invoke(shopId, itemId);
        public static void RequestSellItem(string shopId, string itemId) => OnSellItemRequested?.Invoke(shopId, itemId);
    }
}
