using UnityEngine;
using UnityEngine.UI;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Config;
using NightHunt.Inventory.UI.Slots;
using NightHunt.Inventory.UI.Data;
using NightHunt.Inventory.UI;
using System.Collections.Generic;

namespace NightHunt.Inventory.UI.Panels
{
    /// <summary>
    /// Panel controller for inventory grid.
    /// Spawns InventorySlotUI[], subscribes to InventoryEvents, manages layout, updates from events.
    /// </summary>
    public class InventoryPanel : MonoBehaviour
    {
        [Header("References")] [SerializeField]
        private Transform slotContainer;

        [SerializeField] private InventorySlotUI inventorySlotPrefab;
        [SerializeField] private InventoryUIDataProvider dataProvider;
        [SerializeField] private MainInventoryUIManager uiManager;
        [SerializeField] private SlotLayoutConfig slotLayoutConfig;

        [Header("Dynamic Expansion Settings")]
        [Tooltip("Number of empty slots to spawn initially (overrides config if > 0, default: 40)")]
        [SerializeField]
        private int initialEmptySlots = 40;

        [Tooltip("Item count threshold - when exceeded, spawn 1 empty slot per item added")] [SerializeField]
        private int itemCountThreshold = 20;

        [Tooltip("Number of slots to add when drag-dropping near end")] [SerializeField]
        private int expandBySlots = 5;

        [Tooltip("Expand when remaining empty slots <= this threshold (for drag-drop)")] [SerializeField]
        private int expandThreshold = 3;

        [Header("Debug")] [SerializeField] private bool enableDebugLogs = false;

        // State
        private List<InventorySlotUI> slotUIs = new List<InventorySlotUI>();
        private int actualSlotCount = 0; // Actual inventory size (from server)
        private int totalSlotCount = 0; // Total UI slots (actual + empty)
        private int lastItemCount = 0; // Track last item count to detect new items

        // === Public API ===

        /// <summary>
        /// Initialize panel - spawn initial empty slots.
        /// </summary>
        public void Initialize(int actualSlotCount = 0)
        {
            this.actualSlotCount = actualSlotCount;

            // Get initial empty slots from config or use default
            int emptySlots =
                initialEmptySlots > 0
                    ? initialEmptySlots
                    : (slotLayoutConfig != null ? 40 : 40); // Default 40 empty slots

            // Spawn ONLY empty slots initially (40 empty slots)
            // Items will fill into these empty slots when added
            this.totalSlotCount = emptySlots;

            // Spawn ALL empty slots - UI shows empty slots ready for items
            SpawnSlots();

            // Mark all as empty slots initially
            MarkAllAsEmptySlots();

            // Refresh from data (will fill items into empty slots)
            RefreshFromData();

            Log($"Initialized with {emptySlots} empty slots spawned. Actual inventory size: {actualSlotCount}");
        }

        /// <summary>
        /// Refresh all slots from inventory data.
        /// </summary>
        public void RefreshFromData()
        {
            if (dataProvider == null)
                return;

            // Update actual slot count from data provider
            actualSlotCount = dataProvider.GetInventorySlotCount();

            // Refresh all slots - items will fill into empty slots
            for (int i = 0; i < slotUIs.Count; i++)
            {
                slotUIs[i].RefreshFromInventory();

                // Mark as empty if beyond actual inventory size OR if slot has no item
                if (i >= actualSlotCount)
                {
                    slotUIs[i].SetIsEmptySlot(true);
                }
                else
                {
                    // Check if slot actually has item
                    var item = dataProvider.GetInventoryItemAtSlot(i);
                    slotUIs[i].SetIsEmptySlot(item == null);
                }
            }
        }

        /// <summary>
        /// Refresh specific slot.
        /// </summary>
        public void RefreshSlot(int slotIndex)
        {
            Log($"[InventoryPanel] RefreshSlot called: slotIndex={slotIndex}, slotUIs.Count={slotUIs.Count}");

            if (slotIndex >= 0 && slotIndex < slotUIs.Count)
            {
                var slotUI = slotUIs[slotIndex];
                if (slotUI != null)
                {
                    Log($"[InventoryPanel] Refreshing slot {slotIndex}...");
                    slotUI.RefreshFromInventory();
                }
                else
                {
                    LogWarning($"[InventoryPanel] SlotUI at index {slotIndex} is null!");
                }
            }
            else
            {
                LogWarning($"[InventoryPanel] Invalid slotIndex: {slotIndex} (slotUIs.Count={slotUIs.Count})");
            }
        }

