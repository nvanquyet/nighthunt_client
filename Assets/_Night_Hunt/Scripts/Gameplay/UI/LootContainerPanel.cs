using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using NightHunt.Gameplay.Inventory;
using NightHunt.Gameplay.Inventory.Events;
using NightHunt.InteractionSystem.Loot;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Core.Interfaces;
using NightHunt.InteractionSystem.Core.Abstractions;
using NightHunt.InteractionSystem.Events;
using NightHunt.InteractionSystem.Interaction.Implementations;
using FishNet.Object.Synchronizing;
using FishNet;

namespace NightHunt.Gameplay.UI
{
    /// <summary>
    /// Loot container panel - displayed on the right in Loot Mode
    /// Shows items from container (chest, player corpse, etc.)
    /// Supports drag & drop between inventory and container
    /// </summary>
    public class LootContainerPanel : MonoBehaviour
    {
        [Header("Container Info")]
        [SerializeField] private TextMeshProUGUI containerTitleText;

        [Header("Loot Grid")]
        [SerializeField] private Transform lootGridParent;
        [SerializeField] private GameObject lootSlotPrefab;
        [SerializeField] private GridLayoutGroup gridLayout;
        [SerializeField] private int minSlots = 4; // Minimum number of slots to always display (even if container is empty)
        [SerializeField] private int extraEmptySlots = 6; // Additional empty slots after items (4-8 range for better UX)

        [Header("Panel Root")]
        [SerializeField] private GameObject panelRoot; // Panel UI GameObject (separate from script GameObject)

        private InventoryPanel inventoryPanel;
        private List<ItemCell> lootSlots = new List<ItemCell>();
        private NetworkLootContainer currentContainer;
        private bool isInitialized = false;
        private bool isRaycastingContainer = false; // Track if currently raycasting a container
        private bool isUIPanelVisible = false; // Track if UI panel is currently visible

        /// <summary>
        /// Initialize loot container panel
        /// </summary>
        public void Initialize(InventoryPanel panel)
        {
            // UI components should not run on server (headless server compatibility)
            if (InstanceFinder.IsServer)
            {
                Debug.Log($"[LootContainerPanel] Running on server - disabling UI component (headless server compatibility)");
                enabled = false;
                return;
            }
            
            inventoryPanel = panel;
            isInitialized = true;
            // Hide initially (set flag to false, don't set to true first)
            isUIPanelVisible = panelRoot != null ? panelRoot.activeSelf : false;
            Hide();
            
            // Subscribe to container opened/closed events
            InventoryEvents.OnLootContainerOpened += HandleContainerOpened;
            InventoryEvents.OnLootContainerClosed += HandleContainerClosed;
            
            // Subscribe to interaction events to track raycast (replaces ContainerDistanceManager)
            InteractionEvents.OnInteractTargetChanged += HandleInteractTargetChanged;
            InteractionEvents.OnInteractTargetLost += HandleInteractTargetLost;
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            InventoryEvents.OnLootContainerOpened -= HandleContainerOpened;
            InventoryEvents.OnLootContainerClosed -= HandleContainerClosed;
            InteractionEvents.OnInteractTargetChanged -= HandleInteractTargetChanged;
            InteractionEvents.OnInteractTargetLost -= HandleInteractTargetLost;
            
            // Unsubscribe from container if loaded
            if (currentContainer != null)
            {
                UnsubscribeFromContainer();
            }
        }

        /// <summary>
        /// Handle container opened event - track opened container
        /// </summary>
        private void HandleContainerOpened(ILootContainer container)
        {
            if (container == null)
            {
                Debug.LogWarning("[LootContainerPanel] HandleContainerOpened: container is null!");
                return;
            }

            // REMOVED: No longer cache openedContainer
            // UI will ONLY show when player is actively raycasting a container
            // This prevents showing stale container data when player moves away
            Debug.Log($"[LootContainerPanel] HandleContainerOpened - Container opened: {container?.GetType().Name ?? "null"} (UI will show only when raycasting)");
        }

