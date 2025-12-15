using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace NightHunt.Networking
{
    /// <summary>
    /// Network Game Manager - Quản lý Server và Client
    /// Không còn Host mode - chỉ có Server và Client riêng biệt
    /// </summary>
    public class NetworkGameManager : MonoBehaviour
    {
        private static NetworkGameManager _instance;
        public static NetworkGameManager Instance => _instance;

        [Header("Network Manager Reference")]
        [SerializeField] private NetworkManager networkManager;

        [Header("Network Settings")]
        [SerializeField] private ushort port = 7777;
        [SerializeField] private string defaultServerAddress = "localhost";

        public NetworkManager NetworkManager => networkManager;
        public bool IsServer => networkManager != null && networkManager.IsServerStarted;
        public bool IsClient => networkManager != null && networkManager.IsClientStarted;

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

            if (networkManager == null)
            {
                networkManager = FindFirstObjectByType<NetworkManager>();
                if (networkManager == null)
                {
                    Debug.LogError("[NetworkGameManager] NetworkManager not found! Please add NetworkManager to scene.");
                }
            }
        }

        /// <summary>
        /// Start Server only (dedicated server)
        /// Hiện tại có UI, sau này sẽ chuyển sang headless server
        /// </summary>
        public void StartServer()
        {
            if (networkManager == null)
            {
                Debug.LogError("[NetworkGameManager] NetworkManager is null!");
                return;
            }

            Debug.Log($"[NetworkGameManager] Starting Dedicated Server on port {port}...");

            if (!networkManager.ServerManager.StartConnection())
            {
                Debug.LogError("[NetworkGameManager] Failed to start server!");
            }
            else
            {
                Debug.Log("[NetworkGameManager] Dedicated Server started successfully!");
            }
        }

        /// <summary>
        /// Start Client và kết nối tới server
        /// </summary>
        /// <param name="serverAddress">Địa chỉ server (null = dùng defaultServerAddress)</param>
        public void StartClient(string serverAddress = null)
        {
            if (networkManager == null)
            {
                Debug.LogError("[NetworkGameManager] NetworkManager is null!");
                return;
            }

            string targetAddress = serverAddress ?? defaultServerAddress;
            Debug.Log($"[NetworkGameManager] Starting Client, connecting to {targetAddress}:{port}...");

            // Set transport address and port
            SetTransportAddress(targetAddress, port);

            // Start client connection
            if (!networkManager.ClientManager.StartConnection())
            {
                Debug.LogError("[NetworkGameManager] Failed to start client connection!");
            }
            else
            {
                Debug.Log("[NetworkGameManager] Client connection started!");
            }
        }

        /// <summary>
        /// Set transport address and port
        /// </summary>
        private void SetTransportAddress(string address, ushort port)
        {
            var transport = networkManager.TransportManager.Transport;
            if (transport == null)
            {
                Debug.LogWarning("[NetworkGameManager] Transport is null!");
                return;
            }

            var transportType = transport.GetType();
            
            // Try to set ClientAddress property (Tugboat, etc.)
            try
            {
                var addressProperty = transportType.GetProperty("ClientAddress");
                if (addressProperty != null && addressProperty.CanWrite)
                {
                    addressProperty.SetValue(transport, address);
                    Debug.Log($"[NetworkGameManager] Set client address to: {address}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[NetworkGameManager] Could not set ClientAddress: {e.Message}");
            }

            // Try to set Port property
            try
            {
                var portProperty = transportType.GetProperty("Port");
                if (portProperty != null && portProperty.CanWrite)
                {
                    portProperty.SetValue(transport, port);
                    Debug.Log($"[NetworkGameManager] Set port to: {port}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[NetworkGameManager] Could not set Port: {e.Message}");
            }
        }

        /// <summary>
        /// Disconnect from network
        /// </summary>
        public void Disconnect()
        {
            if (networkManager == null) return;

            Debug.Log("[NetworkGameManager] Disconnecting...");

            if (IsServer)
            {
                networkManager.ServerManager.StopConnection(true);
            }

            if (IsClient)
            {
                networkManager.ClientManager.StopConnection();
            }
        }

        /// <summary>
        /// Get current player count
        /// </summary>
        public int GetPlayerCount()
        {
            if (networkManager?.ServerManager == null) return 0;
            return networkManager.ServerManager.Clients.Count;
        }
    }
}
