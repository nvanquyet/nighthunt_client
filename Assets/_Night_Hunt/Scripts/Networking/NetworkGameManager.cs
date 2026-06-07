using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using NightHunt.Common;
using NightHunt.Core;
using NightHunt.Data.DTOs;
using NightHunt.State;
using NightHunt.UI;
using NightHunt.Services.Game;
using System.Collections;
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
        private bool _intentionalDisconnect;
        private bool _dsReady;          // true after ds_ready WS received
        private bool _relayHostReady;   // true after relay_host_ready WS received
        private bool _relayHostReadyReported;
        private bool _gameSceneLoaded;  // true after 02_Map_* scene finishes loading
        private bool _reconnectModalOpen;
        private bool _returningHome;
        private float _connectedSinceRealtime = -1f;
        private static readonly byte[] RelayHostRegistrationPayload =
            { 78, 72, 95, 82, 69, 76, 65, 89, 95, 72, 79, 83, 84 };

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
            if (networkManager.SceneManager == null || networkManager.ClientManager == null)
            {
                Debug.LogError("[NetworkGameManager] NetworkManager did not finish initializing. Check earlier FishNet errors, especially DefaultPrefabObjects spawnable prefab registry errors.");
                return;
            }

            networkManager.SceneManager.OnLoadEnd               += OnSceneLoadEnd;
            networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
            if (GameWebSocketService.Instance != null)
            {
                GameWebSocketService.Instance.OnDsReady += OnDsReadyReceived;
                GameWebSocketService.Instance.OnRelayHostReady += OnRelayHostReadyReceived;
            }

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

            // ── Fix C: catch Unity-native scene loads ────────────────────────
            // SceneLoader.LoadGame() calls Unity's SceneManager.LoadSceneAsync which
            // DOES NOT fire FishNet's SceneManager.OnLoadEnd. The DontDestroyOnLoad
            // instance started in Home so Fix A above never triggers for it.
            // Subscribing here ensures _gameSceneLoaded is set when the map scene
            // activates regardless of how it was loaded.
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnUnitySceneLoaded;

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
            {
                GameWebSocketService.Instance.OnDsReady -= OnDsReadyReceived;
                GameWebSocketService.Instance.OnRelayHostReady -= OnRelayHostReadyReceived;
            }
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnUnitySceneLoaded;
            base.OnDestroy();
        }

        /// <summary>
        /// Unity-native scene-loaded callback. Catches map scenes loaded via
        /// SceneLoader.LoadGame() (which uses LoadSceneAsync, not FishNet's loader)
        /// so the DontDestroyOnLoad singleton can set _gameSceneLoaded and
        /// trigger TryConnectIfReady() even in the normal Home→Game flow.
        /// </summary>
        private void OnUnitySceneLoaded(UnityEngine.SceneManagement.Scene scene,
                                        UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (scene.name.StartsWith("02_Map_", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"[NetworkGameManager] Unity scene loaded: '{scene.name}' — setting _gameSceneLoaded = true.");
                _gameSceneLoaded = true;
                TryConnectIfReady();
            }
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
        ///   1. FishNet Server binds to 0.0.0.0 (all interfaces).
        ///      MUST NOT bind to 127.0.0.1: the relay forwards packets from its own IP,
        ///      not from loopback, so a loopback-bound server drops all relay traffic.
        ///   2. FishNet Client connects outbound to relay:sessionPort.
        ///   3. Relay forwards all subsequent packets between host and non-host clients.
        ///
        /// Security: session isolation is enforced by the relay via session-token; binding
        /// 0.0.0.0 does not expose the server to arbitrary internet connections.
        /// </summary>
        public void StartHostWithRelay(string relayIp, ushort relayPort, string sessionId)
        {
            if (networkManager == null) { Debug.LogError("[NetworkGameManager] NetworkManager is null!"); return; }
            Debug.Log($"[FLOW §5] NetworkGameManager.StartHostWithRelay: relay={relayIp}:{relayPort} session={sessionId}  t={System.DateTime.UtcNow:HH:mm:ss.fff}");

            // ⚠️  MUST bind 0.0.0.0, NOT 127.0.0.1.
            // The relay server forwards packets originating from its own public IP.
            // A loopback-only bind rejects those packets, causing:
            //   • Client packets never reaching the host server → no FishNet server ack
            //   • Host's own client-side prediction never reconciled  → frozen movement
            SetTransportServerBindAddress("0.0.0.0");
            // FIX: use relayPort (dynamic, from backend) — NOT the inspector `port` field (hardcoded 7777).
            // The relay server allocates a unique UDP port per session (e.g. 7779).
            // Both the server bind AND the host-client connect MUST use this same port
            // so the relay can forward packets to the correct session.
            SetTransportPort(relayPort);
            Debug.Log($"[FLOW §5] Host server binding 0.0.0.0:{relayPort} (relay session={sessionId}) — required for relay forwarding.");

            // Start FishNet server
            if (!networkManager.IsServerStarted && !networkManager.ServerManager.StartConnection())
            {
                Debug.LogError("[NetworkGameManager] Failed to start relay host server!");
                return;
            }
            Debug.Log("[FLOW §5] FishNet server started.");

            StartCoroutine(StartRelayHostClientAfterRegistration(relayIp, relayPort));

        }

        private IEnumerator StartRelayHostClientAfterRegistration(string relayIp, ushort relayPort)
        {
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!networkManager.IsServerStarted && Time.realtimeSinceStartup < deadline)
                yield return null;

            for (int i = 0; i < 5; i++)
            {
                bool sent = TrySendRelayHostRegistration(relayIp, relayPort);
                Debug.Log($"[FLOW 5] Relay host registration packet #{i + 1} sent={sent} relay={relayIp}:{relayPort}");
                yield return new WaitForSecondsRealtime(0.1f);
            }

            var hostPorts = RoomState.Instance?.RelayHostPorts;
            if (hostPorts == null || hostPorts.Length == 0)
            {
                Debug.LogError("[FLOW 5] Relay host upstream ports missing. Backend/relay must provide relayHostPorts before Custom_Relay can accept remote guests.");
                yield break;
            }

            for (int i = 0; i < 3; i++)
            {
                foreach (ushort hostPort in hostPorts)
                {
                    bool sent = TrySendRelayHostRegistration(relayIp, hostPort);
                    Debug.Log($"[FLOW 5] Relay host upstream punch #{i + 1} sent={sent} relay={relayIp}:{hostPort}");
                }
                yield return new WaitForSecondsRealtime(0.1f);
            }

            if (networkManager.IsClientStarted)
            {
                Debug.Log("[FLOW 5] Relay host client already started; registration refresh complete.");
                yield break;
            }

            // The host's own client runs in the same process/device as the server.
            // Do not route that local loopback connection through the public relay:
            // many NATs only allow replies from the exact relay session port, while
            // the relay may use separate upstream sockets for forwarded clients.
            // The server still advertises/registers with the relay for remote players.
            const string hostLoopbackAddress = "127.0.0.1";
            SetTransportAddress(hostLoopbackAddress, relayPort);
            if (!networkManager.ClientManager.StartConnection())
                Debug.LogError("[NetworkGameManager] Failed to start relay host local client!");
            else
                Debug.Log($"[FLOW 5] Relay Host local client connecting -> {hostLoopbackAddress}:{relayPort} (relay advertised {relayIp}:{relayPort})");
        }

        private bool TrySendRelayHostRegistration(string relayIp, ushort relayPort)
        {
            var transport = networkManager?.TransportManager?.Transport;
            if (transport == null)
                return false;

            var method = transport.GetType().GetMethod(
                "SendServerUnconnectedMessage",
                new[] { typeof(byte[]), typeof(string), typeof(ushort) });

            if (method == null)
            {
                Debug.LogWarning($"[NetworkGameManager] Transport {transport.GetType().Name} cannot send relay host registration.");
                return false;
            }

            try
            {
                return method.Invoke(transport, new object[] { RelayHostRegistrationPayload, relayIp, relayPort }) is bool ok && ok;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[NetworkGameManager] Relay host registration failed: {e.Message}");
                return false;
            }
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

        private void OnRelayHostReadyReceived(GameWebSocketService.RelayHostReadyEvent _)
        {
            _relayHostReady = true;
            RoomState.Instance?.SetRelayHostReady(true);
            Debug.Log("[NetworkGameManager] Relay host is ready - checking if scene loaded to connect.");
            MatchLoadingOverlay.Instance?.MarkDsReady();
            TryConnectIfReady();
        }

        public void PrepareForMatchLoad()
        {
            CancelInvoke(nameof(RetryConnect));
            CancelInvoke(nameof(LoadHome));
            _retryCount = 0;
            _connectionStarted = false;
            _connected = false;
            _connectedSinceRealtime = -1f;
            _gameSceneLoaded = false;
            _dsReady = false;
            _relayHostReady = false;
            _relayHostReadyReported = false;
            RoomState.Instance?.SetRelayHostReady(false);
            HideReconnectModal(showToast: false);
        }

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

            // Relay mode: the host starts after the map scene loads; non-host clients
            // wait for relay_host_ready so they never race ahead of the host server.
            if (room.CurrentGameMode == GameMode.Custom_Relay)
            {
                if (!_gameSceneLoaded)
                {
                    Debug.Log("[NetworkGameManager] TryConnectIfReady: waiting for map scene to load (Relay mode).");
                    return;
                }

                bool relayHostReady = room.IsHostPlayer || _relayHostReady || room.RelayHostReady;
                if (!relayHostReady)
                {
                    Debug.Log("[NetworkGameManager] TryConnectIfReady: waiting for relay_host_ready before non-host client connects.");
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
            Debug.Log($"[NetworkGameManager] Connect gates met: mode={room.CurrentGameMode}, sceneLoaded={_gameSceneLoaded}, dsReady={_dsReady}, relayHostReady={_relayHostReady || room.RelayHostReady}, isHost={room.IsHostPlayer}.");
            AutoConnectFromRoomState();
        }

        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            switch (args.ConnectionState)
            {
                case LocalConnectionState.Started:
                    _intentionalDisconnect = false;
                    _returningHome = false;
                    _connected = true;
                    _connectedSinceRealtime = Time.realtimeSinceStartup;
                    _retryCount = 0;
                    CancelInvoke(nameof(RetryConnect));
                    CancelInvoke(nameof(LoadHome));
                    Debug.Log("[NetworkGameManager] ✅ Client connected to match server.");
                    MatchLoadingOverlay.Instance?.MarkConnected();
                    HideReconnectModal();
                    TryReportRelayHostReady();
                    break;

                case LocalConnectionState.Stopped:
                    if (_intentionalDisconnect)
                    {
                        _connectionStarted = false;
                        _connected = false;
                        _connectedSinceRealtime = -1f;
                        _intentionalDisconnect = false;
                        HideReconnectModal(showToast: false);
                        break;
                    }

                    if (_connectionStarted)
                    {
                        bool wasConnected = _connected;
                        float connectedFor = _connectedSinceRealtime >= 0f
                            ? Time.realtimeSinceStartup - _connectedSinceRealtime
                            : 0f;
                        bool showReconnectUi = wasConnected && connectedFor >= 2f;
                        _connected = false;
                        _connectionStarted = false;
                        if (wasConnected)
                        {
                            Debug.LogWarning($"[NetworkGameManager] Match server connection dropped after {connectedFor:F2}s. Attempting reconnect.");
                            if (showReconnectUi)
                                ShowReconnectModal();
                        }
                        else
                        {
                            Debug.LogWarning("[NetworkGameManager] Connection to match server failed.");
                        }
                        TryRetry(showReconnectUi);
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

            _returningHome = false;
            _connectionStarted = true;
            _connected         = false;
            _connectedSinceRealtime = -1f;

            if (room.IsHostPlayer)
            {
                Debug.Log($"[FLOW §5] AutoConnectRelay: IsHostPlayer=true → StartHostWithRelay {room.RelayIp}:{room.RelayPort}  t={System.DateTime.UtcNow:HH:mm:ss.fff}");
                StartHostWithRelay(room.RelayIp, room.RelayPort, room.RelaySessionId);

                int expected = room.PlayerCount > 0 ? room.PlayerCount : 2;
                if (ServerGameManager.Instance != null)
                {
                    ServerGameManager.Instance.SetExpectedPlayerCount(expected);
                    Debug.Log($"[FLOW §5] Host: expectedPlayerCount set to {expected}");
                }
                else
                {
                    Debug.LogWarning("[NetworkGameManager] ServerGameManager.Instance null — expectedPlayerCount not applied.");
                }
            }
            else
            {
                Debug.Log($"[FLOW §5] AutoConnectRelay: IsHostPlayer=false → StartClientWithRelay {room.RelayIp}:{room.RelayPort}  t={System.DateTime.UtcNow:HH:mm:ss.fff}");
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

            _returningHome = false;
            _connectionStarted = true;
            _connected         = false;
            _connectedSinceRealtime = -1f;

            Debug.Log($"[NetworkGameManager] Connecting to DS {room.DsIp}:{room.DsPort}");
            StartClientDS(room.DsIp, room.DsPort);
        }

        private void TryRetry(bool showUi = true)
        {
            if (_retryCount >= _maxRetries)
            {
                Debug.LogError("[NetworkGameManager] Max retries reached — returning to home.");
                OnRetryAttempt?.Invoke(_maxRetries + 1, _maxRetries);
                if (showUi)
                    HandleReconnectUI(_maxRetries + 1, _maxRetries);
                Invoke(nameof(LoadHome), 1.5f);
                return;
            }
            _retryCount++;
            Debug.Log($"[NetworkGameManager] Retrying connection in {_retryDelay}s (attempt {_retryCount}/{_maxRetries})…");
            OnRetryAttempt?.Invoke(_retryCount, _maxRetries);
            if (showUi)
                HandleReconnectUI(_retryCount, _maxRetries);
            CancelInvoke(nameof(RetryConnect));
            Invoke(nameof(RetryConnect), _retryDelay);
        }

        private void RetryConnect()
        {
            _connected = false;
            AutoConnectFromRoomState();
        }

        private void LoadHome()
        {
            if (_returningHome)
                return;

            _returningHome = true;
            CancelInvoke(nameof(RetryConnect));
            CancelInvoke(nameof(LoadHome));
            ReportOwnMatchPresenceBestEffort("DISCONNECTED", "CLIENT_CONNECT_FAILED");
            Disconnect();
            _dsReady         = false;
            _gameSceneLoaded = false;
            RoomState.Instance?.ClearRoom();
            HideReconnectModal(showToast: false);
            if (SceneLoader.IsInHomeScene)
            {
                UINavigator.Instance?.GoHome();
                return;
            }
            SceneLoader.LoadHome();
        }

        // ── Disconnect ────────────────────────────────────────────────────────

        public void ReturnHomeAfterConnectionFailure()
        {
            ReturnHomeFromReconnect();
        }

        /// <summary>
        /// Disconnect and optionally notify the relay/backend based on game mode.
        /// Call this from match-end or user leaving.
        /// </summary>
        public async Task DisconnectWithCleanup()
        {
            var roomState = RoomState.Instance;
            string relaySessionId = roomState?.RelaySessionId;
            bool cleanupRelay = roomState != null
                && roomState.CurrentGameMode == GameMode.Custom_Relay
                && !string.IsNullOrEmpty(relaySessionId);

            Disconnect();
            roomState?.ClearNetworkSession();

            if (cleanupRelay)
            {
                // Notify backend to clean up relay session
                await NotifyRelayCleanup(relaySessionId);
            }
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
            CancelInvoke(nameof(RetryConnect));
            CancelInvoke(nameof(LoadHome));
            _connectionStarted = false;
            _connected         = false;
            _connectedSinceRealtime = -1f;
            _dsReady           = false;
            _relayHostReady    = false;
            _relayHostReadyReported = false;
            _gameSceneLoaded   = false;
            _retryCount        = 0;
            HideReconnectModal(showToast: false);
        }

        /// <summary>Immediately stop all FishNet connections.</summary>
        public void Disconnect()
        {
            if (networkManager == null) return;
            Debug.Log("[NetworkGameManager] Disconnecting...");
            _intentionalDisconnect = true;
            if (IsServer) networkManager.ServerManager.StopConnection(true);
            if (IsClient) networkManager.ClientManager.StopConnection();
        }

        // ── Reconnect UI ──────────────────────────────────────────────────────

        private void ShowReconnectModal()
        {
            if (_reconnectModalOpen) return;
            GameModalWindow.Instance?.ShowConfirm(
                "Connection Lost",
                "Reconnecting to game server...",
                onConfirm: () => { _reconnectModalOpen = false; },
                onCancel:  ReturnHomeFromReconnect,
                confirmText: "Keep Waiting",
                cancelText:  "Return Home");
            _reconnectModalOpen = true;
        }

        private void HideReconnectModal(bool showToast = true)
        {
            bool wasOpen = _reconnectModalOpen;
            GameModalWindow.Instance?.Close();
            _reconnectModalOpen = false;
            if (showToast && wasOpen)
                PersistentUICanvas.Instance?.ToastService?.Show("Reconnected", "Game connection restored.");
        }

        private void HandleReconnectUI(int current, int max)
        {
            if (current > max)
            {
                _reconnectModalOpen = false;
                GameModalWindow.Instance?.ShowNotice(
                    "Connection Failed",
                    "Could not reconnect to the game server.",
                    "Return Home",
                    ReturnHomeFromReconnect);
                _reconnectModalOpen = true;
                return;
            }

            string status = $"Reconnecting... (attempt {current}/{max})";
            if (_reconnectModalOpen)
                GameModalWindow.Instance?.UpdateDescription(status);
            else
                PersistentUICanvas.Instance?.ToastService?.Show("Reconnecting", status);
        }

        private void ReturnHomeFromReconnect()
        {
            if (_returningHome)
                return;

            _returningHome = true;
            CancelInvoke(nameof(RetryConnect));
            CancelInvoke(nameof(LoadHome));
            _reconnectModalOpen = false;
            ReportOwnMatchPresenceBestEffort("DISCONNECTED", "CLIENT_RETURN_HOME");
            Disconnect();
            RoomState.Instance?.ClearRoom();
            RoomState.Instance?.ClearNetworkSession();
            if (SceneLoader.IsInHomeScene)
            {
                UINavigator.Instance?.GoHome();
                return;
            }
            SceneLoader.LoadHome();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void TryReportRelayHostReady()
        {
            var room = RoomState.Instance;
            if (room == null
                || room.CurrentGameMode != GameMode.Custom_Relay
                || !room.IsHostPlayer
                || _relayHostReadyReported)
                return;

            if (networkManager == null || !networkManager.IsServerStarted)
            {
                Debug.Log("[NetworkGameManager] Relay host local client connected, but server is not started yet; delaying host-ready report.");
                Invoke(nameof(TryReportRelayHostReady), 0.5f);
                return;
            }

            long roomId = room.RoomId;
            if (roomId <= 0)
            {
                Debug.LogWarning("[NetworkGameManager] Cannot report relay_host_ready: RoomState has no roomId.");
                return;
            }

            _relayHostReadyReported = true;
            _relayHostReady = true;
            room.SetRelayHostReady(true);
            MatchLoadingOverlay.Instance?.MarkDsReady();
            _ = ReportRelayHostReadyAsync(roomId);
        }

        private async Task ReportRelayHostReadyAsync(long roomId)
        {
            try
            {
                var backend = GameManager.Instance?.BackendClient;
                if (backend == null)
                {
                    Debug.LogWarning("[NetworkGameManager] Cannot report relay_host_ready: BackendClient is missing.");
                    _relayHostReadyReported = false;
                    Invoke(nameof(TryReportRelayHostReady), 1f);
                    return;
                }

                string endpoint = string.Format(Constants.API_ROOMS_RELAY_HOST_READY, roomId);
                var result = await backend.PostAsync<RoomResponse>(endpoint);
                if (result == null || !result.Success)
                {
                    Debug.LogWarning($"[NetworkGameManager] relay_host_ready report failed: {result?.Message ?? "no response"}");
                    _relayHostReadyReported = false;
                    Invoke(nameof(TryReportRelayHostReady), 1f);
                    return;
                }

                Debug.Log($"[NetworkGameManager] relay_host_ready reported to backend for roomId={roomId}.");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[NetworkGameManager] relay_host_ready report failed: {ex.Message}");
                _relayHostReadyReported = false;
                Invoke(nameof(TryReportRelayHostReady), 1f);
            }
        }

        private void ReportOwnMatchPresenceBestEffort(string state, string reason)
        {
            var roomState = RoomState.Instance;
            string matchId = roomState?.CurrentMatchId;
            if (string.IsNullOrEmpty(matchId))
                matchId = roomState?.CurrentRoom?.matchId;

            long userId = SessionState.Instance?.UserId ?? 0L;
            if (string.IsNullOrEmpty(matchId) || userId <= 0L)
                return;

            _ = ReportOwnMatchPresenceAsync(matchId, userId, state, reason);
        }

        private async Task ReportOwnMatchPresenceAsync(string matchId, long userId, string state, string reason)
        {
            try
            {
                var backend = GameManager.Instance?.BackendClient;
                if (backend == null)
                {
                    Debug.LogWarning($"[NetworkGameManager] Cannot report match presence {state}: BackendClient is missing.");
                    return;
                }

                var result = await backend.PostAsync<object>(Constants.API_MATCH_PRESENCE,
                    new MatchPresenceRequest
                    {
                        matchId = matchId,
                        userId = userId,
                        state = state,
                        reason = reason
                    });

                if (result == null || !result.Success)
                    Debug.LogWarning($"[NetworkGameManager] Match presence {state}/{reason} failed: {result?.Message ?? "no response"}");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[NetworkGameManager] Match presence {state}/{reason} failed: {ex.Message}");
            }
        }

        public int GetPlayerCount()
            => networkManager?.ServerManager?.Clients?.Count ?? 0;

        private void SetTransportAddress(string address, ushort targetPort)
        {
            var transport = networkManager.TransportManager.Transport;
            if (transport == null) { Debug.LogWarning("[NetworkGameManager] Transport is null!"); return; }
            transport.SetClientAddress(address);
            transport.SetPort(targetPort);
        }

        private void SetTransportPort(ushort targetPort)
        {
            var transport = networkManager.TransportManager.Transport;
            if (transport == null) { Debug.LogWarning("[NetworkGameManager] Transport is null!"); return; }
            transport.SetPort(targetPort);
        }

        private void SetTransportServerBindAddress(string bindAddress)
        {
            var transport = networkManager.TransportManager.Transport;
            if (transport == null) return;
            transport.SetServerBindAddress(bindAddress, IPAddressType.IPv4);
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
        private async Task NotifyRelayCleanup(string sessionToken)
        {
            try
            {
                var backend = NightHunt.Core.GameManager.Instance?.BackendClient;
                if (backend == null) return;
                // Backend: POST /api/relay/leave  { sessionToken: "..." }
                var body = new { sessionToken };
                await backend.PostAsync<object>("/api/relay/leave", body);
                Debug.Log($"[NetworkGameManager] Relay session {sessionToken} cleaned up.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[NetworkGameManager] Relay cleanup failed (non-fatal): {e.Message}");
            }
        }
    }
}
