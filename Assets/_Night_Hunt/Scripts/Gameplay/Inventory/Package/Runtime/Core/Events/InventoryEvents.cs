using System;
using NightHunt.Inventory.Core;

namespace NightHunt.Inventory.Events
{
    /// <summary>
    /// Events for inventory operations.
    /// UI layer listens, Domain layer fires.
    /// </summary>
    public static class InventoryEvents
    {
        // Inventory state changes
        public static event Action<InventorySnapshot> OnInventoryChanged;
        public static event Action OnInventoryOpened;
        public static event Action OnInventoryClosed;
        
        // Request operations (UI → Domain)
        public static event Action OnRequestOpenInventory;
        public static event Action OnRequestCloseInventory;
        public static event Action<ItemInstance> OnRequestTrashItem;
        public static event Action<ItemInstance, int> OnRequestDropStack; // item, amount
        public static event Action<ItemInstance, Container.Container> OnRequestTransferToContainer;
        public static event Action<ItemInstance, Container.Container> OnRequestTransferFromContainer;
        
        // Fire events
        public static void FireInventoryChanged(InventorySnapshot snapshot) => OnInventoryChanged?.Invoke(snapshot);
        public static void FireInventoryOpened() => OnInventoryOpened?.Invoke();
        public static void FireInventoryClosed() => OnInventoryClosed?.Invoke();
        public static void FireRequestOpenInventory() => OnRequestOpenInventory?.Invoke();
        public static void FireRequestCloseInventory() => OnRequestCloseInventory?.Invoke();
        public static void FireRequestTrashItem(ItemInstance item) => OnRequestTrashItem?.Invoke(item);
        public static void FireRequestDropStack(ItemInstance item, int amount) => OnRequestDropStack?.Invoke(item, amount);
        public static void FireRequestTransferToContainer(ItemInstance item, Container.Container container) => OnRequestTransferToContainer?.Invoke(item, container);
        public static void FireRequestTransferFromContainer(ItemInstance item, Container.Container container) => OnRequestTransferFromContainer?.Invoke(item, container);
    }
}
