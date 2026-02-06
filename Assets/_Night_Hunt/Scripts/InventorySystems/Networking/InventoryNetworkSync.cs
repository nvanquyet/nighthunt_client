using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Domain.Inventory;
using System.Collections.Generic;

namespace NightHunt.Inventory.Networking
{
    /// <summary>
    /// FishNet network synchronization for inventory.
    /// Implements server-authoritative pattern with delta sync.
    /// </summary>
    public class InventoryNetworkSync : NetworkBehaviour
    {
        [Header("Configuration")] [SerializeField]
        private NetworkSyncConfig config;

        [Header("References")] [SerializeField]
        private InventoryManager inventoryManager;

        [Header("Debug")] [SerializeField] private bool enableDebugLogs = false;

        private int deltaSyncCounter = 0;

        // SyncVar for critical data (backup) - Updated to FishNet v4
        private readonly SyncVar<InventorySnapshotData> syncedSnapshot = new SyncVar<InventorySnapshotData>();

        #region Lifecycle

        public override void OnStartClient()
        {
            base.OnStartClient();
            
            // Subscribe to SyncVar changes (for all clients, including spectators)
            syncedSnapshot.OnChange += OnInventoryDataChanged;
            
            // Only subscribe to local changes if owner
            if (IsOwner)
            {
                InventoryEvents.OnInventoryChanged += OnLocalInventoryChanged;
            }

            if (enableDebugLogs)
                Debug.Log("[InventoryNetworkSync] Client started");
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            // Unsubscribe from SyncVar changes (for all clients)
            syncedSnapshot.OnChange -= OnInventoryDataChanged;
            
            // Unsubscribe from local changes if owner
            if (IsOwner)
            {
                InventoryEvents.OnInventoryChanged -= OnLocalInventoryChanged;
            }
        }

        #endregion

        #region Local Changes (Client → Server)

        private void OnLocalInventoryChanged(InventoryData data)
        {
            if (!IsOwner) return;

            // Send update to server
            if (config.useDeltaSync && deltaSyncCounter < config.fullSyncInterval)
            {
                // Send delta update
                var delta = CreateDelta(data);
                SendDeltaUpdateServerRpc(delta);
                deltaSyncCounter++;
            }
            else
            {
                // Send full sync
                var snapshot = CreateSnapshot(data);
                SendFullSyncServerRpc(snapshot);
                deltaSyncCounter = 0;
            }
        }

        #endregion

        #region Server RPCs (Client → Server)

        [ServerRpc]
        private void SendDeltaUpdateServerRpc(InventoryDeltaData delta, NetworkConnection sender = null)
        {
            if (enableDebugLogs)
                Debug.Log($"[InventoryNetworkSync] Server received delta from {sender.ClientId}");

            // Validate delta (anti-cheat)
            if (!ValidateDelta(delta, sender))
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"[InventoryNetworkSync] Invalid delta from {sender.ClientId}");

                // Send full sync to correct client state
                RequestFullResyncTargetRpc(sender);
                return;
            }

            // Apply delta
            ApplyDelta(delta);