        // === Slot Management ===

        private void SpawnSlots()
        {
            // Clear existing slots
            ClearSlots();

            if (slotContainer == null || inventorySlotPrefab == null)
            {
                LogError("Slot container or prefab not assigned!");
                return;
            }

            // Spawn total slots (actual + empty)
            for (int i = 0; i < totalSlotCount; i++)
            {
                var slotUI = Instantiate(inventorySlotPrefab, slotContainer);

                if (slotUI != null)
                {
                    slotUI.SetSlotIndex(i);

                    // Slots will get slotLayoutConfig from base class or find from panel
                    slotUIs.Add(slotUI);

                    // Subscribe to slot events
                    slotUI.OnSlotClicked += OnSlotClicked;
                    slotUI.OnSlotDoubleClicked += OnSlotDoubleClicked;

                    slotUI.gameObject.SetActive(true);
                }
            }

            Log($"Spawned {slotUIs.Count} inventory slots (total)");
        }

        /// <summary>
        /// Mark all slots as empty initially.
        /// </summary>
        private void MarkAllAsEmptySlots()
        {
            for (int i = 0; i < slotUIs.Count; i++)
            {
                slotUIs[i].SetIsEmptySlot(true);
            }
        }

        /// <summary>
        /// Mark slots beyond actual inventory size as empty slots.
        /// </summary>
        private void MarkEmptySlots()
        {
            for (int i = 0; i < slotUIs.Count; i++)
            {
                if (i >= actualSlotCount)
                {
                    slotUIs[i].SetIsEmptySlot(true);
                }
                else
                {
                    slotUIs[i].SetIsEmptySlot(false);
                }
            }
        }

        /// <summary>
        /// Expand slots by adding more empty slots.
        /// </summary>
        public void ExpandSlots(int additionalSlots)
        {
            if (additionalSlots <= 0)
                return;

            int startIndex = slotUIs.Count;
            totalSlotCount += additionalSlots;

            // Spawn new slots
            for (int i = 0; i < additionalSlots; i++)
            {
                var slotUI = Instantiate(inventorySlotPrefab, slotContainer);

                if (slotUI != null)
                {
                    int slotIndex = startIndex + i;
                    slotUI.SetSlotIndex(slotIndex);
                    slotUI.SetIsEmptySlot(true); // Mark as empty slot

                    slotUIs.Add(slotUI);

                    // Subscribe to slot events
                    slotUI.OnSlotClicked += OnSlotClicked;
                    slotUI.OnSlotDoubleClicked += OnSlotDoubleClicked;

                    slotUI.gameObject.SetActive(true);
                }
            }

            Log($"Expanded slots by {additionalSlots}. Total slots: {totalSlotCount}");
        }

        /// <summary>
        /// Get index of last filled slot (with item).
        /// </summary>
        public int GetLastFilledSlot()
        {
            if (dataProvider == null)
                return -1;

            // Check from end backwards
            for (int i = actualSlotCount - 1; i >= 0; i--)
            {
                var item = dataProvider.GetInventoryItemAtSlot(i);
                if (item != null)
                {
                    return i;
                }
            }

            return -1; // No filled slots
        }

        /// <summary>
        /// Check if expansion is needed and expand if necessary.
        /// Called after item is added or drag-dropped.
        /// </summary>
        public void CheckAndExpandIfNeeded(int targetIndex)
        {
            if (dataProvider == null)
                return;

            // Get current item count
            int currentItemCount = GetCurrentItemCount();

            // Logic 1: If item count > threshold AND item count increased, spawn 1 empty slot
            if (currentItemCount > itemCountThreshold && currentItemCount > lastItemCount)
            {
                // Item count increased - spawn 1 empty slot for this new item
                Log(
                    $"Item count ({currentItemCount}) > threshold ({itemCountThreshold}) and increased. Spawning 1 empty slot");
                ExpandSlots(1);
                lastItemCount = currentItemCount;
            }
            else if (currentItemCount <= itemCountThreshold)
            {
                // Update last item count
                lastItemCount = currentItemCount;
            }

            // Logic 2: If drag-dropping near end, expand by expandBySlots
            int remainingEmptySlots = totalSlotCount - (targetIndex + 1);
            if (remainingEmptySlots <= expandThreshold)
            {
                Log(
                    $"Drag-drop near end: remaining empty slots ({remainingEmptySlots}) <= threshold ({expandThreshold}). Expanding by {expandBySlots}...");
                ExpandSlots(expandBySlots);
            }
        }

