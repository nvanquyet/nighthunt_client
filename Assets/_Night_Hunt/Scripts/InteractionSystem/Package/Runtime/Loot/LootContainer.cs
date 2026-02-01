using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using NightHunt.InteractionSystem.Core.Interfaces;
using NightHunt.InteractionSystem.Core.Structs;
using NightHunt.InteractionSystem.Events;
using NightHunt.InteractionSystem.Loot.Spawn;

namespace NightHunt.InteractionSystem.Loot
{
    /// <summary>
    /// Container generation mode.
    /// </summary>
    public enum LootContainerMode
    {
        /// <summary>
        /// Only use LootTable (weighted random).
        /// </summary>
        Random,
        
        /// <summary>
        /// Only use pre-placed items (no random).
        /// </summary>
        Fixed,
        
        /// <summary>
        /// Pre-placed items + random items from LootTable.
        /// </summary>
        Hybrid
    }

    /// <summary>
    /// Container access permissions - defines what players can do with container items.
    /// </summary>
    public enum ContainerAccessMode
    {
        /// <summary>
        /// Players can only remove items (read-only container).
        /// </summary>
        ReadOnly,
        
        /// <summary>
        /// Players can only add items (write-only container).
        /// </summary>
        WriteOnly,
        
        /// <summary>
        /// Players can both add and remove items (full access).
        /// </summary>
        ReadWrite,
        
        /// <summary>
        /// Players cannot interact with items (no access).
        /// </summary>
        None
    }

    /// <summary>
    /// NetworkBehaviour container that holds loot items.
    /// All settings come from LootContainerPoint when spawned.
    /// Model should be attached directly to prefab (managed by ContainerVisualState).
    /// </summary>
    public class NetworkLootContainer : NetworkBehaviour, ILootContainer
    {
        // All settings are initialized from LootContainerPoint via InitializeFromPoint()
        private int maxSlots = 12;
        private bool isLocked = false;
        private string containerName = "Container";
        private LootContainerMode containerMode = LootContainerMode.Random;
        private List<ItemInstance> initialItems = new List<ItemInstance>();
        private LootTable lootTable;
        private bool generateLootOnFirstOpen = true;
        private bool preGenerateOnStart = false;
        private int overrideMinItems = 0;
        private int overrideMaxItems = 0;
        private bool allowAddItems = true;        // Allow players to add items to container
        private bool allowRemoveItems = true;     // Allow players to remove items from container

        /// <summary>
        /// Get whether items can be added to this container
        /// </summary>
        public bool GetAllowAddItems() => allowAddItems;

        /// <summary>
        /// Get whether items can be removed from this container
        /// </summary>
        public bool GetAllowRemoveItems() => allowRemoveItems;

        /// <summary>
        /// Set container permissions (for corpse - ReadOnly mode)
        /// </summary>
        [Server]
        public void SetContainerPermissions(bool allowAdd, bool allowRemove)
        {
            allowAddItems = allowAdd;
            allowRemoveItems = allowRemove;
            Debug.Log($"[NetworkLootContainer] SetContainerPermissions - Container: {containerName}, AllowAdd: {allowAddItems}, AllowRemove: {allowRemoveItems}");
        }

        /// <summary>
        /// Set container opened state (for corpse - always open)
        /// </summary>
        [Server]
        public void SetOpenedState(bool opened)
        {
            syncIsOpened.Value = opened;
            Debug.Log($"[NetworkLootContainer] SetOpenedState - Container: {containerName}, IsOpened: {syncIsOpened.Value}");
        }

        private readonly SyncVar<bool> syncIsLocked = new SyncVar<bool>();
        private readonly SyncVar<bool> syncHasBeenOpened = new SyncVar<bool>();
        private readonly SyncVar<bool> syncIsOpened = new SyncVar<bool>(); // Track if container is currently opened (for re-interaction)
        private readonly SyncList<ItemInstance> containerItems = new SyncList<ItemInstance>();

        private bool isInitializedFromPoint = false;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            syncIsLocked.Value = isLocked;
            syncHasBeenOpened.Value = false;
            syncIsOpened.Value = false;
            syncIsLocked.OnChange += OnLockedChanged;
            containerItems.OnChange += OnContainerItemsChanged;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            // Pre-generate if enabled
            if (preGenerateOnStart && ShouldPreGenerate())
            {
                PreGenerateLoot();
            }
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (syncIsLocked != null)
                syncIsLocked.OnChange -= OnLockedChanged;
            if (containerItems != null)
                containerItems.OnChange -= OnContainerItemsChanged;
        }

