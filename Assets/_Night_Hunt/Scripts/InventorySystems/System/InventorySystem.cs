using _Night_Hunt.Scripts.InventorySystems.Core.Interfaces;
using UnityEngine;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Enums;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Config;
using NightHunt.Inventory.Stats;

namespace NightHunt.Inventory.Systems
{
    /// <summary>
    /// Main inventory system.
    /// Manages player's main inventory (20-50 slots depending on backpack).
    /// Implements IInventorySystem interface.
    /// </summary>
    public class InventorySystem : MonoBehaviour, IInventorySystem
    {
        [Header("Configuration")]
        [SerializeField] private InventoryConfig config;
        [SerializeField] private SlotLayoutConfig slotLayout;

        [Header("References")]
        [SerializeField] private CharacterStats characterStats;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;

        // Data
        private SlotContainerData containerData;

        // === Lifecycle ===

        void Awake()
        {
            Initialize();
        }

        void Initialize()
        {
            if (config == null || slotLayout == null)
            {
                LogError("InventoryConfig or SlotLayoutConfig not assigned!");
                return;
            }

            // Create container
            int slotCount = slotLayout.MainInventorySlotCount;
            containerData = new SlotContainerData("MainInventory", slotCount, SlotLocationType.Inventory);

            Log($"Initialized inventory with {slotCount} slots");
        }

        // === IInventorySystem Implementation ===

        #region Query

        public ItemInstance GetItemAtSlot(int slotIndex)
        {
            return containerData.GetItemAtSlot(slotIndex);
        }

        public int GetSlotCount()
        {
            return containerData.GetSlotCount();
        }

        public int GetEmptySlotCount()
        {
            return containerData.GetEmptySlotCount();
        }

        public float GetCurrentWeight()
        {
            return containerData.GetTotalWeight();
        }

        public float GetMaxWeight()
        {
            return characterStats != null
                ? characterStats.GetWeightCapacity()
                : config.DefaultWeightCapacity;
        }

        public bool IsOverweight()
        {
            if (!config.EnableWeightSystem)
                return false;

            return GetCurrentWeight() > GetMaxWeight();
        }

        #endregion

        #region Add/Remove

        public OperationResult AddItem(ItemInstance item, out int assignedSlot)
        {
            assignedSlot = -1;

            // Validate
            if (item == null || item.Definition == null)
            {
                LogError($"AddItem failed: item or Definition is null. Item: {item?.GetType().Name}, Definition: {item?.Definition?.GetType().Name}");
                return OperationResult.ItemNotFound;
            }

            if (!containerData.CanAcceptItem(item))
                return OperationResult.InvalidItemType;

            // Check weight - validate existing items first
            if (config.EnableWeightSystem)
            {
                // Check if any existing items have null definitions
                var allItems = containerData.GetAllItems();
                foreach (var existingItem in allItems)
                {
                    if (existingItem?.Definition == null)
                    {
                        LogError($"Found item with null Definition in inventory before adding new item. InstanceId: {existingItem?.InstanceId}");
                    }
                }

                float newWeight = GetCurrentWeight() + item.GetTotalWeight();
                if (newWeight > GetMaxWeight())
                {
                    if (!config.AllowPickupWhenOverweight)
                    {
                        InventoryEvents.InvokeOperationFailed(OperationResult.ExceedsWeightLimit, "Cannot pickup - overweight");
                        return OperationResult.ExceedsWeightLimit;
                    }
                }
            }
            
            // Try auto-merge for stackable items
            if (config.AutoMergeStacks && item.Definition.IsStackable)
            {
                var mergeResult = TryAutoMerge(item, out assignedSlot);
                if (mergeResult == OperationResult.Success)
                    return OperationResult.Success;
            }

            // Add to first empty slot
            if (containerData.TryAddItem(item, out assignedSlot))
            {
                item.IsEquipped = false;
                item.EquippedLocation = SlotLocationType.Inventory;

                InventoryEvents.InvokeItemAdded(item, assignedSlot);
                UpdateWeight();

                Log($"Added item {item.Definition.DisplayName} to slot {assignedSlot}");
                return OperationResult.Success;
            }

            InventoryEvents.InvokeOperationFailed(OperationResult.InventoryFull, "Inventory is full");
            return OperationResult.InventoryFull;
        }

