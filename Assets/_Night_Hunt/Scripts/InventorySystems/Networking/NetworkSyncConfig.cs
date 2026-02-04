using UnityEngine;

namespace NightHunt.Inventory.Networking
{
    /// <summary>
    /// Configuration for network synchronization.
    /// </summary>
    [CreateAssetMenu(fileName = "NetworkSyncConfig", menuName = "NightHunt/Inventory/Network Sync Config")]
    public class NetworkSyncConfig : ScriptableObject
    {
        [Header("Sync Strategy")] [Tooltip("Send delta sync on every change")]
        public bool useDeltaSync = true;

        [Tooltip("Send full sync every N delta syncs (anti-cheat)")]
        public int fullSyncInterval = 5;

        [Header("Optimization")] [Tooltip("Compress snapshot data")]
        public bool useCompression = true;

        [Header("Client Prediction")] [Tooltip("Enable optimistic updates on client")]
        public bool enableClientPrediction = true;

        [Tooltip("Maximum pending operations before disabling prediction")]
        public int maxPendingOperations = 3;
    }
}