        /// <summary>
        /// Check if container is opened and load it (called when inventory opens)
        /// Also checks if we're currently raycasting a container by checking InteractionDetector
        /// </summary>
        public void CheckAndLoadOpenedContainer()
        {
            Debug.Log($"[LootContainerPanel] CheckAndLoadOpenedContainer - isRaycastingContainer: {isRaycastingContainer}, currentContainer: {currentContainer?.name ?? "null"}");
            
            // REMOVED: No longer use cache - only show when actively raycasting
            // Check if we're currently raycasting a container (re-check from InteractionDetector)
            NightHunt.InteractionSystem.Interaction.Detection.InteractionDetector interactionDetector = null;
            if (inventoryPanel != null)
            {
                interactionDetector = inventoryPanel.GetComponentInParent<NightHunt.InteractionSystem.Interaction.Detection.InteractionDetector>();
                if (interactionDetector == null)
                {
                    interactionDetector = FindFirstObjectByType<NightHunt.InteractionSystem.Interaction.Detection.InteractionDetector>();
                }
            }
            
            if (interactionDetector != null)
            {
                var currentTarget = interactionDetector.GetCurrentTarget();
                Debug.Log($"[LootContainerPanel] CheckAndLoadOpenedContainer - InteractionDetector found, currentTarget: {currentTarget?.GetType().Name ?? "null"}");
                
                if (currentTarget is ContainerInteractable || currentTarget is NightHunt.InteractionSystem.Interaction.Implementations.CorpseInteractable)
                {
                    var containerGameObject = (currentTarget as MonoBehaviour)?.gameObject;
                    if (containerGameObject != null)
                    {
                        var raycastContainer = containerGameObject.GetComponent<NetworkLootContainer>();
                        if (raycastContainer != null)
                        {
                            bool isCorpse = currentTarget is NightHunt.InteractionSystem.Interaction.Implementations.CorpseInteractable;
                            bool isOpened = isCorpse || raycastContainer.IsOpened();
                            
                            Debug.Log($"[LootContainerPanel] CheckAndLoadOpenedContainer - Container found: {raycastContainer.name}, isCorpse: {isCorpse}, isOpened: {isOpened}");
                            
                            if (isOpened)
                            {
                                Debug.Log($"[LootContainerPanel] CheckAndLoadOpenedContainer - Loading raycast container: {raycastContainer.name}");
                                isRaycastingContainer = true;
                                
                                if (currentContainer != raycastContainer)
                                {
                                    LoadContainer(raycastContainer);
                                }
                                Show();
                                if (inventoryPanel != null)
                                {
                                    inventoryPanel.SetMode(InventoryMode.Loot);
                                }
                                return;
                            }
                        }
                    }
                }
            }
            
            // Not raycasting a container - hide panel
            if (!isRaycastingContainer)
            {
                Debug.Log($"[LootContainerPanel] CheckAndLoadOpenedContainer - Not raycasting container, hiding panel");
                if (isUIPanelVisible)
                {
                    Hide();
                }
                return;
            }
            
            // If we reach here, we're not raycasting or container is not valid - hide panel
            if (isUIPanelVisible)
            {
                Debug.Log($"[LootContainerPanel] CheckAndLoadOpenedContainer - No valid container to show, hiding panel");
                Hide();
            }
        }

        /// <summary>
        /// Load container and display items
        /// </summary>
        public void LoadContainer(NetworkLootContainer container)
        {
            if (container == null)
            {
                Debug.LogWarning("[LootContainerPanel] LoadContainer: container is null");
                return;
            }

            // Unsubscribe from previous container
            if (currentContainer != null)
            {
                UnsubscribeFromContainer();
            }

            currentContainer = container;
            SetContainerTitle(container.GetDisplayName());

            // Subscribe to container events
            SubscribeToContainer();

            // Display items - use coroutine to wait for SyncList to sync if needed
            StartCoroutine(RefreshLootGridWithRetry());
        }

