using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using NightHunt.Config;
using NightHunt.Core;
using NightHunt.State;
using NightHunt.UI;
using NightHunt.Utils;

namespace NightHunt.Core
{
    /// <summary>
    /// LoadingManager — App startup bootstrap. Chạy một lần khi 01_Home load.
    ///
    /// Flow (theo thứ tự):
    ///   1. Chờ GameManager khởi tạo (DontDestroyOnLoad)
    ///   2. Chờ PersistentUICanvas sẵn sàng
    ///   3. Services warm-up (ngắn)
    ///   4. Internet check → nếu OFFLINE: block + hiện Retry button (KHÔNG xoá token)
    ///                     → nếu ONLINE: tiếp tục
    ///   5. AutoLogin check:
    ///        - Không có token  → UINavigator.GoLogin()
    ///        - AutoLogin OK    → UINavigator.GoHome() hoặc GoLobby()
    ///        - AutoLogin FAIL  → xoá token → UINavigator.GoLogin()
    ///
    /// Khác với MatchLoadingOverlay: LoadingManager chỉ chạy khi app khởi động,
    /// MatchLoadingOverlay chạy trước mỗi lần vào gameplay.
    /// </summary>
    public class LoadingManager : MonoBehaviour
    {
        // ─── PlayerPrefs keys ───────────────────────────────────────────────
        public const string KEY_REFRESH_TOKEN = "auth_refresh_token";
        public const string KEY_REMEMBER_ME   = "auth_remember_me";

        // ─── Singleton ──────────────────────────────────────────────────────
        private static LoadingManager _instance;
        public static LoadingManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    if (PersistentUICanvas.Instance != null)
                        _instance = PersistentUICanvas.Instance.LoadingManager;

