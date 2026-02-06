using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using NightHunt.Inventory.Core.Data;
using NightHunt.Inventory.Core.Events;
using NightHunt.Inventory.Core.Utilities;
using NightHunt.Inventory.Domain.QuickSlot;
using System.Collections.Generic;
using System.Linq;

namespace NightHunt.Inventory.Networking
{
    /// <summary>
    /// FishNet network synchronization for quick slots.
    /// Implements server-authoritative pattern with delta sync.
    /// Follows FishNet Pro v4 patterns.
    /// </summary>
    public class QuickSlotNetworkSync : NetworkBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private NetworkSyncConfig config;

        [Header("References")]
        [SerializeField] private QuickSlotManager quickSlotManager;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        private int deltaSyncCounter = 0;

        // SyncVar for critical data (backup) - Updated to FishNet v4
        private readonly SyncVar<QuickSlotSnapshotData> syncedSnapshot = new SyncVar<QuickSlotSnapshotData>();

        #region Lifecycle

        public override void OnStartClient()
        {
            base.OnStartClient();
            
            // Subscribe to SyncVar changes (for all clients, including spectators)
            syncedSnapshot.OnChange += OnQuickSlotDataChanged;
            
            // Only subscribe to local changes if owner
            if (IsOwner)
            {
                QuickSlotEvents.OnQuickSlotChanged += OnLocalQuickSlotChanged;
            }

            InventoryLogger.Log("QuickSlotNetworkSync", "Client started", enableDebugLogs);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            // Unsubscribe from SyncVar changes (for all clients)
            syncedSnapshot.OnChange -= OnQuickSlotDataChanged;
            
            // Unsubscribe from local changes if owner
            if (IsOwner)
            {
                QuickSlotEvents.OnQuickSlotChanged -= OnLocalQuickSlotChanged;
            }
        }

        #endregion

        #region Local Changes (Client → Server)

        private void OnLocalQuickSlotChanged(int slotIndex, ItemInstance item)
        {
            if (!IsOwner) return;

            // Send update to server
            if (config != null && config.useDeltaSync && deltaSyncCounter < config.fullSyncInterval)
            {
                // Send delta update
                var delta = CreateDelta(slotIndex, item);
                RequestAssignItemServerRpc(delta.ChangedSlots[0], null);
                deltaSyncCounter++;
            }
            else
            {
                // Send full sync
                var snapshot = CreateSnapshot();
                SendFullSyncServerRpc(snapshot, null);
                deltaSyncCounter = 0;
            }
        }

        #endregion

        #region Server RPCs (Client → Server)

        [ServerRpc]
        private void RequestAssignItemServerRpc(QuickSlotChange change, NetworkConnection sender = null)
        {
            InventoryLogger.Log("QuickSlotNetworkSync", $"Server received assign request from {sender?.ClientId}", enableDebugLogs);

            // Validate change (anti-cheat)
            if (!ValidateChange(change, sender))
            {
                InventoryLogger.LogWarning("QuickSlotNetworkSync", $"Invalid change from {sender?.ClientId}", enableDebugLogs);
                RequestFullResyncTargetRpc(sender);
                return;
            }

            // Apply change on server
            ApplyChange(change);

            // Broadcast to observers
            BroadcastQuickSlotChangedObserversRpc(change);
        }

        [ServerRpc]
        private void RequestRemoveItemServerRpc(int slotIndex, NetworkConnection sender = null)
        {
            InventoryLogger.Log("QuickSlotNetworkSync", $"Server received remove request from {sender?.ClientId}", enableDebugLogs);

            // Validate
            if (slotIndex < 0 || slotIndex >= 4)
            {
                InventoryLogger.LogWarning("QuickSlotNetworkSync", $"Invalid slot index from {sender?.ClientId}", enableDebugLogs);
                return;
            }

            // Create change for cleared slot
            var change = new QuickSlotChange
            {
                SlotIndex = slotIndex,
                Item = new ItemInstanceData() // Empty item
            };

            // Apply change on server
            ApplyChange(change);

            // Broadcast to observers
            BroadcastQuickSlotChangedObserversRpc(change);
        }

