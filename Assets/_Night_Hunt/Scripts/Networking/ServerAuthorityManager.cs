using FishNet;
using NightHunt.Core;
using UnityEngine;

namespace NightHunt.Networking
{
    /// <summary>
    /// Centralized server authority manager
    /// Ensures all server logic is separated and can work with both host and dedicated server
    /// </summary>
    public class ServerAuthorityManager : SingletonPersistent<ServerAuthorityManager>
    {

        [Header("Server Settings")]
        [SerializeField] private bool isDedicatedServer = false;
        [SerializeField] private bool enableServerLogging = true;

        public bool IsDedicatedServer => isDedicatedServer;
        public bool IsServerRunning => InstanceFinder.NetworkManager != null && InstanceFinder.NetworkManager.IsServerStarted;

        protected override void OnSingletonAwake()
        {
            // Detect if running as dedicated server
            #if UNITY_SERVER || UNITY_EDITOR_LINUX || UNITY_EDITOR_OSX
            isDedicatedServer = true;
            #endif
        }

        private void Start()
        {
            // Check if server started
            if (InstanceFinder.NetworkManager != null && InstanceFinder.NetworkManager.IsServerStarted)
            {
                if (enableServerLogging)
                {
                    Debug.Log($"[ServerAuthorityManager] Server started - Dedicated: {isDedicatedServer}");
                }

                // Initialize server-only systems
                InitializeServerSystems();
            }
        }

        /// <summary>
        /// Initialize all server-only systems
        /// This ensures they work on both host and dedicated server
        /// </summary>
        private void InitializeServerSystems()
        {
            // All server systems should be initialized here
            // This ensures they work regardless of host or dedicated server mode
        }

        /// <summary>
        /// Check if current instance is server (host or dedicated)
        /// </summary>
        public static bool IsServer()
        {
            return InstanceFinder.NetworkManager != null && InstanceFinder.NetworkManager.IsServerStarted;
        }

        /// <summary>
        /// Check if current instance is client-only (not host)
        /// </summary>
        public static bool IsClientOnly()
        {
            var nm = InstanceFinder.NetworkManager;
            return nm != null && nm.IsClientStarted && !nm.IsServerStarted;
        }
    }
}

