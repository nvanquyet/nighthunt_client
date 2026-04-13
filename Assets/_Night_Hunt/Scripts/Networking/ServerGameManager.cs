using UnityEngine;
using FishNet.Object;
using FishNet.Managing;
using FishNet.Connection;
using FishNet.Transporting;
using FishNet;
using NightHunt.Gameplay.Spawn;
using NightHunt.Gameplay.Match;
using NightHunt.Gameplay.Core.Events;
using NightHunt.Gameplay.Character;
using System.Collections.Generic;
using NightHunt.Networking.Player;
using NightHunt.Utilities;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.Config;
using NightHunt.State;

namespace NightHunt.Networking
{
    /// <summary>
    /// ServerGameManager - Server-authoritative orchestrator
    /// </summary>
    public class ServerGameManager : NetworkBehaviour
    {
        public static ServerGameManager Instance { get; private set; }

        [Header("Dependencies")] [SerializeField]
        private GameObject playerPrefab;

        [SerializeField] private SpawnSystem _spawnSystem;
        [SerializeField] private MatchPhaseManager _matchPhaseManager;
        [SerializeField] private ClientNetworkHandler clientNetworkHandlerPrefab;

        [Header("Match Settings")]
        [Tooltip("Total players expected before starting Phase 1.\n" +
                 "Tự động resolve từ RoomState.PlayerCount (Ranked) hoặc GameModeConfig khi OnStartNetwork.\n" +
                 "Chỉ cần đặt thủ công khi test trong Editor (dev mode, GameMode.None).")]
        [SerializeField]
        private int _expectedPlayerCount = 2;
        [Header("Debug")] [SerializeField] private NightHuntDebugConfig _debugConfig;

        private RegistryService _registryService;
        private NetworkManager _networkManager;
        private int _spawnedPlayerCount = 0;

        // Tracking
        private Dictionary<int, GameObject> _spawnedPlayers = new(); // FishNet ClientId â†’ GameObject

        /// <summary>Fired on server when all expected players have spawned.</summary>
        public event System.Action OnAllPlayersReady;

        /// <summary>Call this from backend/room flow to set how many players to wait for.</summary>
        public void SetExpectedPlayerCount(int count) => _expectedPlayerCount = count;

        // ===== LIFECYCLE =====

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
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

            // Auto-resolve expected player count from RoomState (set by backend match_ready WS).
            // Priority: RoomState.PlayerCount (Ranked_DS, most accurate)
            //           → GameModeConfig lookup by modeKey
            //           → keep Inspector value (dev/editor fallback)
            ResolveExpectedPlayerCount();

            if (_debugConfig != null && _debugConfig.EnableNetworkDebugLogs)
                Debug.Log("[ServerGameManager] ✅ Initialized");
        }

