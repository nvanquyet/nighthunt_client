using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using NightHunt.Inventory.Core;
using NightHunt.Inventory.Domain;
using NightHunt.Inventory.Events;

namespace NightHunt.Inventory.Networking
{
    /// <summary>
    /// Network sync for inventory using FishNet.
    /// Hybrid sync: Delta + Periodic Full (anti-cheat).
    /// </summary>
    public class InventoryNetworkSync : NetworkBehaviour
    {
        [SerializeField] private NetworkSyncConfig config;
        private InventoryManager inventoryManager;
        private int deltaSyncCounter = 0;
        
        void Awake()
        {
            // Use ComponentFinder to find InventoryManager in hierarchy
            inventoryManager = ComponentFinder.FindInHierarchy<InventoryManager>(this);
        }
        
        void OnEnable()
        {
            InventoryEvents.OnInventoryChanged += OnInventoryChanged;
        }
        
        void OnDisable()
        {
            InventoryEvents.OnInventoryChanged -= OnInventoryChanged;
        }
        
        void OnInventoryChanged(InventorySnapshot snapshot)
        {
            if (!IsServer) return;
            
            if (config.useDeltaSync && deltaSyncCounter < config.fullSyncInterval)
            {
                // Send delta
                var deltaData = snapshot.Serialize();
                if (config.useCompression)
                {
                    deltaData = Compress(deltaData);
                }
                
                SyncDeltaObserversRpc(deltaData);
                deltaSyncCounter++;
            }
            else
            {
                // Send full sync
                var snapshotData = snapshot.Serialize();
                if (config.useCompression)
                {
                    snapshotData = Compress(snapshotData);
                }
                
                SyncFullSnapshotObserversRpc(snapshotData);
                deltaSyncCounter = 0; // Reset counter
            }
        }
        
        [ObserversRpc]
        void SyncDeltaObserversRpc(byte[] deltaData)
        {
            if (IsServer) return; // Don't apply on server
            
            if (config.useCompression)
            {
                deltaData = Decompress(deltaData);
            }
            
            var delta = InventorySnapshot.Deserialize(deltaData);
            if (inventoryManager != null)
            {
                inventoryManager.ApplySnapshot(delta);
            }
            
            // Refresh UI
            InventoryEvents.FireInventoryChanged(delta);
        }
        
        [ObserversRpc]
        void SyncFullSnapshotObserversRpc(byte[] snapshotData)
        {
            if (IsServer) return;
            
            if (config.useCompression)
            {
                snapshotData = Decompress(snapshotData);
            }
            
            var snapshot = InventorySnapshot.Deserialize(snapshotData);
            if (inventoryManager != null)
            {
                inventoryManager.RestoreSnapshot(snapshot);
            }
            
            // Refresh UI
            InventoryEvents.FireInventoryChanged(snapshot);
            
            // Anti-cheat: verify integrity
            // TODO: Implement integrity check
        }
        
        private byte[] Compress(byte[] data)
        {
            // TODO: Implement compression (GZip, LZ4, etc.)
            return data;
        }
        
        private byte[] Decompress(byte[] data)
        {
            // TODO: Implement decompression
            return data;
        }
    }
}