        /// <summary>
        /// Subscribe to container events
        /// </summary>
        private void SubscribeToContainer()
        {
            if (currentContainer == null)
                return;

            // Subscribe to container item change events
            InventoryEvents.OnLootContainerItemsChanged += HandleContainerItemsChanged;
        }

        /// <summary>
        /// Unsubscribe from container events
        /// </summary>
        private void UnsubscribeFromContainer()
        {
            if (currentContainer == null)
                return;

            // Unsubscribe from events
            InventoryEvents.OnLootContainerItemsChanged -= HandleContainerItemsChanged;
        }

        /// <summary>
        /// Handle container items changed event - refresh UI
        /// NOTE: This should NOT close the UI, only refresh the grid
        /// </summary>
        private void HandleContainerItemsChanged(ILootContainer container)
        {
            // Only refresh if this is the current container we're displaying
            if (container == currentContainer && isRaycastingContainer)
            {
                RefreshLootGrid();
            }
        }

        /// <summary>
        /// Display items in loot grid
        /// Always spawn at least minSlots slots (even if container is empty)
        /// Spawn additional slots if there are more items
        /// </summary>
        private void DisplayItems(List<ItemInstance> items)
        {
            Debug.Log($"[LootContainerPanel] ===== DisplayItems() called =====");
            Debug.Log($"[LootContainerPanel] Items parameter: {(items == null ? "null" : items.Count.ToString())} items");
            
            ClearLootGrid();

            if (lootGridParent == null || lootSlotPrefab == null)
            {
                Debug.LogWarning("[LootContainerPanel] DisplayItems: lootGridParent or lootSlotPrefab is null!");
                return;
            }

            // Calculate total slots needed: at least minSlots, or more if there are more items
            int itemCount = items != null ? items.Count : 0;
            int validItemCount = 0;
            if (items != null)
            {
                Debug.Log($"[LootContainerPanel] DisplayItems - Processing {itemCount} items from container");
                foreach (var item in items)
                {
                    if (item.IsValid())
                    {
                        validItemCount++;
                        Debug.Log($"[LootContainerPanel] DisplayItems - Valid item: ID={item.itemDataId}, Qty={item.quantity}, InstanceId={item.instanceId ?? "null"}");
                    }
                    else
                    {
                        Debug.LogWarning($"[LootContainerPanel] DisplayItems - Invalid item found: ID={item.itemDataId}, InstanceId={item.instanceId ?? "null"}, Qty={item.quantity}");
                    }
                }
            }
            else
            {
                Debug.LogWarning("[LootContainerPanel] DisplayItems - Items list is null!");
            }
            
            // Total slots = valid items + extra empty slots (always show at least minSlots)
            int totalSlotsNeeded = Mathf.Max(minSlots, validItemCount + extraEmptySlots);
            Debug.Log($"[LootContainerPanel] DisplayItems - Total items: {itemCount}, Valid items: {validItemCount}, Extra empty slots: {extraEmptySlots}, Total slots needed: {totalSlotsNeeded}");

            // First, create list of valid items with their ItemDataBase
            // Use ItemDataRegistry directly (no conversion needed)
            var validItemsWithData = new List<(ItemInstance item, ItemDataBase itemData)>();
            var registry = NightHunt.InteractionSystem.Core.Abstractions.ItemDataRegistry.Load();
            
            if (items != null && registry != null)
            {
                foreach (var item in items)
                {
                    if (!item.IsValid())
                    {
                        Debug.LogWarning($"[LootContainerPanel] DisplayItems - Skipping invalid item: ID={item.itemDataId}, InstanceId={item.instanceId}, Qty={item.quantity}");
                        continue;
                    }
                    
                    // Load ItemDataBase directly from ItemDataRegistry
                    var itemData = registry.GetById(item.itemDataId);
                    if (itemData != null)
                    {
                        validItemsWithData.Add((item, itemData));
                        Debug.Log($"[LootContainerPanel] DisplayItems - Prepared item: {item.itemDataId} (Qty: {item.quantity})");
                    }
                    else
                    {
                        Debug.LogWarning($"[LootContainerPanel] DisplayItems - Item not found in ItemDataRegistry: {item.itemDataId}");
                    }
                }
            }
            else if (registry == null)
            {
                Debug.LogWarning("[LootContainerPanel] DisplayItems - ItemDataRegistry is null!");
            }
            
            // Create slots: first fill with items, then add empty slots if needed
            for (int slotIndex = 0; slotIndex < totalSlotsNeeded; slotIndex++)
            {
                // Create slot
                var slot = new InventorySlot();
                
                // Fill slot with item if available
                if (slotIndex < validItemsWithData.Count)
                {
                    var (item, itemData) = validItemsWithData[slotIndex];
                    // Preserve instanceId so drag from container → inventory can keep the same identity
                    slot.SetItem(itemData, item.quantity, item.instanceId);
                    Debug.Log($"[LootContainerPanel] DisplayItems - Added item to slot {slotIndex}: {item.itemDataId} (Qty: {item.quantity}), Slot empty: {slot.IsEmpty}");
                }
                // If no item, slot remains empty (default InventorySlot is empty)

                // Create slot UI
                GameObject slotObj = Instantiate(lootSlotPrefab, lootGridParent);
                slotObj.SetActive(true);
                ItemCell slotUI = slotObj.GetComponent<ItemCell>();
                if (slotUI == null)
                {
                    slotUI = slotObj.AddComponent<ItemCell>();
                }

                // Initialize with ItemCellLocation.Container and index
                slotUI.Initialize(slot, inventoryPanel, ItemCellLocation.Container, slotIndex);
                lootSlots.Add(slotUI);
                
                Debug.Log($"[LootContainerPanel] DisplayItems - Created slot {slotIndex}: Empty={slot.IsEmpty}, ItemId={(slot.IsEmpty ? "null" : slot.Item?.ItemId ?? "null")}");
            }
            
            Debug.Log($"[LootContainerPanel] DisplayItems() completed - Created {lootSlots.Count} slots");
        }

