using System;
using System.Collections;
using FishNet.Managing;
using UnityEngine;
using UnityEngine.Networking;

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
    /// Gắn script này vào GameObject trong Scene: 99_Dedicated_Server
    /// </summary>
    public class ServerBootstrap : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private NetworkManager networkManager;

        [Header("Fallback (Production-Localhost)")]
        [SerializeField] private ushort  fallbackPort       = 7777;
        [SerializeField] private string  fallbackBackendUrl = "https://localhost:8443";
        [SerializeField] private string  fallbackServerId   = "localhost-production-test";
        [SerializeField] private int     fallbackMaxPlayers  = 16;

        // Được parse từ CLI args (Docker truyền vào qua entrypoint.sh)
        private string _serverId;
        private ushort _gamePort;
        private string _backendUrl;
        private string _serverSecret;
        private int    _maxPlayers;

        // ─────────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            // Disable tất cả MonoBehaviour không cần trong server
            Application.targetFrameRate = 30;
            QualitySettings.vSyncCount  = 0;

#if UNITY_SERVER
            ParseCommandLineArgs();
#else
            // Editor fallback uses the same localhost production endpoint.
            _serverId     = fallbackServerId;
            _gamePort     = fallbackPort;
            _backendUrl   = fallbackBackendUrl;
            _serverSecret = "replace-this-production-ds-admin-secret";
            _maxPlayers   = fallbackMaxPlayers;
            Debug.LogWarning("[ServerBootstrap] Running in editor with localhost production fallback config.");
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
                    case "--maxPlayers":   int.TryParse(args[i + 1], out _maxPlayers); break;
                }
            }

            Debug.Log("[ServerBootstrap] Args parsed:" +
                      $"\n  ServerId   : {_serverId}" +
                      $"\n  Port       : {_gamePort}" +
                      $"\n  BackendUrl : {_backendUrl}" +
                      $"\n  MaxPlayers : {_maxPlayers}");
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
                    WriteHealthFile(); // Báo cho Docker health check biết server OK
                    StartCoroutine(HeartbeatLoop());
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
    }
}