        /// <summary>
        /// Get all items in the container.
        /// </summary>
        public List<ItemInstance> GetItems()
        {
            Debug.Log($"[NetworkLootContainer] ===== GetItems() called =====");
            Debug.Log($"[NetworkLootContainer] Container: {containerName}, SyncList count: {containerItems.Count}, IsServer: {IsServer}, IsClient: {IsClient}");
            
            List<ItemInstance> items = new List<ItemInstance>();
            int index = 0;
            foreach (var item in containerItems)
            {
                Debug.Log($"[NetworkLootContainer] Item [{index}]: ID={item.itemDataId}, Qty={item.quantity}, InstanceId={item.instanceId}, Valid={item.IsValid()}");
                items.Add(item);
                index++;
            }
            
            Debug.Log($"[NetworkLootContainer] GetItems() returning {items.Count} items");
            return items;
        }

        /// <summary>
        /// Get the maximum number of slots.
        /// </summary>
        public int GetMaxSlots()
        {
            return maxSlots;
        }

        /// <summary>
        /// Check if the container is locked.
        /// </summary>
        public bool IsLocked()
        {
            return syncIsLocked.Value;
        }

        /// <summary>
        /// Check if the container is currently opened (for re-interaction).
        /// </summary>
        public bool IsOpened()
        {
            return syncIsOpened.Value;
        }

        /// <summary>
        /// Add an item to the container (server only).
        /// </summary>
        [Server]
        public bool AddItem(ItemInstance item)
        {
            Debug.Log($"[NetworkLootContainer] ===== AddItem (Server) called =====");
            Debug.Log($"[NetworkLootContainer] Item: {item.itemDataId}, Quantity: {item.quantity}");
            Debug.Log($"[NetworkLootContainer] Container: {containerName}, allowAddItems: {allowAddItems}, IsLocked: {syncIsLocked.Value}");
            Debug.Log($"[NetworkLootContainer] AddItem() - Before: SyncList count={containerItems.Count}, maxSlots={maxSlots}");
            
            if (syncIsLocked.Value)
            {
                Debug.LogWarning($"[NetworkLootContainer] AddItem failed: Container '{containerName}' is locked");
                return false;
            }

            if (!allowAddItems)
            {
                Debug.LogWarning($"[NetworkLootContainer] AddItem failed: Container '{containerName}' does not allow adding items (read-only)");
                return false;
            }

            if (containerItems.Count >= maxSlots)
            {
                Debug.LogWarning($"[NetworkLootContainer] AddItem failed: Container '{containerName}' is full ({containerItems.Count}/{maxSlots})");
                return false;
            }

            // Ensure item has instanceId before adding to SyncList
            ItemInstance validItem = item;
            if (string.IsNullOrEmpty(item.instanceId))
            {
                // Create new instanceId if missing
                validItem = new ItemInstance(item.itemDataId, item.quantity, item.durability);
                Debug.Log($"[NetworkLootContainer] AddItem - Generated new instanceId for item: {item.itemDataId}, instanceId: {validItem.instanceId}");
            }
            
            containerItems.Add(validItem);
            Debug.Log($"[NetworkLootContainer] AddItem() - After: SyncList count={containerItems.Count}, Item added: {validItem.itemDataId}, Valid: {validItem.IsValid()}, InstanceId: {validItem.instanceId}");
            return true;
        }

        /// <summary>
        /// Remove an item from the container (server only).
        /// </summary>
        [Server]
        public bool RemoveItem(string itemId, int quantity = 1)
        {
            Debug.Log($"[NetworkLootContainer] ===== RemoveItem (Server) called =====");
            Debug.Log($"[NetworkLootContainer] RemoveItem() - Before: SyncList count={containerItems.Count}, Looking for: {itemId}, Quantity: {quantity}");
            
            if (syncIsLocked.Value)
            {
                Debug.LogWarning($"[NetworkLootContainer] RemoveItem failed: Container '{containerName}' is locked");
                return false;
            }

            int remaining = quantity;
            int initialCount = containerItems.Count;

            for (int i = containerItems.Count - 1; i >= 0; i--)
            {
                var item = containerItems[i];
                if (item.itemDataId == itemId)
                {
                    int toRemove = Mathf.Min(remaining, item.quantity);
                    int newQuantity = item.quantity - toRemove;
                    remaining -= toRemove;

                    if (newQuantity <= 0)
                    {
                        containerItems.RemoveAt(i);
                    }
                    else
                    {
                        containerItems[i] = item.WithQuantity(newQuantity);
                    }

                    if (remaining <= 0)
                    {
                        bool success = true;
                        Debug.Log($"[NetworkLootContainer] RemoveItem() - After: SyncList count={containerItems.Count}, Removed: {quantity}/{quantity}, Success: {success}");
                        return success;
                    }
                }
            }

            bool result = remaining < quantity;
            Debug.Log($"[NetworkLootContainer] RemoveItem() - After: SyncList count={containerItems.Count}, Removed: {quantity - remaining}/{quantity}, Success: {result}");
            return result;
        }