        /// <summary>
        /// Refresh loot grid display
        /// </summary>
        public void RefreshLootGrid()
        {
            if (currentContainer == null)
            {
                ClearLootGrid();
                return;
            }

            var items = currentContainer.GetItems();
            DisplayItems(items);
        }

        /// <summary>
        /// Refresh loot grid with retry logic to wait for SyncList to sync
        /// </summary>
        private IEnumerator RefreshLootGridWithRetry()
        {
            if (currentContainer == null)
            {
                ClearLootGrid();
                yield break;
            }

            int maxRetries = 5;
            int retryCount = 0;
            
            while (retryCount < maxRetries)
            {
                var items = currentContainer.GetItems();
                Debug.Log($"[LootContainerPanel] RefreshLootGridWithRetry - Attempt {retryCount + 1}/{maxRetries}, Items count: {items?.Count ?? 0}");
                
                // Always display items (even if empty) on last retry or if we have items
                if (items != null && (items.Count > 0 || retryCount >= maxRetries - 1))
                {
                    Debug.Log($"[LootContainerPanel] RefreshLootGridWithRetry - Displaying {items.Count} items");
                    DisplayItems(items);
                    yield break;
                }
                
                // Wait a frame and retry (SyncList might not be synced yet)
                yield return null;
                retryCount++;
            }
            
            // Final attempt
            var finalItems = currentContainer.GetItems();
            Debug.Log($"[LootContainerPanel] RefreshLootGridWithRetry - Final attempt, Items count: {finalItems?.Count ?? 0}");
            DisplayItems(finalItems);
        }

        /// <summary>
        /// Clear loot grid
        /// </summary>
        private void ClearLootGrid()
        {
            foreach (var slot in lootSlots)
            {
                if (slot != null)
                {
                    Destroy(slot.gameObject);
                }
            }
            lootSlots.Clear();
        }

        /// <summary>
        /// Set container title
        /// </summary>
        public void SetContainerTitle(string title)
        {
            if (containerTitleText != null)
            {
                containerTitleText.text = title ?? "Container";
            }
        }