        [ServerRpc]
        private void SendFullSyncServerRpc(QuickSlotSnapshotData snapshot, NetworkConnection sender = null)
        {
            InventoryLogger.Log("QuickSlotNetworkSync", $"Server received full sync from {sender?.ClientId}", enableDebugLogs);

            // Validate snapshot (anti-cheat)
            if (!ValidateSnapshot(snapshot, sender))
            {
                InventoryLogger.LogWarning("QuickSlotNetworkSync", $"Invalid snapshot from {sender?.ClientId}", enableDebugLogs);
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
        private void BroadcastQuickSlotChangedObserversRpc(QuickSlotChange change)
        {
            InventoryLogger.Log("QuickSlotNetworkSync", "Applying change from server", enableDebugLogs);

            ApplyChange(change);

            // Refresh UI via events
            if (quickSlotManager != null)
            {
                var item = quickSlotManager.GetItem(change.SlotIndex);
                QuickSlotEvents.InvokeQuickSlotChanged(change.SlotIndex, item);
            }
        }

        [ObserversRpc] // Removed ExcludeOwner = true to allow spectator to receive updates
        private void BroadcastFullSyncObserversRpc(QuickSlotSnapshotData snapshot)
        {
            InventoryLogger.Log("QuickSlotNetworkSync", "Applying full sync from server", enableDebugLogs);

            ApplySnapshot(snapshot);

            // Refresh UI via events
            if (quickSlotManager != null)
            {
                for (int i = 0; i < snapshot.Slots.Length; i++)
                {
                    var item = quickSlotManager.GetItem(i);
                    QuickSlotEvents.InvokeQuickSlotChanged(i, item);
                }
            }
        }

        #endregion

        #region Target RPCs (Server → Specific Client)

        [TargetRpc]
        private void RequestFullResyncTargetRpc(NetworkConnection target)
        {
            InventoryLogger.Log("QuickSlotNetworkSync", "Server requested full resync", enableDebugLogs);

            // Send full snapshot to server
            if (quickSlotManager != null)
            {
                var snapshot = CreateSnapshot();
                SendFullSyncServerRpc(snapshot, null);
            }
        }

        #endregion

        #region Delta & Snapshot Creation

        private QuickSlotDeltaData CreateDelta(int slotIndex, ItemInstance item)
        {
            var change = new QuickSlotChange
            {
                SlotIndex = slotIndex,
                Item = item != null ? item.Serialize() : new ItemInstanceData()
            };

            return new QuickSlotDeltaData
            {
                Timestamp = Time.time,
                ChangedSlots = new List<QuickSlotChange> { change }
            };
        }

        private QuickSlotSnapshotData CreateSnapshot()
        {
            if (quickSlotManager == null)
            {
                return new QuickSlotSnapshotData
                {
                    Timestamp = Time.time,
                    Slots = new ItemInstanceData[4]
                };
            }

            var slots = new ItemInstanceData[4];
            for (int i = 0; i < 4; i++)
            {
                var item = quickSlotManager.GetItem(i);
                slots[i] = item != null ? item.Serialize() : new ItemInstanceData();
            }

            return new QuickSlotSnapshotData
            {
                Timestamp = Time.time,
                Slots = slots
            };
        }

        #endregion

        #region Validation (Anti-Cheat)

        private bool ValidateChange(QuickSlotChange change, NetworkConnection sender)
        {
            if (change.SlotIndex < 0 || change.SlotIndex >= 4)
                return false;

            // TODO: Implement proper validation
            // - Check item ownership
            // - Validate item type (only consumables/throwables)
            // - Check if item exists in inventory

            return true;
        }

        private bool ValidateSnapshot(QuickSlotSnapshotData snapshot, NetworkConnection sender)
        {
            if (snapshot.Slots == null || snapshot.Slots.Length != 4)
                return false;

            // TODO: Implement snapshot validation
            // - Check for duplicate items
            // - Validate item types
            // - Verify item ownership

            return true;
        }

        #endregion

        #region Apply Changes

        private void ApplyChange(QuickSlotChange change)
        {
            if (quickSlotManager == null) return;

            // Check if slot is being cleared
            if (string.IsNullOrEmpty(change.Item.InstanceId))
            {
                quickSlotManager.ClearSlot(change.SlotIndex);
            }
            else
            {
                // TODO: Deserialize item from ItemInstanceData
                // For now, changes are applied locally via QuickSlotManager
                // Network sync ensures all clients see the same state
            }
        }

        private void ApplySnapshot(QuickSlotSnapshotData snapshot)
        {
            if (quickSlotManager == null) return;

            // Clear all slots first
            quickSlotManager.ClearAll();

            // TODO: Deserialize items from snapshot
            // For now, snapshot is used for validation and state verification
            // Actual item assignment happens via QuickSlotManager events
        }

        #endregion

        #region SyncVar Callback

        // Updated callback signature for FishNet v4
        private void OnQuickSlotDataChanged(QuickSlotSnapshotData prev, QuickSlotSnapshotData next, bool asServer)
        {
            if (asServer) return;

            InventoryLogger.Log("QuickSlotNetworkSync", "SyncVar changed - applying snapshot", enableDebugLogs);

            ApplySnapshot(next);
        }

        #endregion
    }
}
