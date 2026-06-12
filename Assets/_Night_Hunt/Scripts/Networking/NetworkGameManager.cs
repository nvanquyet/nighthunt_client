using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using FishNet.Transporting.Tugboat;
using NightHunt.Common;
using NightHunt.Core;
using NightHunt.Data.DTOs;
using NightHunt.GameplaySystems.Core.Configs;
using NightHunt.Networking.Player;
using NightHunt.Networking.Relay;
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
        [Tooltip("Minimum retry delay for relay mode. Must be longer than relay upstream cooldown.")]
        [SerializeField] private float _relayRetryDelay = 7f;
        [Tooltip("Maximum relay reconnect attempts. Keep this aligned with the server-side relay reconnect hold window.")]
        [SerializeField] private int _relayMaxRetries = 10;
        [Tooltip("Seconds a connection must stay up before retry counter is reset.")]
        [SerializeField] private float _stableConnectionSecondsToResetRetries = 15f;
        [Tooltip("Extra settle time after host upstream punch before clients receive relay_host_ready.")]
        [SerializeField] private float _relayHostReadySettleSeconds = 1.0f;
        [SerializeField] private int   _maxRetries = 2;
        private int _defaultMaxRetries = -1;

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
        private RelayIdentityPacketLayer _activeRelayIdentityLayer;
        private string _activeRelayIdentitySessionId;
        private ulong _activeRelayIdentityPeerId;
        private bool _relayHostUpstreamPunchComplete;
        private float _relayHostReadyEarliestRealtime = -1f;
        private Coroutine _markGameSceneLoadedCoroutine;
        private bool _clientHandshakeBroadcastRegistered;
        private float _lastPlayerDataBroadcastRealtime = -1f;
        private const float PlayerDataBroadcastThrottleSeconds = 0.25f;
        private const int UnitySceneActivationSettleFrames = 2;
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
            ConfigureConnectionDropTrace();
            if (networkManager.SceneManager == null || networkManager.ClientManager == null)
            {
                Debug.LogError("[NetworkGameManager] NetworkManager did not finish initializing. Check earlier FishNet errors, especially DefaultPrefabObjects spawnable prefab registry errors.");
                return;
            }

            networkManager.SceneManager.OnLoadEnd               += OnSceneLoadEnd;
            networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
            RegisterClientHandshakeBroadcast();
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
            // Recover only if RoomState was marked ready by the real ds_ready path.
            if (!_dsReady
                && RoomState.Instance?.CurrentGameMode == GameMode.Ranked_DS
                && RoomState.Instance.DedicatedServerReady)
            {
                Debug.Log($"[NH_CONN][GATE] Start: recovered ds_ready from RoomState; DS={RoomState.Instance.DsIp}:{RoomState.Instance.DsPort}.");
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
            UnregisterClientHandshakeBroadcast();
            if (GameWebSocketService.Instance != null)
            {
                GameWebSocketService.Instance.OnDsReady -= OnDsReadyReceived;
                GameWebSocketService.Instance.OnRelayHostReady -= OnRelayHostReadyReceived;
            }
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnUnitySceneLoaded;
            CancelGameSceneLoadedDelay();
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
                Debug.Log($"[NetworkGameManager] Unity scene loaded: '{scene.name}' — delaying match connect until next frame.");
                if (_markGameSceneLoadedCoroutine != null)
                    StopCoroutine(_markGameSceneLoadedCoroutine);
                _markGameSceneLoadedCoroutine = StartCoroutine(MarkGameSceneLoadedAfterUnityActivation(scene.name));
            }
        }

        private IEnumerator MarkGameSceneLoadedAfterUnityActivation(string sceneName)
        {
            // Unity's sceneLoaded fires as the scene activates. FishNet scene-object
            // lookup is safer after a couple frames, once scene NetworkObjects have
            // completed their activation pass in both Editor and player builds.
            for (int i = 0; i < UnitySceneActivationSettleFrames; i++)
                yield return null;

            _markGameSceneLoadedCoroutine = null;
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded
                || !activeScene.name.StartsWith("02_Map_", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning(
                    $"[NH_CONN][GATE] Delayed scene-ready skipped: loadedScene={sceneName} " +
                    $"activeScene={activeScene.name} valid={activeScene.IsValid()} loaded={activeScene.isLoaded}.");
                yield break;
            }

            Debug.Log($"[NH_CONN][GATE] Unity map scene ready after activation frame: '{activeScene.name}'.");
            _gameSceneLoaded = true;
            TryConnectIfReady();
        }

        // ── Dedicated Server (Ranked mode) ────────────────────────────────────

        /// <summary>
        /// Start FishNet Server only (dedicated server build / dev tools).
        /// Called by NetworkStartMenu (dev tool).
        /// </summary>
        public void StartServer()
        {
            if (networkManager == null) { Debug.LogError("[NetworkGameManager] NetworkManager is null!"); return; }
            ConfigureConnectionDropTrace();
            ClearRelayPacketLayer();
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
            ConfigureConnectionDropTrace();
            ConfigureTransportTimeout();
            string ip = string.IsNullOrEmpty(dsIp) ? defaultServerAddress : dsIp;
            ushort p  = dsPort > 0 ? dsPort : port;
            ClearRelayPacketLayer();
            ConnectionDropTrace.BeginClientAttempt(
                "StartClientDS",
                "ds-client",
                $"{ip}:{p}",
                RoomState.Instance?.CurrentGameMode.ToString() ?? "unknown",
                null,
                _retryCount,
                _maxRetries);
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
            ConfigureConnectionDropTrace();
            ConfigureTransportTimeout();
            Debug.Log($"[NH_CONN][NH_RELAY][NH_HANDSHAKE][HOST_START] StartHostWithRelay relay={relayIp}:{relayPort} session={sessionId} t={System.DateTime.UtcNow:HH:mm:ss.fff}");
            _relayHostUpstreamPunchComplete = false;
            _relayHostReadyEarliestRealtime = -1f;

            // ⚠️  MUST bind 0.0.0.0, NOT 127.0.0.1.
            // The relay server forwards packets originating from its own public IP.
            // A loopback-only bind rejects those packets, causing:
            //   • Client packets never reaching the host server → no FishNet server ack
            //   • Host's own client-side prediction never reconciled  → frozen movement
            ConfigureRelayPacketLayer(sessionId);
            SetTransportServerBindAddress("0.0.0.0");
            EnsureRelayOwnedObjectPreserver();
            // FIX: use relayPort (dynamic, from backend) — NOT the inspector `port` field (hardcoded 7777).
            // The relay server allocates a unique UDP port per session (e.g. 7779).
            // Both the server bind AND the host-client connect MUST use this same port
            // so the relay can forward packets to the correct session.
            SetTransportPort(relayPort);
            Debug.Log($"[NH_CONN][NH_RELAY][HOST_BIND] Host server binding 0.0.0.0:{relayPort} relaySession={sessionId}");

            // Start FishNet server
            if (!networkManager.IsServerStarted && !networkManager.ServerManager.StartConnection())
            {
                Debug.LogError("[NH_CONN][NH_RELAY][NH_DROP][HOST_START_FAIL] Failed to start relay host server.");
                return;
            }
            Debug.Log("[NH_CONN][NH_RELAY][HOST_STARTED] FishNet server started.");

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
                Debug.Log($"[NH_CONN][NH_RELAY][HOST_REGISTER] Relay host registration packet #{i + 1} sent={sent} relay={relayIp}:{relayPort}");
                yield return new WaitForSecondsRealtime(0.1f);
            }

            var hostPorts = RoomState.Instance?.RelayHostPorts;
            if (hostPorts == null || hostPorts.Length == 0)
            {
                Debug.LogError("[NH_CONN][NH_RELAY][NH_DROP][HOST_UPSTREAM_MISSING] Relay host upstream ports missing. Backend/relay must provide relayHostPorts before Custom_Relay can accept remote guests.");
                yield break;
            }

            for (int i = 0; i < 3; i++)
            {
                foreach (ushort hostPort in hostPorts)
                {
                    bool sent = TrySendRelayHostRegistration(relayIp, hostPort);
                    Debug.Log($"[NH_CONN][NH_RELAY][HOST_UPSTREAM_PUNCH] Relay host upstream punch #{i + 1} sent={sent} relay={relayIp}:{hostPort}");
                }
                yield return new WaitForSecondsRealtime(0.1f);
            }

            _relayHostUpstreamPunchComplete = true;
            _relayHostReadyEarliestRealtime = Time.realtimeSinceStartup + Mathf.Max(0f, _relayHostReadySettleSeconds);
            Debug.Log($"[NH_CONN][NH_RELAY][HOST_UPSTREAM_READY] Host upstream punches complete ports={hostPorts.Length} settle={Mathf.Max(0f, _relayHostReadySettleSeconds):F1}s.");

            if (networkManager.IsClientStarted)
            {
                Debug.Log("[NH_CONN][NH_RELAY][HOST_CLIENT_ALREADY_STARTED] Relay host client already started; registration refresh complete.");
                TryReportRelayHostReady();
                yield break;
            }

            // The host's own client runs in the same process/device as the server.
            // Do not route that local loopback connection through the public relay:
            // many NATs only allow replies from the exact relay session port, while
            // the relay may use separate upstream sockets for forwarded clients.
            // The server still advertises/registers with the relay for remote players.
            const string hostLoopbackAddress = "127.0.0.1";
            SetTransportAddress(hostLoopbackAddress, relayPort);
            ConnectionDropTrace.BeginClientAttempt(
                "StartRelayHostClientAfterRegistration",
                "relay-host-local-client",
                $"{hostLoopbackAddress}:{relayPort}",
                RoomState.Instance?.CurrentGameMode.ToString() ?? "unknown",
                _activeRelayIdentityLayer,
                _retryCount,
                _maxRetries);
            if (!networkManager.ClientManager.StartConnection())
                Debug.LogError("[NH_CONN][NH_RELAY][NH_DROP][HOST_LOCAL_CLIENT_FAIL] Failed to start relay host local client.");
            else
                Debug.Log($"[NH_CONN][NH_RELAY][HOST_LOCAL_CLIENT_START] Relay host local client connecting -> {hostLoopbackAddress}:{relayPort} advertised={relayIp}:{relayPort}");
        }

        private bool TrySendRelayHostRegistration(string relayIp, ushort relayPort)
        {
            var transport = networkManager?.TransportManager?.Transport as Tugboat;
            if (transport == null)
            {
                Debug.LogWarning("[NH_CONN][NH_RELAY][NH_DROP][HOST_REGISTER_UNSUPPORTED] Relay host registration requires Tugboat transport.");
                return false;
            }

            try
            {
                return transport.SendServerUnconnectedMessage(RelayHostRegistrationPayload, relayIp, relayPort);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[NH_CONN][NH_RELAY][NH_DROP][HOST_REGISTER_FAIL] Relay host registration failed: {e.Message}");
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
            ConfigureConnectionDropTrace();
            ConfigureTransportTimeout();
            Debug.Log($"[NH_CONN][NH_RELAY][NH_HANDSHAKE][CLIENT_START] StartClientWithRelay relay={relayIp}:{relayPort} session={sessionId} t={System.DateTime.UtcNow:HH:mm:ss.fff}");
            ConfigureRelayPacketLayer(sessionId, forceNewLayer: true);
            SetTransportAddress(relayIp, relayPort);
            ConnectionDropTrace.BeginClientAttempt(
                "StartClientWithRelay",
                "relay-client",
                $"{relayIp}:{relayPort}",
                RoomState.Instance?.CurrentGameMode.ToString() ?? "unknown",
                _activeRelayIdentityLayer,
                _retryCount,
                _maxRetries);
            if (!networkManager.ClientManager.StartConnection())
                Debug.LogError("[NH_CONN][NH_RELAY][NH_DROP][CLIENT_START_FAIL] Failed to start relay client connection.");
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
            CancelInvoke(nameof(MarkConnectionStable));
            CancelGameSceneLoadedDelay();
            MatchLoadingOverlay.ResetReadinessSignal();
            _retryCount = 0;
            _connectionStarted = false;
            _connected = false;
            _connectedSinceRealtime = -1f;
            _gameSceneLoaded = false;
            _dsReady = false;
            _relayHostReady = false;
            _relayHostReadyReported = false;
            _relayHostUpstreamPunchComplete = false;
            _relayHostReadyEarliestRealtime = -1f;
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
                    Debug.Log("[NH_CONN][GATE] TryConnectIfReady waiting for map scene to load mode=Custom_Relay.");
                    return;
                }

                bool relayHostReady = room.IsHostPlayer || _relayHostReady || room.RelayHostReady;
                if (!relayHostReady)
                {
                    Debug.Log("[NH_CONN][GATE] TryConnectIfReady waiting for relay_host_ready before non-host client connects.");
                    return;
                }
            }
            else if (room.CurrentGameMode == GameMode.Ranked_DS)
            {
                if (!_gameSceneLoaded)
                {
                    Debug.Log("[NH_CONN][GATE] TryConnectIfReady waiting for map scene to load mode=Ranked_DS.");
                    return;
                }
                if (!_dsReady && room.DedicatedServerReady)
                {
                    _dsReady = true;
                    Debug.Log($"[NH_CONN][GATE] TryConnectIfReady: recovered ds_ready from RoomState; DS={room.DsIp}:{room.DsPort}.");
                }
                if (!_dsReady)
                {
                    Debug.Log($"[NH_CONN][GATE] TryConnectIfReady waiting for ds_ready WS event " +
                              $"(DsIp={room.DsIp} DsPort={room.DsPort} roomReady={room.DedicatedServerReady}). " +
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

            Debug.Log($"[NH_CONN][GATE] Connect gates satisfied sceneLoaded={_gameSceneLoaded} dsReady={_dsReady}; auto-connecting.");
            _connected         = false;
            Debug.Log($"[NH_CONN][GATE] Connect gates detail mode={room.CurrentGameMode} sceneLoaded={_gameSceneLoaded} dsReady={_dsReady} relayHostReady={_relayHostReady || room.RelayHostReady} isHost={room.IsHostPlayer}.");
            AutoConnectFromRoomState();
        }

        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            ConfigureConnectionDropTrace();
            float currentConnectedFor = _connectedSinceRealtime >= 0f
                ? Time.realtimeSinceStartup - _connectedSinceRealtime
                : 0f;
            ConnectionDropTrace.MarkClientState(
                "NetworkGameManager.OnClientConnectionState",
                args.ConnectionState.ToString(),
                _connectionStarted,
                _connected,
                _intentionalDisconnect,
                _retryCount,
                _maxRetries,
                currentConnectedFor);

            switch (args.ConnectionState)
            {
                case LocalConnectionState.Started:
                    _intentionalDisconnect = false;
                    _returningHome = false;
                    _connected = true;
                    _connectedSinceRealtime = Time.realtimeSinceStartup;
                    CancelInvoke(nameof(RetryConnect));
                    CancelInvoke(nameof(LoadHome));
                    CancelInvoke(nameof(MarkConnectionStable));
                    Invoke(nameof(MarkConnectionStable), Mathf.Max(1f, _stableConnectionSecondsToResetRetries));
                    Debug.Log($"[NH_CONN][STARTED] Client connected to match server retry={_retryCount}/{_maxRetries} t={System.DateTime.UtcNow:HH:mm:ss.fff}.");
                    // Keep the app simulating while backgrounded so a brief alt-tab / screen-off on
                    // mobile does not let the UDP relay/DS connection time out → mid-game kick to Home.
                    NightHunt.Core.GameManager.Instance?.EnableBackgroundRunForMatch();
                    RegisterClientHandshakeBroadcast();
                    SendPlayerDataBroadcastToServer("ClientConnectionStarted", force: true);
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
                        _lastPlayerDataBroadcastRealtime = -1f;
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
                        _lastPlayerDataBroadcastRealtime = -1f;
                        CancelInvoke(nameof(MarkConnectionStable));

                        if (wasConnected)
                        {
                            Debug.LogWarning($"[NH_CONN][NH_DROP][STOPPED] Match server connection dropped connectedFor={connectedFor:F2}s retry={_retryCount}/{_maxRetries}; attempting reconnect.");
                            if (showReconnectUi)
                                ShowReconnectModal();
                        }
                        else
                        {
                            // Never connected — initial connection failed (e.g. relay ConnectionFailed).
                            // Previously: overlay was silently stuck for ALL retries (~120s) with no feedback.
                            // Now: update overlay text and show reconnect modal after first retry so user
                            // knows the system is retrying and can choose to return home.
                            Debug.LogWarning($"[NH_CONN][NH_DROP][FAILED] Connection to match server failed before stable start retry={_retryCount}/{_maxRetries}.");

                            bool isRelayMode = RoomState.Instance?.CurrentGameMode == GameMode.Custom_Relay;
                            if (isRelayMode && _retryCount >= 1)
                            {
                                // Show reconnect modal after first failed retry so user isn't stuck on
                                // the frozen loading screen for the full relay retry budget (~120s).
                                showReconnectUi = true;
                                ShowReconnectModal();
                            }
                            else
                            {
                                // First attempt failed — quietly update overlay with status text.
                                MatchLoadingOverlay.Instance?.SetStatus(
                                    $"Connecting to game server... (attempt {_retryCount + 1}/{_maxRetries})");
                            }
                        }

                        TryRetry(showReconnectUi);
                    }

                    break;
            }
        }

        private void RegisterClientHandshakeBroadcast()
        {
            if (_clientHandshakeBroadcastRegistered || networkManager?.ClientManager == null)
                return;

            networkManager.ClientManager.RegisterBroadcast<RequestPlayerDataBroadcast>(OnServerRequestedPlayerDataBroadcast);
            _clientHandshakeBroadcastRegistered = true;
            Debug.Log("[NH_HANDSHAKE][CLIENT_BROADCAST][REGISTER] RequestPlayerDataBroadcast registered on NetworkGameManager.");
        }

        private void UnregisterClientHandshakeBroadcast()
        {
            if (!_clientHandshakeBroadcastRegistered || networkManager?.ClientManager == null)
                return;

            networkManager.ClientManager.UnregisterBroadcast<RequestPlayerDataBroadcast>(OnServerRequestedPlayerDataBroadcast);
            _clientHandshakeBroadcastRegistered = false;
        }

        private void OnServerRequestedPlayerDataBroadcast(RequestPlayerDataBroadcast broadcast, Channel channel)
        {
            Debug.Log($"[NH_HANDSHAKE][CLIENT_BROADCAST][REQUEST] Host requested player data via broadcast channel={channel}.");
            SendPlayerDataBroadcastToServer("ServerBroadcastRequest", force: true);
        }

        private void SendPlayerDataBroadcastToServer(string source, bool force = false)
        {
            if (networkManager?.ClientManager == null)
                return;

            if (RoomState.Instance == null || RoomState.Instance.CurrentGameMode != GameMode.Custom_Relay)
                return;

            if (!networkManager.IsClientStarted)
            {
                Debug.Log($"[NH_HANDSHAKE][CLIENT_BROADCAST][WAIT] source={source} client not started.");
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (!force && _lastPlayerDataBroadcastRealtime >= 0f &&
                (now - _lastPlayerDataBroadcastRealtime) < PlayerDataBroadcastThrottleSeconds)
            {
                return;
            }

            PlayerRegistryData data = PlayerIdentityFactory.BuildLocalPlayerData();
            networkManager.ClientManager.Broadcast(new SubmitPlayerDataBroadcast { Data = data }, Channel.Reliable);
            _lastPlayerDataBroadcastRealtime = now;
            Debug.Log(
                $"[NH_HANDSHAKE][CLIENT_BROADCAST][SEND] source={source} backendId={data.BackendPlayerId} " +
                $"name={data.DisplayName} teamId={data.TeamId} charModelIdx={data.CharacterModelIndex} " +
                $"instance={NightHunt.Config.InstanceConfig.GetInstanceId()}.");
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

            UseRelayRetryBudget();
            _returningHome = false;
            _connectionStarted = true;
            _connected         = false;
            _connectedSinceRealtime = -1f;

            if (room.IsHostPlayer)
            {
                Debug.Log($"[NH_CONN][NH_RELAY][AUTO_CONNECT] role=host endpoint={room.RelayIp}:{room.RelayPort} session={room.RelaySessionId} instance={NightHunt.Config.InstanceConfig.GetInstanceId()} userId={SessionState.Instance?.UserId ?? 0L} t={System.DateTime.UtcNow:HH:mm:ss.fff}");
                StartHostWithRelay(room.RelayIp, room.RelayPort, room.RelaySessionId);

                int expected = room.PlayerCount > 0 ? room.PlayerCount : 2;
                if (ServerGameManager.Instance != null)
                {
                    ServerGameManager.Instance.SetExpectedPlayerCount(expected);
                    Debug.Log($"[NH_CONN][NH_RELAY][HOST_EXPECTED] expectedPlayerCount={expected}");
                }
                else
                {
                    Debug.LogWarning("[NetworkGameManager] ServerGameManager.Instance null — expectedPlayerCount not applied.");
                }
            }
            else
            {
                Debug.Log($"[NH_CONN][NH_RELAY][AUTO_CONNECT] role=client endpoint={room.RelayIp}:{room.RelayPort} session={room.RelaySessionId} instance={NightHunt.Config.InstanceConfig.GetInstanceId()} userId={SessionState.Instance?.UserId ?? 0L} t={System.DateTime.UtcNow:HH:mm:ss.fff}");
                StartClientWithRelay(room.RelayIp, room.RelayPort, room.RelaySessionId);
            }
        }

        private void AutoConnectDS(RoomState room)
        {
            if (!_dsReady && !room.DedicatedServerReady)
            {
                Debug.LogError("[NetworkGameManager] Ranked_DS connect blocked: ds_ready has not been received yet.");
                return;
            }

            if (string.IsNullOrEmpty(room.DsIp) || room.DsPort == 0)
            {
                Debug.LogError("[NetworkGameManager] Ranked_DS mode but DsIp/DsPort not set in RoomState!");
                return;
            }

            RestoreDefaultRetryBudget();
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
                Debug.LogError($"[NH_CONN][NH_DROP][RETRY_EXHAUSTED] Max retries reached retry={_retryCount}/{_maxRetries}; returning to home.");
                OnRetryAttempt?.Invoke(_maxRetries + 1, _maxRetries);
                if (showUi)
                    HandleReconnectUI(_maxRetries + 1, _maxRetries);
                Invoke(nameof(LoadHome), 1.5f);
                return;
            }
            _retryCount++;
            float delay = GetRetryDelay();
            ConnectionDropTrace.Log(
                "RETRY_SCHEDULE",
                $"delay={delay:F1}s nextAttempt={_retryCount}/{_maxRetries} showUi={showUi}",
                warning: true);
            Debug.Log($"[NH_CONN][RETRY] Retrying connection delay={delay:F1}s attempt={_retryCount}/{_maxRetries}.");
            OnRetryAttempt?.Invoke(_retryCount, _maxRetries);
            if (showUi)
                HandleReconnectUI(_retryCount, _maxRetries);
            CancelInvoke(nameof(RetryConnect));
            Invoke(nameof(RetryConnect), delay);
        }

        private float GetRetryDelay()
        {
            var room = RoomState.Instance;
            if (room != null && room.CurrentGameMode == GameMode.Custom_Relay)
                return Mathf.Max(_retryDelay, _relayRetryDelay);

            return _retryDelay;
        }

        private void UseRelayRetryBudget()
        {
            if (_defaultMaxRetries < 0)
                _defaultMaxRetries = _maxRetries;

            _maxRetries = Mathf.Max(_maxRetries, Mathf.Max(1, _relayMaxRetries));
        }

        private void RestoreDefaultRetryBudget()
        {
            if (_defaultMaxRetries < 0)
                return;

            _maxRetries = _defaultMaxRetries;
            _defaultMaxRetries = -1;
        }

        private void MarkConnectionStable()
        {
            if (!_connected || _connectedSinceRealtime < 0f)
                return;

            float connectedFor = Time.realtimeSinceStartup - _connectedSinceRealtime;
            if (connectedFor + 0.1f < _stableConnectionSecondsToResetRetries)
                return;

            if (_retryCount != 0)
                Debug.Log($"[NH_CONN][STABLE] Connection stable connectedFor={connectedFor:F1}s; resetting retry counter.");
            _retryCount = 0;
        }

        private void RetryConnect()
        {
            ConnectionDropTrace.Log(
                "RETRY_EXECUTE",
                $"isClientStarted={networkManager != null && networkManager.IsClientStarted} retry={_retryCount}/{_maxRetries}",
                warning: false);
            _connected = false;
            _connectionStarted = false;

            // Ensure the previous FishNet client is fully stopped before starting a new connection.
            // If it is still in a Stopping/Started state (e.g. transport queued a deferred stop event),
            // calling StartConnection again creates a double-connection race that causes an infinite loop:
            //   old connection fires Stopped → TryRetry → new connection fires Stopped → TryRetry → ...
            if (networkManager != null && networkManager.IsClientStarted)
            {
                ConnectionDropTrace.MarkLocalStopRequest("NetworkGameManager.RetryConnect.StopPreviousClient", intentional: false);
                Debug.Log("[NH_CONN][RETRY] Previous client still active; stopping before retry.");
                networkManager.ClientManager.StopConnection();
                // Wait one frame then retry; the Stopped event will NOT trigger TryRetry again
                // because _connectionStarted is now false.
                Invoke(nameof(RetryConnect), 0.25f);
                return;
            }

            AutoConnectFromRoomState();
        }

        private void LoadHome()
        {
            if (_returningHome)
                return;

            _returningHome = true;
            CancelInvoke(nameof(RetryConnect));
            CancelInvoke(nameof(LoadHome));
            MatchLoadingOverlay.Instance?.ForceHide("load-home");
            ReportOwnMatchPresenceBestEffort("DISCONNECTED", "CLIENT_CONNECT_FAILED");
            Disconnect();
            _dsReady         = false;
            _gameSceneLoaded = false;
            RestoreDefaultRetryBudget();
            // Match is over / abandoned — stop forcing background simulation.
            NightHunt.Core.GameManager.Instance?.RestoreBackgroundRunDefault();
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

            MatchLoadingOverlay.Instance?.ForceHide("disconnect-cleanup");
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
            CancelInvoke(nameof(MarkConnectionStable));
            CancelGameSceneLoadedDelay();
            MatchLoadingOverlay.Instance?.ForceHide("reset-flags");
            _connectionStarted = false;
            _connected         = false;
            _connectedSinceRealtime = -1f;
            _dsReady           = false;
            _relayHostReady    = false;
            _relayHostReadyReported = false;
            _relayHostUpstreamPunchComplete = false;
            _relayHostReadyEarliestRealtime = -1f;
            _gameSceneLoaded   = false;
            _retryCount        = 0;
            RestoreDefaultRetryBudget();
            HideReconnectModal(showToast: false);
        }

        /// <summary>Immediately stop all FishNet connections.</summary>
        public void Disconnect()
        {
            if (networkManager == null) return;
            Debug.Log("[NH_CONN][DISCONNECT] Disconnecting FishNet server/client.");
            _intentionalDisconnect = true;
            ConnectionDropTrace.MarkLocalStopRequest("NetworkGameManager.Disconnect", intentional: true);
            if (IsServer) networkManager.ServerManager.StopConnection(true);
            if (IsClient) networkManager.ClientManager.StopConnection();
        }

        private void CancelGameSceneLoadedDelay()
        {
            if (_markGameSceneLoadedCoroutine == null)
                return;

            StopCoroutine(_markGameSceneLoadedCoroutine);
            _markGameSceneLoadedCoroutine = null;
        }

        // ── Reconnect UI ──────────────────────────────────────────────────────

        private void ShowReconnectModal()
        {
            if (_reconnectModalOpen) return;
            MatchLoadingOverlay.Instance?.ForceHide("reconnect-modal");
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
                MatchLoadingOverlay.Instance?.ForceHide("reconnect-failed");
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
            CancelInvoke(nameof(MarkConnectionStable));
            MatchLoadingOverlay.Instance?.ForceHide("return-home");
            _reconnectModalOpen = false;
            ReportOwnMatchPresenceBestEffort("DISCONNECTED", "CLIENT_RETURN_HOME");
            Disconnect();
            RestoreDefaultRetryBudget();
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

            if (!_relayHostUpstreamPunchComplete)
            {
                Debug.Log("[NH_CONN][NH_RELAY][HOST_READY_WAIT] Relay host local client connected, but upstream punches are not complete yet.");
                Invoke(nameof(TryReportRelayHostReady), 0.25f);
                return;
            }

            if (_relayHostReadyEarliestRealtime > 0f && Time.realtimeSinceStartup < _relayHostReadyEarliestRealtime)
            {
                float remaining = Mathf.Max(0.1f, _relayHostReadyEarliestRealtime - Time.realtimeSinceStartup);
                Debug.Log($"[NH_CONN][NH_RELAY][HOST_READY_WAIT] Waiting {remaining:F2}s for relay upstream settle before broadcasting relay_host_ready.");
                Invoke(nameof(TryReportRelayHostReady), remaining);
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

        private void ConfigureRelayPacketLayer(string sessionId, bool forceNewLayer = false)
        {
            ConfigureConnectionDropTrace();
            var transport = networkManager?.TransportManager?.Transport as Tugboat;
            if (transport == null)
            {
                Debug.LogWarning("[NetworkGameManager] Custom relay identity header requires Tugboat transport.");
                return;
            }

            ulong peerId = ResolveRelayPeerId();
            if (forceNewLayer
                || _activeRelayIdentityLayer == null
                || _activeRelayIdentitySessionId != sessionId
                || _activeRelayIdentityPeerId != peerId)
            {
                _activeRelayIdentityLayer = new RelayIdentityPacketLayer(sessionId, peerId);
                _activeRelayIdentitySessionId = sessionId;
                _activeRelayIdentityPeerId = peerId;
            }

            transport.SetPacketLayer(_activeRelayIdentityLayer);
            ConnectionDropTrace.SetRelayIdentity(sessionId, _activeRelayIdentityLayer, forceNewLayer);
            Debug.Log($"[NH_CONN][NH_RELAY][IDENTITY] Relay identity enabled sessionHash={_activeRelayIdentityLayer.SessionHash:x16} peerId={peerId} nonce={_activeRelayIdentityLayer.Nonce:x16} forceNew={forceNewLayer}");
        }

        private void ConfigureTransportTimeout()
        {
            var transport = networkManager?.TransportManager?.Transport as Tugboat;
            if (transport != null)
            {
                // Set disconnect/handshake timeout to 30 seconds to survive heavy scene load/initialization freezes on mobile
                transport.SetTimeout(30f, asServer: true);
                transport.SetTimeout(30f, asServer: false);
                Debug.Log("[NetworkGameManager] Configured Tugboat timeout to 30 seconds for both client and server.");
            }
        }

        private void ClearRelayPacketLayer()
        {
            ConfigureConnectionDropTrace();
            if (networkManager?.TransportManager?.Transport is Tugboat transport)
                transport.SetPacketLayer(null);

            ConnectionDropTrace.ClearRelayIdentity("NetworkGameManager.ClearRelayPacketLayer");
            _activeRelayIdentityLayer = null;
            _activeRelayIdentitySessionId = null;
            _activeRelayIdentityPeerId = 0UL;
        }

        private static void ConfigureConnectionDropTrace()
        {
            var cfg = NightHuntDebugConfig.Instance;
            ConnectionDropTrace.Configure(
                cfg != null && cfg.EnableConnectionDropTraceLogs,
                cfg != null && cfg.EnableConnectionDropTraceStackTraces);
        }

        private void EnsureRelayOwnedObjectPreserver()
        {
            if (networkManager == null || networkManager.ServerManager == null)
                return;

            var preserver = networkManager.GetComponent<RelayOwnedObjectPreserver>();
            if (preserver == null)
                preserver = networkManager.gameObject.AddComponent<RelayOwnedObjectPreserver>();

            preserver.Initialize(networkManager.ServerManager);
        }

        private static ulong ResolveRelayPeerId()
        {
            long userId = SessionState.Instance?.UserId ?? 0L;
            if (userId > 0L)
                return (ulong)userId;

            string fallback = SessionState.Instance?.Username;
            if (string.IsNullOrEmpty(fallback))
                fallback = SystemInfo.deviceUniqueIdentifier;
            return RelayIdentityPacketLayer.ComputeHash64($"guest:{fallback}");
        }

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
