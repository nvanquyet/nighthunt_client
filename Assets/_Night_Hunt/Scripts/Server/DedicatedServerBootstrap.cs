using System;
using System.Collections;
using System.Text;
using NightHunt.Networking;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace NightHunt.Server
{
    /// <summary>
    /// DedicatedServerBootstrap — khởi động logic khi Unity chạy ở chế độ Headless Server.
    ///
    /// Vòng đời:
    ///   1. Đọc env vars: SERVER_ID, GAME_PORT, BACKEND_URL, SERVER_SECRET, MAP_ID.
    ///   2. Load map scene (additive).
    ///   3. Start FishNet server trên GAME_PORT.
    ///   4. Register với backend (POST /api/ds/register).
    ///   5. Heartbeat mỗi 30 giây.
    ///
    /// Chỉ kích hoạt khi <see cref="Application.isBatchMode"/> = true.
    /// Trong Editor / Client build: component tự disable.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class DedicatedServerBootstrap : MonoBehaviour
    {
        [Header("References (auto-found if null)")]
        [SerializeField] private NetworkGameManager networkGameManager;

        // ── Env vars ──────────────────────────────────────────────────────────
        private string _serverId;
        private int    _gamePort;
        private string _backendUrl;
        private string _serverSecret;
        private string _mapId;

        // ── State ─────────────────────────────────────────────────────────────
        private bool _registered;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (!Application.isBatchMode)
            {
                // Không phải headless build — vô hiệu hóa component
                enabled = false;
                return;
            }

            _serverId     = GetEnv("SERVER_ID",     "unknown");
            _gamePort     = int.TryParse(GetEnv("GAME_PORT", "7777"), out int p) ? p : 7777;
            _backendUrl   = GetEnv("BACKEND_URL",   "http://localhost:8080/api");
            _serverSecret = GetEnv("SERVER_SECRET", "");
            _mapId        = GetEnv("MAP_ID",        "map_01");

            if (networkGameManager == null)
                networkGameManager = FindFirstObjectByType<NetworkGameManager>();

            Debug.Log($"[DS Bootstrap] serverId={_serverId} port={_gamePort} map={_mapId} backendUrl={_backendUrl}");
        }

        private IEnumerator Start()
        {
            // 1. Load map scene (additive) - scene name phải match Build Settings
            yield return LoadMapScene(_mapId);

            // 2. Start FishNet server
            if (networkGameManager != null)
            {
                networkGameManager.StartServer();
                Debug.Log($"[DS Bootstrap] FishNet server started on port {_gamePort}");
            }
            else
            {
                Debug.LogError("[DS Bootstrap] NetworkGameManager not found! Server not started.");
            }

            // 3. Đợi để server ổn định trước khi register
            yield return new WaitForSeconds(2f);

            // 4. Register với backend (retry tối đa 5 lần)
            for (int attempt = 1; attempt <= 5; attempt++)
            {
                yield return RegisterWithBackend();
                if (_registered) break;

                Debug.LogWarning($"[DS Bootstrap] Register attempt {attempt}/5 failed. Retrying in 5s...");
                yield return new WaitForSeconds(5f);
            }

            if (!_registered)
            {
                Debug.LogError("[DS Bootstrap] Could not register with backend after 5 attempts. Shutting down.");
                Application.Quit(1);
                yield break;
            }

            // 5. Start heartbeat
            InvokeRepeating(nameof(SendHeartbeat), 30f, 30f);
        }

        // ── Map loading ───────────────────────────────────────────────────────

        private IEnumerator LoadMapScene(string mapId)
        {
            // Map naming convention: map_01 → GameMap_01
            string sceneName = MapIdToSceneName(mapId);
            Debug.Log($"[DS Bootstrap] Loading map scene: {sceneName}");

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op == null)
            {
                Debug.LogError($"[DS Bootstrap] Scene '{sceneName}' not found in Build Settings! " +
                               "Add it to Build Settings before building DS.");
                yield break;
            }
            yield return op;
            Debug.Log($"[DS Bootstrap] Scene '{sceneName}' loaded.");
        }

        private static string MapIdToSceneName(string mapId)
        {
            // map_01 → GameMap_01,  map_02 → GameMap_02
            if (mapId.StartsWith("map_"))
            {
                string num = mapId.Substring(4); // "01"
                return $"GameMap_{num}";
            }
            return mapId; // Fallback: dùng nguyên
        }

        // ── Register ──────────────────────────────────────────────────────────

        private IEnumerator RegisterWithBackend()
        {
            string url  = $"{_backendUrl}/ds/register";
            string json = $"{{\"serverId\":\"{_serverId}\",\"serverSecret\":\"{_serverSecret}\",\"maxPlayers\":{GetEnv("MAX_PLAYERS", "16")}}}";

            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("X-DS-Secret", _serverSecret);
            req.timeout = 10;

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                _registered = true;
                Debug.Log($"[DS Bootstrap] Registered with backend. Response: {req.downloadHandler.text}");
            }
            else
            {
                Debug.LogWarning($"[DS Bootstrap] Register failed ({req.responseCode}): {req.error}");
            }
        }

        // ── Heartbeat ─────────────────────────────────────────────────────────

        private void SendHeartbeat()
        {
            StartCoroutine(HeartbeatCoroutine());
        }

        private IEnumerator HeartbeatCoroutine()
        {
            string url  = $"{_backendUrl}/ds/heartbeat";
            int    curr = networkGameManager?.NetworkManager?.ServerManager?.Clients?.Count ?? 0;
            string json = $"{{\"serverId\":\"{_serverId}\",\"serverSecret\":\"{_serverSecret}\",\"currentPlayers\":{curr}}}";

            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("X-DS-Secret", _serverSecret);
            req.timeout = 5;

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
                Debug.LogWarning($"[DS Bootstrap] Heartbeat failed: {req.error}");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string GetEnv(string key, string defaultValue = "")
        {
            string val = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrEmpty(val) ? defaultValue : val;
        }
    }
}