        public OperationResult AddItemAtSlot(ItemInstance item, int slotIndex)
        {
            // Validate
            if (item == null || item.Definition == null)
                return OperationResult.ItemNotFound;

            if (!containerData.CanAcceptItem(item))
                return OperationResult.InvalidItemType;

            if (slotIndex < 0 || slotIndex >= GetSlotCount())
                return OperationResult.InvalidSlotIndex;

            if (!containerData.IsSlotEmpty(slotIndex))
                return OperationResult.SlotOccupied;

            // Check weight
            if (config.EnableWeightSystem)
            {
                float newWeight = GetCurrentWeight() + item.GetTotalWeight();
                if (newWeight > GetMaxWeight() && !config.AllowPickupWhenOverweight)
                {
                    return OperationResult.ExceedsWeightLimit;
                }
            }

            // Add
            if (containerData.SetItemAtSlot(slotIndex, item))
            {
                item.IsEquipped = false;
                item.EquippedLocation = SlotLocationType.Inventory;

                InventoryEvents.InvokeItemAdded(item, slotIndex);
                UpdateWeight();

                Log($"Added item {item.Definition.DisplayName} to slot {slotIndex}");
                return OperationResult.Success;
            }

            return OperationResult.UnknownError;
        }

        public OperationResult RemoveItem(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
                return OperationResult.ItemNotFound;

            if (containerData.RemoveItem(instanceId, out int slotIndex))
            {
                var item = containerData.GetItemAtSlot(slotIndex);

                InventoryEvents.InvokeItemRemoved(item, slotIndex);
                UpdateWeight();

                Log($"Removed item from slot {slotIndex}");
                return OperationResult.Success;
            }

            return OperationResult.ItemNotFound;
        }

        public OperationResult RemoveItemAtSlot(int slotIndex, out ItemInstance removedItem)
        {
            removedItem = null;

            if (slotIndex < 0 || slotIndex >= GetSlotCount())
                return OperationResult.InvalidSlotIndex;

            removedItem = containerData.RemoveItemAtSlot(slotIndex);

            if (removedItem != null)
            {
                InventoryEvents.InvokeItemRemoved(removedItem, slotIndex);
                UpdateWeight();

                Log($"Removed item {removedItem.Definition.DisplayName} from slot {slotIndex}");
                return OperationResult.Success;
            }

            return OperationResult.ItemNotFound;
        }

        #endregion

        #region Move/Swap

        public OperationResult MoveItem(int fromSlot, int toSlot)
        {
            if (fromSlot < 0 || fromSlot >= GetSlotCount() || toSlot < 0 || toSlot >= GetSlotCount())
                return OperationResult.InvalidSlotIndex;

            var item = containerData.GetItemAtSlot(fromSlot);
            if (item == null)
                return OperationResult.ItemNotFound;

            if (!containerData.IsSlotEmpty(toSlot))
                return OperationResult.SlotOccupied;

            // Move
            containerData.SetItemAtSlot(toSlot, item);
            containerData.SetItemAtSlot(fromSlot, null);

            InventoryEvents.InvokeItemMoved(item, fromSlot, toSlot);

            Log($"Moved item from slot {fromSlot} to {toSlot}");
            return OperationResult.Success;
        }

        public OperationResult SwapItems(int slotA, int slotB)
        {
            if (slotA < 0 || slotA >= GetSlotCount() || slotB < 0 || slotB >= GetSlotCount())
                return OperationResult.InvalidSlotIndex;

            var itemA = containerData.GetItemAtSlot(slotA);
            var itemB = containerData.GetItemAtSlot(slotB);

            if (itemA == null && itemB == null)
                return OperationResult.ItemNotFound;

            // Swap
            containerData.SwapSlots(slotA, slotB);

            InventoryEvents.InvokeItemsSwapped(itemA, slotA, itemB, slotB);

            Log($"Swapped items between slot {slotA} and {slotB}");
            return OperationResult.Success;
        }

        #endregion

        #region Stack Operations

        public OperationResult SplitStack(int slotIndex, int amount, out ItemInstance splitItem)
        {
            splitItem = null;

            if (slotIndex < 0 || slotIndex >= GetSlotCount())
                return OperationResult.InvalidSlotIndex;

            var item = containerData.GetItemAtSlot(slotIndex);
            if (item == null)
                return OperationResult.ItemNotFound;

            if (!item.Definition.IsStackable)
                return OperationResult.NotStackable;

            if (amount <= 0 || amount >= item.StackSize)
                return OperationResult.StackLimitExceeded;

            // Find empty slot for split
            int emptySlot = containerData.FindFirstEmptySlot();
            if (emptySlot == -1)
                return OperationResult.InventoryFull;

            // Create split item
            splitItem = item.Clone(ItemInstanceFactory.CreateInstance(item.Definition).InstanceId);
            splitItem.StackSize = amount;
            item.StackSize -= amount;

            // Add to empty slot
            containerData.SetItemAtSlot(emptySlot, splitItem);

            InventoryEvents.InvokeStackSplit(item, slotIndex, splitItem, emptySlot, amount);

            Log($"Split stack: {amount} items from slot {slotIndex} to slot {emptySlot}");
            return OperationResult.Success;
        }

