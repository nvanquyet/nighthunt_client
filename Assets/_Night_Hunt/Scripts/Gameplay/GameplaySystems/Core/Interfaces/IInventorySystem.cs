using System;
using System.Collections.Generic;
using NightHunt.GameplaySystems.Core.Data;
using NightHunt.GameplaySystems.Inventory;

namespace NightHunt.GameplaySystems.Core.Interfaces
{
    /// <summary>
    /// Interface for inventory management system
    /// 
    /// RESPONSIBILITIES:
    /// - Provides access to inventory operations
    /// - Manages item storage, stacking, and organization
    /// - Calculates inventory weight
    /// - Implemented by InventorySystem (NetworkBehaviour)
    /// 
    /// NETWORK ARCHITECTURE:
    /// - Server-authoritative: All operations on server
    /// - Client receives updates via SyncList
    /// </summary>
    public interface IInventorySystem
    {
        #region Getters
        
        /// <summary>
        /// Get all items in inventory
        /// Returns read-only list
        /// </summary>
        IReadOnlyList<ItemInstance> GetAllItems();
        
        /// <summary>
        /// Get item at specific inventory index
        /// Returns null if no item at that index
        /// </summary>
        ItemInstance GetItemAt(int index);
        
        /// <summary>
        /// Get item by instance ID
        /// </summary>
        ItemInstance GetItemByInstanceID(string instanceID);
        
        /// <summary>
        /// Get total quantity of specific item definition
        /// Sums all stacks of same item
        /// </summary>
        int GetItemCount(string itemDefinitionID);
        
        /// <summary>
        /// Get all instances of specific item definition
        /// Useful for showing all stacks
        /// </summary>
        List<ItemInstance> GetItemsByDefinition(string itemDefinitionID);
        
        /// <summary>
        /// Check if has item with minimum quantity
        /// </summary>
        bool HasItem(string itemDefinitionID, int minQuantity = 1);
        
        /// <summary>
        /// Get highest inventory index currently used
        /// Returns -1 if inventory empty
        /// </summary>
        int GetMaxIndex();
        
        #endregion
        
        #region Add/Remove
        
        /// <summary>
        /// Add item to inventory
        /// Auto-stacks if possible (based on config)
        /// Server-side only, syncs to clients
        /// </summary>
        void AddItem(string itemDefinitionID, int quantity);

        /// <summary>
        /// Restore a previously serialised item (e.g. picked up from world) into inventory,
        /// preserving all runtime state: CurrentMagazine, CurrentResource, AttachedItems, etc.
        /// Does NOT try to stack — always creates a new inventory slot entry.
        /// Server-side only.
        /// </summary>
        void AddItemFromData(ItemInstanceData data);
        
        /// <summary>
        /// Remove specific item instance
        /// Can remove partial quantity from stack
        /// </summary>
        void RemoveItem(string instanceID, int quantity);
        
        /// <summary>
        /// Remove item from inventory slots only (for attachment).
        /// Keeps instance in ItemDatabase so ItemStatSystem can resolve modifiers.
        /// </summary>
        void RemoveItemFromSlotsOnly(string instanceID);

        /// <summary>
        /// Restore an item instance back to inventory slots.
        /// Reverse of RemoveItemFromSlotsOnly – used by AttachmentSystem on detach.
        /// The instance must already exist in ItemDatabase.
        /// Server-side only.
        /// </summary>
        void RestoreItemToSlots(ItemInstance item);

        /// <summary>
        /// Force-push the current in-memory state of an item to the SyncList so clients
        /// receive the update (e.g. after InventoryIndex changes due to equip/unequip).
        /// Also keeps the server-side _itemsByIndex cache in sync.
        /// Server-side only.
        /// </summary>
        void SyncItemState(string instanceID);

        /// <summary>
        /// Returns the first free (gap) inventory grid index.
        /// Used by EquipmentSystem/WeaponSystem to restore items to the correct slot
        /// on unequip rather than always appending beyond the highest used index.
        /// Server-side only.
        /// </summary>
        int GetNextFreeInventoryIndex();
        
        /// <summary>
        /// Remove items by definition ID
        /// Removes oldest stacks first
        /// </summary>
        void RemoveItemByDefinition(string itemDefinitionID, int quantity);
        
        /// <summary>
        /// Drop item to world (spawns dropped prefab)
        /// Removes from inventory
        /// </summary>
        void DropItem(string instanceID, int quantity);
        