            // Broadcast to observers
            BroadcastDeltaObserversRpc(delta);
        }

        [ServerRpc]
        private void SendFullSyncServerRpc(InventorySnapshotData snapshot, NetworkConnection sender = null)
        {
            if (enableDebugLogs)
                Debug.Log($"[InventoryNetworkSync] Server received full sync from {sender.ClientId}");

            // Validate snapshot (anti-cheat)
            if (!ValidateSnapshot(snapshot, sender))
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"[InventoryNetworkSync] Invalid snapshot from {sender.ClientId}");

                RequestFullResyncTargetRpc(sender);
                return;
            }

            // Update synced data - Updated to FishNet v4
            syncedSnapshot.Value = snapshot;

            // Broadcast to observers
            BroadcastFullSyncObserversRpc(snapshot);
        }

        #endregion

        #region Observers RPCs (Server → All Clients)

        [ObserversRpc] // Removed ExcludeOwner = true to allow spectator to receive updates
        private void BroadcastDeltaObserversRpc(InventoryDeltaData delta)
        {
            if (enableDebugLogs)
                Debug.Log("[InventoryNetworkSync] Applying delta from server");

            ApplyDelta(delta);

            // Refresh UI
            if (inventoryManager != null)
            {
                InventoryEvents.InvokeInventoryChanged(inventoryManager.GetInventoryData());
            }
        }

        [ObserversRpc] // Removed ExcludeOwner = true to allow spectator to receive updates
        private void BroadcastFullSyncObserversRpc(InventorySnapshotData snapshot)
        {
            if (enableDebugLogs)
                Debug.Log("[InventoryNetworkSync] Applying full sync from server");

            ApplySnapshot(snapshot);

            // Refresh UI
            if (inventoryManager != null)
            {
                InventoryEvents.InvokeInventoryChanged(inventoryManager.GetInventoryData());
            }
        }

        #endregion

        #region Target RPCs (Server → Specific Client)

        [TargetRpc]
        private void RequestFullResyncTargetRpc(NetworkConnection target)
        {
            if (enableDebugLogs)
                Debug.Log("[InventoryNetworkSync] Server requested full resync");

            // Send full snapshot to server
            if (inventoryManager != null)
            {
                var snapshot = CreateSnapshot(inventoryManager.GetInventoryData());
                SendFullSyncServerRpc(snapshot);
            }
        }

        #endregion

        #region Delta & Snapshot Creation

        private InventoryDeltaData CreateDelta(InventoryData data)
        {
            // Create delta from last state
            // For simplicity, we send changed slots only
            var delta = new InventoryDeltaData
            {
                Timestamp = Time.time,
                ChangedSlots = new List<SlotChange>()
            };

            // TODO: Track changes and create proper delta
            // For now, treat as full sync

            return delta;
        }

        private InventorySnapshotData CreateSnapshot(InventoryData data)
        {
            var snapshot = new InventorySnapshotData
            {
                Timestamp = Time.time,
                Items = new List<ItemInstanceData>()
            };

            foreach (var item in data.GetAllItems())
            {
                snapshot.Items.Add(item.Serialize());
            }

            return snapshot;
        }

        #endregion

        #region Validation (Anti-Cheat)

        private bool ValidateDelta(InventoryDeltaData delta, NetworkConnection sender)
        {
            // Basic validation
            if (delta.Equals(null)) return false;

            // TODO: Implement proper delta validation
            // - Check item ownership
            // - Validate weight limits
            // - Check stack sizes

            return true;
        }

        private bool ValidateSnapshot(InventorySnapshotData snapshot, NetworkConnection sender)
        {
            if (snapshot.Equals(null)) return false;

            // TODO: Implement snapshot validation
            // - Check for duplicate items
            // - Validate weight limits
            // - Verify item ownership

            return true;
        }

        #endregion

        #region Apply Changes

        private void ApplyDelta(InventoryDeltaData delta)
        {
            if (inventoryManager == null) return;

            // Apply delta changes
            foreach (var change in delta.ChangedSlots)
            {
                // TODO: Apply specific slot changes
            }
        }

        private void ApplySnapshot(InventorySnapshotData snapshot)
        {
            if (inventoryManager == null) return;

            // Clear and rebuild inventory from snapshot
            var inventory = inventoryManager.GetInventoryData();
            inventory.Clear();

            foreach (var itemData in snapshot.Items)
            {
                // Deserialize and add item
                // TODO: Load ItemDefinition from database/registry
                // var definition = ItemRegistry.GetDefinition(itemData.ItemId);
                // var item = ItemInstance.Deserialize(itemData, definition);
                // inventory.TryAddItem(item);
            }
        }

        #endregion

        #region SyncVar Callback

        // Updated callback signature for FishNet v4
        private void OnInventoryDataChanged(InventorySnapshotData prev, InventorySnapshotData next, bool asServer)
        {
            if (asServer) return;

            if (enableDebugLogs)
                Debug.Log("[InventoryNetworkSync] SyncVar changed - applying snapshot");

            ApplySnapshot(next);
        }

        #endregion
    }
}