        public OperationResult MergeStacks(int sourceSlot, int targetSlot)
        {
            if (sourceSlot < 0 || sourceSlot >= GetSlotCount() || targetSlot < 0 || targetSlot >= GetSlotCount())
                return OperationResult.InvalidSlotIndex;

            var sourceItem = containerData.GetItemAtSlot(sourceSlot);
            var targetItem = containerData.GetItemAtSlot(targetSlot);

            if (sourceItem == null || targetItem == null)
                return OperationResult.ItemNotFound;

            if (!sourceItem.Definition.IsStackable || !targetItem.Definition.IsStackable)
                return OperationResult.NotStackable;

            if (sourceItem.Definition.ItemId != targetItem.Definition.ItemId)
                return OperationResult.IncompatibleSlot;

            int maxStack = targetItem.Definition.MaxStackSize;
            int availableSpace = maxStack - targetItem.StackSize;

            if (availableSpace <= 0)
                return OperationResult.StackLimitExceeded;

            int mergeAmount = Mathf.Min(sourceItem.StackSize, availableSpace);

            targetItem.StackSize += mergeAmount;
            sourceItem.StackSize -= mergeAmount;

            // Remove source if empty
            if (sourceItem.StackSize <= 0)
            {
                containerData.SetItemAtSlot(sourceSlot, null);
            }

            InventoryEvents.InvokeStacksMerged(sourceItem, sourceSlot, targetItem, targetSlot, mergeAmount);

            Log($"Merged {mergeAmount} items from slot {sourceSlot} to {targetSlot}");
            return OperationResult.Success;
        }

        #endregion

        #region Utility

        public bool HasItem(string instanceId)
        {
            return containerData.HasItem(instanceId);
        }

        public ItemInstance FindItem(string instanceId)
        {
            return containerData.FindItem(instanceId);
        }

        public void Clear()
        {
            containerData.Clear();
            InventoryEvents.InvokeInventoryCleared();
            UpdateWeight();

            Log("Inventory cleared");
        }

        #endregion

        // === Private Helpers ===

        private OperationResult TryAutoMerge(ItemInstance item, out int mergedSlot)
        {
            mergedSlot = -1;

            // Find existing stack with same ItemId
            var slots = containerData.GetAllSlots();
            for (int i = 0; i < slots.Count; i++)
            {
                var existingItem = slots[i].Item;
                if (existingItem != null &&
                    existingItem.Definition.ItemId == item.Definition.ItemId &&
                    existingItem.StackSize < existingItem.Definition.MaxStackSize)
                {
                    int availableSpace = existingItem.Definition.MaxStackSize - existingItem.StackSize;
                    int mergeAmount = Mathf.Min(item.StackSize, availableSpace);

                    existingItem.StackSize += mergeAmount;
                    item.StackSize -= mergeAmount;

                    mergedSlot = i;

                    InventoryEvents.InvokeStacksMerged(item, -1, existingItem, i, mergeAmount);
                    UpdateWeight();

                    // If entire stack was merged, return success
                    if (item.StackSize <= 0)
                    {
                        Log($"Auto-merged full stack into slot {i}");
                        return OperationResult.Success;
                    }
                }
            }

            return OperationResult.InventoryFull; // Partial merge or no merge
        }

        private void UpdateWeight()
        {
            if (!config.EnableWeightSystem)
                return;

            float currentWeight = GetCurrentWeight();
            float maxWeight = GetMaxWeight();
            bool isOverweight = currentWeight > maxWeight;

            InventoryEvents.InvokeWeightChanged(currentWeight, maxWeight, isOverweight);

            // Apply overweight penalty to character stats (handled by CharacterStats)
            // TODO: Trigger movement speed penalty
        }

        // === Public API - Additional ===

        /// <summary>
        /// Expand inventory size (called when backpack is equipped).
        /// </summary>
        public void ExpandInventory(int additionalSlots)
        {
            if (!slotLayout.AllowDynamicInventoryExpansion)
                return;

            int newSize = Mathf.Min(
                GetSlotCount() + additionalSlots,
                slotLayout.MaxInventorySlotCount
            );

            containerData.ExpandCapacity(additionalSlots);

            InventoryEvents.InvokeInventorySizeChanged(newSize);

            Log($"Expanded inventory to {newSize} slots");
        }

        /// <summary>
        /// Get container data for serialization.
        /// </summary>
        public SlotContainerData GetContainerData() => containerData;

        // === Debug ===

        void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[InventorySystem] {message}");
        }

        void LogError(string message)
        {
            Debug.LogError($"[InventorySystem] {message}");
        }
    }
}