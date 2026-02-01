using System;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.Gameplay.UI;

namespace NightHunt.Gameplay.Inventory.Events
{
    /// <summary>
    /// Events fired by Logic Layer when inventory state changes.
    /// UI Layer subscribes to these events to update display.
    /// 
    /// ARCHITECTURE NOTE - Event System Separation:
    /// This class (InventoryLogicEvents) is part of the Gameplay layer and is used for
    /// UI updates. It is separate from InteractionSystem.Events.InventoryEvents which is
    /// used by the InteractionSystem package components (GridInventoryComponent, EquipmentManager, etc.).
    /// 
    /// Event Flow:
    /// 1. InteractionSystem components fire InventoryEvents (package-level events)
    /// 2. InventoryService subscribes to InventoryEvents and acts as a bridge
    /// 3. InventoryService fires InventoryLogicEvents (this class) for UI layer
    /// 
    /// This separation allows:
    /// - InteractionSystem package to remain independent
    /// - Gameplay layer to have its own event system for UI
    /// - Clear separation of concerns between package and gameplay code
    /// 
    /// IMPORTANT FOR HEADLESS SERVER:
    /// - These events are CLIENT-ONLY (for UI updates)
    /// - Should only be fired on client, never on server
    /// - UI components should check IsServer before subscribing
    /// - InventoryService already checks IsServer before firing these events
    /// </summary>
    public static class InventoryLogicEvents
    {
        // Item management events
        public static event Action<ItemInstance> OnItemAdded;
        public static event Action<ItemInstance, int> OnItemRemoved; // item, removedQuantity
        public static event Action<ItemInstance, int> OnItemQuantityChanged; // item, newQuantity
        public static event Action OnInventoryChanged; // Generic inventory change

        // Item movement events
        public static event Action<string, int, int, int, int> OnItemMoved; // itemId, fromX, fromY, toX, toY
        public static event Action<string, int, int> OnItemMovedToSlot; // itemId, toX, toY

        // Equipment events
        public static event Action<string, EquipmentSlotType> OnItemEquipped; // itemId, slotType
        public static event Action<string, EquipmentSlotType> OnItemUnequipped; // itemId, slotType

        // Quick slot events
        public static event Action<int, string> OnQuickSlotAssigned; // slotIndex, itemId
        public static event Action<int> OnQuickSlotCleared; // slotIndex

        // Usage events
        public static event Action<string> OnItemUseStarted; // itemId
        public static event Action<string> OnItemUseCompleted; // itemId
        public static event Action<string> OnItemUseCancelled; // itemId
        public static event Action<string, float> OnItemUseProgress; // itemId, progress (0-1)

        // Container/Shop events
        public static event Action<string> OnContainerOpened; // containerId
        public static event Action<string> OnContainerClosed; // containerId
        public static event Action<string, ItemInstance> OnContainerItemAdded; // containerId, item
        public static event Action<string, ItemInstance> OnContainerItemRemoved; // containerId, item

        // Shop events
        public static event Action<string, string> OnItemPurchased; // shopId, itemId
        public static event Action<string, string> OnItemSold; // shopId, itemId

        // Fire methods (called by Logic Layer)
        public static void FireItemAdded(ItemInstance item) => OnItemAdded?.Invoke(item);
        public static void FireItemRemoved(ItemInstance item, int removedQuantity) => OnItemRemoved?.Invoke(item, removedQuantity);
        public static void FireItemQuantityChanged(ItemInstance item, int newQuantity) => OnItemQuantityChanged?.Invoke(item, newQuantity);
        public static void FireInventoryChanged() => OnInventoryChanged?.Invoke();
        public static void FireItemMoved(string itemId, int fromX, int fromY, int toX, int toY) => OnItemMoved?.Invoke(itemId, fromX, fromY, toX, toY);
        public static void FireItemMovedToSlot(string itemId, int toX, int toY) => OnItemMovedToSlot?.Invoke(itemId, toX, toY);
        public static void FireItemEquipped(string itemId, EquipmentSlotType slotType) => OnItemEquipped?.Invoke(itemId, slotType);
        public static void FireItemUnequipped(string itemId, EquipmentSlotType slotType) => OnItemUnequipped?.Invoke(itemId, slotType);
        public static void FireQuickSlotAssigned(int slotIndex, string itemId) => OnQuickSlotAssigned?.Invoke(slotIndex, itemId);
        public static void FireQuickSlotCleared(int slotIndex) => OnQuickSlotCleared?.Invoke(slotIndex);
        public static void FireItemUseStarted(string itemId) => OnItemUseStarted?.Invoke(itemId);
        public static void FireItemUseCompleted(string itemId) => OnItemUseCompleted?.Invoke(itemId);
        public static void FireItemUseCancelled(string itemId) => OnItemUseCancelled?.Invoke(itemId);
        public static void FireItemUseProgress(string itemId, float progress) => OnItemUseProgress?.Invoke(itemId, progress);
        public static void FireContainerOpened(string containerId) => OnContainerOpened?.Invoke(containerId);
        public static void FireContainerClosed(string containerId) => OnContainerClosed?.Invoke(containerId);
        public static void FireContainerItemAdded(string containerId, ItemInstance item) => OnContainerItemAdded?.Invoke(containerId, item);
        public static void FireContainerItemRemoved(string containerId, ItemInstance item) => OnContainerItemRemoved?.Invoke(containerId, item);
        public static void FireItemPurchased(string shopId, string itemId) => OnItemPurchased?.Invoke(shopId, itemId);
        public static void FireItemSold(string shopId, string itemId) => OnItemSold?.Invoke(shopId, itemId);
    }
}
