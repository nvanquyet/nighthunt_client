using System;
using System.Collections;
using System.Reflection;
using FishNet.Managing;
using FishNet.Managing.Scened;
using NightHunt.Gameplay.Core.Events;   // MatchEndReason
using NightHunt.Gameplay.Match;          // MatchEndManager
using NightHunt.Common;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace NightHunt.Server
{
    /// <summary>
    /// Dedicated Server Bootstrap - Entry point cho DS build.
    ///
    /// Flow:
    ///   1. Parse command-line args (từ Docker ENV → entrypoint.sh)
    ///   2. Set port cho FishNet Tugboat transport
    ///   3. Start FishNet Server
    ///   4. Đăng ký với Backend API (POST /api/ds/register)
    ///   5. Send heartbeat định kỳ (POST /api/ds/heartbeat)
    ///
    /// Gắn script này vào GameObject trong Scene: 00_DS_Boot
    /// </summary>
    // ExecutionOrder -100 ensures Awake() runs BEFORE NetworkManager.Awake().
    // FishNet's NetworkManager auto-starts the server in headless mode (_startOnHeadless=true).
    // If we don't prevent that, two StartConnection() calls are made:
    //   1. NetworkManager.Awake() → StartForHeadless() → StartConnection() → server starts OK
    //   2. BootSequence → StartConnection() → NetManager.Start() sees IsRunning=true → returns false
    //      → FishNet logs "Server failed to start" → StopConnection() → scene load blocked forever
    // Fix: set _startOnHeadless=false in our Awake() (before NetworkManager.Awake() fires),
    //      then call StartConnection() exactly once in BootSequence with the correct CLI port.
    [UnityEngine.DefaultExecutionOrder(-100)]
    public class ServerBootstrap : MonoBehaviour
    {
        public static ServerBootstrap Instance { get; private set; }

        [Header("References")]
        [SerializeField] private NetworkManager networkManager;

        [Header("Fallback (Editor only — fill in from backend /api/internal/allocate response)")]
        [SerializeField] private ushort  fallbackPort       = 7777;
        [SerializeField] private string  fallbackBackendUrl = "https://localhost:8443";
        [SerializeField] private string  fallbackServerId   = "localhost-production-test";
        [SerializeField] private string  fallbackServerSecret = "replace-with-devSecret-from-allocate";
        [SerializeField] private string  fallbackMapId      = "map_01";
        [SerializeField] private int     fallbackMaxPlayers  = 16;

        // Được parse từ CLI args (Docker ENV qua entrypoint.sh, hoặc -e MAP_ID=...)
        private string _serverId;
        private ushort _gamePort;
        private string _backendUrl;
        private string _serverSecret;
        private int    _maxPlayers;
        private int    _expectedPlayers = -1; // -1 = not set → ServerGameManager uses its own default
        private string _mapId; // e.g. "map_01", "map_02" — empty = dùng scene hiện tại
        private string _matchId; // Set từ DS allocation (via ENV hoặc game-ready response)

        /// <summary>
        /// Expected player count parsed from --expectedPlayers CLI arg.
        /// ServerGameManager.ResolveExpectedPlayerCount() reads this as a priority fallback
        /// when RoomState is unavailable (always the case on a headless dedicated server).
        /// -1 = not set (ServerGameManager will use its Inspector default).
        /// </summary>
        public static int BootstrappedExpectedPlayers { get; private set; } = -1;

        // ─────────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Survive scene changes: 00_DS_Boot gets unloaded when map scene loads (ReplaceScenes=All).
            // HeartbeatLoop, SubscribeMatchEnd, and NotifyGameReady all live on this object →
            // must persist across the scene transition.
            DontDestroyOnLoad(gameObject);

            // Disable tất cả MonoBehaviour không cần trong server
            Application.targetFrameRate = 30;
            QualitySettings.vSyncCount  = 0;

#if UNITY_SERVER
            ParseCommandLineArgs();
#else
            // Editor fallback: values must be filled from backend /api/internal/allocate response.
            // 1. Call POST /api/internal/allocate → get serverId + devSecret
            // 2. Paste serverId → fallbackServerId, devSecret → fallbackServerSecret
            // 3. Press Play — server will register with backend successfully.
            _serverId         = fallbackServerId;
            _gamePort         = fallbackPort;
            _backendUrl       = fallbackBackendUrl;
            _serverSecret     = fallbackServerSecret;
            _maxPlayers       = fallbackMaxPlayers;
            _mapId            = fallbackMapId;
            _expectedPlayers  = 1; // Editor: default to 1 so solo test works without waiting for more players
            BootstrappedExpectedPlayers = _expectedPlayers;
                Debug.LogWarning($"[DS-Boot] EDITOR MODE \u2014 serverId='{_serverId}' backendUrl='{_backendUrl}'. " +
                             "Fill fallbackServerId/fallbackServerSecret from POST /api/internal/allocate if registration fails.");
#endif

            if (networkManager == null)
                networkManager = FindFirstObjectByType<NetworkManager>();

            if (networkManager == null)
            {
                Debug.LogError("[DS-Boot] NetworkManager not found in scene!");
                Application.Quit(1);
                return;
            }

            // CRITICAL: Prevent FishNet from auto-starting the server in headless mode.
            // NetworkManager.Awake() calls StartForHeadless() which calls StartConnection() if
            // _startOnHeadless=true (the default). Since our Awake() runs first (ExecutionOrder=-100),
            // we disable that here so BootSequence can start the server exactly once, on the
            // correct CLI-specified port, with the correct settings.
            networkManager.ServerManager.SetStartOnHeadless(false);
            Debug.Log("[DS-Boot] Awake: _startOnHeadless disabled — server will be started manually in BootSequence.");

            StartCoroutine(BootSequence());
        }

        public void ReportMatchPresence(string backendUserId, string state, string reason)
        {
            if (!long.TryParse(backendUserId, out long userId) || userId <= 0)
            {
                Debug.LogWarning($"[DS-Boot] Presence ignored: invalid backendUserId='{backendUserId}' state={state}");
                return;
            }

            if (string.IsNullOrEmpty(_backendUrl) || string.IsNullOrEmpty(_serverId) || string.IsNullOrEmpty(_serverSecret))
            {
                Debug.LogWarning($"[DS-Boot] Presence ignored for userId={userId}: DS backend credentials are not ready.");
                return;
            }

            StartCoroutine(ReportMatchPresenceRoutine(userId, state, reason));
        }

        private IEnumerator ReportMatchPresenceRoutine(long userId, string state, string reason)
        {
            var body = new MatchPresenceRequest
            {
                serverId = _serverId,
                serverSecret = _serverSecret,
                matchId = _matchId,
                userId = userId,
                state = state,
                reason = reason,
            };
            string json = JsonUtility.ToJson(body);

            for (int attempt = 1; attempt <= 2; attempt++)
            {
                using var req = new UnityWebRequest($"{_backendUrl}{Constants.API_DS_MATCH_PRESENCE}", "POST");
                req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("X-DS-Secret", _serverSecret);
                req.timeout = 5;

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[DS-Boot] Presence {state} reported for userId={userId} matchId={_matchId}.");
                    yield break;
                }

                Debug.LogWarning($"[DS-Boot] Presence report {state} userId={userId} attempt {attempt}/2 failed: HTTP={req.responseCode} err={req.error} body={req.downloadHandler?.text}");
                if (attempt < 2)
                    yield return new WaitForSeconds(1f);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────

        private void ParseCommandLineArgs()
        {
            // Defaults
            _gamePort   = fallbackPort;
            _backendUrl = fallbackBackendUrl;
            _maxPlayers = fallbackMaxPlayers;

            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                switch (args[i])
                {
                    case "--serverId":     _serverId     = args[i + 1]; break;
                    case "--serverPort":   ushort.TryParse(args[i + 1], out _gamePort); break;
                    case "--backendUrl":   _backendUrl   = args[i + 1]; break;
                    case "--serverSecret": _serverSecret = args[i + 1]; break;
                    case "--maxPlayers":      int.TryParse(args[i + 1], out _maxPlayers);      break;
                    case "--expectedPlayers": int.TryParse(args[i + 1], out _expectedPlayers); break;
                    case "--mapId":           _mapId = args[i + 1];                             break;
                    case "--matchId":         _matchId = args[i + 1];                          break;
                }
            }

            // Expose to ServerGameManager which reads this before RoomState can be checked on DS.
            BootstrappedExpectedPlayers = _expectedPlayers;

            Debug.Log("[DS-Boot] Args parsed:" +
                      $"\n  ServerId         : {_serverId}" +
                      $"\n  Port             : {_gamePort}" +
                      $"\n  BackendUrl       : {_backendUrl}" +
                      $"\n  MaxPlayers       : {_maxPlayers}" +
                      $"\n  ExpectedPlayers  : {(_expectedPlayers < 0 ? "(not set \u2014 ServerGameManager default)" : _expectedPlayers.ToString())}" +
                      $"\n  MapId            : {(string.IsNullOrEmpty(_mapId) ? "(default/current scene)" : _mapId)}" +
                      $"\n  MatchId          : {(string.IsNullOrEmpty(_matchId) ? "(not provided)" : _matchId)}");
        }

        // ─────────────────────────────────────────────────────────────────────────

        private IEnumerator BootSequence()
        {
            Debug.Log("[DS-Boot] ═══ Boot Sequence Start ═══");

            // Step 1: Wait 1 frame để mọi Awake() khác chạy xong
            yield return null;

            // Step 2: Set port vào Tugboat transport
            var transport = networkManager.TransportManager.Transport;
            transport.SetPort(_gamePort);
            Debug.Log($"[DS-Boot] Port set → {_gamePort}");

            // Step 2.5: Disable IPv6 on Tugboat to prevent LiteNetLib dual-stack bind conflict on Linux.
            // Default: LiteNetLib creates an IPv6 socket with IPV6_V6ONLY=0 (Linux default), which causes
            // that socket to claim BOTH [::]:port AND 0.0.0.0:port. The subsequent IPv4 bind then fails
            // with EADDRINUSE. FishNet briefly reports Started=true then false → LoadGlobalScenes is a no-op
            // → OnLoadEnd never fires → game-ready never sent → clients wait forever for ds_ready.
            // Fix: disable IPv6 entirely so the server only binds 0.0.0.0:port (IPv4), which is sufficient.
            if (networkManager.TransportManager.Transport is FishNet.Transporting.Tugboat.Tugboat tugboat)
            {
                var ipv6Field = typeof(FishNet.Transporting.Tugboat.Tugboat)
                    .GetField("_enableIpv6",
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);
                if (ipv6Field != null)
                {
                    ipv6Field.SetValue(tugboat, false);
                    Debug.Log("[DS-Boot] Step 2.5: IPv6 disabled on Tugboat — server will bind IPv4 only (dual-stack fix).");
                }
                else
                {
                    Debug.LogWarning("[DS-Boot] Step 2.5: Could not find _enableIpv6 on Tugboat — IPv6 bind conflict may occur.");
                }
            }

            // Step 3: Khởi động FishNet Server
            networkManager.ServerManager.StartConnection();
            Debug.Log("[DS-Boot] Step 3: FishNet ServerManager.StartConnection()...");

            // Step 4: Wait server active (timeout 10s)
            float elapsed = 0f;
            while (!networkManager.ServerManager.Started && elapsed < 10f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Race-condition guard: FishNet may set Started=true briefly before the async
            // bind failure propagates. Wait 2 extra frames for the transport to settle.
            yield return null;
            yield return null;

            if (!networkManager.ServerManager.Started)
            {
                Debug.LogError($"[DS-Boot] FATAL: FishNet Server failed to start (port {_gamePort} conflict or bind error). Shutting down.");
                Application.Quit(1);
                yield break;
            }

            Debug.Log($"[DS-Boot] ✓ FishNet Server active on port {_gamePort} (IPv4-only)");

            // Step 5: Đăng ký với backend
            yield return RegisterWithBackend();
        }

        // ─────────────────────────────────────────────────────────────────────────

        private IEnumerator RegisterWithBackend()
        {
            // Retry 3 lần (backend có thể chưa ready ngay khi container mới start)
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                Debug.Log($"[DS-Boot] Step 4 Register attempt {attempt}/3 \u2192 POST {_backendUrl}/api/ds/register (serverId={_serverId} port={_gamePort} maxPlayers={_maxPlayers})");

                string json = JsonUtility.ToJson(new RegisterRequest
                {
                    serverId     = _serverId,
                    port         = _gamePort,
                    status       = "ready",
                    maxPlayers   = _maxPlayers,
                    serverSecret = _serverSecret,
                });

                using var req = new UnityWebRequest($"{_backendUrl}/api/ds/register", "POST");
                req.uploadHandler   = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("X-DS-Secret", _serverSecret);
                req.timeout = 10;

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("[DS-Boot] Step 4 OK: Registered with backend. Step 5: Loading game scene...");
                    WriteHealthFile();
                    StartCoroutine(HeartbeatLoop());
                    LoadGameScene();
                    yield break;
                }

                Debug.LogWarning($"[DS-Boot] Step 4 FAILED attempt {attempt}/3: HTTP={req.responseCode} err={req.error} body={req.downloadHandler?.text}");

                if (attempt < 3)
                    yield return new WaitForSeconds(3f);
            }

            // Nếu không register được → server này sẽ not available ai vào
            // Vẫn chạy nhưng log error để backend biết
            Debug.LogError("[DS-Boot] Step 4 ERROR: Failed to register after 3 attempts. Server running but NOT visible to matchmaking!");
        }

        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Load game scene theo mapId nhận từ Docker ENV.
        ///
        /// DS boot vào 00_DS_Boot.unity (không phải map), nên LUÔN load map scene.
        /// - MAP_ID=map_01 → LoadGlobalScenes("02_Map_01", ReplaceScenes=All)
        /// - MAP_ID=map_02 → LoadGlobalScenes("02_Map_02", ReplaceScenes=All)
        ///   FishNet unload 00_DS_Boot, load map scene.
        ///   NetworkManager (DontDestroyOnLoad=1) survive sang map scene.
        ///   Map scene có NetworkManager riêng → FishNet destroy duplicate (DestroyNewest) → dùng boot NM.
        ///
        /// Client KHÔNG cần gọi hàm này. FishNet SceneManager tự sync scene cho client.
        /// Add map mới: thêm case vào switch + thêm scene vào BuildScript.SERVER_SCENES.
        /// </summary>
        // Tracks whether OnLoadEnd (FishNet) or sceneLoaded (Unity) already triggered setup.
        // Prevents double-invocation of PostSceneLoadSetup if both fire.
        private bool _sceneLoadSetupStarted = false;

        private void LoadGameScene()
        {
            // Resolve scene name via MapConfig + SceneConfig — no hardcoded map names.
            // When a new map is added: add it in MapConfig.asset + SceneConfig.asset only.
            string sceneName = "02_Map_01"; // safe fallback
            if (!string.IsNullOrEmpty(_mapId))
            {
                if (NightHunt.Config.MapConfig.TryGetById(_mapId, out NightHunt.Config.MapEntry mapEntry))
                    sceneName = NightHunt.Config.SceneConfig.GetSceneName(mapEntry.sceneId);
                else
                    Debug.LogWarning($"[DS-Boot] mapId='{_mapId}' not found in MapConfig — falling back to '{sceneName}'");
            }

            Debug.Log($"[DS-Boot] mapId='{_mapId}' → loading scene '{sceneName}'...");

            // Subscribe Unity SceneManager as a FALLBACK.
            // FishNet's SceneManager.OnLoadEnd is preferred, but if it silently fails
            // (null ref, FishNet bug, etc.), Unity's sceneLoaded fires unconditionally.
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnUnitySceneLoaded;

            // Start a timeout: if neither FishNet nor Unity fires the scene-loaded event
            // within 60s, call NotifyGameReady anyway — the server IS running, just the
            // scene load is stuck. Without this, ds_ready is never broadcast and the
            // container is cleaned as 'dead' by the backend watchdog.
            StartCoroutine(SceneLoadTimeout(60f));

            // ReplaceScenes=All: unload 00_DS_Boot, load map scene.
            // NetworkManager (DontDestroyOnLoad=1) tự survive sang scene mới.
            var sld = new SceneLoadData(sceneName)
            {
                ReplaceScenes = ReplaceOption.All,
                Options       = new LoadOptions { AllowStacking = false },
            };

            // Subscribe to FishNet SceneManager.OnLoadEnd to know when map scene is ready.
            if (networkManager.SceneManager != null)
            {
                networkManager.SceneManager.OnLoadEnd += OnGameSceneLoadEnd;
                try
                {
                    networkManager.SceneManager.LoadGlobalScenes(sld);
                    Debug.Log($"[DS-Boot] SceneManager.LoadGlobalScenes('{sceneName}') called successfully.");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[DS-Boot] LoadGlobalScenes threw an exception: {ex.Message}\n{ex.StackTrace}");
                    Debug.LogWarning("[DS-Boot] Falling back to Unity SceneManager.LoadScene directly.");
                    UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
                }
            }
            else
            {
                Debug.LogError("[DS-Boot] FishNet SceneManager is NULL — using Unity SceneManager.LoadScene fallback.");
                UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
            }
        }

        /// <summary>
        /// Unity SceneManager fallback: fires for ALL scene loads, including FishNet-managed ones.
        /// Prevents the case where FishNet's OnLoadEnd silently doesn't fire.
        /// </summary>
        private void OnUnitySceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (!scene.name.StartsWith("02_Map_", StringComparison.OrdinalIgnoreCase)) return;

            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnUnitySceneLoaded;
            Debug.Log($"[DS-Boot] Unity sceneLoaded: '{scene.name}' — triggering PostSceneLoadSetup.");

            if (!_sceneLoadSetupStarted)
            {
                _sceneLoadSetupStarted = true;
                StartCoroutine(PostSceneLoadSetup(1.5f));
            }
        }

        /// <summary>
        /// Timeout fallback: if scene load never fires either event within 60s,
        /// call NotifyGameReady directly. The FishNet server IS running — clients
        /// can still attempt to connect; they'll just skip scene sync.
        /// </summary>
        private IEnumerator SceneLoadTimeout(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (!_sceneLoadSetupStarted)
            {
                Debug.LogError($"[DS-Boot] TIMEOUT: Scene load did not complete within {seconds}s. " +
                               "Calling NotifyGameReady anyway so clients are not blocked forever.");
                _sceneLoadSetupStarted = true;
                yield return NotifyGameReady();
            }
        }

        private void OnGameSceneLoadEnd(SceneLoadEndEventArgs args)
        {
            foreach (var scene in args.LoadedScenes)
            {
                if (scene.name.StartsWith("02_Map_", StringComparison.OrdinalIgnoreCase))
                {
                    networkManager.SceneManager.OnLoadEnd -= OnGameSceneLoadEnd;
                    // Unsubscribe Unity fallback to prevent double-fire
                    UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnUnitySceneLoaded;
                    Debug.Log($"[DS-Boot] FishNet OnLoadEnd: scene '{scene.name}' loaded.");

                    if (!_sceneLoadSetupStarted)
                    {
                        _sceneLoadSetupStarted = true;
                        StartCoroutine(PostSceneLoadSetup(1.5f));
                    }
                    return;
                }
            }
        }

        /// <summary>
        /// Runs after game scene fully loads:
        ///   1. Subscribes to MatchEndManager.OnMatchEnded (single definition here, no race condition)
        ///   2. Calls POST /api/ds/game-ready so backend can broadcast ds_ready to players
        /// </summary>
        private IEnumerator PostSceneLoadSetup(float delay)
        {
            yield return new WaitForSeconds(delay);

#if UNITY_SERVER
            // MatchEndManager is now in the map scene — subscribe exactly once here
            var matchEndManager = FindFirstObjectByType<MatchEndManager>();
            if (matchEndManager != null)
            {
                matchEndManager.OnMatchEnded += OnMatchEnded;
                Debug.Log("[DS-Boot] ✓ Subscribed to MatchEndManager.OnMatchEnded");
            }
            else
            {
                Debug.LogError("[DS-Boot] Step 6 ERROR: MatchEndManager not found after scene load \u2014 " +
                               "game-end reporting to backend is DISABLED. Check scene setup.");
            }
#endif

            yield return NotifyGameReady();
        }

        /// <summary>
        /// POST /api/ds/game-ready — tells backend DS is fully ready.
        /// Backend broadcasts "ds_ready" WebSocket event to all players in the match,
        /// allowing clients to start connecting to this DS.
        /// </summary>
        private IEnumerator NotifyGameReady()
        {
            var body = new GameReadyRequest { serverId = _serverId, serverSecret = _serverSecret };
            string json = JsonUtility.ToJson(body);

            for (int attempt = 1; attempt <= 3; attempt++)
            {
                using var req = new UnityWebRequest($"{_backendUrl}/api/ds/game-ready", "POST");
                req.uploadHandler   = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("X-DS-Secret", _serverSecret);
                req.timeout = 10;

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[DS-Boot] Step 7 OK: game-ready notified \u2192 backend will broadcast ds_ready to players.  t={System.DateTime.UtcNow:HH:mm:ss.fff}");
                    yield break;
                }

                Debug.LogWarning($"[DS-Boot] Step 7 FAILED attempt {attempt}/3: HTTP={req.responseCode} err={req.error} body={req.downloadHandler?.text}");
                if (attempt < 3) yield return new WaitForSeconds(2f);
            }

            Debug.LogError("[DS-Boot] Step 7 ERROR: Failed to notify game-ready after 3 attempts. Clients may time out waiting for ds_ready WS.");
        }

        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Send heartbeat mỗi 30s để backend biết server còn alive.
        /// Backend timeout = 90s (3 lần miss) → mark server dead → cleanup container.
        /// </summary>
        private IEnumerator HeartbeatLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(30f);

                // Do NOT exit loop when ServerManager.Started=false — that would stop heartbeats
                // prematurely (e.g., during scene transitions) and cause the backend's dead-server
                // watchdog to reclaim this container before game-ready is sent.
                // The backend will reclaim the container naturally after match ends.
                int currentPlayers = 0;
                if (networkManager != null && networkManager.ServerManager != null && networkManager.ServerManager.Started)
                    currentPlayers = networkManager.ServerManager.Clients.Count;
                else
                    Debug.LogWarning("[DS-Boot] Heartbeat: ServerManager not Started — sending anyway to keep container alive.");

                Debug.Log($"[DS-Boot] Heartbeat — players={currentPlayers}/{_maxPlayers} matchId={_matchId}  t={System.DateTime.UtcNow:HH:mm:ss.fff}");

                string json = JsonUtility.ToJson(new HeartbeatRequest
                {
                    serverId       = _serverId,
                    currentPlayers = currentPlayers,
                    serverSecret   = _serverSecret,
                });

                using var req = new UnityWebRequest($"{_backendUrl}/api/ds/heartbeat", "POST");
                req.uploadHandler   = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("X-DS-Secret", _serverSecret);
                req.timeout = 5;

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                    WriteHealthFile(); // Refresh timestamp để health check biết server còn alive
                else
                    Debug.LogWarning($"[DS-Boot] Heartbeat failed: {req.error}");
            }
        }

        // ─────────────────────────────────────────────────────────────────────────

        private void OnMatchEnded(int winnerTeamId, MatchEndReason reason)
        {
            Debug.Log($"[DS-Boot] Match ended — winnerTeam={winnerTeamId}, reason={reason}. Reporting to backend...");
            StartCoroutine(ReportMatchEndAndShutdown(winnerTeamId, reason));
        }

        /// <summary>
        /// Send kết quả match về backend (POST /api/match/end/ranked) rồi tự shutdown.
        /// DS thu thập player data từ MatchEndManager / RegistryService.
        /// </summary>
        private System.Collections.IEnumerator ReportMatchEndAndShutdown(int winnerTeamId, MatchEndReason reason)
        {
            // Thu thập kết quả từng player từ MatchEndManager
            var matchEndManager = FindFirstObjectByType<MatchEndManager>();
            MatchResult[] playerResults = matchEndManager != null
                ? matchEndManager.GetFinalResults(winnerTeamId, reason)
                : System.Array.Empty<MatchResult>();

            // Build request body
            var entries = new System.Collections.Generic.List<MatchEndPlayerEntry>();
            foreach (var r in playerResults)
            {
                entries.Add(new MatchEndPlayerEntry
                {
                    userId      = r.BackendPlayerId,
                    displayName = r.DisplayName,
                    teamId      = r.TeamId,
                    kills       = r.Kills,
                    deaths      = r.Deaths,
                    score       = r.Score,
                });
            }

            var body = new MatchEndRequest
            {
                serverId      = _serverId,
                serverSecret  = _serverSecret,
                matchId       = _matchId,      // set từ game-ready response hoặc DS allocation
                winnerTeamId  = winnerTeamId,
                endReason     = reason.ToString(),
                playerResults = entries.ToArray(),
            };
            string json = JsonUtility.ToJson(body);

            // POST với retry 3 lần
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                using var req = new UnityWebRequest($"{_backendUrl}/api/match/end/ranked", "POST");
                req.uploadHandler   = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("X-DS-Secret", _serverSecret);
                req.timeout = 10;

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[DS-Boot] ✓ Match result reported (matchId={_matchId}, winner={winnerTeamId}).");
                    break;
                }

                Debug.LogWarning($"[DS-Boot] game-end report attempt {attempt}/3 failed: {req.responseCode} {req.error}");
                if (attempt < 3) yield return new WaitForSeconds(2f);
            }

            // Cho client 5s đọc kết quả rồi shutdown
            Debug.Log("[DS-Boot] Shutting down in 5s...");
            yield return new WaitForSeconds(5f);
            Application.Quit(0);
        }

        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Create/update file /app/logs/.healthy.
        /// Docker HEALTHCHECK script check file này để xác định server còn alive.
        /// </summary>
        private static void WriteHealthFile()
        {
            try
            {
                const string healthPath = "/app/logs/.healthy";
                System.IO.File.WriteAllText(healthPath, DateTime.UtcNow.ToString("O"));
            }
            catch (Exception e)
            {
                // Không crash server vì health file - chỉ là monitoring
                Debug.LogWarning($"[DS-Boot] Could not write health file: {e.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────────────────────

        [Serializable]
        private struct RegisterRequest
        {
            public string serverId;
            public int    port;
            public string status;
            public int    maxPlayers;
            public string serverSecret;
        }

        [Serializable]
        private struct HeartbeatRequest
        {
            public string serverId;
            public int    currentPlayers;
            public string serverSecret;
        }

        [Serializable]
        private struct GameReadyRequest
        {
            public string serverId;
            public string serverSecret;
        }

        [Serializable]
        private struct MatchEndRequest
        {
            public string serverId;
            public string serverSecret;
            public string matchId;
            public int    winnerTeamId;
            public string endReason;
            public MatchEndPlayerEntry[] playerResults;
        }

        [Serializable]
        private struct MatchEndPlayerEntry
        {
            public string userId;
            public string displayName;
            public int    teamId;
            public int    kills;
            public int    deaths;
            public float  score;
        }

        [Serializable]
        private struct MatchPresenceRequest
        {
            public string serverId;
            public string serverSecret;
            public string matchId;
            public long   userId;
            public string state;
            public string reason;
        }
    }
}