        /// <summary>
        /// Show loot container panel
        /// </summary>
        public void Show()
        {
            if (isUIPanelVisible)
            {
                Debug.Log($"[LootContainerPanel] Show() - Panel already visible, skipping");
                return;
            }
                
            isUIPanelVisible = true;
            Debug.Log($"[LootContainerPanel] Show() - Setting panelRoot active, panelRoot: {panelRoot?.name ?? "null"}");
            
            // Show panel UI GameObject (script GameObject remains active)
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
                Debug.Log($"[LootContainerPanel] Show() - panelRoot.SetActive(true) called, active: {panelRoot.activeSelf}, activeInHierarchy: {panelRoot.activeInHierarchy}");
            }
            else
            {
                Debug.LogWarning($"[LootContainerPanel] panelRoot is null! Cannot show panel UI.");
            }
        }

        /// <summary>
        /// Coroutine to load container after inventory opens
        /// </summary>
        private IEnumerator LoadContainerAfterInventoryOpen(NetworkLootContainer container)
        {
            // Wait a frame for inventory to fully open
            yield return null;
            
            // Wait until inventory is actually open
            int maxWaitFrames = 10;
            int waitFrames = 0;
            while (inventoryPanel != null && !inventoryPanel.IsOpen() && waitFrames < maxWaitFrames)
            {
                yield return null;
                waitFrames++;
            }
            
            if (inventoryPanel != null && inventoryPanel.IsOpen())
            {
                LoadContainer(container);
                Show();
                inventoryPanel.SetMode(InventoryMode.Loot);
            }
            else
            {
                Debug.LogWarning($"[LootContainerPanel] Inventory did not open within {maxWaitFrames} frames, storing container reference");
                // Store reference - will be loaded when inventory opens via CheckAndLoadOpenedContainer()
            }
        }