        /// <summary>
        /// Get current item count in inventory.
        /// </summary>
        private int GetCurrentItemCount()
        {
            if (dataProvider == null)
                return 0;

            int count = 0;
            for (int i = 0; i < actualSlotCount; i++)
            {
                var item = dataProvider.GetInventoryItemAtSlot(i);
                if (item != null)
                    count++;
            }

            return count;
        }

        private void ClearSlots()
        {
            foreach (var slot in slotUIs)
            {
                if (slot != null)
                {
                    slot.OnSlotClicked -= OnSlotClicked;
                    slot.OnSlotDoubleClicked -= OnSlotDoubleClicked;
                    Destroy(slot.gameObject);
                }
            }

            slotUIs.Clear();
        }

        // === Event Handlers ===

        private void OnSlotClicked(ItemSlotUI slot)
        {
            if (slot is InventorySlotUI inventorySlot)
            {
                var item = inventorySlot.GetItem();
                if (uiManager != null && item != null)
                {
                    uiManager.SelectInventoryItem(item);
                }
            }
        }

        private void OnSlotDoubleClicked(ItemSlotUI slot)
        {
            // Double click handling is done in InventorySlotUI
        }

        private void OnInventoryItemAdded(ItemInstance item, int slotIndex)
        {
            Log(
                $"[InventoryPanel] OnInventoryItemAdded event received: {item?.Definition?.DisplayName ?? "null"} at slot {slotIndex}");

            RefreshSlot(slotIndex);

            // Update actual slot count
            if (dataProvider != null)
            {
                actualSlotCount = dataProvider.GetInventorySlotCount();
            }

            // Check if expansion is needed (spawn 1 empty slot if item count > threshold)
            CheckAndExpandIfNeeded(slotIndex);

            Log($"Item added at slot {slotIndex}: {item.Definition.DisplayName}");
        }

        private void OnInventoryItemRemoved(ItemInstance item, int slotIndex)
        {
            RefreshSlot(slotIndex);
            Log($"Item removed from slot {slotIndex}");
        }

        private void OnInventoryItemMoved(ItemInstance item, int fromSlot, int toSlot)
        {
            RefreshSlot(fromSlot);
            RefreshSlot(toSlot);
            Log($"Item moved: {fromSlot} → {toSlot}");
        }

        private void OnInventoryItemsSwapped(ItemInstance itemA, int slotA, ItemInstance itemB, int slotB)
        {
            RefreshSlot(slotA);
            RefreshSlot(slotB);
            Log($"Items swapped: {slotA} ↔ {slotB}");
        }

        // === Event Subscription ===

        void Start()
        {
            if (dataProvider == null)
                dataProvider = FindObjectOfType<InventoryUIDataProvider>();

            if (uiManager == null)
                uiManager = FindObjectOfType<MainInventoryUIManager>();

            // Subscribe to events
            InventoryEvents.OnItemAdded += OnInventoryItemAdded;
            InventoryEvents.OnItemRemoved += OnInventoryItemRemoved;
            InventoryEvents.OnItemMoved += OnInventoryItemMoved;
            InventoryEvents.OnItemsSwapped += OnInventoryItemsSwapped;

            // Get actual inventory size from data provider (if available)
            int actualSlotCount = 0;
            if (dataProvider != null)
            {
                actualSlotCount = dataProvider.GetInventorySlotCount();
            }

            // Initialize with actual slot count (will spawn 40 empty slots initially)
            Initialize(actualSlotCount);

            // Initialize last item count
            if (dataProvider != null)
            {
                lastItemCount = GetCurrentItemCount();
            }
        }

        void OnDestroy()
        {
            // Unsubscribe
            InventoryEvents.OnItemAdded -= OnInventoryItemAdded;
            InventoryEvents.OnItemRemoved -= OnInventoryItemRemoved;
            InventoryEvents.OnItemMoved -= OnInventoryItemMoved;
            InventoryEvents.OnItemsSwapped -= OnInventoryItemsSwapped;
        }

        // === Lifecycle ===

        void Awake()
        {
            if (slotContainer == null)
                slotContainer = transform;
        }

        // === Debug ===

        void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[InventoryPanel] {message}");
        }

        void LogError(string message)
        {
            if (enableDebugLogs)
                Debug.LogError($"[InventoryPanel] {message}");
        }

        void LogWarning(string message)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[InventoryPanel] {message}");
        }
    }
}