        /// <summary>
        /// Check if the container is empty.
        /// </summary>
        public bool IsEmpty()
        {
            return containerItems.Count == 0;
        }

        /// <summary>
        /// Get the container display name.
        /// </summary>
        public string GetDisplayName()
        {
            return containerName;
        }

        /// <summary>
        /// Request to open the container (client calls this).
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestOpenContainer(NetworkConnection conn)
        {
            Debug.Log($"[NetworkLootContainer] ===== RequestOpenContainer (ServerRpc) called =====");
            Debug.Log($"[NetworkLootContainer] Connection: {conn?.ClientId ?? -1}");
            Debug.Log($"[NetworkLootContainer] Container: {containerName}");
            Debug.Log($"[NetworkLootContainer] IsLocked: {syncIsLocked.Value}, HasBeenOpened: {syncHasBeenOpened.Value}, IsEmpty: {IsEmpty()}");
            Debug.Log($"[NetworkLootContainer] Current items count: {containerItems.Count}");
            
            if (syncIsLocked.Value)
            {
                Debug.LogWarning("[NetworkLootContainer] RequestOpenContainer failed: Container is locked!");
                return;
            }

            // Only generate on first open if NOT pre-generated
            if (!preGenerateOnStart && generateLootOnFirstOpen && !syncHasBeenOpened.Value && IsEmpty())
            {
                Debug.Log("[NetworkLootContainer] Generating loot on first open (not pre-generated, empty container)");
                GenerateItems();
                syncHasBeenOpened.Value = true;
                Debug.Log($"[NetworkLootContainer] Loot generated, new items count: {containerItems.Count}");
            }
            else
            {
                Debug.Log($"[NetworkLootContainer] Skipping loot generation - preGenerateOnStart: {preGenerateOnStart}, generateLootOnFirstOpen: {generateLootOnFirstOpen}, HasBeenOpened: {syncHasBeenOpened.Value}, IsEmpty: {IsEmpty()}");
            }

            // Set opened state
            Debug.Log($"[NetworkLootContainer] Setting syncIsOpened to true...");
            syncIsOpened.Value = true;
            Debug.Log($"[NetworkLootContainer] syncIsOpened set to: {syncIsOpened.Value}");
            
            // Send container data to requesting player
            Debug.Log($"[NetworkLootContainer] Calling OpenContainerForPlayer (TargetRpc) for connection: {conn?.ClientId ?? -1}");
            OpenContainerForPlayer(conn);
            Debug.Log($"[NetworkLootContainer] RequestOpenContainer completed");
        }

        /// <summary>
        /// Unified generation logic for all modes (Random, Fixed, Hybrid).
        /// </summary>
        [Server]
        private void GenerateItems()
        {
            // Add pre-placed items (Fixed or Hybrid mode)
            if (containerMode == LootContainerMode.Fixed || containerMode == LootContainerMode.Hybrid)
            {
                foreach (var item in initialItems)
                {
                    if (containerItems.Count >= maxSlots)
                    {
                        Debug.LogWarning($"[LootContainer] {containerName} is full, cannot add more items!");
                        break;
                    }

                    // Ensure item has instanceId before adding
                    ItemInstance validItem = item;
                    if (string.IsNullOrEmpty(item.instanceId))
                    {
                        validItem = new ItemInstance(item.itemDataId, item.quantity, item.durability);
                        Debug.Log($"[NetworkLootContainer] GenerateItems - Generated instanceId for initial item: {item.itemDataId}");
                    }

                    if (!AddItem(validItem))
                    {
                        Debug.LogWarning($"[NetworkLootContainer] {containerName} failed to add initial item: {item.itemDataId}");
                    }
                }
            }

            // Generate random items from LootTable (Random or Hybrid mode)
            if (containerMode == LootContainerMode.Random || containerMode == LootContainerMode.Hybrid)
            {
                GenerateLootFromTable();
            }
        }