        [Server]
        private void ResolveExpectedPlayerCount()
        {
            // 1. Ranked DS: room already has all slots filled → use PlayerCount directly
            var roomState = RoomState.Instance;
            if (roomState != null && roomState.CurrentGameMode == GameMode.Ranked_DS
                && roomState.PlayerCount > 0)
            {
                _expectedPlayerCount = roomState.PlayerCount;
                if (_debugConfig != null && _debugConfig.EnableNetworkDebugLogs)
                    Debug.Log($"[ServerGameManager] ExpectedPlayerCount resolved from RoomState: {_expectedPlayerCount}");
                return;
            }

            // 2. ServerBootstrap CLI arg (--expectedPlayers).
            //    RoomState is always empty on a headless dedicated server — the DS doesn't
            //    have a WebSocket connection to the backend, so match_ready never populates it.
            //    ServerBootstrap.BootstrappedExpectedPlayers > 0 means the arg was passed.
            if (NightHunt.Server.ServerBootstrap.BootstrappedExpectedPlayers > 0)
            {
                _expectedPlayerCount = NightHunt.Server.ServerBootstrap.BootstrappedExpectedPlayers;
                if (_debugConfig != null && _debugConfig.EnableNetworkDebugLogs)
                    Debug.Log($"[ServerGameManager] ExpectedPlayerCount resolved from ServerBootstrap.BootstrappedExpectedPlayers: {_expectedPlayerCount}");
                return;
            }

            // 3. Fallback: resolve via GameModeConfig using room's mode key (field name is "mode" in RoomResponse)
            string modeKey = roomState?.CurrentRoom?.mode;
            if (!string.IsNullOrEmpty(modeKey))
            {
                // Try allowFill=true first, then false
                if (GameModeConfig.TryGetByKey(modeKey, true, out GameModeEntry entry)
                    || GameModeConfig.TryGetByKey(modeKey, false, out entry))
                {
                    _expectedPlayerCount = entry.playersPerTeam * 2;
                    if (_debugConfig != null && _debugConfig.EnableNetworkDebugLogs)
                        Debug.Log($"[ServerGameManager] ExpectedPlayerCount resolved from GameModeConfig ({modeKey}): {_expectedPlayerCount}");
                    return;
                }
            }

            // 4. Keep Inspector value — dev mode or no data available
            if (_debugConfig != null && _debugConfig.EnableNetworkDebugLogs)
                Debug.LogWarning($"[ServerGameManager] ExpectedPlayerCount using Inspector value: {_expectedPlayerCount}. " +
                                 "Ensure RoomState is populated or --expectedPlayers is passed before scene load in production.");
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
                    // BUG 1 FIX: Wait until connection has loaded start scenes before spawning.
                    // Spawning before start scenes are loaded causes the FishNet warning:
                    // "spawned but it's recommended to not spawn objects for connections
                    //  until they have loaded start scenes."
                    conn.OnLoadedStartScenes += OnConnectionLoadedStartScenes;
                    break;

                case RemoteConnectionState.Stopped:
                    OnPlayerDisconnected(conn);
                    break;
            }
        }

        private void OnConnectionLoadedStartScenes(NetworkConnection conn, bool asServer)
        {
            conn.OnLoadedStartScenes -= OnConnectionLoadedStartScenes;
            OnPlayerConnected(conn);
        }

        [Server]
        private void OnPlayerConnected(NetworkConnection conn)
        {
            int fishnetClientId = conn.ClientId;
            if (_debugConfig != null && _debugConfig.EnableNetworkDebugLogs)
                Debug.Log($"[ServerGameManager] Player connected - FishNet ClientId: {fishnetClientId}");

            //Spawn ClientNetworkHandler cho client nÃ y
            ClientNetworkHandler cnh = Instantiate(clientNetworkHandlerPrefab);
            _networkManager.ServerManager.Spawn(cnh.gameObject, conn);

            // ClientNetworkHandler sáº½ tá»± Ä‘á»™ng gá»­i data lÃªn
            // Server chá» nháº­n data rá»“i má»›i spawn
        }

        /// <summary>
        /// Server: Nháº­n data tá»« client (called by ClientNetworkHandler)
        /// </summary>
        [Server]
        public void OnClientDataReceived(NetworkConnection conn, PlayerRegistryData clientData)
        {
            int fishnetClientId = conn.ClientId;

            if (_debugConfig != null && _debugConfig.EnableNetworkDebugLogs)
                Debug.Log(
                $"[ServerGameManager] Received client data - FishNet ID: {fishnetClientId}, Backend ID: {clientData.BackendPlayerId}, Name: {clientData.DisplayName}");

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

            if (_debugConfig != null && _debugConfig.EnableNetworkDebugLogs)
                Debug.Log($"[ServerGameManager] === Starting spawn workflow for ClientId: {fishnetClientId} ===");

            // STEP 1: Instantiate prefab
            GameObject playerObj = Instantiate(playerPrefab);

            if (playerObj == null)
            {
                Debug.LogError($"[ServerGameManager] Failed to instantiate player prefab!");
                return;
            }

            NetworkPlayer networkPlayer = ComponentResolver.Find<NetworkPlayer>(playerObj)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] NetworkPlayer not found")
                .Resolve();

            if (networkPlayer == null)
            {
                Debug.LogError($"[ServerGameManager] Player prefab missing NetworkPlayer component!");
                Destroy(playerObj);
                return;
            }

            if (_debugConfig != null && _debugConfig.EnableNetworkDebugLogs)
                Debug.Log($"[ServerGameManager] Step 1: Prefab instantiated");

            // STEP 2: SpawnSystem xá»­ lÃ½ (assign team, position)
            PlayerRegistryData serverData;
            if (_spawnSystem != null)
            {
                serverData = _spawnSystem.ProcessSpawn(playerObj, conn, clientData);
                if (_debugConfig != null && _debugConfig.EnableNetworkDebugLogs)
                    Debug.Log($"[ServerGameManager] Step 2: SpawnSystem processed - Team: {serverData.TeamId}");
            }
            else
            {
                Debug.LogWarning(
                    "[ServerGameManager] _spawnSystem is null â€“ spawning at origin. Assign SpawnSystem in Inspector.");
                serverData = clientData;
                playerObj.transform.position = Vector3.zero;
            }

            // STEP 3: Network spawn FIRST â€” FishNet includes the current SyncVar
            // values in the spawn packet it sends to all observers.  If we call
            // SetPublicData BEFORE Spawn(), the SyncVar write happens on an
            // unspawned object and FishNet may not pick it up, meaning every
            // client (especially late-joiners whose spawn packet is sent during
            // FishNet's initial-state reconciliation) receives an empty struct.
            NetworkObject netObj = ComponentResolver.Find<NetworkObject>(playerObj)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] NetworkObject not found")
                .Resolve();

            if (netObj == null)
            {
                Debug.LogError($"[ServerGameManager] Player prefab missing NetworkObject component!");
                Destroy(playerObj);
                return;
            }

            _networkManager.ServerManager.Spawn(netObj, conn);

            if (_debugConfig != null && _debugConfig.EnableNetworkDebugLogs)
                Debug.Log($"[ServerGameManager] Step 3: Network spawned");

            // STEP 4: Set PUBLIC data AFTER spawn so the SyncVar change is
            // broadcast via the normal dirty-sync channel to all current observers,
            // AND will be included in the spawn packet for any client that connects
            // while this object is alive (FishNet re-serialises all SyncVars into
            // every new observer's spawn packet using the current value at the time
            // of connection).
            PlayerPublicData publicData = PlayerPublicData.FromRegistryData(serverData);
            networkPlayer.SetPublicData(publicData);

            if (_debugConfig != null && _debugConfig.EnableNetworkDebugLogs)
                Debug.Log($"[ServerGameManager] Step 4: Public data set");

            // STEP 4b: Inform PlayerModelLoader of the chosen character skin.
            // SetModelIndex replicates via its own SyncVar so every client (including
            // late-joiners) instantiates the correct model under the Model child.
            PlayerModelLoader modelLoader = ComponentResolver.Find<PlayerModelLoader>(playerObj)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] PlayerModelLoader not found")
                .Resolve();
            if (modelLoader != null)
                modelLoader.SetModelIndex(serverData.CharacterModelIndex);
            else
                Debug.LogWarning("[ServerGameManager] PlayerModelLoader not found on player prefab. " +
                                 "Add PlayerModelLoader to the root PlayerPrefab.");

            if (_debugConfig != null && _debugConfig.EnableNetworkDebugLogs)
                Debug.Log($"[ServerGameManager] Step 4b: ModelIndex set to {serverData.CharacterModelIndex}");

            // STEP 5: Register vá»›i RegistryService (lÆ°u PRIVATE data)
            _registryService.RegisterPlayer(networkPlayer, serverData);

            if (_debugConfig != null && _debugConfig.EnableNetworkDebugLogs)
                Debug.Log($"[ServerGameManager] Step 5: Registered with RegistryService");

            // STEP 6: Track
            _spawnedPlayers[fishnetClientId] = playerObj;
            networkPlayer.SetAlive(true);
            _spawnedPlayerCount++;

            // Notify all clients to enter "Spawning" stage on first spawn
            if (_spawnedPlayerCount == 1)
            {
                RpcOnSpawningStarted();
            }

            // Notify all clients of per-player spawn progress so the loading overlay
            // can show "N / M players ready" in real-time.
            RpcOnPlayerSpawned(serverData.DisplayName, _spawnedPlayerCount, _expectedPlayerCount);

            if (_debugConfig != null && _debugConfig.EnableNetworkDebugLogs)
                Debug.Log(
                $"[ServerGameManager] === âœ… Spawn complete ({_spawnedPlayerCount}/{_expectedPlayerCount}) - {serverData.DisplayName}, Team: {serverData.TeamId} ===");

            // STEP 7: Check if all expected players have spawned
            if (_spawnedPlayerCount >= _expectedPlayerCount)
            {
                OnAllPlayersSpawned();
            }
        }

        [Server]
        private void OnAllPlayersSpawned()
        {
            if (_debugConfig != null && _debugConfig.EnableNetworkDebugLogs)
                Debug.Log("[ServerGameManager] âœ… All players spawned â€” starting match!");
            OnAllPlayersReady?.Invoke();

            // Notify all clients: hide loading screen, show game HUD
            RpcOnAllPlayersReady();

            // Start first phase with countdown delay
            if (_matchPhaseManager != null)
                _matchPhaseManager.BeginMatch();
            else
                Debug.LogError("[ServerGameManager] MatchPhaseManager is null — BeginMatch not called!");
        }

        [ObserversRpc]
        private void RpcOnSpawningStarted()
        {
            if (_debugConfig != null && _debugConfig.EnableNetworkDebugLogs)
                Debug.Log("[ServerGameManager] CLIENT: First player spawned — advancing to Spawning stage.");
            GameplayEventBus.Instance?.Publish(new SpawningStartedEvent());
        }

        /// <summary>
        /// Broadcast to all clients every time any player finishes spawning.
        /// MatchLoadingOverlay subscribes to <see cref="PlayerSpawnedEvent"/> (published here)
        /// to update the "N / M players ready" counter in real-time.
        /// </summary>
        [ObserversRpc]
        private void RpcOnPlayerSpawned(string displayName, int spawnedCount, int expectedCount)
        {
            if (_debugConfig != null && _debugConfig.EnableNetworkDebugLogs)
                Debug.Log($"[ServerGameManager] CLIENT: Player spawned — {displayName} ({spawnedCount}/{expectedCount})");
            GameplayEventBus.Instance?.Publish(new PlayerSpawnedEvent
            {
                DisplayName   = displayName,
                SpawnedCount  = spawnedCount,
                ExpectedCount = expectedCount
            });
        }

        [ObserversRpc]
        private void RpcOnAllPlayersReady()
        {
            if (_debugConfig != null && _debugConfig.EnableNetworkDebugLogs)
                Debug.Log("[ServerGameManager] CLIENT: All players ready — dismissing loading screen.");
            GameplayEventBus.Instance?.Publish(new AllPlayersReadyEvent());
        }

        // ===== DISCONNECT HANDLING =====

        [Server]
        private void OnPlayerDisconnected(NetworkConnection conn)
        {
            int fishnetClientId = conn.ClientId;

            if (_debugConfig != null && _debugConfig.EnableNetworkDebugLogs)
                Debug.Log($"[ServerGameManager] Player disconnecting - FishNet ClientId: {fishnetClientId}");

            // Get player object
            if (!_spawnedPlayers.TryGetValue(fishnetClientId, out GameObject playerObj))
            {
                Debug.LogWarning($"[ServerGameManager] No spawned player for ClientId: {fishnetClientId}");
                return;
            }

            NetworkPlayer networkPlayer = ComponentResolver.Find<NetworkPlayer>(playerObj)
                .OnSelf()
                .InChildren()
                .OrLogWarning("[Auto] NetworkPlayer not found")
                .Resolve();

            if (networkPlayer == null)
            {
                Debug.LogError($"[ServerGameManager] NetworkPlayer component missing!");
                return;
            }

            string backendId = _registryService.GetBackendIdByFishNetId(fishnetClientId);

            if (_debugConfig != null && _debugConfig.EnableNetworkDebugLogs)
                Debug.Log($"[ServerGameManager] Cleaning up - Backend ID: {backendId}, Name: {networkPlayer.DisplayName}");

            // Unregister (RegistryService lÆ°u data cho reconnect)
            _registryService.UnregisterPlayer(networkPlayer);

            // SpawnSystem cleanup
            if (_spawnSystem != null)
                _spawnSystem.OnPlayerDisconnected(fishnetClientId);

            // Despawn
            _networkManager.ServerManager.Despawn(playerObj);

            // Remove tracking
            _spawnedPlayers.Remove(fishnetClientId);

            if (_debugConfig != null && _debugConfig.EnableNetworkDebugLogs)
                Debug.Log($"[ServerGameManager] âœ… Cleanup complete for ClientId: {fishnetClientId}");
        }
