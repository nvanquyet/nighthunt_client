using UnityEngine;
using FishNet.Object;
using FishNet.Managing;
using FishNet.Connection;
using FishNet.Transporting;
using FishNet;
using NightHunt.Gameplay.Spawn;
using NightHunt.Gameplay.Match;
using NightHunt.Gameplay.Core.Events;
using System.Collections.Generic;
using NightHunt.Networking.Player;

namespace NightHunt.Networking
{
    /// <summary>
    /// ServerGameManager - Server-authoritative orchestrator
    /// </summary>
    public class ServerGameManager : NetworkBehaviour
    {
        public static ServerGameManager Instance { get; private set; }
        
        [Header("Dependencies")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private SpawnSystem _spawnSystem;
        [SerializeField] private MatchPhaseManager _matchPhaseManager;
        [SerializeField] private ClientNetworkHandler clientNetworkHandlerPrefab;

        [Header("Match Settings")]
        [Tooltip("Total players expected before starting Phase 1 (set by backend/room config).")]
        [SerializeField] private int _expectedPlayerCount = 2;
        
        private RegistryService _registryService;
        private NetworkManager _networkManager;
        private int _spawnedPlayerCount = 0;
        
        // Tracking
        private Dictionary<int, GameObject> _spawnedPlayers = new(); // FishNet ClientId → GameObject

        /// <summary>Fired on server when all expected players have spawned.</summary>
        public event System.Action OnAllPlayersReady;

        /// <summary>Call this from backend/room flow to set how many players to wait for.</summary>
        public void SetExpectedPlayerCount(int count) => _expectedPlayerCount = count;
        
        // ===== LIFECYCLE =====
        
        private void Awake()
        {
            Instance = this;
        }
        
        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            
            if (!IsServerStarted) return;
            
            Initialize();
        }
        
        [Server]
        private void Initialize()
        {
            _networkManager = InstanceFinder.NetworkManager;
            
            if (_networkManager == null)
            {
                Debug.LogError("[ServerGameManager] NetworkManager not found!");
                return;
            }
            
            if (playerPrefab == null)
            {
                Debug.LogError("[ServerGameManager] Player prefab is missing!");
                return;
            }
            
            // Get RegistryService
            _registryService = RegistryService.Instance;
            
            if (_registryService == null)
            {
                Debug.LogError("[ServerGameManager] RegistryService not found!");
                return;
            }
            
            // Subscribe to connection events
            _networkManager.ServerManager.OnRemoteConnectionState += OnServerConnectionState;
            
            Debug.Log("[ServerGameManager] ✅ Initialized");
        }
        
        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            
            if (_networkManager != null && _networkManager.ServerManager != null)
            {
                _networkManager.ServerManager.OnRemoteConnectionState -= OnServerConnectionState;
            }
        }
        
        // ===== CONNECTION EVENTS =====
        