        /// <summary>
        /// Generate loot items from LootTable and add to container (server only).
        /// </summary>
        [Server]
        private void GenerateLootFromTable()
        {
            if (lootTable == null)
            {
                Debug.LogWarning($"[NetworkLootContainer] {containerName} has no LootTable assigned!");
                return;
            }

            // Use override if set, otherwise use LootTable default (0 = use default)
            var generatedItems = lootTable.GenerateLoot(overrideMinItems, overrideMaxItems);

            if (generatedItems == null || generatedItems.Count == 0)
            {
                Debug.LogWarning($"[NetworkLootContainer] {containerName} generated no items from LootTable!");
                return;
            }

            // Add generated items to container (respect max slots)
            foreach (var item in generatedItems)
            {
                if (containerItems.Count >= maxSlots)
                {
                    Debug.LogWarning($"[LootContainer] {containerName} is full, cannot add more items!");
                    break;
                }

                // Ensure item has instanceId before adding (AddItem will also check, but we validate here too)
                ItemInstance validItem = item;
                if (string.IsNullOrEmpty(item.instanceId))
                {
                    validItem = new ItemInstance(item.itemDataId, item.quantity, item.durability);
                    Debug.Log($"[NetworkLootContainer] GenerateLootFromTable - Generated instanceId for: {item.itemDataId}, Valid: {validItem.IsValid()}");
                }
                
                AddItem(validItem);
            }

            Debug.Log($"[NetworkLootContainer] {containerName} generated {generatedItems.Count} items from LootTable.");
        }

        /// <summary>
        /// Open container for specific player.
        /// </summary>
        [TargetRpc]
        private void OpenContainerForPlayer(NetworkConnection conn)
        {
            Debug.Log($"[NetworkLootContainer] ===== OpenContainerForPlayer (TargetRpc) called =====");
            Debug.Log($"[NetworkLootContainer] Connection: {conn?.ClientId ?? -1}");
            Debug.Log($"[NetworkLootContainer] Container: {containerName}, IsOpened: {syncIsOpened.Value}");
            Debug.Log($"[NetworkLootContainer] OpenContainerForPlayer - Event fired, SyncList count at this moment: {containerItems.Count}");
            
            // Log SyncList state to verify it's synced when event fires
            Debug.Log($"[NetworkLootContainer] SyncList verification - Count: {containerItems.Count}");
            for (int i = 0; i < containerItems.Count; i++)
            {
                var item = containerItems[i];
                Debug.Log($"[NetworkLootContainer] SyncList item [{i}]: {item.itemDataId}, Qty: {item.quantity}");
            }
            
            // Invoke event for UI to listen
            Debug.Log($"[NetworkLootContainer] Invoking InventoryEvents.InvokeLootContainerOpened...");
            InventoryEvents.InvokeLootContainerOpened(this);
            Debug.Log($"[NetworkLootContainer] InventoryEvents.InvokeLootContainerOpened completed");
        }

        /// <summary>
        /// Close the container (set opened state to false).
        /// Called when player moves away or explicitly closes container.
        /// </summary>
        [Server]
        public void CloseContainer()
        {
            if (syncIsOpened.Value)
            {
                syncIsOpened.Value = false;
                Debug.Log($"[NetworkLootContainer] Container closed: {containerName}, IsOpened={syncIsOpened.Value}");
                
                // Fire event to notify clients that container is closed
                CloseContainerForAllClients();
            }
        }

        /// <summary>
        /// Notify all clients that container is closed (ObserversRpc).
        /// </summary>
        [ObserversRpc]
        private void CloseContainerForAllClients()
        {
            // Fire event for UI layer to hide container UI
            InventoryEvents.InvokeLootContainerClosed(this);
        }

        /// <summary>
        /// Request to close container (client calls this via ServerRpc).
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestCloseContainer()
        {
            CloseContainer();
        }

        /// <summary>
        /// Sync container state to all observers.
        /// </summary>
        [ObserversRpc]
        public void SyncContainerState()
        {
            // Container items are already synced via SyncList
            // This can be used for additional state sync if needed
        }