        /// <summary>
        /// Clear entire inventory
        /// </summary>
        void ClearInventory();
        
        #endregion
        
        #region Move / Swap

        /// <summary>
        /// Move an item to a new grid index.
        /// If the target slot is occupied, the two items swap positions.
        /// If both items share the same definition and are stackable, they merge.
        /// Owning client routes to the server automatically.
        /// </summary>
        void MoveItem(string instanceID, int targetIndex);

        /// <summary>
        /// Swap the grid positions of two items by instance ID.
        /// Owning client routes to the server automatically.
        /// </summary>
        void SwapItems(string instanceID1, string instanceID2);

        /// <summary>
        /// Batch-reassign inventory indices for multiple items atomically.
        /// The dictionary maps instanceID → desired new inventory index.
        /// All assignments happen in one pass with no cascading swaps.
        /// Used by the sort feature to avoid the collision/cascade problem
        /// that occurs when sequential MoveItem calls trigger accidental swaps.
        /// Server-side / owning-client only.
        /// </summary>
        void BatchAssignIndices(Dictionary<string, int> assignments);

        /// <summary>
        /// Server-authoritative sort: reorders all inventory items by ItemType then DefinitionID,
        /// assigning contiguous grid indices 0, 1, 2 …
        /// Safe to call from the owning client — routes to the server via ServerRpc.
        /// </summary>
        void RequestSortByType();

        /// <summary>
        /// Server-authoritative compact: eliminates index gaps by packing all inventory items
        /// into contiguous indices starting at 0, preserving their current relative order.
        ///
        /// Gaps appear when items are equipped, dropped, or when stacks are merged.
        /// Example: [0]=Scope, [3]=Rifle, [5]=Helmet → [0]=Scope, [1]=Rifle, [2]=Helmet.
        ///
        /// Safe to call from the owning client — routes to the server via ServerRpc.
        /// </summary>
        void RequestCompact();

        #endregion
        
        #region Stack Operations

        /// <summary>Returns true when <paramref name="item1"/> and <paramref name="item2"/> can be merged into the same stack.</summary>
        bool CanStackWith(ItemInstance item1, ItemInstance item2);

        /// <summary>
        /// Merge all or part of <paramref name="sourceInstanceID"/> into <paramref name="targetInstanceID"/>.
        /// The source is removed when fully consumed.
        /// Owning client routes to the server automatically.
        /// </summary>
        void StackItems(string targetInstanceID, string sourceInstanceID);

        /// <summary>
        /// Split <paramref name="splitQuantity"/> units off <paramref name="instanceID"/> into a new stack
        /// placed at the first available grid index.
        /// Owning client routes to the server automatically.
        /// </summary>
        void SplitStack(string instanceID, int splitQuantity);

        #endregion
        
        #region Weight
        
        /// <summary>
        /// Calculate total weight of all items in inventory
        /// Used by PlayerStatSystem to update weight stat
        /// </summary>
        float CalculateTotalWeight();
        
        #endregion
        
        #region Events - Sync (All clients)
        
        /// <summary>
        /// Event fired when item added to inventory
        /// Parameters: (item)
        /// </summary>
        event Action<ItemInstance> OnItemAdded;
        
        /// <summary>
        /// Event fired when item removed
        /// Parameters: (item, quantity removed)
        /// </summary>
        event Action<ItemInstance, int> OnItemRemoved;
        
        /// <summary>
        /// Event fired when item moved
        /// Parameters: (item, oldIndex, newIndex)
        /// </summary>
        event Action<ItemInstance, int, int> OnItemMoved;
        
        /// <summary>
        /// Event fired when two items swapped
        /// Parameters: (item1, item2)
        /// </summary>
        event Action<ItemInstance, ItemInstance> OnItemsSwapped;
        
        /// <summary>
        /// Event fired when items stacked
        /// Parameters: (targetItem, sourceItem, amountStacked)
        /// </summary>
        event Action<ItemInstance, ItemInstance, int> OnItemsStacked;
        
        /// <summary>
        /// Event fired when inventory cleared
        /// </summary>
        event Action OnInventoryCleared;

        /// <summary>
        /// Event fired when a specific inventory slot is cleared because the item moved out
        /// of inventory (e.g., equipped to Equipment/Weapon slot or attached to another item).
        /// Subscribers should clear the corresponding UI slot.
        /// Parameters: (inventoryIndex)
        /// </summary>
        event Action<int> OnInventorySlotCleared;
        
        #endregion
    }
}
