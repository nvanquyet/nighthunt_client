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
    /// NetworkBehaviour container that holds loot items.
    /// Can spawn items from LootTable when first opened (if empty).
    /// </summary>
    public class LootContainer : NetworkBehaviour, ILootContainer
    {
        [Header("Container Settings")]
        [SerializeField] private int maxSlots = 12;
        [SerializeField] private bool isLocked = false;
        [SerializeField] private string containerName = "Container";

        [Header("Loot Generation")]
        [Tooltip("LootTable to generate items from when container is first opened (if empty)")]
        [SerializeField] private LootTable lootTable;

        [Tooltip("Generate loot when container is first opened (if empty)?")]
        [SerializeField] private bool generateLootOnFirstOpen = true;

        private readonly SyncVar<bool> syncIsLocked = new SyncVar<bool>();
        private readonly SyncVar<bool> syncHasBeenOpened = new SyncVar<bool>();
        private readonly SyncList<ItemInstance> containerItems = new SyncList<ItemInstance>();

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            syncIsLocked.Value = isLocked;
            syncHasBeenOpened.Value = false;
            syncIsLocked.OnChange += OnLockedChanged;
            containerItems.OnChange += OnContainerItemsChanged;
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
            List<ItemInstance> items = new List<ItemInstance>();
            foreach (var item in containerItems)
            {
                items.Add(item);
            }
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
        /// Add an item to the container (server only).
        /// </summary>
        [Server]
        public bool AddItem(ItemInstance item)
        {
            if (syncIsLocked.Value)
                return false;

            if (containerItems.Count >= maxSlots)
                return false;

            containerItems.Add(item);
            return true;
        }

        /// <summary>
        /// Remove an item from the container (server only).
        /// </summary>
        [Server]
        public bool RemoveItem(string itemId, int quantity = 1)
        {
            if (syncIsLocked.Value)
                return false;

            int remaining = quantity;

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
                        return true;
                }
            }

            return remaining < quantity;
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
            if (syncIsLocked.Value)
                return;

            // Generate loot on first open (if empty and enabled)
            if (generateLootOnFirstOpen && !syncHasBeenOpened.Value && IsEmpty() && lootTable != null)
            {
                GenerateLootFromTable();
                syncHasBeenOpened.Value = true;
            }

            // Send container data to requesting player
            OpenContainerForPlayer(conn);
        }

        /// <summary>
        /// Generate loot items from LootTable and add to container (server only).
        /// </summary>
        [Server]
        private void GenerateLootFromTable()
        {
            if (lootTable == null)
            {
                Debug.LogWarning($"[LootContainer] {containerName} has no LootTable assigned!");
                return;
            }

            var generatedItems = lootTable.GenerateLoot();
            if (generatedItems == null || generatedItems.Count == 0)
            {
                Debug.LogWarning($"[LootContainer] {containerName} generated no items from LootTable!");
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

                AddItem(item);
            }

            Debug.Log($"[LootContainer] {containerName} generated {generatedItems.Count} items from LootTable.");
        }

        /// <summary>
        /// Open container for specific player.
        /// </summary>
        [TargetRpc]
        private void OpenContainerForPlayer(NetworkConnection conn)
        {
            // Invoke event for UI to listen
            InventoryEvents.InvokeLootContainerOpened(this);
            
            // Fallback: direct UI reference (for backward compatibility)
            var uiController = FindObjectOfType<LootContainerUI>();
            if (uiController != null)
            {
                uiController.OpenContainer(this);
            }
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
            // Handle item changes
        }

        private void OnLockedChanged(bool oldValue, bool newValue, bool asServer)
        {
            isLocked = newValue;
            syncIsLocked.Value = newValue;
        }
    }
}