                    if (_instance == null)
                        _instance = FindFirstObjectByType<LoadingManager>();
                }
                return _instance;
            }
        }

        // ─── Inspector ──────────────────────────────────────────────────────
        [Header("UI References")]
        [SerializeField] private GameObject           loadingPanel;
        [SerializeField] private UnityEngine.UI.Slider        progressBar;
        [SerializeField] private TMPro.TextMeshProUGUI         loadingText;
        [SerializeField] private UnityEngine.UI.Button         retryButton;  // Hie\u0323n khi offline

        [Header("Settings")]
        [SerializeField] private float minLoadingTime  = 1.2f;
        [SerializeField] private float internetTimeout = 5f;   // Giây ping / health check timeout

        [Header("Backend Health")]
        [Tooltip("Config chứa apiHost để ping health endpoint.")]
        [SerializeField] private BackendConfig _backendConfig;
        [Tooltip("Path của health endpoint (full path with /api prefix).")]
        [SerializeField] private string        _healthPath = "/api/actuator/health"; // Spring Boot Actuator path with /api context

        // ─── State ──────────────────────────────────────────────────────────
        private bool      _isShowing;
        private bool      _retryRequested;
        private PanelType _targetPanel = PanelType.Login; // default fallback

        // ─────────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_instance == null)
                _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Start()
        {
            // Auto-load BackendConfig from Resources if not assigned in inspector
            if (_backendConfig == null)
            {
                // Try to find in scene first
                _backendConfig = FindFirstObjectByType<BackendConfig>();
                
                if (_backendConfig == null)
                {
                    // Try to load from Resources folder (must be in Assets/Resources/Configs/)
                    _backendConfig = Resources.Load<BackendConfig>("Configs/BackendConfig");
                }

                if (_backendConfig == null)
                {
                    Debug.LogError("[LoadingManager] ❌ CRITICAL: BackendConfig not found!");
                    Debug.LogError("[LoadingManager] Please either:");
                    Debug.LogError("  1. Assign BackendConfig asset in LoadingManager inspector");
                    Debug.LogError("  2. Place BackendConfig.asset in Assets/Resources/Configs/");
                    Debug.LogError("  3. Place BackendConfig.asset in scene as a component");
                    
                    // Use hardcoded fallback for now
                    Debug.LogWarning("[LoadingManager] ⚠️ Using fallback config (HTTPS localhost:8443)");
                    // Will proceed with null and rely on fallback URL
                }
                else
                {
                    Debug.Log($"[LoadingManager] ✅ BackendConfig auto-loaded: {_backendConfig.apiHost}");
                }
            }

            // Auto-find loadingPanel nếu chưa được assign trong Inspector
            if (loadingPanel == null)
            {
                // Tìm child có tên "LoadingPanel" (hoặc "Loading Panel")
                var found = transform.Find("LoadingPanel") ?? transform.Find("Loading Panel");
                if (found != null)
                {
                    loadingPanel = found.gameObject;
                    Debug.LogWarning($"[LoadingManager] ⚠️ loadingPanel tự động tìm thấy: '{found.name}'. Hãy gán vào Inspector.");
                }
                else
                {
                    Debug.LogError("[LoadingManager] ❌ loadingPanel chưa được gán trong Inspector và không tìm thấy child 'LoadingPanel'.");
                    Debug.LogError("[LoadingManager]    Gán GameObject chứa loading UI vào field 'Loading Panel' trong Inspector.");
                }
            }

            if (loadingPanel != null)
            {
                loadingPanel.SetActive(true);
                _isShowing = true;
                Debug.Log($"[LoadingManager] ✅ loadingPanel SetActive(true): '{loadingPanel.name}'");
            }

            // Retry button ẩn mặc định, chỉ hiện khi offline
            if (retryButton != null)
            {
                retryButton.gameObject.SetActive(false);
                retryButton.onClick.AddListener(OnRetryClicked);
            }

            
            // Allow self-signed cert bypass for .NET ClientWebSocket (NativeWebSocket on desktop/editor).
            // Only active when allowSelfSignedCert=true (local/staging dev against an IP with no trusted cert).
            // Production uses Let's Encrypt — no bypass needed.
#if !UNITY_WEBGL || UNITY_EDITOR
            if (_backendConfig != null && _backendConfig.ShouldBypassSslCertificateValidation())
            {
                System.Net.ServicePointManager.ServerCertificateValidationCallback =
                    (sender, certificate, chain, sslPolicyErrors) => true;
                Debug.LogWarning("[LoadingManager] ⚠️ SSL cert validation DISABLED (allowSelfSignedCert=true). For production use a trusted cert.");
            }
            else
            {
                // Restore default validation (important after hot-reload in Editor)
                System.Net.ServicePointManager.ServerCertificateValidationCallback = null;
            }
#endif

            StartCoroutine(InitFlow());
        }

        // ─────────────────────────────────────────────────────────────────────
        // Main Init Flow
        // ─────────────────────────────────────────────────────────────────────

        private IEnumerator InitFlow()
        {
            float startTime = Time.time;

            // ── Step 1: Chờ GameManager ──────────────────────────────────────
            UpdateLoadingUI("Khởi động game...", 0.05f);
            yield return StartCoroutine(WaitForGameManager());

            // ── Step 2: Chờ PersistentUICanvas ──────────────────────────────
            UpdateLoadingUI("Khởi tạo giao diện...", 0.25f);
            yield return StartCoroutine(WaitForPersistentUICanvas());

            // ── Step 3: Services warm-up ─────────────────────────────────
            UpdateLoadingUI("Tải dịch vụ...", 0.40f);
            yield return new WaitForSeconds(0.1f);

            // ── Step 4: Kiểm tra kết nối internet (OS-level) ─────────────────
            yield return StartCoroutine(WaitForInternet());

            // ── Step 5: Kiểm tra backend có hoạt động không ─────────────────
            yield return StartCoroutine(WaitForBackendHealth());

            // ── Step 5.5: Fetch game config (modes + maps) from backend ──────
            yield return StartCoroutine(FetchGameConfigFlow());

            // ── Step 6: Remember Me / Auto-Login check ───────────────────
            yield return StartCoroutine(CheckAutoLoginFlow());

            // ── Step 7: Đảm bảo thời gian tối thiểu ────────────────────────
            float elapsed = Time.time - startTime;
            if (elapsed < minLoadingTime)
                yield return new WaitForSeconds(minLoadingTime - elapsed);

            // ── Step 8: Điều hướng ───────────────────────────────────────────
            UpdateLoadingUI("Hoàn tất!", 1.0f);
            yield return new WaitForSeconds(0.15f);

            Navigate();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Game Config Fetch
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fetch game modes and maps from backend; populates GameModeConfig + MapConfig.
        /// Non-fatal: if it fails the ScriptableObject defaults stay in place.
        /// </summary>
        private IEnumerator FetchGameConfigFlow()
        {
            UpdateLoadingUI("Tải cấu hình game...", 0.50f);

            if (GameManager.Instance?.GameConfigService == null)
            {
                Debug.LogWarning("[LoadingManager] GameConfigService not available — skipping.");
                yield break;
            }

            bool completed = false;
            GameManager.Instance.GameConfigService
                .FetchAsync()
                .ContinueWith(_ => completed = true);

            float waited = 0f;
            while (!completed && waited < 8f)
            {
                waited += UnityEngine.Time.deltaTime;
                yield return null;
            }

            if (!completed)
                Debug.LogWarning("[LoadingManager] GameConfigService.FetchAsync() timed out — using defaults.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Auto-Login Flow
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Kiểm tra refreshToken local.
        /// Nếu có → gọi API AutoLogin → lấy accessToken mới.
        /// Nếu không / thất bại → về LoginPanel.
        /// </summary>
        private IEnumerator CheckAutoLoginFlow()
        {
            // ── Đọc token local ──────────────────────────────────────────────
            string refreshToken = SecureStorage.GetString(KEY_REFRESH_TOKEN, "");

            if (string.IsNullOrEmpty(refreshToken))
            {
                UpdateLoadingUI("Vui lòng đăng nhập...", 0.85f);
                _targetPanel = PanelType.Login;
                yield break;
            }

            // ── Gọi AutoLogin API ────────────────────────────────────────────
            UpdateLoadingUI("Đang xác thực tài khoản...", 0.60f);

            bool completed = false;
            bool success   = false;

            if (GameManager.Instance?.AuthService != null)
            {
                GameManager.Instance.AuthService
                    .AutoLogin()
                    .ContinueWith(task =>
                    {
                        success   = task.Result.Success;
                        completed = true;
                    });
            }
            else
            {
                // AuthService không có → về Login
                Debug.LogWarning("[LoadingManager] AuthService null — skipping AutoLogin");
                PlayerPrefs.DeleteKey(KEY_REFRESH_TOKEN);
                PlayerPrefs.Save();
                _targetPanel = PanelType.Login;
                yield break;
            }

            // ── Chờ kết quả (progress tăng dần 60% → 82%) ───────────────────
            float timeout = 10f, waited = 0f;
            while (!completed && waited < timeout)
            {
                waited += Time.deltaTime;
                float t = Mathf.Clamp01(waited / timeout);
                UpdateLoadingUI("Đang xác thực tài khoản...", Mathf.Lerp(0.60f, 0.82f, t));
                yield return null;
            }

            // ── Xử lý kết quả ───────────────────────────────────────────────
            if (success)
            {
                UpdateLoadingUI("Đăng nhập thành công!", 0.88f);
                yield return new WaitForSeconds(0.1f);

                // Còn trong room → vào thẳng Lobby
                bool wasInRoom = GameManager.Instance?.RoomState != null
                              && GameManager.Instance.RoomState.IsInRoom
                              && !string.IsNullOrEmpty(GameManager.Instance.RoomState.RoomId.ToString());

                _targetPanel = wasInRoom ? PanelType.Lobby : PanelType.Home;
                Debug.Log($"[LoadingManager] AutoLogin OK → target={_targetPanel}");
            }
            else
            {
                // Token hết hạn hoặc API lỗi → xóa token, về Login
                Debug.Log("[LoadingManager] AutoLogin failed — clearing token");
                SecureStorage.DeleteKey(KEY_REFRESH_TOKEN);

                UpdateLoadingUI("Phiên đăng nhập hết hạn...", 0.88f);
                yield return new WaitForSeconds(0.1f);

                _targetPanel = PanelType.Login;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Internet check (block + retry)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Block flow đến khi có kết nối mạng.
        /// Dùng Application.internetReachability — nhanh, không cần ping thực.
        /// QUAN TRỌNG: KHÔNG bao giờ xóa token khi offline.
        /// </summary>
        private IEnumerator WaitForInternet()
        {
            if (Application.internetReachability != NetworkReachability.NotReachable)
            {
                UpdateLoadingUI("Kết nối sẵn sàng...", 0.44f);
                yield break;
            }

            Debug.LogWarning("[LoadingManager] Offline detected — blocking until internet returns.");
            ShowRetryButton(true);

            while (Application.internetReachability == NetworkReachability.NotReachable)
            {
                UpdateLoadingUI("⚠️ Mất kết nối internet. Kiểm tra mạng rồi bấm Thử lại.", 0.42f);

                _retryRequested = false;
                float waited = 0f;
                while (!_retryRequested && waited < 2f)
                {
                    waited += Time.deltaTime;
                    yield return null;
                }

                if (_retryRequested && Application.internetReachability == NetworkReachability.NotReachable)
                    ToastService.Instance?.Show("Vẫn mất mạng", "Chưa phát hiện kết nối internet.");

                _retryRequested = false;
            }

            ShowRetryButton(false);
            UpdateLoadingUI("Kết nối đã khôi phục!", 0.44f);
            yield return new WaitForSeconds(0.3f);
            Debug.Log("[LoadingManager] Internet restored — continuing.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Backend health check
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Ping GET {_healthPath} để xác nhận server đang chạy.
        /// Block flow nếu server không phản hồi, cho phép retry thủ công hoặc tự động sau 5s.
        /// Phân biệt rõ: "không kết nối được" vs "server trả lỗi 5xx (bảo trì)".
        /// </summary>
        private IEnumerator WaitForBackendHealth()
        {
            while (true)
            {
                UpdateLoadingUI("Kiểm tra server...", 0.50f);

                // Ensure health path has /api prefix (defensive check)
                string healthPath = _healthPath;
                if (!healthPath.StartsWith("/api/"))
                {
                    healthPath = "/api" + (healthPath.StartsWith("/") ? healthPath : "/" + healthPath);
                    Debug.LogWarning($"[LoadingManager] ⚠️ Health path corrected: {_healthPath} → {healthPath}");
                }

                string url = _backendConfig != null
                    ? $"{_backendConfig.GetApiBaseUrl()}{healthPath}"
                    : "https://localhost:8443" + healthPath; // Fallback: HTTPS localhost (self-signed cert)

                using var req = UnityWebRequest.Get(url);
                req.timeout = Mathf.Max(1, (int)internetTimeout);

                // Attach AcceptAllCertificatesHandler nếu server dùng self-signed cert (mkcert + IP)
                if (_backendConfig != null && _backendConfig.ShouldBypassSslCertificateValidation())
                {
                    req.certificateHandler = new NightHunt.Config.AcceptAllCertificatesHandler();
                }

                // 📋 DEBUG LOG: Health check request details
                Debug.Log($"[LoadingManager] 🔍 Health Check Request:");
                Debug.Log($"  URL: {url}");
                Debug.Log($"  Timeout: {req.timeout}s");
                // HTTPS luon duoc su dung — khong co fallback HTTP
                if (_backendConfig != null)
                {
                    Debug.Log($"  Host: {_backendConfig.apiHost} (HTTPS)");
                }
                else
                {
                    Debug.Log($"  Config: NULL (using fallback HTTPS localhost)");
                }

                yield return req.SendWebRequest();

                bool ok = req.result == UnityWebRequest.Result.Success
                       && (req.responseCode == 200 || req.responseCode == 204);

                if (ok)
                {
                    ShowRetryButton(false);
                    UpdateLoadingUI("Server sẵn sàng!", 0.58f);
                    Debug.Log($"[LoadingManager] ✅ Backend health check PASSED - Status: {req.responseCode}");
                    yield break;
                }

                // Xác định loại lỗi cho Toast rõ ràng hơn
                bool serverError = req.responseCode >= 500;
                string errorMsg = serverError
                    ? "⚠️ Server đang bảo trì. Vui lòng thử lại sau."
                    : "⚠️ Không kết nối được server.";

                UpdateLoadingUI(errorMsg, 0.50f);
                ShowRetryButton(true);

                // 📋 ENHANCED DEBUG LOG: Detailed error info
                Debug.LogWarning($"[LoadingManager] ❌ Backend health FAILED:");
                Debug.LogWarning($"  Result: {req.result}");
                Debug.LogWarning($"  Status Code: {req.responseCode}");
                Debug.LogWarning($"  Error: {req.error}");
                
                // Log response body if available (might contain useful error info)
                if (!string.IsNullOrEmpty(req.downloadHandler?.text))
                {
                    Debug.LogWarning($"  Response Body: {req.downloadHandler.text}");
                }

                // Diagnose specific connection issues
                if (req.result == UnityWebRequest.Result.ConnectionError)
                {
                    // Case-insensitive SSL detection + Curl error 60 (CN mismatch)
                    bool isSslError = req.error != null &&
                        (req.error.IndexOf("SSL",        System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         req.error.IndexOf("certificate", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         req.error.IndexOf("Cert",        System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         req.error.IndexOf("CA",          System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         req.error.IndexOf("TLS",         System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         req.error.IndexOf("UnityTls",    System.StringComparison.OrdinalIgnoreCase) >= 0);

                    if (isSslError)
                    {
                        bool isLocalHost = url.IndexOf("localhost",  System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                           url.Contains("127.0.0.1");
                        bool isCnMismatch = req.error.IndexOf("mismatch",    System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            req.error.IndexOf("Common Name",  System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            req.error.IndexOf("CN",           System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            req.error.IndexOf("Cert verify",  System.StringComparison.OrdinalIgnoreCase) >= 0;

                        if (isLocalHost)
                        {
                            // Local dev: mkcert CA not trusted
                            Debug.LogError($"[LoadingManager] SSL CERT ERROR (local) — mkcert CA chua duoc install vao Windows");
                            Debug.LogError($"  URL: {url}");
                            Debug.LogError($"  Fix: Chay Tools/setup-dev-cert.ps1 hoac 'mkcert -install' trong PowerShell");
                            Debug.LogError($"  Sau do RESTART Unity Editor");
                        }
                        else if (isCnMismatch)
                        {
                            // Cloud server: certificate CN does not match the host being connected to
                            Debug.LogError($"[LoadingManager] SSL CERT ERROR (cloud CN mismatch) — Certificate CN khong khop voi host");
                            Debug.LogError($"  URL: {url}");
                            Debug.LogError($"  Host dang ket noi: {_backendConfig?.apiHost}");
                            Debug.LogError($"  Nguyen nhan co the:");
                            Debug.LogError($"    1. 'prodApiHost' trong BackendConfig sai hoac khong khop voi CN tren cert cua server");
                            Debug.LogError($"    2. Server dang dung self-signed cert (mkcert) thay vi Let's Encrypt");
                            Debug.LogError($"    3. Trong editor: bat 'Force Production In Editor' tren BackendConfig de test cloud");
                            Debug.LogError($"  Raw error: {req.error}");
                        }
                        else
                        {
                            // Cloud server: cert not trusted (CA chain issue)
                            Debug.LogError($"[LoadingManager] SSL CERT ERROR (cloud) — Server cert khong duoc trust");
                            Debug.LogError($"  URL: {url}");
                            Debug.LogError($"  Kiem tra: server co Let's Encrypt cert hop le khong?");
                            Debug.LogError($"  Raw error: {req.error}");
                        }
                    }
                    else
                    {
                        Debug.LogError($"[LoadingManager] CONNECTION ERROR:");
                        Debug.LogError($"  URL: {url}");
                        Debug.LogError($"  1. Server chua chay tren {_backendConfig?.apiHost ?? "localhost:8443"}");
                        Debug.LogError($"  2. SSL cert chua duoc setup — chay Tools/setup-dev-cert.ps1");
                        Debug.LogError($"  3. Port bi block boi Firewall");
                    }
                }
                else if (req.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError($"[LoadingManager] 🔴 PROTOCOL ERROR - HTTP {req.responseCode}");
                    if (req.responseCode == 404)
                    {
                        Debug.LogError($"  Health path problem! Check if path is correct: {url}");
                        Debug.LogError($"  Expected: https://localhost:8443/api/actuator/health");
                        Debug.LogError($"  Actual: {url}");
                    }
                }

                // Tự retry sau 5s hoặc ngay khi user bấm Retry
                _retryRequested = false;
                float waited = 0f;
                while (!_retryRequested && waited < 5f)
                {
                    waited += Time.deltaTime;
                    yield return null;
                }
                _retryRequested = false;

                UpdateLoadingUI("Đang thử lại...", 0.50f);
                yield return new WaitForSeconds(0.2f);
            }
        }

        private void OnRetryClicked()
        {
            _retryRequested = true;
            ToastService.Instance?.Show("Đang thử lại", "Đang kiểm tra kết nối...");
            Debug.Log("[LoadingManager] Retry requested.");
        }

        private void ShowRetryButton(bool show)
        {
            if (retryButton != null)
                retryButton.gameObject.SetActive(show);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Navigate
        // ─────────────────────────────────────────────────────────────────────

        private void Navigate()
        {
            if (UINavigator.Instance == null)
            {
                Debug.LogError("[LoadingManager] UINavigator.Instance is null!");
                Hide();
                return;
            }

            // Hide loading panel BEFORE showing target panel to ensure it doesn't block raycasts
            Hide();
            UINavigator.Instance.ShowPanel(_targetPanel, forceInstant: false);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Wait helpers
        // ─────────────────────────────────────────────────────────────────────

        private IEnumerator WaitForGameManager()
        {
            float timeout = 5f, elapsed = 0f;
            while (GameManager.Instance == null && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                UpdateLoadingUI("Khởi động game...", Mathf.Lerp(0.05f, 0.22f, elapsed / timeout));
                yield return null;
            }

            if (GameManager.Instance == null)
                Debug.LogError("[LoadingManager] GameManager failed to initialize!");

            yield return new WaitForSeconds(0.05f);
        }

        private IEnumerator WaitForPersistentUICanvas()
        {
            float timeout = 3f, elapsed = 0f;
            while (PersistentUICanvas.Instance == null && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                UpdateLoadingUI("Khởi tạo giao diện...", Mathf.Lerp(0.25f, 0.42f, elapsed / timeout));
                yield return null;
            }

            if (PersistentUICanvas.Instance == null)
            {
                Debug.LogWarning("[LoadingManager] PersistentUICanvas not found — creating manually");
                PersistentUICanvas.GetOrCreate();
            }

            yield return new WaitForSeconds(0.05f);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Hiện loading overlay với message tuỳ chọn.</summary>
        public void Show(string message = null)
        {
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(true);
                _isShowing = true;
            }
            if (!string.IsNullOrEmpty(message))
                UpdateLoadingUI(message, progressBar != null ? progressBar.value : 0f);
        }

        /// <summary>Ẩn loading overlay.</summary>
        public void Hide()
        {
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(false);
                _isShowing = false;
                Debug.Log("[LoadingManager] ✅ loadingPanel SetActive(false) — ẩn loading UI.");
            }
            else
            {
                Debug.LogError("[LoadingManager] ❌ Hide() gọi nhưng loadingPanel là null! Loading UI sẽ không tắt.");
            }
        }

        public bool IsShowing() => _isShowing;

        // ─────────────────────────────────────────────────────────────────────
        // Internal helpers
        // ─────────────────────────────────────────────────────────────────────

        private void UpdateLoadingUI(string message, float progress)
        {
            if (loadingText  != null) loadingText.text  = message;
            if (progressBar  != null) progressBar.value = progress;
            Debug.Log($"[Loading] {progress:P0}  {message}");
        }
    }
}