        /// <summary>
        /// Hide loot container panel
        /// </summary>
        public void Hide()
        {
            if (!isUIPanelVisible)
            {
                Debug.Log($"[LootContainerPanel] Hide() - Panel already hidden, skipping");
                return;
            }
                
            isUIPanelVisible = false;
            
            // Log stack trace to see who called Hide()
            Debug.Log($"[LootContainerPanel] Hide() - Called from: {System.Environment.StackTrace}");
            Debug.Log($"[LootContainerPanel] Hide() - Current state: isRaycastingContainer={isRaycastingContainer}, currentContainer={currentContainer?.name ?? "null"}, inventoryOpen={inventoryPanel?.IsOpen() ?? false}");
            
            // Hide panel UI GameObject (script GameObject remains active)
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
                Debug.Log($"[LootContainerPanel] Hide() - panelRoot.SetActive(false) called, active: {panelRoot.activeSelf}");
            }
            else
            {
                Debug.LogWarning($"[LootContainerPanel] Hide() - panelRoot is null! Cannot hide panel UI.");
            }
        }

        /// <summary>
        /// Clear container data
        /// </summary>
        public void ClearContainer()
        {
            if (currentContainer != null)
            {
                UnsubscribeFromContainer();
            }

            currentContainer = null;
            ClearLootGrid();

            if (containerTitleText != null)
            {
                containerTitleText.text = "";
            }
        }

        /// <summary>
        /// Get container items (for drag & drop)
        /// </summary>
        public List<InventorySlot> GetContainerItems()
        {
            if (currentContainer == null)
                return new List<InventorySlot>();

            var items = currentContainer.GetItems();
            var slots = new List<InventorySlot>();
            var registry = NightHunt.InteractionSystem.Core.Abstractions.ItemDataRegistry.Load();
            
            foreach (var item in items)
            {
                var slot = new InventorySlot();
                // Use ItemDataRegistry directly
                if (registry != null)
                {
                    var itemData = registry.GetById(item.itemDataId);
                    if (itemData != null)
                    {
                        // Preserve instanceId for container preview as well
                        slot.SetItem(itemData, item.quantity, item.instanceId);
                    }
                }
                slots.Add(slot);
            }
            return slots;
        }

        /// <summary>
        /// Get container ID (NetworkObject ID)
        /// </summary>
        public string GetContainerId()
        {
            if (currentContainer == null)
                return null;

            return currentContainer.NetworkObject.ObjectId.ToString();
        }

        /// <summary>
        /// Get slot at index (for drag & drop)
        /// </summary>
        public ItemCell GetSlotAt(int index)
        {
            if (index < 0 || index >= lootSlots.Count)
                return null;

            return lootSlots[index];
        }

        /// <summary>
        /// Move item from inventory to container
        /// </summary>
        public void MoveItemToContainer(string itemId, int fromX, int fromY)
        {
            if (currentContainer == null)
            {
                Debug.LogWarning("[LootContainerPanel] MoveItemToContainer: No container loaded");
                return;
            }

            // Fire UI event - Logic layer will handle it
            InventoryUIEvents.RequestMoveItemToContainer(GetContainerId(), itemId, fromX, fromY);
        }

        /// <summary>
        /// Move item from container to inventory
        /// </summary>
        public void MoveItemFromContainer(string itemId, int toX, int toY)
        {
            Debug.Log($"[LootContainerPanel] MoveItemFromContainer called - itemId: {itemId}, toX: {toX}, toY: {toY}, containerId: {GetContainerId() ?? "null"}");
            
            if (currentContainer == null)
            {
                Debug.LogWarning("[LootContainerPanel] MoveItemFromContainer: No container loaded");
                return;
            }

            // Fire UI event - Logic layer will handle it
            string containerId = GetContainerId();
            Debug.Log($"[LootContainerPanel] Firing RequestMoveItemFromContainer event - containerId: {containerId}, itemId: {itemId}, toX: {toX}, toY: {toY}");
            InventoryUIEvents.RequestMoveItemFromContainer(containerId, itemId, toX, toY);
            Debug.Log($"[LootContainerPanel] RequestMoveItemFromContainer event fired");
        }

        /// <summary>
        /// Check if panel is showing a container
        /// </summary>
        public bool IsContainerLoaded()
        {
            return currentContainer != null;
        }

        /// <summary>
        /// Get current container
        /// </summary>
        public NetworkLootContainer GetCurrentContainer()
        {
            return currentContainer;
        }

        /// <summary>
        /// Check if items can be added to the current container
        /// </summary>
        public bool CanAddItems()
        {
            return currentContainer?.GetAllowAddItems() ?? false;
        }

        /// <summary>
        /// Check if items can be removed from the current container
        /// </summary>
        public bool CanRemoveItems()
        {
            return currentContainer?.GetAllowRemoveItems() ?? false;
        }

        /// <summary>
        /// Handle interaction target changed (raycast detected) - replaces ContainerDistanceManager logic
        /// UI panel ONLY shows when raycasting container that is opened
        /// Handles both ContainerInteractable (chests) and CorpseInteractable (player corpses)
        /// </summary>
        private void HandleInteractTargetChanged(IInteractable interactable, string promptText)
        {
            Debug.Log($"[LootContainerPanel] HandleInteractTargetChanged - Interactable: {interactable?.GetType().Name ?? "null"}, Prompt: {promptText}");
            NetworkLootContainer raycastContainer = null;
            MonoBehaviour interactableMono = interactable as MonoBehaviour;
            
            // Check if raycast target is a ContainerInteractable or CorpseInteractable
            if (interactable is ContainerInteractable || interactable is NightHunt.InteractionSystem.Interaction.Implementations.CorpseInteractable)
            {
                // Get the NetworkLootContainer from the same GameObject
                var containerGameObject = interactableMono?.gameObject;
                if (containerGameObject != null)
                {
                    raycastContainer = containerGameObject.GetComponent<NetworkLootContainer>();
                    
                    // For corpse, container is always opened, so we don't need to check IsOpened()
                    // For regular containers, check if opened
                    bool isCorpse = interactable is NightHunt.InteractionSystem.Interaction.Implementations.CorpseInteractable;
                    bool isContainerOpened = isCorpse || (raycastContainer != null && raycastContainer.IsOpened());
                    
                    if (raycastContainer != null && isContainerOpened)
                    {
                        Debug.Log($"[LootContainerPanel] HandleInteractTargetChanged - Container found: {raycastContainer.name}, isCorpse: {isCorpse}, isOpened: {isContainerOpened}");
                        isRaycastingContainer = true;
                        
                        // REMOVED: No longer cache openedContainer
                        
                        // Show UI if inventory is open, otherwise it will be shown when inventory opens
                        if (inventoryPanel != null && inventoryPanel.IsOpen())
                        {
                            Debug.Log($"[LootContainerPanel] HandleInteractTargetChanged - Inventory is open, showing container UI immediately");
                            // Load container if not already loaded
                            if (currentContainer != raycastContainer)
                            {
                                LoadContainer(raycastContainer);
                            }
                            Show();
                            inventoryPanel.SetMode(InventoryMode.Loot);
                        }
                        else
                        {
                            Debug.Log($"[LootContainerPanel] HandleInteractTargetChanged - Inventory not open yet, container will be loaded when inventory opens");
                            // Inventory not open yet - container will be loaded when inventory opens via CheckAndLoadOpenedContainer()
                        }
                    }
                    else
                    {
                        // Container is not opened or not found - hide UI when raycast is lost
                        Debug.Log($"[LootContainerPanel] HandleInteractTargetChanged - Container not found or not opened: raycastContainer={raycastContainer?.name ?? "null"}, isContainerOpened={isContainerOpened}, hiding panel");
                        isRaycastingContainer = false;
                        if (isUIPanelVisible)
                        {
                            Hide();
                        }
                    }
                }
                else
                {
                    // Container GameObject is null - hide UI when raycast is lost
                    Debug.Log($"[LootContainerPanel] HandleInteractTargetChanged - Container GameObject is null, hiding panel");
                    isRaycastingContainer = false;
                    if (isUIPanelVisible)
                    {
                        Hide();
                    }
                }
            }
            else
            {
                // Not a container or corpse interactable - hide UI when raycast is lost
                Debug.Log($"[LootContainerPanel] HandleInteractTargetChanged - Not a container interactable, hiding panel");
                isRaycastingContainer = false;
                if (isUIPanelVisible)
                {
                    Hide();
                }
            }
        }

        /// <summary>
        /// Handle container closed event
        /// NOTE: Only hide UI if container is actually closed AND not actively raycasting
        /// This prevents UI from closing when items are moved (which might trigger close events)
        /// </summary>
        private void HandleContainerClosed(ILootContainer container)
        {
            // Only process if this is our current container
            if (container != currentContainer) return;
            
            // CRITICAL: Only hide if we're NOT actively raycasting the container
            // This prevents UI from closing when items are moved between inventory and container
            if (!isRaycastingContainer && isUIPanelVisible)
            {
                Hide();
                ClearContainer();
            }
            // If still raycasting, ignore the close event (container might be closing due to item changes, not player leaving)
        }

        /// <summary>
        /// Handle interaction target lost (raycast lost) - replaces ContainerDistanceManager logic
        /// Hide UI immediately when raycast is lost and clear container reference
        /// </summary>
        private void HandleInteractTargetLost()
        {
            Debug.Log($"[LootContainerPanel] HandleInteractTargetLost - Raycast lost, isUIPanelVisible: {isUIPanelVisible}, isRaycastingContainer: {isRaycastingContainer}");
            isRaycastingContainer = false;
            
            // Clear container reference when raycast is lost
            if (currentContainer != null)
            {
                Debug.Log($"[LootContainerPanel] HandleInteractTargetLost - Clearing container reference: {currentContainer.name}");
                ClearContainer();
            }
            
            // Hide UI immediately when raycast is lost
            if (isUIPanelVisible)
            {
                Debug.Log($"[LootContainerPanel] HandleInteractTargetLost - Hiding panel because raycast is lost");
                Hide();
            }
        }

    }
}
