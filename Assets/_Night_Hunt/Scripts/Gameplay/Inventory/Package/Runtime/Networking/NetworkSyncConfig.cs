using UnityEngine;
using NightHunt.Inventory.Core;

namespace NightHunt.Inventory.Networking
{
    /// <summary>
    /// Configuration for network sync system.
    /// </summary>
    [CreateAssetMenu(fileName = "NetworkSyncConfig", menuName = "Inventory/NetworkSyncConfig")]
    public class NetworkSyncConfig : ScriptableObject
    {
        [Tooltip("Send delta sync on every change")]
        public bool useDeltaSync = true;
        
        [Tooltip("Send full sync every N delta syncs (anti-cheat)")]
        public int fullSyncInterval = 5;
        
        [Tooltip("Compress snapshot data")]
        public bool useCompression = true;
    }
}
