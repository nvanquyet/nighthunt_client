using System.Collections.Generic;
using _Night_Hunt.Scripts.Gameplay.Character;
using FishNet.Connection;
using FishNet.Transporting;
using UnityEngine;

namespace _Night_Hunt.Scripts.Network.Core
{
    public class NightHuntNetworkManager: MonoBehaviour
    {
        public static NightHuntNetworkManager Instance { get; private set; }
        
        [SerializeField] private FishNet.Managing.NetworkManager fishNetManager;
        [SerializeField] private GameObject playerPrefab;
        
        private Dictionary<int, PlayerNetworkController> connectedPlayers = new Dictionary<int, PlayerNetworkController>();
        
        public bool IsServer => fishNetManager.IsServer;
        public bool IsClient => fishNetManager.IsClient;
        public bool IsHost => IsServer && IsClient;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            if (fishNetManager == null)
                fishNetManager = GetComponent<FishNet.Managing.NetworkManager>();
        }

        private void OnEnable()
        {
            if (fishNetManager != null)
            {
                fishNetManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
                fishNetManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
                fishNetManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
            }
        }

        private void OnDisable()
        {
            if (fishNetManager != null)
            {
                fishNetManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
                fishNetManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
                fishNetManager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
            }
        }

        #region Connection Management

        public void StartHost()
        {
            Debug.Log("[Network] Starting as Host...");
            fishNetManager.ServerManager.StartConnection();
            fishNetManager.ClientManager.StartConnection();
        }

        public void StartServer()
        {
            Debug.Log("[Network] Starting as Server...");
            fishNetManager.ServerManager.StartConnection();
        }

        public void StartClient()
        {
            Debug.Log("[Network] Starting as Client...");
            fishNetManager.ClientManager.StartConnection();
        }

        public void StopConnection()
        {
            Debug.Log("[Network] Stopping connection...");
            
            if (IsServer)
                fishNetManager.ServerManager.StopConnection(true);
            
            if (IsClient)
                fishNetManager.ClientManager.StopConnection();
        }

        #endregion

        #region Callbacks

        private void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            Debug.Log($"[Network] Server state: {args.ConnectionState}");
            
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                OnServerStarted();
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                OnServerStopped();
            }
        }

        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            Debug.Log($"[Network] Client state: {args.ConnectionState}");
            
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                OnClientConnected();
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                OnClientDisconnected();
            }
        }

        private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            Debug.Log($"[Network] Remote connection {conn.ClientId}: {args.ConnectionState}");
            
            if (args.ConnectionState == RemoteConnectionState.Started)
            {
                OnPlayerConnected(conn);
            }
            else if (args.ConnectionState == RemoteConnectionState.Stopped)
            {
                OnPlayerDisconnected(conn);
            }
        }

        #endregion

        #region Server Events

        private void OnServerStarted()
        {
            Debug.Log("[Network] Server started successfully");
        }

        private void OnServerStopped()
        {
            Debug.Log("[Network] Server stopped");
            connectedPlayers.Clear();
        }

        private void OnPlayerConnected(NetworkConnection conn)
        {
            Debug.Log($"[Network] Player {conn.ClientId} connected");
            
            // Spawn player on server
            if (IsServer)
            {
                SpawnPlayerForConnection(conn);
            }
        }

        private void OnPlayerDisconnected(NetworkConnection conn)
        {
            Debug.Log($"[Network] Player {conn.ClientId} disconnected");
            
            if (connectedPlayers.TryGetValue(conn.ClientId, out var player))
            {
                connectedPlayers.Remove(conn.ClientId);
                
                if (player != null)
                {
                    fishNetManager.ServerManager.Despawn(player.gameObject);
                }
            }
        }

        private void SpawnPlayerForConnection(NetworkConnection conn)
        {
            // Find spawn point
            Vector3 spawnPos = GetRandomSpawnPoint();
            Quaternion spawnRot = Quaternion.identity;
            
            // Spawn player object
            GameObject playerObj = Instantiate(playerPrefab, spawnPos, spawnRot);
            fishNetManager.ServerManager.Spawn(playerObj, conn);
            
            // Track player
            var playerController = playerObj.GetComponent<PlayerNetworkController>();
            if (playerController != null)
            {
                connectedPlayers[conn.ClientId] = playerController;
            }
            
            Debug.Log($"[Network] Spawned player for connection {conn.ClientId} at {spawnPos}");
        }

        #endregion

        #region Client Events

        private void OnClientConnected()
        {
            Debug.Log("[Network] Client connected to server");
        }

        private void OnClientDisconnected()
        {
            Debug.Log("[Network] Client disconnected from server");
        }

        #endregion

        #region Utility

        private Vector3 GetRandomSpawnPoint()
        {
            // TODO: Get from spawn manager
            float radius = 50f;
            Vector2 randomCircle = Random.insideUnitCircle * radius;
            return new Vector3(randomCircle.x, 0, randomCircle.y);
        }

        public PlayerNetworkController GetPlayer(int clientId)
        {
            return connectedPlayers.TryGetValue(clientId, out var player) ? player : null;
        }

        public IEnumerable<PlayerNetworkController> GetAllPlayers()
        {
            return connectedPlayers.Values;
        }

        #endregion
    }
}