#if UNITY_EDITOR
        // ── Editor — Context Menu: Auto-assign Known Prefabs ──────────────────

        [ContextMenu("NightHunt/Auto-Assign Player & Handler Prefabs")]
        private void Editor_AutoAssignPrefabs()
        {
            bool changed = false;

            if (playerPrefab == null)
            {
                string[] candidates =
                {
                    "Assets/_Night_Hunt/Prefabs/PlayerPrefab.prefab",
                    "Assets/_Night_Hunt/Prefabs/Network_Player Rigidbody Predict.prefab",
                };
                foreach (var p in candidates)
                {
                    var found = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(p);
                    if (found != null)
                    {
                        playerPrefab = found; changed = true;
                        Debug.Log($"[ServerGameManager] Auto-assigned playerPrefab from {p}");
                        break;
                    }
                }
                if (playerPrefab == null) Debug.LogWarning("[ServerGameManager] playerPrefab not found — assign manually.");
            }

            if (clientNetworkHandlerPrefab == null)
            {
                const string handlerPath = "Assets/_Night_Hunt/Prefabs/Networking/ClientNetworkHandlerPrefab.prefab";
                var found = UnityEditor.AssetDatabase.LoadAssetAtPath<ClientNetworkHandler>(handlerPath);
                if (found != null)
                {
                    clientNetworkHandlerPrefab = found; changed = true;
                    Debug.Log($"[ServerGameManager] Auto-assigned clientNetworkHandlerPrefab from {handlerPath}");
                }
                else Debug.LogWarning($"[ServerGameManager] ClientNetworkHandlerPrefab not found at {handlerPath}.");
            }

            if (changed) UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
