using UnityEngine;
using FishNet.Managing;

namespace NightHunt.Networking
{
    /// <summary>
    /// Centralized server authority manager
    /// Ensures all server logic is separated and can work with both host and dedicated server
    /// </summary>
    public class ServerAuthorityManager : MonoBehaviour
    {
        private static ServerAuthorityManager _instance;
        public static ServerAuthorityManager Instance => _instance;

        [Header("Server Settings")]
        [SerializeField] private bool isDedicatedServer = false;
        [SerializeField] private bool enableServerLogging = true;

        private NetworkManager networkManager;

        public bool IsDedicatedServer => isDedicatedServer;
        public bool IsServerRunning => networkManager != null && networkManager.IsServerStarted;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            networkManager = FindObjectOfType<NetworkManager>();
            
            // Detect if running as dedicated server
            #if UNITY_SERVER || UNITY_EDITOR_LINUX || UNITY_EDITOR_OSX
            isDedicatedServer = true;
            #endif
        }

        private void Start()
        {
            // Check if server started
            if (networkManager != null && networkManager.IsServerStarted)
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
            if (_instance != null)
            {
                return _instance.IsServerRunning;
            }
            
            var networkManager = FindObjectOfType<NetworkManager>();
            return networkManager != null && networkManager.IsServerStarted;
        }

        /// <summary>
        /// Check if current instance is client-only (not host)
        /// </summary>
        public static bool IsClientOnly()
        {
            if (_instance != null)
            {
                return !_instance.IsDedicatedServer && !_instance.IsServerRunning;
            }
            
            var networkManager = FindObjectOfType<NetworkManager>();
            return networkManager != null && networkManager.IsClientStarted && !networkManager.IsServerStarted;
        }
    }
}

