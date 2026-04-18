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
        private bool _matchEnded;       // true after a clean match end — suppresses TryRetry

        /// <summary>Fired on each retry attempt (currentAttempt, maxAttempts).</summary>
        public event System.Action<int, int> OnRetryAttempt;

        // Survives scene transitions: set true only when ds_ready WS event is received
        private static bool s_dsReadyReceived = false;

        protected override void OnSingletonAwake()
        {
            if (networkManager == null)
            {
                networkManager = FindFirstObjectByType<NetworkManager>();
                if (networkManager == null)
                    Debug.LogError("[NGM] NetworkManager not found! Please add NetworkManager to scene.");
            }
        }

        private void Start()
        {
#if UNITY_SERVER
            // DS is started by ServerBootstrap — no client auto-connect needed.
            return;
#endif
            if (networkManager == null) return;
            if (networkManager.SceneManager != null)
                networkManager.SceneManager.OnLoadEnd += OnSceneLoadEnd;
            else
                Debug.LogWarning("[NGM] FishNet SceneManager component is missing on NetworkManager — OnLoadEnd not subscribed. Add SceneManager component in Inspector. Using Unity SceneManager fallback.");
            if (networkManager.ClientManager != null)
                networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
            else
                Debug.LogError("[NGM] ClientManager is null — FishNet ClientManager component missing on NetworkManager! Connection state events will NOT fire. Add ClientManager in Inspector.");
            UnityEngine.SceneManagement.SceneManager.sceneLoaded  += OnUnitySceneLoaded;
            if (GameWebSocketService.Instance != null)
                GameWebSocketService.Instance.OnDsReady += OnDsReadyReceived;

            // ── Fix A: scene-scoped singleton starts INSIDE the map scene ────
            // SceneLoader.LoadGame() uses Unity's SceneManager.LoadScene() which does NOT
            // fire FishNet's SceneManager.OnLoadEnd. If this instance started inside a map
            // scene, mark the flag immediately.
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene.name.StartsWith("02_Map_", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"[NGM] Started inside map scene '{activeScene.name}' — setting _gameSceneLoaded = true.");
                _gameSceneLoaded = true;
            }

            // ── Fix B: ds_ready may have arrived before this instance subscribed ─
            // s_dsReadyReceived is static and survives scene transitions.
            // It is set ONLY when ds_ready WS event is received (NOT from match_ready).
            if (!_dsReady && s_dsReadyReceived
                && RoomState.Instance?.CurrentGameMode == GameMode.Ranked_DS)
            {
                Debug.Log("[NGM] Fix B: ds_ready was received before this instance subscribed — setting _dsReady = true.");
                _dsReady = true;
                MatchLoadingOverlay.Instance?.MarkDsReady();
            }

            // Try connect now if both flags are already true (most common case in map scene)
            if (_gameSceneLoaded)
                TryConnectIfReady();
        }

        protected override void OnDestroy()
        {
            if (networkManager != null)
            {
                if (networkManager.SceneManager != null)
                    networkManager.SceneManager.OnLoadEnd -= OnSceneLoadEnd;
                if (networkManager.ClientManager != null)
                    networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
            }
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnUnitySceneLoaded;
            if (GameWebSocketService.Instance != null)
                GameWebSocketService.Instance.OnDsReady -= OnDsReadyReceived;
            base.OnDestroy();
        }

        /// <summary>
        /// Called by Unity SceneManager when any scene finishes loading.
        /// Handles the case where SceneLoader.LoadGame() uses Unity's LoadScene
        /// (which does not fire FishNet's OnLoadEnd).
        /// </summary>
        private void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name.StartsWith("02_Map_", System.StringComparison.OrdinalIgnoreCase))
            {
                _gameSceneLoaded = true;
                TryConnectIfReady();
            }
        }

        /// <summary>
        /// Resets static connection flags. Call at the start of every new matchmaking cycle
        /// (e.g. from RoomState.ClearRoom) so stale ds_ready signals don't leak across matches.
        /// </summary>
        public static void ResetConnectionFlags()
        {
            s_dsReadyReceived = false;
        }

        // ── Dedicated Server (Ranked mode) ────────────────────────────────────

        /// <summary>
        /// Start FishNet Server only (dedicated server build / dev tools).
        /// Called by NetworkStartMenu (dev tool).
        /// </summary>
        public void StartServer()
        {
            if (networkManager == null) { Debug.LogError("[NGM] NetworkManager is null!"); return; }
            Debug.Log($"[NGM] Starting Dedicated Server on port {port}...");
            if (!networkManager.ServerManager.StartConnection())
                Debug.LogError("[NGM] Failed to start server!");
            else
                Debug.Log("[NGM] Dedicated Server started successfully!");
        }

        /// <summary>
        /// Connect client to a Dedicated Server (Ranked_DS mode).
        /// </summary>
        public void StartClientDS(string dsIp = null, ushort dsPort = 0)
        {
            if (networkManager == null) { Debug.LogError("[NGM] NetworkManager is null!"); return; }
            string ip = string.IsNullOrEmpty(dsIp) ? defaultServerAddress : dsIp;
            ushort p  = dsPort > 0 ? dsPort : port;
            Debug.Log($"[NGM] Connecting to DS {ip}:{p}...");
            SetTransportAddress(ip, p);
            if (!networkManager.ClientManager.StartConnection())
                Debug.LogError("[NGM] Failed to start DS client connection!");
        }

        /// <summary>Legacy overload kept for backwards compatibility.</summary>
        public void StartClient(string serverAddress = null) => StartClientDS(serverAddress);

        // ── Relay (Custom mode) ───────────────────────────────────────────────

        /// <summary>
        /// Start FishNet HOST in relay mode – used by the room owner (Custom mode).
        ///
        /// Relay topology (transparent UDP forwarding):
        ///   - Relay VPS has a dedicated UDP port per session (allocated by relay_server.py).
        ///   - All non-host players connect outbound to relay:sessionPort → relay forwards to host.
        ///   - Host's FishNet Server binds 0.0.0.0:localPort to receive forwarded packets.
        ///   - Host also starts FishNet Client → connects to local loopback (host player view).
        ///   - A UDP heartbeat keeps the NAT hole open: relay:sessionPort ← host (prevents NAT timeout).
        ///
        /// Why this works across different networks / mobile:
        ///   All connections are OUTBOUND from each player's device to the relay VPS.
        ///   NAT routers always allow outbound connections. The relay forwards packets
        ///   back through the established NAT state — no port forwarding needed.
        ///
        /// Requirements:
        ///   - relay_server.py running on VPS (see docker-compose.yml).
        ///   - VPS firewall: open UDP port range 7777–7900.
        ///   - Host machine: FishNet Server port (7777) must accept packets from relay's IP
        ///     (no local firewall blocking UDP 7777 from external IPs).
        /// </summary>
        public void StartHostWithRelay(string relayIp, ushort relayPort, string sessionId)
        {
            if (networkManager == null) { Debug.LogError("[NGM] NetworkManager is null!"); return; }
            Debug.Log($"[NGM] Starting HOST via Relay {relayIp}:{relayPort} session={sessionId}");

            // Bind server to all interfaces so the relay can forward packets to us.
            // The relay's IP sends us UDP; our server must accept it (not localhost-only).
            SetTransportServerBindAddress("0.0.0.0");

            // Start FishNet Server — game logic runs here, accepts forwarded client packets.
            if (!networkManager.ServerManager.StartConnection())
            {
                Debug.LogError("[NGM] Failed to start relay host server!");
                return;
            }

            // Host's own FishNet Client connects to localhost (loopback) — no relay needed for host player.
            // The relay only carries traffic for REMOTE clients.
            // GetPort() reads Tugboat's configured server port (default 7777).
            ushort localPort = networkManager.TransportManager.Transport?.GetPort() ?? 7777;
            SetTransportAddress("127.0.0.1", localPort);  // local server port (Tugboat default 7777)
            if (!networkManager.ClientManager.StartConnection())
            {
                Debug.LogError("[NGM] Failed to start relay host local client!");
                return;
            }

            // Start heartbeat: send periodic UDP packets from the host's SERVER port to relay.
            // This keeps the NAT mapping alive so the relay can route client packets back to us.
            StartRelayHeartbeat(relayIp, relayPort, sessionId);

            Debug.Log("[NGM] Relay Host started. Heartbeat active to keep NAT hole open.");
        }

        private System.Collections.IEnumerator _heartbeatCoroutine;

        private void StartRelayHeartbeat(string relayIp, ushort relayPort, string sessionId)
        {
            if (_heartbeatCoroutine != null)
                StopCoroutine(_heartbeatCoroutine);
            _heartbeatCoroutine = RelayHeartbeatRoutine(relayIp, relayPort, sessionId);
            StartCoroutine(_heartbeatCoroutine);
        }

        private System.Collections.IEnumerator RelayHeartbeatRoutine(string relayIp, ushort relayPort, string sessionId)
        {
            // Send a tiny UDP packet to the relay every 10 seconds from the host's perspective.
            // This ensures the NAT mapping (relay → host) stays alive (most NATs expire after 30-120s).
            // We use a raw UdpClient so we can control the source port to match FishNet's server port.
            System.Net.Sockets.UdpClient udp = null;
            byte[] ping = System.Text.Encoding.ASCII.GetBytes("NHRELAY_PING");
            var remoteEp = new System.Net.IPEndPoint(System.Net.IPAddress.Parse(relayIp), relayPort);

            try
            {
                udp = new System.Net.Sockets.UdpClient();
                udp.ExclusiveAddressUse = false;
                // Bind to any available port — just need outbound NAT hole from this machine.
                udp.Client.SetSocketOption(System.Net.Sockets.SocketOptionLevel.Socket,
                    System.Net.Sockets.SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0));
                Debug.Log($"[NGM] Relay heartbeat started → {relayIp}:{relayPort}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[NGM] Relay heartbeat socket failed: {e.Message}");
                yield break;
            }

            while (networkManager != null && (networkManager.IsServerStarted || networkManager.IsClientStarted))
            {
                try { udp.Send(ping, ping.Length, remoteEp); }
                catch (System.Exception e) { Debug.LogWarning($"[NGM] Heartbeat send failed: {e.Message}"); }
                yield return new UnityEngine.WaitForSeconds(10f);
            }

            udp?.Close();
            Debug.Log("[NGM] Relay heartbeat stopped.");
        }

        /// <summary>
        /// Connect as a client through the relay – used by non-host players (Custom mode).
        ///
        /// The relay:sessionPort acts as the FishNet server endpoint from the client's view.
        /// </summary>
        public void StartClientWithRelay(string relayIp, ushort relayPort, string sessionId)
        {
            if (networkManager == null) { Debug.LogError("[NGM] NetworkManager is null!"); return; }
            Debug.Log($"[NGM] Connecting CLIENT via Relay {relayIp}:{relayPort} session={sessionId}");
            SetTransportAddress(relayIp, relayPort);
            if (!networkManager.ClientManager.StartConnection())
                Debug.LogError("[NGM] Failed to start relay client connection!");
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
        /// Static entry point: signals that ds_ready WS arrived regardless of whether an
        /// NGM instance exists yet (handles the scene-transition timing gap).
        /// Sets the static flag first, then delegates to the instance if one is alive.
        /// Call this from MatchFlowCoordinator.HandleDsReady() — NOT from the match_ready path.
        /// </summary>
        public static void SignalDsReady()
        {
            s_dsReadyReceived = true;
            if (Instance != null)
                Instance.NotifyDsReady();
        }

        /// <summary>
        /// Called by PartyController when a ds_ready WS event is received.
        /// Sets the DS-ready flag and attempts connection if the game scene is also loaded.
        /// </summary>
        public void NotifyDsReady()
        {
            s_dsReadyReceived = true;
            _dsReady = true;
            var room = RoomState.Instance;
            Debug.Log($"[NGM] NotifyDsReady \u25ba dsIp={room?.DsIp} dsPort={room?.DsPort} gameSceneLoaded={_gameSceneLoaded}  t={System.DateTime.UtcNow:HH:mm:ss.fff}");
            MatchLoadingOverlay.Instance?.MarkDsReady();
            TryConnectIfReady();
        }
        /// <summary>
        /// Called by MatchFlowCoordinator on match_ready when mode is Custom_Relay.
        /// Relay games do not wait for ds_ready — connect as soon as scene loads.
        /// Sets _dsReady=true so TryConnectIfReady() does not block on the DS flag.
        /// </summary>
        public void NotifyRelayReady()
        {
            s_dsReadyReceived = true;
            _dsReady = true;
            var room = RoomState.Instance;
            Debug.Log($"[NGM] NotifyRelayReady ▶ relayIp={room?.RelayIp} relayPort={room?.RelayPort} isHost={room?.IsHostPlayer} gameSceneLoaded={_gameSceneLoaded}  t={System.DateTime.UtcNow:HH:mm:ss.fff}");
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
                Debug.LogWarning("[NGM] TryConnectIfReady: RoomState is null — cannot auto-connect. " +
                                 "Use NetworkStartMenu for dev/editor testing (GameMode.None path).");
                return;
            }

            // Relay mode: connect as soon as the scene loads (no DS boot wait)
            if (room.CurrentGameMode == GameMode.Custom_Relay)
            {
                if (!_gameSceneLoaded)
                {
                    Debug.Log("[NGM] TryConnectIfReady: WAITING for map scene (Relay mode).");
                    return;
                }
            }
            else if (room.CurrentGameMode == GameMode.Ranked_DS)
            {
                if (!_gameSceneLoaded)
                {
                    Debug.Log("[NGM] TryConnectIfReady: WAITING for map scene (Ranked_DS mode).");
                    return;
                }
                if (!_dsReady)
                {
                    Debug.Log($"[NGM] TryConnectIfReady: WAITING for ds_ready WS \u2014 DsIp={room.DsIp} DsPort={room.DsPort}. DS may still be booting.");
                    return;
                }
            }
            else
            {
                Debug.Log("[NGM] TryConnectIfReady: GameMode.None \u2014 skipping (use NetworkStartMenu for dev).");
                return;
            }

            Debug.Log($"[NGM] TryConnectIfReady PASS — sceneLoaded={_gameSceneLoaded} dsReady={_dsReady}. Starting auto-connect.");
            _retryCount        = 0;
            _connectionStarted = false;
            _connected         = false;
            AutoConnectFromRoomState();
        }

        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            switch (args.ConnectionState)
            {
                case LocalConnectionState.Starting:
                    Debug.Log($"[NGM] FishNet client STARTING — t={System.DateTime.UtcNow:HH:mm:ss.fff}");
                    break;

                case LocalConnectionState.Started:
                    _connected = true;
                    Debug.Log($"[NGM] FishNet client CONNECTED  t={System.DateTime.UtcNow:HH:mm:ss.fff}");
                    MatchLoadingOverlay.Instance?.MarkConnected();
                    break;

                case LocalConnectionState.Stopping:
                    Debug.Log($"[NGM] FishNet client STOPPING — t={System.DateTime.UtcNow:HH:mm:ss.fff}");
                    break;

                case LocalConnectionState.Stopped:
                    Debug.Log($"[NGM] FishNet client STOPPED — connectionStarted={_connectionStarted} wasConnected={_connected} matchEnded={_matchEnded}  t={System.DateTime.UtcNow:HH:mm:ss.fff}");
                    if (_connectionStarted && !_connected)
                    {
                        if (_matchEnded)
                        {
                            Debug.Log("[NGM] Match ended cleanly — skipping reconnect retry.");
                            return;
                        }
                        Debug.LogWarning($"[NGM] FishNet connect FAILED (never reached Started) — attempt {_retryCount + 1}/{_maxRetries}.");
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
                Debug.LogWarning("[NGM] RoomState not found — skipping auto-connect (dev mode).");
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
                    Debug.Log("[NGM] GameMode.None — skipping auto-connect (use NetworkStartMenu for dev testing).");
                    break;
            }
        }

        private void AutoConnectRelay(RoomState room)
        {
            if (string.IsNullOrEmpty(room.RelayIp) || room.RelayPort == 0)
            {
                Debug.LogError($"[NGM] AutoConnectRelay FAILED \u2014 RelayIp='{room.RelayIp}' RelayPort={room.RelayPort} not set in RoomState!");
                return;
            }

            _connectionStarted = true;
            _connected         = false;

            if (room.IsHostPlayer)
            {
                Debug.Log($"[NGM] AutoConnectRelay HOST \u2014 relayIp={room.RelayIp} relayPort={room.RelayPort} session={room.RelaySessionId} playerCount={room.PlayerCount}  t={System.DateTime.UtcNow:HH:mm:ss.fff}");
                StartHostWithRelay(room.RelayIp, room.RelayPort, room.RelaySessionId);

                int expected = room.PlayerCount > 0 ? room.PlayerCount : 2;
                if (ServerGameManager.Instance != null)
                {
                    ServerGameManager.Instance.SetExpectedPlayerCount(expected);
                    Debug.Log($"[NGM] Relay HOST: expectedPlayerCount set to {expected}");
                }
                else
                {
                    Debug.LogWarning("[NGM] ServerGameManager.Instance null \u2014 expectedPlayerCount not applied.");
                }
            }
            else
            {
                Debug.Log($"[NGM] AutoConnectRelay CLIENT \u2014 relayIp={room.RelayIp} relayPort={room.RelayPort} session={room.RelaySessionId}  t={System.DateTime.UtcNow:HH:mm:ss.fff}");
                StartClientWithRelay(room.RelayIp, room.RelayPort, room.RelaySessionId);
            }
        }

        private void AutoConnectDS(RoomState room)
        {
            if (string.IsNullOrEmpty(room.DsIp) || room.DsPort == 0)
            {
                Debug.LogError($"[NGM] AutoConnectDS FAILED \u2014 DsIp='{room.DsIp}' DsPort={room.DsPort} not set in RoomState! " +
                               "GWS.SetDedicatedServer must be called on ds_ready before reaching here.");
                return;
            }

            _connectionStarted = true;
            _connected         = false;

            Debug.Log($"[NGM] AutoConnectDS \u25ba dsIp={room.DsIp} dsPort={room.DsPort} matchId={room.CurrentMatchId} mapId={room.DsMapId}  t={System.DateTime.UtcNow:HH:mm:ss.fff}");
            StartClientDS(room.DsIp, room.DsPort);
        }

        private void TryRetry()
        {
            if (_retryCount >= _maxRetries)
            {
                Debug.LogError($"[NGM] TryRetry \u2014 max retries ({_maxRetries}) reached \u2014 returning to Home.");
                Invoke(nameof(LoadHome), 1.5f);
                return;
            }
            _retryCount++;
            OnRetryAttempt?.Invoke(_retryCount, _maxRetries);
            Debug.LogWarning($"[NGM] TryRetry \u25ba attempt {_retryCount}/{_maxRetries} \u2014 retrying in {_retryDelay}s");
            Invoke(nameof(RetryConnect), _retryDelay);
        }

        private void RetryConnect()
        {
            _connected = false;
            AutoConnectFromRoomState();
        }

        /// <summary>Signals that the match ended cleanly — suppresses TryRetry on disconnect.</summary>
        public void NotifyMatchEnded()
        {
            _matchEnded = true;
            Debug.Log("[NGM] Match ended — retry suppressed.");
        }

        private void LoadHome()
        {
            s_dsReadyReceived = false;
            _dsReady          = false;
            _gameSceneLoaded  = false;
            _matchEnded       = false;
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

        /// <summary>Immediately stop all FishNet connections.</summary>
        public void Disconnect()
        {
            if (_heartbeatCoroutine != null)
            {
                StopCoroutine(_heartbeatCoroutine);
                _heartbeatCoroutine = null;
            }
            if (networkManager == null) return;
            Debug.Log("[NGM] Disconnecting...");
            if (IsServer) networkManager.ServerManager.StopConnection(true);
            if (IsClient) networkManager.ClientManager.StopConnection();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        public int GetPlayerCount()
            => networkManager?.ServerManager?.Clients?.Count ?? 0;

        private void SetTransportAddress(string address, ushort targetPort)
        {
            var transport = networkManager.TransportManager.Transport;
            if (transport == null) { Debug.LogWarning("[NGM] Transport is null!"); return; }
            transport.SetClientAddress(address);
            transport.SetPort(targetPort);
            Debug.Log($"[NGM] Transport address set → {address}:{targetPort} (type={transport.GetType().Name})");
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
                    Debug.LogWarning($"[NGM] Property '{propName}' not found on {type.Name}.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[NGM] Could not set '{propName}': {e.Message}");
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
                Debug.Log($"[NGM] Relay session {sessionId} cleaned up.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[NGM] Relay cleanup failed (non-fatal): {e.Message}");
            }
        }
    }
}
