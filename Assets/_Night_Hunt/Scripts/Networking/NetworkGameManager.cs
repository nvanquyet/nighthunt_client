using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using NightHunt.Core;
using NightHunt.State;
using NightHunt.UI;
using NightHunt.Services.Game;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NightHunt.Networking
{
    /// <summary>
    /// Network Game Manager - Manages Server and Client connections.
    ///
    /// Supports two connection modes:
    ///   Ranked_DS     : Client connects directly to a Dedicated Server (existing flow)
    ///   Custom_Relay  : Host starts FishNet Host; all players connect via Mini Relay server
    ///
    /// Relay topology (Custom mode):
    ///   All packets route through relay:sessionPort (unique UDP port per session).
    ///   The relay forwards every incoming packet to all other registered connections
    ///   in the same session – no game-logic parsing on the relay side.
    ///   The host player runs FishNet Host (Server + Client) bound to localhost;
    ///   the relay proxy exposes the session port to the internet.
    /// </summary>
    public class NetworkGameManager : Singleton<NetworkGameManager>
    {


        [Header("Network Manager Reference")]
        [SerializeField] private NetworkManager networkManager;

        [Header("Network Settings")]
        [SerializeField] private ushort port = 7777;
        [SerializeField] private string defaultServerAddress = "localhost";

        public NetworkManager NetworkManager => networkManager;
        public bool IsServer => networkManager != null && networkManager.IsServerStarted;
        public bool IsClient => networkManager != null && networkManager.IsClientStarted;

        [Header("Auto-Connect Retry")]
        [Tooltip("Seconds to wait before re-attempting connection on failure.")]
        [SerializeField] private float _retryDelay = 3f;
        [SerializeField] private int   _maxRetries = 2;

        private int  _retryCount;
        private bool _connectionStarted;
        private bool _connected;
        private bool _dsReady;          // true after ds_ready WS received
        private bool _gameSceneLoaded;  // true after 02_Map_* scene finishes loading

        /// <summary>Fired each reconnect attempt. Parameters: (currentAttempt, maxAttempts).</summary>
        public event System.Action<int, int> OnRetryAttempt;

        protected override void OnSingletonAwake()
        {
            if (networkManager == null)
            {
                networkManager = FindFirstObjectByType<NetworkManager>();
                if (networkManager == null)
                    Debug.LogError("[NetworkGameManager] NetworkManager not found! Please add NetworkManager to scene.");
            }
        }

        private void Start()
        {
#if UNITY_SERVER
            // DS is started by ServerBootstrap — no client auto-connect needed.
            return;
#endif
            if (networkManager == null) return;
            networkManager.SceneManager.OnLoadEnd               += OnSceneLoadEnd;
            networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
            if (GameWebSocketService.Instance != null)
                GameWebSocketService.Instance.OnDsReady += OnDsReadyReceived;

            // ── Fix A: scene-scoped singleton starts INSIDE the map scene ────
            // SceneLoader.LoadGame() uses Unity's SceneManager.LoadScene() which does NOT
            // fire FishNet's SceneManager.OnLoadEnd. If this instance started inside a map
            // scene, mark the flag immediately.
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene.name.StartsWith("02_Map_", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"[NetworkGameManager] Started inside map scene '{activeScene.name}' — setting _gameSceneLoaded = true.");
                _gameSceneLoaded = true;
            }

            // ── Fix B: ds_ready may have arrived before this instance subscribed ─
            // GameWebSocketService broadcasts ds_ready once. If the previous scene's
            // NetworkGameManager was destroyed before receiving it (or it arrived during
            // scene transition), _dsReady stays false even though DS is up.
            // Fallback: if RoomState already has DsIp set (from match_ready or ds_ready
            // handled by GameWebSocketService directly), treat DS as ready.
            if (!_dsReady
                && RoomState.Instance?.CurrentGameMode == GameMode.Ranked_DS
                && !string.IsNullOrEmpty(RoomState.Instance?.DsIp))
            {
                Debug.Log("[NetworkGameManager] RoomState has DsIp set — assuming ds_ready already received, setting _dsReady = true.");
                _dsReady = true;
            }

            // Try connect now if both flags are already true (most common case in map scene)
            if (_gameSceneLoaded)
                TryConnectIfReady();
        }

        protected override void OnDestroy()
        {
            if (networkManager != null)
            {
                networkManager.SceneManager.OnLoadEnd               -= OnSceneLoadEnd;
                networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
            }
            if (GameWebSocketService.Instance != null)
                GameWebSocketService.Instance.OnDsReady -= OnDsReadyReceived;
            base.OnDestroy();
        }

        // ── Dedicated Server (Ranked mode) ────────────────────────────────────

        /// <summary>
        /// Start FishNet Server only (dedicated server build / dev tools).
        /// Called by NetworkStartMenu (dev tool).
        /// </summary>
        public void StartServer()
        {
            if (networkManager == null) { Debug.LogError("[NetworkGameManager] NetworkManager is null!"); return; }
            Debug.Log($"[NetworkGameManager] Starting Dedicated Server on port {port}...");
            if (!networkManager.ServerManager.StartConnection())
                Debug.LogError("[NetworkGameManager] Failed to start server!");
            else
                Debug.Log("[NetworkGameManager] Dedicated Server started successfully!");
        }

        /// <summary>
        /// Connect client to a Dedicated Server (Ranked_DS mode).
        /// </summary>
        public void StartClientDS(string dsIp = null, ushort dsPort = 0)
        {
            if (networkManager == null) { Debug.LogError("[NetworkGameManager] NetworkManager is null!"); return; }
            string ip = string.IsNullOrEmpty(dsIp) ? defaultServerAddress : dsIp;
            ushort p  = dsPort > 0 ? dsPort : port;
            Debug.Log($"[NetworkGameManager] Connecting to DS {ip}:{p}...");
            SetTransportAddress(ip, p);
            if (!networkManager.ClientManager.StartConnection())
                Debug.LogError("[NetworkGameManager] Failed to start DS client connection!");
        }

        /// <summary>Legacy overload kept for backwards compatibility.</summary>
        public void StartClient(string serverAddress = null) => StartClientDS(serverAddress);

        // ── Relay (Custom mode) ───────────────────────────────────────────────

        /// <summary>
        /// Start FishNet HOST through the relay – used by the room owner (Custom mode).
        ///
        /// How it works:
        ///   1. FishNet Server binds locally (loopback only – not internet-exposed).
        ///   2. FishNet Client connects outbound to relay:sessionPort
        ///      (relay registered this connection as "host" via POST /api/relay/create).
        ///   3. Relay forwards all subsequent packets between host and clients.
        ///
        /// NOTE: This requires the Tugboat transport to be configured so the *server*
        /// bind address is 127.0.0.1 (localhost) and the *client* address is the relay.
        /// A custom RelayTransport shim (future work) will handle the packet header.
        /// </summary>
        public void StartHostWithRelay(string relayIp, ushort relayPort, string sessionId)
        {
            if (networkManager == null) { Debug.LogError("[NetworkGameManager] NetworkManager is null!"); return; }
            Debug.Log($"[NetworkGameManager] Starting HOST via Relay {relayIp}:{relayPort} session={sessionId}");

            // Bind server to loopback so it never accepts direct internet connections
            SetTransportServerBindAddress("127.0.0.1");

            // Start server (loopback only)
            if (!networkManager.ServerManager.StartConnection())
            {
                Debug.LogError("[NetworkGameManager] Failed to start relay host server!");
                return;
            }

            // Connect client side to relay (host traffic is proxied by relay to all clients)
            SetTransportAddress(relayIp, relayPort);
            if (!networkManager.ClientManager.StartConnection())
                Debug.LogError("[NetworkGameManager] Failed to start relay host client!");
            else
                Debug.Log("[NetworkGameManager] Relay Host started.");
        }

        /// <summary>
        /// Connect as a client through the relay – used by non-host players (Custom mode).
        ///
        /// The relay:sessionPort acts as the FishNet server endpoint from the client's view.
        /// </summary>
        public void StartClientWithRelay(string relayIp, ushort relayPort, string sessionId)
        {
            if (networkManager == null) { Debug.LogError("[NetworkGameManager] NetworkManager is null!"); return; }
            Debug.Log($"[NetworkGameManager] Connecting CLIENT via Relay {relayIp}:{relayPort} session={sessionId}");
            SetTransportAddress(relayIp, relayPort);
            if (!networkManager.ClientManager.StartConnection())
                Debug.LogError("[NetworkGameManager] Failed to start relay client connection!");
        }

        // ── Auto-connect (triggered by scene load) ────────────────────────────

        /// <summary>
        /// Called by FishNet SceneManager after any scene finishes loading.
        /// When a game map scene loads, automatically connects to the server
        /// based on the current RoomState.
        /// </summary>
        private void OnSceneLoadEnd(SceneLoadEndEventArgs args)
        {
            foreach (Scene scene in args.LoadedScenes)
            {
                // Game map scenes are named "02_Map_XX"
                if (scene.name.StartsWith("02_Map_", System.StringComparison.OrdinalIgnoreCase))
                {
                    _gameSceneLoaded = true;
                    TryConnectIfReady();
                    return;
                }
            }
        }

        /// <summary>
        /// Called by PartyController when a ds_ready WS event is received.
        /// Sets the DS-ready flag and attempts connection if the game scene is also loaded.
        /// </summary>
        public void NotifyDsReady()
        {
            _dsReady = true;
            Debug.Log("[NetworkGameManager] DS is ready — checking if scene loaded to connect.");
            MatchLoadingOverlay.Instance?.MarkDsReady();
            TryConnectIfReady();
        }

        private void OnDsReadyReceived(GameWebSocketService.DsReadyEvent _) => NotifyDsReady();

        /// <summary>
        /// Initiates connection only when BOTH the game scene is loaded AND the DS signals ready.
        /// Relay games do not need ds_ready (no dedicated server), so they bypass the flag check.
        /// </summary>
        private void TryConnectIfReady()
        {
            if (_connectionStarted) return;

            var room = RoomState.Instance;
            if (room == null)
            {
                Debug.LogWarning("[NetworkGameManager] TryConnectIfReady: RoomState is null — cannot auto-connect. " +
                                 "Use NetworkStartMenu for dev/editor testing (GameMode.None path).");
                return;
            }

            // Relay mode: connect as soon as the scene loads (no DS boot wait)
            if (room.CurrentGameMode == GameMode.Custom_Relay)
            {
                if (!_gameSceneLoaded)
                {
                    Debug.Log("[NetworkGameManager] TryConnectIfReady: waiting for map scene to load (Relay mode).");
                    return;
                }
            }
            else if (room.CurrentGameMode == GameMode.Ranked_DS)
            {
                if (!_gameSceneLoaded)
                {
                    Debug.Log("[NetworkGameManager] TryConnectIfReady: waiting for map scene to load (Ranked_DS mode).");
                    return;
                }
                if (!_dsReady)
                {
                    Debug.Log($"[NetworkGameManager] TryConnectIfReady: waiting for ds_ready WS event " +
                              $"(DsIp={room.DsIp} DsPort={room.DsPort}). " +
                              "DS may still be booting — this is expected if DS was just allocated.");
                    return;
                }
            }
            else
            {
                // GameMode.None — dev/editor; NetworkStartMenu handles connection manually
                Debug.Log("[NetworkGameManager] TryConnectIfReady: GameMode.None — skipping auto-connect. " +
                          "Use NetworkStartMenu buttons (StartServer / StartClientLocal / StartClient) for testing.");
                return;
            }

            Debug.Log($"[NetworkGameManager] ✅ Both conditions met (_gameSceneLoaded={_gameSceneLoaded}, _dsReady={_dsReady}) — auto-connecting.");
            _retryCount        = 0;
            _connectionStarted = false;
            _connected         = false;
            AutoConnectFromRoomState();
        }

        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            switch (args.ConnectionState)
            {
                case LocalConnectionState.Started:
                    _connected = true;
                    Debug.Log("[NetworkGameManager] ✅ Client connected to match server.");
                    MatchLoadingOverlay.Instance?.MarkConnected();
                    break;

                case LocalConnectionState.Stopped:
                    if (_connectionStarted && !_connected)
                    {
                        Debug.LogWarning("[NetworkGameManager] Connection to match server failed.");
                        _connectionStarted = false;
                        TryRetry();
                    }
                    break;
            }
        }

        private void AutoConnectFromRoomState()
        {
            var room = RoomState.Instance;
            if (room == null)
            {
                Debug.LogWarning("[NetworkGameManager] RoomState not found — skipping auto-connect (dev mode).");
                return;
            }

            switch (room.CurrentGameMode)
            {
                case GameMode.Custom_Relay:
                    AutoConnectRelay(room);
                    break;

                case GameMode.Ranked_DS:
                    AutoConnectDS(room);
                    break;

                default:
                    // GameMode.None → dev / editor test, NetworkStartMenu handles it
                    Debug.Log("[NetworkGameManager] GameMode.None — skipping auto-connect (use NetworkStartMenu for dev testing).");
                    break;
            }
        }

        private void AutoConnectRelay(RoomState room)
        {
            if (string.IsNullOrEmpty(room.RelayIp) || room.RelayPort == 0)
            {
                Debug.LogError("[NetworkGameManager] Custom_Relay mode but RelayIp/Port not set in RoomState!");
                return;
            }

            _connectionStarted = true;
            _connected         = false;

            if (room.IsHostPlayer)
            {
                Debug.Log($"[NetworkGameManager] Starting HOST via relay {room.RelayIp}:{room.RelayPort} session={room.RelaySessionId}");
                StartHostWithRelay(room.RelayIp, room.RelayPort, room.RelaySessionId);

                int expected = room.PlayerCount > 0 ? room.PlayerCount : 2;
                if (ServerGameManager.Instance != null)
                {
                    ServerGameManager.Instance.SetExpectedPlayerCount(expected);
                    Debug.Log($"[NetworkGameManager] Host: expectedPlayerCount set to {expected}");
                }
                else
                {
                    Debug.LogWarning("[NetworkGameManager] ServerGameManager.Instance null — expectedPlayerCount not applied.");
                }
            }
            else
            {
                Debug.Log($"[NetworkGameManager] Connecting CLIENT via relay {room.RelayIp}:{room.RelayPort}");
                StartClientWithRelay(room.RelayIp, room.RelayPort, room.RelaySessionId);
            }
        }

        private void AutoConnectDS(RoomState room)
        {
            if (string.IsNullOrEmpty(room.DsIp) || room.DsPort == 0)
            {
                Debug.LogError("[NetworkGameManager] Ranked_DS mode but DsIp/DsPort not set in RoomState!");
                return;
            }

            _connectionStarted = true;
            _connected         = false;

            Debug.Log($"[NetworkGameManager] Connecting to DS {room.DsIp}:{room.DsPort}");
            StartClientDS(room.DsIp, room.DsPort);
        }

        private void TryRetry()
        {
            if (_retryCount >= _maxRetries)
            {
                Debug.LogError("[NetworkGameManager] Max retries reached — returning to home.");
                Invoke(nameof(LoadHome), 1.5f);
                return;
            }
            _retryCount++;
            OnRetryAttempt?.Invoke(_retryCount, _maxRetries);
            Debug.Log($"[NetworkGameManager] Retrying connection in {_retryDelay}s (attempt {_retryCount}/{_maxRetries})…");
            Invoke(nameof(RetryConnect), _retryDelay);
        }

        private void RetryConnect()
        {
            _connected = false;
            AutoConnectFromRoomState();
        }

        private void LoadHome()
        {
            _dsReady         = false;
            _gameSceneLoaded = false;
            RoomState.Instance?.ClearRoom();
            SceneLoader.LoadHome();
        }

        // ── Disconnect ────────────────────────────────────────────────────────

        /// <summary>
        /// Disconnect and optionally notify the relay/backend based on game mode.
        /// Call this from match-end or user leaving.
        /// </summary>
        public async Task DisconnectWithCleanup()
        {
            var roomState = RoomState.Instance;

            if (roomState != null && roomState.CurrentGameMode == GameMode.Custom_Relay
                && !string.IsNullOrEmpty(roomState.RelaySessionId))
            {
                // Notify backend to clean up relay session
                await NotifyRelayCleanup(roomState.RelaySessionId);
            }

            Disconnect();
            roomState?.ClearNetworkSession();
        }

        /// <summary>
        /// Called by MatchFlowCoordinator in Custom_Relay mode (no DS to wait for).
        /// Marks the relay as ready and attempts connection if the game scene is loaded.
        /// </summary>
        public void NotifyRelayReady()
        {
            _dsReady = true;
            Debug.Log("[NetworkGameManager] Relay ready — checking if scene loaded to connect.");
            TryConnectIfReady();
        }

        /// <summary>Static convenience wrapper for <see cref="NotifyDsReady"/> (called from MatchFlowCoordinator).</summary>
        public static void SignalDsReady() => Instance?.NotifyDsReady();

        /// <summary>Resets connection-state flags. Called by RoomState.ClearRoom().</summary>
        public static void ResetConnectionFlags() => Instance?.ResetFlags();

        private void ResetFlags()
        {
            _connectionStarted = false;
            _connected         = false;
            _dsReady           = false;
            _gameSceneLoaded   = false;
            _retryCount        = 0;
        }

        /// <summary>Immediately stop all FishNet connections.</summary>
        public void Disconnect()
        {
            if (networkManager == null) return;
            Debug.Log("[NetworkGameManager] Disconnecting...");
            if (IsServer) networkManager.ServerManager.StopConnection(true);
            if (IsClient) networkManager.ClientManager.StopConnection();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        public int GetPlayerCount()
            => networkManager?.ServerManager?.Clients?.Count ?? 0;

        private void SetTransportAddress(string address, ushort targetPort)
        {
            var transport = networkManager.TransportManager.Transport;
            if (transport == null) { Debug.LogWarning("[NetworkGameManager] Transport is null!"); return; }
            var t = transport.GetType();
            TrySetProperty(transport, t, "ClientAddress", address);
            TrySetProperty(transport, t, "Port", targetPort);
        }

        private void SetTransportServerBindAddress(string bindAddress)
        {
            var transport = networkManager.TransportManager.Transport;
            if (transport == null) return;
            // Tugboat exposes IPv4BindAddress; fall back silently if not found
            TrySetProperty(transport, transport.GetType(), "IPv4BindAddress", bindAddress,
                logMissing: false);
        }

        private static void TrySetProperty(object target, System.Type type, string propName,
            object value, bool logMissing = true)
        {
            try
            {
                var prop = type.GetProperty(propName);
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(target, value);
                    return;
                }
                if (logMissing)
                    Debug.LogWarning($"[NetworkGameManager] Property '{propName}' not found on {type.Name}.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[NetworkGameManager] Could not set '{propName}': {e.Message}");
            }
        }

        /// <summary>
        /// Tell backend to release the relay session.
        /// Fire-and-forget; failures are logged but not fatal.
        /// </summary>
        private async Task NotifyRelayCleanup(string sessionId)
        {
            try
            {
                var backend = NightHunt.Core.GameManager.Instance?.BackendClient;
                if (backend == null) return;
                await backend.DeleteAsync<object>($"/api/relay/{sessionId}");
                Debug.Log($"[NetworkGameManager] Relay session {sessionId} cleaned up.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[NetworkGameManager] Relay cleanup failed (non-fatal): {e.Message}");
            }
        }
    }
}
