using UnityEngine;
using FishNet.Object;
using FishNet.Managing;
using FishNet.Connection;
using FishNet.Transporting;
using FishNet;
using NightHunt.Gameplay.Spawn;
using NightHunt.Gameplay.Zone;
using NightHunt.Gameplay.Scoring;
using NightHunt.Gameplay.Core.Events;
using NightHunt.Gameplay.Character;
using System.Collections;
using System.Collections.Generic;
using NightHunt.Networking.Player;
using NightHunt.Utilities;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.Config;
using NightHunt.Common;
using NightHunt.Core;
using NightHunt.Data.DTOs;
using NightHunt.Services.Game;
using NightHunt.State;
using NightHunt.UI;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

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
        [SerializeField] private ClientNetworkHandler clientNetworkHandlerPrefab;

        [Header("Match Settings")]
        [Tooltip("Total players expected before starting Phase 1.\n" +
                 "Tự động resolve từ RoomState.PlayerCount (Ranked) hoặc GameModeConfig khi OnStartNetwork.\n" +
                 "Chỉ cần đặt thủ công khi test trong Editor (dev mode, GameMode.None).")]
        private int _expectedPlayerCount = 2;

        [Header("Runtime Ready Gate")]
        [Tooltip("Maximum seconds to wait after all players spawn for owner clients to finish model/HUD runtime init.")]
        [SerializeField] private float _clientRuntimeReadyTimeoutSeconds = 10f;

        [Header("Relay Scene Gate")]
        [Tooltip("Custom_Relay clients load the map with Unity SceneManager before connecting. If FishNet does not emit OnLoadedStartScenes, mark the connection ready for identity broadcast after this delay when the host is already in a map scene.")]
        [SerializeField] private float _relayStartScenesFallbackSeconds = 1.0f;
        [Tooltip("Seconds to wait after a connection is ready before logging that player identity never arrived.")]
        [SerializeField] private float _clientDataTimeoutSeconds = 5.0f;
        [Tooltip("Seconds to preserve a Custom_Relay player object after transport disconnect so the same backend user can reconnect without a despawn/spawn loop.")]
        [SerializeField] private float _relayReconnectHoldSeconds = 60.0f;

        private RegistryService _registryService;
        private NetworkManager _networkManager;
        private int _spawnedPlayerCount = 0;
        private bool _matchStartTriggered;
        private Coroutine _runtimeReadyTimeoutCoroutine;
        private readonly HashSet<int> _runtimeReadyClients = new();
        private readonly HashSet<long> _abandonedBackendUserIds = new();
        private readonly HashSet<int> _identityHandshakeConnections = new();
        private readonly Dictionary<int, Coroutine> _relaySceneFallbackCoroutines = new();
        private readonly Dictionary<int, Coroutine> _clientDataTimeoutCoroutines = new();
        private readonly Dictionary<int, Coroutine> _relayDisconnectHoldCoroutines = new();
        private bool _serverPlayerDataBroadcastRegistered;

        // Tracking
        private Dictionary<int, GameObject> _spawnedPlayers = new(); // FishNet ClientId â†’ GameObject
        private readonly Dictionary<int, Coroutine> _dataRequestRetryCoroutines = new();


        /// <summary>Fired on server when all expected players have spawned.</summary>
        public event System.Action OnAllPlayersReady;

        public bool IsMatchStarted => _matchStartTriggered;

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
            RegisterPlayerDataBroadcastHandler();
            if (GameWebSocketService.Instance != null)
                GameWebSocketService.Instance.OnPlayerAbandoned += HandlePlayerAbandoned;

            // Auto-resolve expected player count from RoomState (set by backend match_ready WS).
            // Priority: RoomState.PlayerCount (Ranked_DS, most accurate)
            //           → GameModeConfig lookup by modeKey
            //           → keep Inspector value (dev/editor fallback)
            ResolveExpectedPlayerCount();

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableNetworkDebugLogs)
                Debug.Log("[ServerGameManager] ✅ Initialized");
        }

        [Server]
        private void ResolveExpectedPlayerCount()
        {
            var roomState = RoomState.Instance;

            // 1. RoomState.PlayerCount is populated by match_ready (Ranked_DS) or by party custom mode
            //    room data (Custom_Relay host). Use it for both modes.
            if (roomState != null && roomState.PlayerCount > 0)
            {
                _expectedPlayerCount = roomState.PlayerCount;
                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableNetworkDebugLogs)
                    Debug.Log($"[ServerGameManager] ExpectedPlayerCount resolved from RoomState: {_expectedPlayerCount} (mode={roomState.CurrentGameMode})");
                return;
            }

            // 2. ServerBootstrap CLI arg (--expectedPlayers).
            //    RoomState is always empty on a headless dedicated server — the DS doesn't
            //    have a WebSocket connection to the backend, so match_ready never populates it.
            //    ServerBootstrap.BootstrappedExpectedPlayers > 0 means the arg was passed.
            if (NightHunt.Server.ServerBootstrap.BootstrappedExpectedPlayers > 0)
            {
                _expectedPlayerCount = NightHunt.Server.ServerBootstrap.BootstrappedExpectedPlayers;
                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableNetworkDebugLogs)
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
                    if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableNetworkDebugLogs)
                        Debug.Log($"[ServerGameManager] ExpectedPlayerCount resolved from GameModeConfig ({modeKey}): {_expectedPlayerCount}");
                    return;
                }
            }

            // 4. Keep Inspector value — dev mode or no data available
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableNetworkDebugLogs)
                Debug.LogWarning($"[ServerGameManager] ExpectedPlayerCount using Inspector value: {_expectedPlayerCount}. " +
                                 "Ensure RoomState is populated or --expectedPlayers is passed before scene load in production.");
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();

            if (_networkManager != null && _networkManager.ServerManager != null)
            {
                _networkManager.ServerManager.OnRemoteConnectionState -= OnServerConnectionState;
                UnregisterPlayerDataBroadcastHandler();
            }

            if (GameWebSocketService.Instance != null)
                GameWebSocketService.Instance.OnPlayerAbandoned -= HandlePlayerAbandoned;

            if (_runtimeReadyTimeoutCoroutine != null)
            {
                StopCoroutine(_runtimeReadyTimeoutCoroutine);
                _runtimeReadyTimeoutCoroutine = null;
            }

            _runtimeReadyClients.Clear();
            _abandonedBackendUserIds.Clear();
            _identityHandshakeConnections.Clear();
            foreach (var fallback in _relaySceneFallbackCoroutines.Values)
            {
                if (fallback != null)
                    StopCoroutine(fallback);
            }
            _relaySceneFallbackCoroutines.Clear();
            foreach (var timeout in _clientDataTimeoutCoroutines.Values)
            {
                if (timeout != null)
                    StopCoroutine(timeout);
            }
            _clientDataTimeoutCoroutines.Clear();
            _matchStartTriggered = false;
            _spawnedPlayers.Clear();
            _spawnedPlayerCount = 0;
        }

        [Server]
        private void RegisterPlayerDataBroadcastHandler()
        {
            if (_serverPlayerDataBroadcastRegistered || _networkManager?.ServerManager == null)
                return;

            _networkManager.ServerManager.RegisterBroadcast<SubmitPlayerDataBroadcast>(OnClientSubmittedPlayerDataBroadcast, requireAuthentication: false);
            _serverPlayerDataBroadcastRegistered = true;
            Debug.Log("[NH_HANDSHAKE][HOST_BROADCAST][REGISTER] SubmitPlayerDataBroadcast registered requireAuth=False.");
        }

        [Server]
        private void UnregisterPlayerDataBroadcastHandler()
        {
            if (!_serverPlayerDataBroadcastRegistered || _networkManager?.ServerManager == null)
                return;

            _networkManager.ServerManager.UnregisterBroadcast<SubmitPlayerDataBroadcast>(OnClientSubmittedPlayerDataBroadcast);
            _serverPlayerDataBroadcastRegistered = false;
        }

        // ===== CONNECTION EVENTS =====

        [Server]
        private void OnServerConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            Debug.Log($"[NH_CONN][HOST_REMOTE_STATE] clientId={conn?.ClientId.ToString() ?? "null"} state={args.ConnectionState}");

            switch (args.ConnectionState)
            {
                case RemoteConnectionState.Started:
                    // BUG 1 FIX: Wait until connection has loaded start scenes before spawning.
                    // Spawning before start scenes are loaded causes the FishNet warning:
                    // "spawned but it's recommended to not spawn objects for connections
                    //  until they have loaded start scenes."
                    conn.OnLoadedStartScenes += OnConnectionLoadedStartScenes;
                    TryStartRelaySceneFallback(conn);
                    break;

                case RemoteConnectionState.Stopped:
                    conn.OnLoadedStartScenes -= OnConnectionLoadedStartScenes;
                    CancelRelaySceneFallback(conn);
                    CancelClientDataTimeout(conn);
                    if (conn != null)
                    {
                        _identityHandshakeConnections.Remove(conn.ClientId);
                        CancelDataRequestRetry(conn.ClientId);
                    }
                    OnPlayerDisconnected(conn);
                    break;
            }
        }

        private void OnConnectionLoadedStartScenes(NetworkConnection conn, bool asServer)
        {
            conn.OnLoadedStartScenes -= OnConnectionLoadedStartScenes;
            CancelRelaySceneFallback(conn);
            OnPlayerConnected(conn, "loaded-start-scenes");
        }

        [Server]
        private void TryStartRelaySceneFallback(NetworkConnection conn)
        {
            if (conn == null || !ShouldUseRelayLocalSceneFallback())
                return;

            CancelRelaySceneFallback(conn);
            _relaySceneFallbackCoroutines[conn.ClientId] = StartCoroutine(RelaySceneFallbackAfterDelay(conn));
        }

        [Server]
        private IEnumerator RelaySceneFallbackAfterDelay(NetworkConnection conn)
        {
            int clientId = conn != null ? conn.ClientId : -1;
            float waitSeconds = Mathf.Max(0.25f, _relayStartScenesFallbackSeconds);
            yield return new WaitForSecondsRealtime(waitSeconds);

            if (!IsServerStarted || conn == null || !conn.IsActive)
            {
                _relaySceneFallbackCoroutines.Remove(clientId);
                yield break;
            }

            _relaySceneFallbackCoroutines.Remove(clientId);

            if (!ShouldUseRelayLocalSceneFallback())
                yield break;

            Debug.LogWarning(
                $"[NH_HANDSHAKE][HOST_FALLBACK] Relay local-scene fallback firing for clientId={clientId} after {waitSeconds:F2}s. " +
                "Custom_Relay loads the map with Unity SceneManager before connecting, so FishNet may not emit OnLoadedStartScenes for remote clients.");
            OnPlayerConnected(conn, "relay-local-scene-fallback");
        }

        [Server]
        private void CancelRelaySceneFallback(NetworkConnection conn)
        {
            if (conn == null)
                return;

            if (_relaySceneFallbackCoroutines.TryGetValue(conn.ClientId, out var fallback))
            {
                if (fallback != null)
                    StopCoroutine(fallback);
                _relaySceneFallbackCoroutines.Remove(conn.ClientId);
            }
        }

        private static bool ShouldUseRelayLocalSceneFallback()
        {
            var roomState = RoomState.Instance;
            if (roomState == null || roomState.CurrentGameMode != GameMode.Custom_Relay)
                return false;

            string activeSceneName = UnitySceneManager.GetActiveScene().name;
            return activeSceneName.StartsWith("02_Map_", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldUseRelayConnectionHold()
        {
            return ShouldUseRelayLocalSceneFallback();
        }

        private static bool ShouldUseConnectionBroadcastIdentity()
        {
            return RoomState.Instance?.CurrentGameMode == GameMode.Custom_Relay;
        }

        [Server]
        private void OnPlayerConnected(NetworkConnection conn, string source)
        {
            if (conn == null || !conn.IsActive)
                return;

            int fishnetClientId = conn.ClientId;
            if (_spawnedPlayers.ContainsKey(fishnetClientId))
            {
                Debug.Log($"[NH_HANDSHAKE][HOST_CONNECT_DUPLICATE] Player connected ignored already-spawned clientId={fishnetClientId} source={source}");
                return;
            }

            if (!_identityHandshakeConnections.Add(fishnetClientId))
            {
                Debug.Log($"[NH_HANDSHAKE][HOST_CONNECT_DUPLICATE] Player connected ignored duplicate clientId={fishnetClientId} source={source}");
                return;
            }

            AddRelayConnectionToActiveScene(conn, source);

            Debug.Log($"[NH_HANDSHAKE][HOST_CONNECT] Player connected clientId={fishnetClientId} source={source}");

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableNetworkDebugLogs)
                Debug.Log($"[ServerGameManager] Player connected - FishNet ClientId: {fishnetClientId}");

            if (ShouldUseConnectionBroadcastIdentity())
            {
                Debug.Log($"[NH_HANDSHAKE][HOST_IDENTITY_READY] clientId={fishnetClientId} source={source} path=connection-broadcast");
                StartClientDataTimeout(conn, source);
                StartDataRequestRetry(conn, source);
                StartCoroutine(RequestClientDataAfterDelay(conn));
                return;
            }

            Debug.Log($"[NH_HANDSHAKE][HOST_IDENTITY_READY] clientId={fishnetClientId} source={source} path=owner-rpc-handler");
            if (clientNetworkHandlerPrefab == null)
            {
                Debug.LogError($"[NH_HANDSHAKE][NH_DROP][HOST_IDENTITY_PREFAB_MISSING] ClientNetworkHandler prefab missing clientId={fishnetClientId}");
                conn.Disconnect(true);
                return;
            }

            ClientNetworkHandler cnh = Instantiate(clientNetworkHandlerPrefab);
            _networkManager.ServerManager.Spawn(cnh.gameObject, conn);
            StartClientDataTimeout(conn, source);
        }

        private IEnumerator RequestClientDataAfterDelay(NetworkConnection conn)
        {
            yield return new WaitForSeconds(2f);
    
            if (conn == null || !conn.IsActive)
                yield break;

            if (!ShouldUseConnectionBroadcastIdentity() || _spawnedPlayers.ContainsKey(conn.ClientId))
                yield break;
        
            Debug.Log($"[NH_HANDSHAKE][HOST_IDENTITY_REQUEST] Broadcasting identity request to clientId={conn.ClientId}");
            ServerManager.Broadcast(conn, new RequestPlayerDataBroadcast(), requireAuthenticated: false);
        }
        
        [Server]
        private void StartDataRequestRetry(NetworkConnection conn, string source)
        {
            if (conn == null) return;
            CancelDataRequestRetry(conn.ClientId);
            _dataRequestRetryCoroutines[conn.ClientId] = 
                StartCoroutine(DataRequestRetryCoroutine(conn, source));
        }

        [Server]
        private void CancelDataRequestRetry(int clientId)
        {
            if (_dataRequestRetryCoroutines.TryGetValue(clientId, out var co))
            {
                if (co != null) StopCoroutine(co);
                _dataRequestRetryCoroutines.Remove(clientId);
            }
        }
        
        [Server]
        private IEnumerator DataRequestRetryCoroutine(NetworkConnection conn, string source)
        {
            int clientId = conn?.ClientId ?? -1;
    
            // Đợi relay ổn định trước khi broadcast
            yield return new WaitForSecondsRealtime(0.5f);
    
            float[] retryDelays = { 0f, 1f, 2f, 3f, 5f };
    
            foreach (float delay in retryDelays)
            {
                if (delay > 0f)
                    yield return new WaitForSecondsRealtime(delay);
        
                // Nếu đã spawn player rồi thì thôi
                if (_spawnedPlayers.ContainsKey(clientId))
                {
                    _dataRequestRetryCoroutines.Remove(clientId);
                    yield break;
                }
        
                if (!IsServerStarted || conn == null || !conn.IsActive)
                {
                    _dataRequestRetryCoroutines.Remove(clientId);
                    yield break;
                }
        
                if (!ShouldUseConnectionBroadcastIdentity())
                {
                    _dataRequestRetryCoroutines.Remove(clientId);
                    yield break;
                }

                Debug.Log($"[NH_HANDSHAKE][HOST_IDENTITY_REQUEST] Broadcasting identity request to clientId={clientId} source={source}");
        
                ServerManager.Broadcast(conn, new RequestPlayerDataBroadcast(), requireAuthenticated: false);
            }
    
            _dataRequestRetryCoroutines.Remove(clientId);
        }

        private float GetClientDataTimeout()
        {
            return ShouldUseRelayLocalSceneFallback() 
                ? Mathf.Max(15f, _clientDataTimeoutSeconds) 
                : _clientDataTimeoutSeconds;
        }

        
        [Server]
        private void StartClientDataTimeout(NetworkConnection conn, string source)
        {
            if (conn == null)
                return;

            CancelClientDataTimeout(conn);
            _clientDataTimeoutCoroutines[conn.ClientId] = StartCoroutine(ClientDataTimeoutAfterDelay(conn, source, GetClientDataTimeout()));
        }

        [Server]
        private IEnumerator ClientDataTimeoutAfterDelay(NetworkConnection conn, string source, float overrideTimeout = -1f)
        {
            int clientId = conn != null ? conn.ClientId : -1;
            float waitSeconds = overrideTimeout > 0f ? overrideTimeout : Mathf.Max(1.0f, _clientDataTimeoutSeconds);
            yield return new WaitForSecondsRealtime(waitSeconds);

            _clientDataTimeoutCoroutines.Remove(clientId);

            if (!IsServerStarted || conn == null || !conn.IsActive || _spawnedPlayers.ContainsKey(clientId))
                yield break;

            Debug.LogError(
                $"[NH_HANDSHAKE][NH_DROP][HOST_DATA_TIMEOUT] Player identity timeout clientId={clientId} after {waitSeconds:F1}s source={source} " +
                $"path={(ShouldUseConnectionBroadcastIdentity() ? "connection-broadcast" : "owner-rpc-handler")}. " +
                "Host did not receive SubmitPlayerDataBroadcast/RpcSendPlayerData; inspect NH_RELAY FORWARD/DROP and client NH_HANDSHAKE CLIENT_BROADCAST logs for the same session.");
        }

        [Server]
        private void CancelClientDataTimeout(NetworkConnection conn)
        {
            if (conn == null)
                return;

            if (_clientDataTimeoutCoroutines.TryGetValue(conn.ClientId, out var timeout))
            {
                if (timeout != null)
                    StopCoroutine(timeout);
                _clientDataTimeoutCoroutines.Remove(conn.ClientId);
            }
        }

        [Server]
        private void AddRelayConnectionToActiveScene(NetworkConnection conn, string source)
        {
            if (conn == null || _networkManager?.SceneManager == null || !ShouldUseRelayLocalSceneFallback())
                return;

            var activeScene = UnitySceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
                return;

            _networkManager.SceneManager.AddConnectionToScene(conn, activeScene);
            Debug.Log($"[NH_HANDSHAKE][HOST_SCENE_ADD] Relay connection added to active scene clientId={conn.ClientId} scene={activeScene.name} source={source}");
        }

        /// <summary>
        /// Server: receive player identity through a connection-level broadcast.
        /// This is the canonical Custom_Relay identity path; it runs before player
        /// objects are spawned and does not depend on scene observers or ownership.
        /// </summary>
        [Server]
        private void OnClientSubmittedPlayerDataBroadcast(NetworkConnection conn, SubmitPlayerDataBroadcast broadcast, Channel channel)
        {
            if (conn == null || !conn.IsActive)
            {
                Debug.LogWarning("[NH_HANDSHAKE][NH_DROP][HOST_BROADCAST_RECEIVED] Ignored player data broadcast from inactive connection.");
                return;
            }

            PlayerRegistryData clientData = broadcast.Data;
            if (!ShouldUseRelayLocalSceneFallback() && !conn.LoadedStartScenes(true))
            {
                Debug.Log(
                    $"[NH_HANDSHAKE][HOST_BROADCAST_DEFER] clientId={conn.ClientId} backendId={clientData.BackendPlayerId} " +
                    "ignored until FishNet start scenes are loaded.");
                return;
            }

            AddRelayConnectionToActiveScene(conn, "client-broadcast-submit");
            Debug.Log(
                $"[NH_HANDSHAKE][HOST_BROADCAST_RECEIVED] clientId={conn.ClientId} backendId={clientData.BackendPlayerId} " +
                $"name={clientData.DisplayName} team={clientData.TeamId} channel={channel}.");
            OnClientDataReceived(conn, clientData);
        }

        /// <summary>
        /// Server: receive identity from the active connection handshake path.
        /// </summary>
        [Server]
        public void OnClientDataReceived(NetworkConnection conn, PlayerRegistryData clientData)
        {
            int fishnetClientId = conn.ClientId;
            CancelClientDataTimeout(conn);
            CancelDataRequestRetry(fishnetClientId);
            Debug.Log($"[NH_HANDSHAKE][HOST_DATA_RECEIVED] Client data received clientId={fishnetClientId} backendId={clientData.BackendPlayerId} name={clientData.DisplayName} team={clientData.TeamId}");

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableNetworkDebugLogs)
                Debug.Log(
                $"[ServerGameManager] Received client data - FishNet ID: {fishnetClientId}, Backend ID: {clientData.BackendPlayerId}, Name: {clientData.DisplayName}");

            // Data validation is the client's responsibility via connection broadcast / JWT.
            // Server-side anti-cheat can be layered here in a future pass.
            if (_spawnedPlayers.ContainsKey(fishnetClientId))
            {
                Debug.LogWarning($"[NH_HANDSHAKE][HOST_DATA_DUPLICATE] Duplicate player data ignored for already-spawned clientId={fishnetClientId} backendId={clientData.BackendPlayerId}.");
                return;
            }

            if (!string.IsNullOrEmpty(clientData.BackendPlayerId))
            {
                NetworkPlayer activePlayer = _registryService.GetActivePlayerByBackendId(clientData.BackendPlayerId);
                int activeClientId = -1;
                _registryService.TryGetFishNetIdByBackendId(clientData.BackendPlayerId, out activeClientId);
                if (activePlayer != null && activeClientId != fishnetClientId)
                {
                    if (TryResumeRelayPlayer(conn, clientData, activePlayer, activeClientId))
                        return;

                    Debug.LogWarning(
                        $"[NH_HANDSHAKE][NH_DROP][HOST_DUP_BACKEND] Duplicate backend identity rejected: backendId={clientData.BackendPlayerId} " +
                        $"newClientId={fishnetClientId} activeClientId={activeClientId}. " +
                        "Use a separate authenticated account per local Editor/clone/build instance.");
                    conn.Disconnect(true);
                    return;
                }
            }

            // Spawn player
            SpawnPlayerWorkflow(conn, clientData);
        }

        [Server]
        private bool TryResumeRelayPlayer(NetworkConnection conn, PlayerRegistryData clientData, NetworkPlayer activePlayer, int previousClientId)
        {
            if (!ShouldUseRelayConnectionHold() || conn == null || !conn.IsActive || activePlayer == null || previousClientId < 0)
                return false;

            if (!_spawnedPlayers.TryGetValue(previousClientId, out GameObject playerObj) || playerObj == null)
                playerObj = activePlayer.gameObject;

            if (playerObj == null)
                return false;

            NetworkObject netObj = ComponentResolver.Find<NetworkObject>(playerObj)
                .OnSelf()
                .InChildren()
                .OrDefault(null)
                .Resolve();

            if (netObj == null)
            {
                Debug.LogWarning($"[NH_HANDSHAKE][NH_DROP][HOST_RESUME_FAIL] Relay resume failed: preserved player missing NetworkObject backendId={clientData.BackendPlayerId} previousClientId={previousClientId}");
                return false;
            }

            CancelRelayDisconnectHold(previousClientId);
            CancelClientDataTimeout(conn);
            AddRelayConnectionToActiveScene(conn, "relay-reconnect-resume");

            _spawnedPlayers.Remove(previousClientId);
            _spawnedPlayers[conn.ClientId] = playerObj;
            _spawnSystem?.OnPlayerReconnected(previousClientId, conn.ClientId);
            _identityHandshakeConnections.Remove(previousClientId);
            _identityHandshakeConnections.Add(conn.ClientId);
            _runtimeReadyClients.Remove(previousClientId);

            clientData.Status = PlayerConnectionStatus.InGame;
            _registryService.RemapPlayerConnection(activePlayer, previousClientId, conn, clientData);
            netObj.GiveOwnership(conn);
            _networkManager.ServerManager.Objects.RebuildObservers(conn);

            ReportMatchPresence(clientData.BackendPlayerId, "CONNECTED", "FISHNET_RECONNECTED");

            Debug.Log(
                $"[NH_HANDSHAKE][HOST_RESUME] Relay player resumed backendId={clientData.BackendPlayerId} " +
                $"oldClientId={previousClientId} newClientId={conn.ClientId} objectId={netObj.ObjectId} spawned={_spawnedPlayers.Count}/{_expectedPlayerCount}");

            if (_matchStartTriggered)
            {
                activePlayer.TargetMatchAllPlayersReady(conn, "relay-reconnect-resume");
                RpcOnAllPlayersReadyTarget(conn);
            }

            return true;
        }

        // ===== SPAWN WORKFLOW =====

        [Server]
        private void SpawnPlayerWorkflow(NetworkConnection conn, PlayerRegistryData clientData)
        {
            int fishnetClientId = conn.ClientId;

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableNetworkDebugLogs)
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

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableNetworkDebugLogs)
                Debug.Log($"[ServerGameManager] Step 1: Prefab instantiated");

            // STEP 2: SpawnSystem xá»­ lÃ½ (assign team, position)
            PlayerRegistryData serverData;
            if (_spawnSystem != null)
            {
                serverData = _spawnSystem.ProcessSpawn(playerObj, conn, clientData);
                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableNetworkDebugLogs)
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

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableNetworkDebugLogs)
                Debug.Log($"[ServerGameManager] Step 3: Network spawned");

            // STEP 4: Set PUBLIC data AFTER spawn so the SyncVar change is
            // broadcast via the normal dirty-sync channel to all current observers,
            // AND will be included in the spawn packet for any client that connects
            // while this object is alive (FishNet re-serialises all SyncVars into
            // every new observer's spawn packet using the current value at the time
            // of connection).
            PlayerPublicData publicData = PlayerPublicData.FromRegistryData(serverData);
            networkPlayer.SetPublicData(publicData);

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableNetworkDebugLogs)
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

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableNetworkDebugLogs)
                Debug.Log($"[ServerGameManager] Step 4b: ModelIndex set to {serverData.CharacterModelIndex}");

            // STEP 5: Register vá»›i RegistryService (lÆ°u PRIVATE data)
            _registryService.RegisterPlayer(networkPlayer, serverData);
            ReportMatchPresence(serverData.BackendPlayerId, "CONNECTED", "FISHNET_CONNECTED");

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableNetworkDebugLogs)
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
            int expectedForUi = Mathf.Max(_expectedPlayerCount, _spawnedPlayerCount);
            RpcOnPlayerSpawned(serverData.DisplayName, _spawnedPlayerCount, expectedForUi);

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableNetworkDebugLogs)
                Debug.Log(
                $"[ServerGameManager] === âœ… Spawn complete ({_spawnedPlayerCount}/{_expectedPlayerCount}) - {serverData.DisplayName}, Team: {serverData.TeamId} ===");
            Debug.Log($"[NH_HANDSHAKE][HOST_SPAWN_COMPLETE] clientId={fishnetClientId} backendId={serverData.BackendPlayerId} name={serverData.DisplayName} spawned={_spawnedPlayerCount}/{_expectedPlayerCount} team={serverData.TeamId}");

            // STEP 7: Check if all expected players have spawned
            if (_spawnedPlayerCount >= _expectedPlayerCount)
            {
                OnAllPlayersSpawned();
            }
        }

        private void HandlePlayerAbandoned(GameWebSocketService.PlayerAbandonedEvent evt)
        {
            if (!IsServerStarted || evt == null || evt.userId <= 0L)
                return;

            var roomState = RoomState.Instance;
            string currentMatchId = roomState?.CurrentMatchId;
            if (string.IsNullOrEmpty(currentMatchId))
                currentMatchId = roomState?.CurrentRoom?.matchId;

            if (!string.IsNullOrEmpty(evt.matchId)
                && !string.IsNullOrEmpty(currentMatchId)
                && !string.Equals(evt.matchId, currentMatchId, System.StringComparison.Ordinal))
            {
                return;
            }

            if (!_abandonedBackendUserIds.Add(evt.userId))
                return;

            RecordBackendPlayerAbandoned(evt);
        }

        [Server]
        private void RecordBackendPlayerAbandoned(GameWebSocketService.PlayerAbandonedEvent evt)
        {
            int abandonedClientId = FindSpawnedClientIdByBackendUserId(evt.userId);

            Debug.LogWarning(
                $"[ServerGameManager] Backend player_abandoned observed userId={evt.userId} reason={evt.reason ?? "unknown"} spawnedClientId={abandonedClientId} matchStarted={_matchStartTriggered}. " +
                "This is presence telemetry only; FishNet remote disconnect/runtime-ready gates remain authoritative for match start.");
        }

        [Server]
        private int FindSpawnedClientIdByBackendUserId(long backendUserId)
        {
            if (_registryService == null || backendUserId <= 0L)
                return -1;

            string targetBackendId = backendUserId.ToString();
            foreach (var kvp in _spawnedPlayers)
            {
                string backendId = _registryService.GetBackendIdByFishNetId(kvp.Key);
                if (string.Equals(backendId, targetBackendId, System.StringComparison.Ordinal))
                    return kvp.Key;
            }

            return -1;
        }

        [Server]
        public void OnPlayerClientRuntimeReady(NetworkConnection conn, NetworkPlayer player)
        {
            if (!IsServerStarted || conn == null)
                return;

            if (!_spawnedPlayers.TryGetValue(conn.ClientId, out var spawned) ||
                player == null || spawned != player.gameObject)
            {
                Debug.LogWarning($"[ServerGameManager] Runtime-ready rejected for ClientId={conn.ClientId}: player does not match spawned registry.");
                return;
            }

            if (!_runtimeReadyClients.Add(conn.ClientId))
                return;

            Debug.Log($"[ServerGameManager] Client runtime ready ({_runtimeReadyClients.Count}/{_expectedPlayerCount}) - {player.DisplayName}");
            Debug.Log($"[NH_HANDSHAKE][HOST_RUNTIME_READY] clientId={conn.ClientId} name={player.DisplayName} ready={_runtimeReadyClients.Count}/{_expectedPlayerCount} spawned={_spawnedPlayerCount}/{_expectedPlayerCount} matchStartTriggered={_matchStartTriggered}");
            RpcOnPlayerRuntimeReady(player.DisplayName, _runtimeReadyClients.Count, _expectedPlayerCount);

            // Late-joiner fix: if the match already started, send the "all ready" signal directly so
            // its MatchLoadingOverlay hides and the HUD shows correctly.
            if (_matchStartTriggered)
            {
                Debug.Log($"[ServerGameManager] Match already started — sending AllPlayersReady directly to late-joiner {player.DisplayName}.");
                player.TargetMatchAllPlayersReady(conn, "late-joiner");
                RpcOnAllPlayersReadyTarget(conn);
                return;
            }

            if (_spawnedPlayerCount >= _expectedPlayerCount)
                OnAllPlayersSpawned();
        }

        [Server]
        private void OnAllPlayersSpawned()
        {
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableNetworkDebugLogs)
                Debug.Log("[ServerGameManager] âœ… All players spawned â€” starting match!");
            if (_matchStartTriggered)
                return;

            if (_runtimeReadyClients.Count < _expectedPlayerCount)
            {
                if (_runtimeReadyTimeoutCoroutine == null)
                    _runtimeReadyTimeoutCoroutine = StartCoroutine(WaitForRuntimeReadyTimeout());
                return;
            }

            _matchStartTriggered = true;
            BeginServerMatchState("runtime-ready");
            OnAllPlayersReady?.Invoke();

            // Notify all clients: hide loading screen, show game HUD
            TargetAllSpawnedPlayersReady("runtime-ready");
            RpcOnAllPlayersReady();
        }

        [Server]
        private IEnumerator WaitForRuntimeReadyTimeout()
        {
            float deadline = Time.time + Mathf.Max(0f, _clientRuntimeReadyTimeoutSeconds);
            while (!_matchStartTriggered && Time.time < deadline)
                yield return null;

            _runtimeReadyTimeoutCoroutine = null;

            if (_matchStartTriggered)
                yield break;

            Debug.LogWarning($"[ServerGameManager] Runtime-ready timeout. Starting match with ready={_runtimeReadyClients.Count}/{_expectedPlayerCount} to avoid a stuck lobby.");
            _matchStartTriggered = true;
            BeginServerMatchState("runtime-ready-timeout");
            OnAllPlayersReady?.Invoke();
            TargetAllSpawnedPlayersReady("runtime-ready-timeout");
            RpcOnAllPlayersReady();
        }

        [Server]
        private void BeginServerMatchState(string reason)
        {
            Debug.Log($"[ServerGameManager] Starting authoritative match state ({reason}).");
            SafeZoneManager.Instance?.BeginMatch();
            SurvivalScoreSystem.Instance?.StartTicking(SafeZoneManager.Instance?.Config);
        }

        [Server]
        private void TargetAllSpawnedPlayersReady(string source)
        {
            int targets = 0;
            foreach (var kvp in _spawnedPlayers)
            {
                NetworkPlayer player = ComponentResolver.Find<NetworkPlayer>(kvp.Value)
                    .OnSelf()
                    .InChildren()
                    .OrDefault(null)
                    .Resolve();

                if (player == null || player.Owner == null)
                    continue;

                player.TargetMatchAllPlayersReady(player.Owner, source);
                targets++;
            }

            Debug.Log($"[NH_HANDSHAKE][HOST_ALL_READY_TARGET] source={source} targets={targets} spawned={_spawnedPlayers.Count}");
        }

        [ObserversRpc]
        private void RpcOnSpawningStarted()
        {
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableNetworkDebugLogs)
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
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableNetworkDebugLogs)
                Debug.Log($"[ServerGameManager] CLIENT: Player spawned — {displayName} ({spawnedCount}/{expectedCount})");
            GameplayEventBus.Instance?.Publish(new PlayerSpawnedEvent
            {
                DisplayName   = displayName,
                SpawnedCount  = spawnedCount,
                ExpectedCount = expectedCount
            });
        }

        [ObserversRpc]
        private void RpcOnPlayerRuntimeReady(string displayName, int readyCount, int expectedCount)
        {
            GameplayEventBus.Instance?.Publish(new PlayerSpawnedEvent
            {
                DisplayName   = displayName,
                SpawnedCount  = readyCount,
                ExpectedCount = expectedCount
            });
        }

        [ObserversRpc]
        private void RpcOnAllPlayersReady()
        {
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableNetworkDebugLogs)
                Debug.Log("[ServerGameManager] CLIENT: All players ready — dismissing loading screen.");
            MatchLoadingOverlay.SignalAllPlayersReady("server-observers-rpc");
            GameplayEventBus.Instance?.Publish(new AllPlayersReadyEvent());
            StartCoroutine(ReplaySafeZoneHudStateNextFrames());
        }

        /// <summary>
        /// Sent ONLY to a single late-joining client whose runtime-ready arrived after
        /// _matchStartTriggered was set (the match had already started via timeout or
        /// when all other players were ready before this client finished loading).
        /// Without this, their MatchLoadingOverlay would never hide.
        /// </summary>
        [TargetRpc]
        private void RpcOnAllPlayersReadyTarget(NetworkConnection conn)
        {
            Debug.Log("[ServerGameManager] CLIENT (late-joiner): received AllPlayersReady — dismissing loading screen.");
            MatchLoadingOverlay.SignalAllPlayersReady("server-target-rpc");
            GameplayEventBus.Instance?.Publish(new AllPlayersReadyEvent());
            StartCoroutine(ReplaySafeZoneHudStateNextFrames());
        }

        private IEnumerator ReplaySafeZoneHudStateNextFrames()
        {
            yield return null;
            SafeZoneManager.Instance?.ReplayCurrentHudState();
            yield return null;
            SafeZoneManager.Instance?.ReplayCurrentHudState();
        }

        // ===== DISCONNECT HANDLING =====

        [Server]
        private void OnPlayerDisconnected(NetworkConnection conn)
        {
            int fishnetClientId = conn.ClientId;
            Debug.Log($"[NH_CONN][NH_DROP][HOST_DISCONNECTING] clientId={fishnetClientId} tracked={_spawnedPlayers.ContainsKey(fishnetClientId)} readyTracked={_runtimeReadyClients.Contains(fishnetClientId)}");

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableNetworkDebugLogs)
                Debug.Log($"[ServerGameManager] Player disconnecting - FishNet ClientId: {fishnetClientId}");

            _runtimeReadyClients.Remove(fishnetClientId);

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
            Debug.Log($"[NH_CONN][NH_DROP][HOST_DISCONNECT_CLEANUP] clientId={fishnetClientId} backendId={backendId} name={networkPlayer.DisplayName}");

            if (ShouldUseRelayConnectionHold())
            {
                HoldRelayDisconnectedPlayer(fishnetClientId, networkPlayer, backendId);
                return;
            }

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableNetworkDebugLogs)
                Debug.Log($"[ServerGameManager] Cleaning up - Backend ID: {backendId}, Name: {networkPlayer.DisplayName}");

            ReportMatchPresence(backendId, "DISCONNECTED", "FISHNET_DISCONNECTED");

            // Unregister (RegistryService lÆ°u data cho reconnect)
            _registryService.UnregisterPlayer(networkPlayer);

            // SpawnSystem cleanup
            if (_spawnSystem != null)
                _spawnSystem.OnPlayerDisconnected(fishnetClientId);

            // Despawn
            _networkManager.ServerManager.Despawn(playerObj);

            // Remove tracking
            _spawnedPlayers.Remove(fishnetClientId);
            _spawnedPlayerCount = _spawnedPlayers.Count;
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableNetworkDebugLogs)
                Debug.Log($"[ServerGameManager] âœ… Cleanup complete for ClientId: {fishnetClientId}");
        }

        [Server]
        private void HoldRelayDisconnectedPlayer(int fishnetClientId, NetworkPlayer networkPlayer, string backendId)
        {
            _registryService.MarkPlayerReconnecting(fishnetClientId);
            _identityHandshakeConnections.Remove(fishnetClientId);
            _runtimeReadyClients.Remove(fishnetClientId);
            CancelRelayDisconnectHold(fishnetClientId);

            float holdSeconds = Mathf.Max(1.0f, _relayReconnectHoldSeconds);
            _relayDisconnectHoldCoroutines[fishnetClientId] =
                StartCoroutine(RelayDisconnectHoldTimeout(fishnetClientId, backendId, holdSeconds));

            Debug.Log(
                $"[NH_CONN][NH_DROP][HOST_RELAY_HOLD] Relay disconnect held clientId={fishnetClientId} backendId={backendId} " +
                $"name={networkPlayer.DisplayName} hold={holdSeconds:F1}s spawned={_spawnedPlayers.Count}/{_expectedPlayerCount}");
        }

        [Server]
        private void CancelRelayDisconnectHold(int fishnetClientId)
        {
            if (_relayDisconnectHoldCoroutines.TryGetValue(fishnetClientId, out Coroutine coroutine))
            {
                if (coroutine != null)
                    StopCoroutine(coroutine);
                _relayDisconnectHoldCoroutines.Remove(fishnetClientId);
            }
        }

        [Server]
        private IEnumerator RelayDisconnectHoldTimeout(int fishnetClientId, string backendId, float holdSeconds)
        {
            yield return new WaitForSecondsRealtime(holdSeconds);
            _relayDisconnectHoldCoroutines.Remove(fishnetClientId);

            if (!_spawnedPlayers.TryGetValue(fishnetClientId, out GameObject playerObj) || playerObj == null)
                yield break;

            NetworkPlayer networkPlayer = ComponentResolver.Find<NetworkPlayer>(playerObj)
                .OnSelf()
                .InChildren()
                .OrDefault(null)
                .Resolve();

            Debug.LogWarning(
                $"[NH_CONN][NH_DROP][HOST_RELAY_HOLD_EXPIRED] Relay reconnect hold expired clientId={fishnetClientId} backendId={backendId}. " +
                "Cleaning up preserved player.");

            ReportMatchPresence(backendId, "DISCONNECTED", "FISHNET_DISCONNECTED_TIMEOUT");
            _registryService.UnregisterPlayerByFishNetId(fishnetClientId);

            if (_spawnSystem != null)
                _spawnSystem.OnPlayerDisconnected(fishnetClientId);

            _networkManager.ServerManager.Despawn(playerObj);
            _spawnedPlayers.Remove(fishnetClientId);
            _runtimeReadyClients.Remove(fishnetClientId);
            _identityHandshakeConnections.Remove(fishnetClientId);
            _spawnedPlayerCount = _spawnedPlayers.Count;

            if (networkPlayer != null && NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableNetworkDebugLogs)
                Debug.Log($"[ServerGameManager] Relay hold cleanup complete for {networkPlayer.DisplayName} clientId={fishnetClientId}");
        }

        private async void ReportMatchPresence(string backendUserId, string state, string reason)
        {
            if (string.IsNullOrEmpty(backendUserId))
                return;

            var dsBootstrap = NightHunt.Server.ServerBootstrap.Instance;
            if (dsBootstrap != null)
            {
                dsBootstrap.ReportMatchPresence(backendUserId, state, reason);
                return;
            }

            if (!long.TryParse(backendUserId, out long userId) || userId <= 0)
            {
                Debug.LogWarning($"[ServerGameManager] Presence ignored: invalid backendUserId='{backendUserId}' state={state}");
                return;
            }

            var roomState = RoomState.Instance;
            string matchId = roomState?.CurrentMatchId;
            if (string.IsNullOrEmpty(matchId))
                matchId = roomState?.CurrentRoom?.matchId;

            var backend = GameManager.Instance?.BackendClient;
            if (backend == null || string.IsNullOrEmpty(matchId))
            {
                Debug.LogWarning($"[ServerGameManager] Presence {state} skipped for userId={userId}: backend or matchId missing.");
                return;
            }

            try
            {
                var result = await backend.PostAsync<object>(Constants.API_MATCH_PRESENCE,
                    new MatchPresenceRequest
                    {
                        matchId = matchId,
                        userId = userId,
                        state = state,
                        reason = reason,
                    });

                if (!result.Success)
                    Debug.LogWarning($"[ServerGameManager] Presence {state} failed for userId={userId}: {result.Message}");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ServerGameManager] Presence {state} error for userId={userId}: {ex.Message}");
            }
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