        private void OnContainerItemsChanged(SyncListOperation op, int index, ItemInstance oldItem, ItemInstance newItem, bool asServer)
        {
            // Only fire UI events on client (not on server for headless server compatibility)
            // UI components subscribe to these events and shouldn't run on server
            if (IsServer)
            {
                Debug.Log($"[NetworkLootContainer] Container items changed on server - Operation: {op}, Index: {index}, Container: {containerName} (skipping UI events for headless server)");
                return;
            }
            
            // Fire event for UI layer to update display
            // This ensures remote clients see real-time updates when other players modify container
            Debug.Log($"[NetworkLootContainer] ===== OnContainerItemsChanged =====");
            Debug.Log($"[NetworkLootContainer] Operation: {op}, Index: {index}, AsServer: {asServer}, Container: {containerName}");
            Debug.Log($"[NetworkLootContainer] OldItem: {oldItem.itemDataId} (Qty: {oldItem.quantity}, Valid: {oldItem.IsValid()}), NewItem: {newItem.itemDataId} (Qty: {newItem.quantity}, Valid: {newItem.IsValid()})");
            Debug.Log($"[NetworkLootContainer] Current SyncList count: {containerItems.Count}");
            Debug.Log($"[NetworkLootContainer] Firing InventoryEvents.InvokeLootContainerItemsChanged (IsServer: {IsServer})");
            
            // Fire event to notify UI layer (LootContainerPanel will refresh)
            InventoryEvents.InvokeLootContainerItemsChanged(this);
            
            // Also fire specific events for added/removed items
            if (op == SyncListOperation.Add && newItem.IsValid())
            {
                Debug.Log($"[NetworkLootContainer] Item added to container: {newItem.itemDataId}, Quantity: {newItem.quantity}");
                InventoryEvents.InvokeItemLooted(newItem, this);
            }
            else if (op == SyncListOperation.RemoveAt && oldItem.IsValid())
            {
                Debug.Log($"[NetworkLootContainer] Item removed from container: {oldItem.itemDataId}, Quantity: {oldItem.quantity}");
                // Note: OnItemLooted might not be appropriate for removal, but we fire container changed event above
            }
            else if (op == SyncListOperation.Set && newItem.IsValid())
            {
                Debug.Log($"[NetworkLootContainer] Item changed in container: {newItem.itemDataId}, Quantity: {newItem.quantity}");
                // Item quantity or other properties changed
            }
        }

        private void OnLockedChanged(bool oldValue, bool newValue, bool asServer)
        {
            isLocked = newValue;
            syncIsLocked.Value = newValue;
        }

        /// <summary>
        /// Check if this container should be pre-generated by manager.
        /// </summary>
        public bool ShouldPreGenerate()
        {
            return preGenerateOnStart && IsEmpty() && 
                   ((containerMode == LootContainerMode.Random || containerMode == LootContainerMode.Hybrid) && lootTable != null ||
                    (containerMode == LootContainerMode.Fixed || containerMode == LootContainerMode.Hybrid) && initialItems.Count > 0);
        }

        /// <summary>
        /// Pre-generate loot (called by manager on game start).
        /// </summary>
        [Server]
        public void PreGenerateLoot()
        {
            if (!IsEmpty())
            {
                Debug.LogWarning($"[NetworkLootContainer] {containerName} is not empty, cannot pre-generate!");
                return;
            }

            GenerateItems();
            syncHasBeenOpened.Value = true;
        }

        /// <summary>
        /// Initialize container with settings from spawn point (called when spawned by LootContainerPoint).
        /// Overrides all prefab settings with point settings.
        /// </summary>
        [Server]
        public void InitializeFromPoint(LootTable pointLootTable, LootContainerMode pointContainerMode, 
            List<ItemInstance> pointInitialItems, bool pointGenerateLootOnFirstOpen, 
            bool pointPreGenerateOnStart, int pointOverrideMinItems, int pointOverrideMaxItems,
            int pointMaxSlots, bool pointIsLocked, string pointContainerName,
            bool pointAllowAddItems, bool pointAllowRemoveItems)
        {
            // Override all settings from point
            lootTable = pointLootTable;
            containerMode = pointContainerMode;
            
            initialItems.Clear();
            if (pointInitialItems != null)
            {
                initialItems.AddRange(pointInitialItems);
            }
            
            generateLootOnFirstOpen = pointGenerateLootOnFirstOpen;
            preGenerateOnStart = pointPreGenerateOnStart;
            overrideMinItems = pointOverrideMinItems;
            overrideMaxItems = pointOverrideMaxItems;
            maxSlots = pointMaxSlots;
            isLocked = pointIsLocked;
            containerName = pointContainerName;
            allowAddItems = pointAllowAddItems;
            allowRemoveItems = pointAllowRemoveItems;
            
            isInitializedFromPoint = true;
            
            // Sync locked state
            syncIsLocked.Value = isLocked;
            
            Debug.Log($"[NetworkLootContainer] InitializeFromPoint completed - Container: {containerName}, AllowAdd: {allowAddItems}, AllowRemove: {allowRemoveItems}");
        }

    }
}