        [Server]
        private void OnServerConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            switch (args.ConnectionState)
            {
                case RemoteConnectionState.Started:
                    OnPlayerConnected(conn);
                    break;
                    
                case RemoteConnectionState.Stopped:
                    OnPlayerDisconnected(conn);
                    break;
            }
        }
        
        [Server]
        private void OnPlayerConnected(NetworkConnection conn)
        {
            int fishnetClientId = conn.ClientId;
            Debug.Log($"[ServerGameManager] Player connected - FishNet ClientId: {fishnetClientId}");
            
            //Spawn ClientNetworkHandler cho client này
            ClientNetworkHandler cnh = Instantiate(clientNetworkHandlerPrefab);
            _networkManager.ServerManager.Spawn(cnh.gameObject, conn);
            
            // ClientNetworkHandler sẽ tự động gửi data lên
            // Server chờ nhận data rồi mới spawn
        }
        
        /// <summary>
        /// Server: Nhận data từ client (called by ClientNetworkHandler)
        /// </summary>
        [Server]
        public void OnClientDataReceived(NetworkConnection conn, PlayerRegistryData clientData)
        {
            int fishnetClientId = conn.ClientId;
            
            Debug.Log($"[ServerGameManager] Received client data - FishNet ID: {fishnetClientId}, Backend ID: {clientData.BackendPlayerId}, Name: {clientData.DisplayName}");
            
            // Data validation is the client's responsibility via RpcSendPlayerData / JWT.
            // Server-side anti-cheat can be layered here in a future pass.

            // Spawn player
            SpawnPlayerWorkflow(conn, clientData);
        }
        
        // ===== SPAWN WORKFLOW =====
        
        [Server]
        private void SpawnPlayerWorkflow(NetworkConnection conn, PlayerRegistryData clientData)
        {
            int fishnetClientId = conn.ClientId;
            
            Debug.Log($"[ServerGameManager] === Starting spawn workflow for ClientId: {fishnetClientId} ===");
            
            // STEP 1: Instantiate prefab
            GameObject playerObj = Instantiate(playerPrefab);
            
            if (playerObj == null)
            {
                Debug.LogError($"[ServerGameManager] Failed to instantiate player prefab!");
                return;
            }
            
            NetworkPlayer networkPlayer = playerObj.GetComponent<NetworkPlayer>();
            
            if (networkPlayer == null)
            {
                Debug.LogError($"[ServerGameManager] Player prefab missing NetworkPlayer component!");
                Destroy(playerObj);
                return;
            }
            
            Debug.Log($"[ServerGameManager] Step 1: Prefab instantiated");
            
            // STEP 2: SpawnSystem xử lý (assign team, position)
            PlayerRegistryData serverData;
            if (_spawnSystem != null)
            {
                serverData = _spawnSystem.ProcessSpawn(playerObj, conn, clientData);
                Debug.Log($"[ServerGameManager] Step 2: SpawnSystem processed - Team: {serverData.TeamId}");
            }
            else
            {
                Debug.LogWarning("[ServerGameManager] _spawnSystem is null – spawning at origin. Assign SpawnSystem in Inspector.");
                serverData = clientData;
                playerObj.transform.position = Vector3.zero;
            }
            
            // STEP 3: Network spawn FIRST — FishNet includes the current SyncVar
            // values in the spawn packet it sends to all observers.  If we call
            // SetPublicData BEFORE Spawn(), the SyncVar write happens on an
            // unspawned object and FishNet may not pick it up, meaning every
            // client (especially late-joiners whose spawn packet is sent during
            // FishNet's initial-state reconciliation) receives an empty struct.
            NetworkObject netObj = playerObj.GetComponent<NetworkObject>();
            
            if (netObj == null)
            {
                Debug.LogError($"[ServerGameManager] Player prefab missing NetworkObject component!");
                Destroy(playerObj);
                return;
            }
            
            _networkManager.ServerManager.Spawn(netObj, conn);
            
            Debug.Log($"[ServerGameManager] Step 3: Network spawned");
            
            // STEP 4: Set PUBLIC data AFTER spawn so the SyncVar change is
            // broadcast via the normal dirty-sync channel to all current observers,
            // AND will be included in the spawn packet for any client that connects
            // while this object is alive (FishNet re-serialises all SyncVars into
            // every new observer's spawn packet using the current value at the time
            // of connection).
            PlayerPublicData publicData = PlayerPublicData.FromRegistryData(serverData);
            networkPlayer.SetPublicData(publicData);
            
            Debug.Log($"[ServerGameManager] Step 4: Public data set");
            
            // STEP 5: Register với RegistryService (lưu PRIVATE data)
            _registryService.RegisterPlayer(networkPlayer, serverData);
            
            Debug.Log($"[ServerGameManager] Step 5: Registered with RegistryService");
            
            // STEP 6: Track
            _spawnedPlayers[fishnetClientId] = playerObj;
            networkPlayer.SetAlive(true);
            _spawnedPlayerCount++;
            
            Debug.Log($"[ServerGameManager] === ✅ Spawn complete ({_spawnedPlayerCount}/{_expectedPlayerCount}) - {serverData.DisplayName}, Team: {serverData.TeamId} ===");

            // STEP 7: Check if all expected players have spawned
            if (_spawnedPlayerCount >= _expectedPlayerCount)
            {
                OnAllPlayersSpawned();
            }
        }

        [Server]
        private void OnAllPlayersSpawned()
        {
            Debug.Log("[ServerGameManager] ✅ All players spawned — starting match!");
            OnAllPlayersReady?.Invoke();

            // Notify all clients: hide loading screen, show game HUD
            RpcOnAllPlayersReady();

            // Start Phase 1
            if (_matchPhaseManager != null)
                _matchPhaseManager.StartPhase(NightHunt.Gameplay.Match.MatchPhaseState.Preparation);
            else
                Debug.LogError("[ServerGameManager] MatchPhaseManager is null — Phase 1 not started!");
        }

        [ObserversRpc]
        private void RpcOnAllPlayersReady()
        {
            Debug.Log("[ServerGameManager] CLIENT: All players ready — dismissing loading screen.");
            GameplayEventBus.Instance?.Publish(new AllPlayersReadyEvent());
        }
        
        // ===== DISCONNECT HANDLING =====
        
        [Server]
        private void OnPlayerDisconnected(NetworkConnection conn)
        {
            int fishnetClientId = conn.ClientId;
            
            Debug.Log($"[ServerGameManager] Player disconnecting - FishNet ClientId: {fishnetClientId}");
            
            // Get player object
            if (!_spawnedPlayers.TryGetValue(fishnetClientId, out GameObject playerObj))
            {
                Debug.LogWarning($"[ServerGameManager] No spawned player for ClientId: {fishnetClientId}");
                return;
            }
            
            NetworkPlayer networkPlayer = playerObj.GetComponent<NetworkPlayer>();
            
            if (networkPlayer == null)
            {
                Debug.LogError($"[ServerGameManager] NetworkPlayer component missing!");
                return;
            }
            
            string backendId = _registryService.GetBackendIdByFishNetId(fishnetClientId);
            
            Debug.Log($"[ServerGameManager] Cleaning up - Backend ID: {backendId}, Name: {networkPlayer.DisplayName}");
            
            // Unregister (RegistryService lưu data cho reconnect)
            _registryService.UnregisterPlayer(networkPlayer);
            
            // SpawnSystem cleanup
            if (_spawnSystem != null)
                _spawnSystem.OnPlayerDisconnected(fishnetClientId);
            
            // Despawn
            _networkManager.ServerManager.Despawn(playerObj);
            
            // Remove tracking
            _spawnedPlayers.Remove(fishnetClientId);
            
            Debug.Log($"[ServerGameManager] ✅ Cleanup complete for ClientId: {fishnetClientId}");
        }
    }
}