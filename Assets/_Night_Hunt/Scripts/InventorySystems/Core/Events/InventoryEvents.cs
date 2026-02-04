using System;
using NightHunt.Inventory.Core.Data;

namespace NightHunt.Inventory.Core.Events
{
    /// <summary>
    /// Central event hub for inventory-related events.
    /// Enables communication between Domain and UI layers.
    /// </summary>
    public static class InventoryEvents
    {
        // === UI Request Events (UI → Domain) ===
        
        /// <summary>Fired when user requests to open inventory UI</summary>
        public static event Action OnRequestOpenInventory;
        
        /// <summary>Fired when user requests to close inventory UI</summary>
        public static event Action OnRequestCloseInventory;
        
        /// <summary>Fired when user requests to add item to inventory</summary>
        public static event Action<ItemInstance> OnRequestAddItem;
        
        /// <summary>Fired when user requests to remove item from inventory</summary>
        public static event Action<string> OnRequestRemoveItem; // instanceId
        
        /// <summary>Fired when user requests to drop item stack</summary>
        public static event Action<ItemInstance, int> OnRequestDropStack; // item, amount
        
        /// <summary>Fired when user requests to trash/destroy item</summary>
        public static event Action<ItemInstance> OnRequestTrashItem;
        
        /// <summary>Fired when user requests to transfer item to container</summary>
        public static event Action<ItemInstance, object> OnRequestTransferToContainer; // item, container
        
        /// <summary>Fired when user requests to transfer item from container</summary>
        public static event Action<ItemInstance, object> OnRequestTransferFromContainer;
        
        /// <summary>Fired when user requests to sort inventory</summary>
        public static event Action OnRequestSortInventory;
        
        /// <summary>Fired when user requests to auto-stack items</summary>
        public static event Action OnRequestAutoStack;
        
        // === State Change Events (Domain → UI) ===
        
        /// <summary>Fired when inventory is opened</summary>
        public static event Action OnInventoryOpened;
        
        /// <summary>Fired when inventory is closed</summary>
        public static event Action OnInventoryClosed;
        
        /// <summary>Fired when inventory contents change</summary>
        public static event Action<InventoryData> OnInventoryChanged;
        
        /// <summary>Fired when item is added successfully</summary>
        public static event Action<ItemInstance, int> OnItemAdded; // item, slotIndex
        
        /// <summary>Fired when item is removed successfully</summary>
        public static event Action<string, int> OnItemRemoved; // instanceId, slotIndex
        
        /// <summary>Fired when item stack size changes</summary>
        public static event Action<ItemInstance> OnStackSizeChanged;
        
        /// <summary>Fired when inventory is full</summary>
        public static event Action OnInventoryFull;
        
        // === Invocation Methods ===
        
        public static void InvokeRequestOpenInventory() => OnRequestOpenInventory?.Invoke();
        public static void InvokeRequestCloseInventory() => OnRequestCloseInventory?.Invoke();
        public static void InvokeRequestAddItem(ItemInstance item) => OnRequestAddItem?.Invoke(item);
        public static void InvokeRequestRemoveItem(string instanceId) => OnRequestRemoveItem?.Invoke(instanceId);
        public static void InvokeRequestDropStack(ItemInstance item, int amount) => OnRequestDropStack?.Invoke(item, amount);
        public static void InvokeRequestTrashItem(ItemInstance item) => OnRequestTrashItem?.Invoke(item);
        public static void InvokeRequestSortInventory() => OnRequestSortInventory?.Invoke();
        public static void InvokeRequestAutoStack() => OnRequestAutoStack?.Invoke();
        
        public static void InvokeInventoryOpened() => OnInventoryOpened?.Invoke();
        public static void InvokeInventoryClosed() => OnInventoryClosed?.Invoke();
        public static void InvokeInventoryChanged(InventoryData data) => OnInventoryChanged?.Invoke(data);
        public static void InvokeItemAdded(ItemInstance item, int slotIndex) => OnItemAdded?.Invoke(item, slotIndex);
        public static void InvokeItemRemoved(string instanceId, int slotIndex) => OnItemRemoved?.Invoke(instanceId, slotIndex);
        public static void InvokeStackSizeChanged(ItemInstance item) => OnStackSizeChanged?.Invoke(item);
        public static void InvokeInventoryFull() => OnInventoryFull?.Invoke();
    }
}