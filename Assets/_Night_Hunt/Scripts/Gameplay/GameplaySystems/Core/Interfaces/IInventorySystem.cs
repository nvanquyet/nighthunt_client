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
        
        #region Move/Swap
        
        /// <summary>
        /// Move item to new inventory index
        /// Handles swapping if target occupied
        /// Auto-stacks if same item (based on config)
        /// </summary>
        void MoveItem(string instanceID, int targetIndex);
        
        /// <summary>
        /// Swap two items' positions
        /// </summary>
        void SwapItems(string instanceID1, string instanceID2);

        /// <summary>
        /// Batch-reassign inventory indices for multiple items atomically.
        /// The dictionary maps instanceID → desired new inventory index.
        /// All assignments happen server-side in one pass with no cascading swaps.
        /// Used by the sort feature to avoid the collision/cascade problem that occurs
        /// when sequential MoveItem calls trigger accidental swaps.
        /// Server-side only.
        /// </summary>
        void BatchAssignIndices(Dictionary<string, int> assignments);
        
        #endregion
        
        #region Stack Operations
        
        /// <summary>
        /// Check if two items can stack together
        /// </summary>
        bool CanStackWith(ItemInstance item1, ItemInstance item2);
        
        /// <summary>
        /// Stack source item into target item
        /// Removes source if fully stacked
        /// </summary>
        void StackItems(string targetInstanceID, string sourceInstanceID);
        
        /// <summary>
        /// Split stack into two stacks
        /// Creates new item instance
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
