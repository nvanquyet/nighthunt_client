using System;
using System.Collections;
using FishNet.Managing;
using FishNet.Managing.Scened;
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
    ///   5. Gửi heartbeat định kỳ (POST /api/ds/heartbeat)
    ///
    /// Gắn script này vào GameObject trong Scene: 00_DS_Boot
    /// </summary>
    public class ServerBootstrap : MonoBehaviour
    {
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
            Debug.LogWarning($"[ServerBootstrap] EDITOR MODE — serverId='{_serverId}' backendUrl='{_backendUrl}'. " +
                             "Fill fallbackServerId/fallbackServerSecret from POST /api/internal/allocate if registration fails.");
#endif

            if (networkManager == null)
                networkManager = FindFirstObjectByType<NetworkManager>();

            if (networkManager == null)
            {
                Debug.LogError("[ServerBootstrap] NetworkManager not found in scene!");
                Application.Quit(1);
                return;
            }

            StartCoroutine(BootSequence());
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

            Debug.Log("[ServerBootstrap] Args parsed:" +
                      $"\n  ServerId         : {_serverId}" +
                      $"\n  Port             : {_gamePort}" +
                      $"\n  BackendUrl       : {_backendUrl}" +
                      $"\n  MaxPlayers       : {_maxPlayers}" +
                      $"\n  ExpectedPlayers  : {(_expectedPlayers < 0 ? "(not set — ServerGameManager default)" : _expectedPlayers.ToString())}" +
                      $"\n  MapId            : {(string.IsNullOrEmpty(_mapId) ? "(default/current scene)" : _mapId)}");
        }

        // ─────────────────────────────────────────────────────────────────────────

        private IEnumerator BootSequence()
        {
            Debug.Log("[ServerBootstrap] ═══ Boot Sequence Start ═══");

            // Step 1: Chờ 1 frame để mọi Awake() khác chạy xong
            yield return null;

            // Step 2: Set port vào Tugboat transport
            var transport = networkManager.TransportManager.Transport;
            transport.SetPort(_gamePort);
            Debug.Log($"[ServerBootstrap] Port set → {_gamePort}");

            // Step 3: Khởi động FishNet Server
            networkManager.ServerManager.StartConnection();
            Debug.Log("[ServerBootstrap] FishNet Server starting...");

            // Step 4: Chờ server active (timeout 10s)
            float elapsed = 0f;
            while (!networkManager.ServerManager.Started && elapsed < 10f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!networkManager.ServerManager.Started)
            {
                Debug.LogError("[ServerBootstrap] Server failed to start within 10s! Shutting down.");
                Application.Quit(1);
                yield break;
            }

            Debug.Log($"[ServerBootstrap] ✓ FishNet Server active on port {_gamePort}");

            // Step 5: Đăng ký với backend
            yield return RegisterWithBackend();
        }

        // ─────────────────────────────────────────────────────────────────────────

        private IEnumerator RegisterWithBackend()
        {
            // Retry 3 lần (backend có thể chưa ready ngay khi container mới start)
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                Debug.Log($"[ServerBootstrap] Register attempt {attempt}/3...");

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
                    Debug.Log("[ServerBootstrap] ✓ Registered with backend successfully!");
                    WriteHealthFile();
                    StartCoroutine(HeartbeatLoop());
                    LoadGameScene();
                    yield break;
                }

                Debug.LogWarning($"[ServerBootstrap] Register failed: {req.responseCode} {req.error}");

                if (attempt < 3)
                    yield return new WaitForSeconds(3f);
            }

            // Nếu không register được → server này sẽ không có ai vào
            // Vẫn chạy nhưng log error để backend biết
            Debug.LogError("[ServerBootstrap] Failed to register after 3 attempts. " +
                           "Server running but NOT visible to matchmaking!");
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
        /// Thêm map mới: thêm case vào switch + thêm scene vào BuildScript.SERVER_SCENES.
        /// </summary>
        private void LoadGameScene()
        {
            // Với dedicated boot scene, LUÔN load map (không còn trường hợp "đã ở đúng scene")
            string sceneName = _mapId switch
            {
                "map_01" => "02_Map_01",
                "map_02" => "02_Map_02",
                // Thêm map mới ở đây:
                // "map_03" => "02_Map_03",
                _ => "02_Map_01", // fallback về map_01
            };

            Debug.Log($"[ServerBootstrap] mapId='{_mapId}' → loading scene '{sceneName}'...");

            // ReplaceScenes=All: unload 00_DS_Boot, load map scene.
            // NetworkManager (DontDestroyOnLoad=1) tự survive sang scene mới.
            var sld = new SceneLoadData(sceneName)
            {
                ReplaceScenes = ReplaceOption.All,
                Options       = new LoadOptions { AllowStacking = false },
            };

            // Subscribe to FishNet SceneManager.OnLoadEnd to know when map scene is ready,
            // then POST /ds/game-ready so clients can start connecting.
            networkManager.SceneManager.OnLoadEnd += OnGameSceneLoadEnd;
            networkManager.SceneManager.LoadGlobalScenes(sld);
        }

        private void OnGameSceneLoadEnd(SceneLoadEndEventArgs args)
        {
            foreach (var scene in args.LoadedScenes)
            {
                if (scene.name.StartsWith("02_Map_", StringComparison.OrdinalIgnoreCase))
                {
                    networkManager.SceneManager.OnLoadEnd -= OnGameSceneLoadEnd;
                    Debug.Log($"[ServerBootstrap] Game scene '{scene.name}' loaded — running post-scene setup.");
                    // Wait 1.5s for all MonoBehaviour Awake()/Start() to complete, then:
                    //   1. Subscribe to MatchEndManager (it now exists in the map scene)
                    //   2. Notify backend DS is ready for clients
                    StartCoroutine(PostSceneLoadSetup(1.5f));
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
            var matchEndManager = FindFirstObjectByType<NightHunt.Gameplay.Match.MatchEndManager>();
            if (matchEndManager != null)
            {
                matchEndManager.OnMatchEnded += OnMatchEnded;
                Debug.Log("[ServerBootstrap] ✓ Subscribed to MatchEndManager.OnMatchEnded");
            }
            else
            {
                Debug.LogError("[ServerBootstrap] MatchEndManager not found after scene load — " +
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
                    Debug.Log("[ServerBootstrap] ✓ ds/game-ready notified — clients can now connect.");
                    yield break;
                }

                Debug.LogWarning($"[ServerBootstrap] game-ready attempt {attempt}/3 failed: {req.responseCode} {req.error}");
                if (attempt < 3) yield return new WaitForSeconds(2f);
            }

            Debug.LogError("[ServerBootstrap] Failed to notify game-ready after 3 attempts. Clients may connect via retry.");
        }

        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Gửi heartbeat mỗi 30s để backend biết server còn alive.
        /// Backend timeout = 90s (3 lần miss) → mark server dead → cleanup container.
        /// </summary>
        private IEnumerator HeartbeatLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(30f);

                if (networkManager == null || !networkManager.ServerManager.Started)
                    yield break;

                int currentPlayers = networkManager.ServerManager.Clients.Count;

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
                    Debug.LogWarning($"[ServerBootstrap] Heartbeat failed: {req.error}");
            }
        }

        // ─────────────────────────────────────────────────────────────────────────

        private void OnMatchEnded(int winnerTeamId, NightHunt.Gameplay.Match.MatchEndReason reason)
        {
            Debug.Log($"[ServerBootstrap] Match ended — winnerTeam={winnerTeamId}, reason={reason}. Reporting to backend...");
            StartCoroutine(ReportMatchEndAndShutdown(winnerTeamId, reason));
        }

        /// <summary>
        /// Gửi kết quả match về backend (POST /api/match/end/ranked) rồi tự shutdown.
        /// DS thu thập player data từ MatchEndManager / RegistryService.
        /// </summary>
        private System.Collections.IEnumerator ReportMatchEndAndShutdown(int winnerTeamId, NightHunt.Gameplay.Match.MatchEndReason reason)
        {
            // Thu thập kết quả từng player từ MatchEndManager
            var matchEndManager = FindFirstObjectByType<NightHunt.Gameplay.Match.MatchEndManager>();
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
                    Debug.Log($"[ServerBootstrap] ✓ Match result reported (matchId={_matchId}, winner={winnerTeamId}).");
                    break;
                }

                Debug.LogWarning($"[ServerBootstrap] game-end report attempt {attempt}/3 failed: {req.responseCode} {req.error}");
                if (attempt < 3) yield return new WaitForSeconds(2f);
            }

            // Cho client 5s đọc kết quả rồi shutdown
            Debug.Log("[ServerBootstrap] Shutting down in 5s...");
            yield return new WaitForSeconds(5f);
            Application.Quit(0);
        }

        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Tạo/cập nhật file /app/logs/.healthy.
        /// Docker HEALTHCHECK script kiểm tra file này để xác định server còn alive.
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
                Debug.LogWarning($"[ServerBootstrap] Could not write health file: {e.Message}");
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
    }
}
