using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using NightHunt.Audio;
using NightHunt.Config;
using NightHunt.Core;
using NightHunt.State;
using NightHunt.UI;
using NightHunt.Utils;

namespace NightHunt.Core
{
    /// <summary>
    /// LoadingManager — App startup bootstrap. Runs once when the 01_Home scene loads.
    ///
    /// Boot flow (in order):
    ///   0. Play BootIntroView before showing the first loading overlay
    ///   1. Wait for GameManager to initialize (DontDestroyOnLoad)
    ///   2. Wait for PersistentUICanvas to be ready
    ///   3. Services warm-up (brief)
    ///   4. Internet check → if OFFLINE: block + show Retry button (do NOT delete token)
    ///                     → if ONLINE: continue
    ///   5. AutoLogin check:
    ///        - No token       → UINavigator.GoLogin()
    ///        - AutoLogin OK   → UINavigator.GoHome() or GoLobby()
    ///        - AutoLogin FAIL → clear token → UINavigator.GoLogin()
    ///
    /// Unlike MatchLoadingOverlay: LoadingManager only runs at app startup,
    /// MatchLoadingOverlay runs before each gameplay session.
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
        [Tooltip("Optional package-skinned progress adapter. Prefer LoadingProgressView over direct Unity Slider access.")]
        [SerializeField] private MonoBehaviour progressViewComponent;
        [SerializeField] private TMPro.TextMeshProUGUI         loadingText;
        [SerializeField] private UnityEngine.UI.Button         retryButton;  // Shown when offline

        [Header("Boot Intro")]
        [Tooltip("Visual intro shown before the first startup loading overlay. Auto-created if missing.")]
        [SerializeField] private BootIntroView bootIntroView;

        [Header("Settings")]
        [SerializeField] private float minLoadingTime  = 1.2f;
        [SerializeField] private float internetTimeout = 5f;   // Seconds for ping / health check timeout
        [SerializeField] private bool playIntroBeforeFirstLoading = true;
        [SerializeField, Min(0f)] private float introMinDuration = 0.35f;
        [SerializeField, Min(0.1f)] private float introMaxDuration = 3f;
        [SerializeField, Min(1f)] private float homePreloadTimeout = 15f;

        [Header("Backend Health")]
        [Tooltip("Config containing apiHost used to ping the health endpoint.")]
        [SerializeField] private BackendConfig _backendConfig;
        [Tooltip("Path of the health endpoint (full path with /api prefix).")]
        [SerializeField] private string        _healthPath = "/api/actuator/health"; // Spring Boot Actuator path with /api context

        // ─── State ──────────────────────────────────────────────────────────
        private bool      _isShowing;
        private bool      _retryRequested;
        private PanelType _targetPanel = PanelType.Login; // default fallback
        private string    _activeOwner;
        private int       _activeHandleId;
        private ILoadingProgressView _progressView;

        public bool HasCompletedInitialFlow { get; private set; }

        // ─────────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_instance == null)
                _instance = this;

            ResolveLoadingReferences(createFallback: false);
            ResolveProgressView();
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

            // Auto-find loadingPanel if not assigned in Inspector
            if (loadingPanel == null)
            {
                // Search for a child named "LoadingPanel" (or "Loading Panel")
                var found = transform.Find("LoadingPanel") ?? transform.Find("Loading Panel");
                if (found != null)
                {
                    loadingPanel = found.gameObject;
                    Debug.LogWarning($"[LoadingManager] ⚠️ loadingPanel auto-detected: '{found.name}'. Please assign it in the Inspector.");
                }
                else
                {
                    Debug.LogError("[LoadingManager] ❌ loadingPanel is not assigned in the Inspector and no child named 'LoadingPanel' was found.");
                    Debug.LogError("[LoadingManager]    Assign the loading UI GameObject to the 'Loading Panel' field in the Inspector.");
                }
            }

            ResolveLoadingReferences(createFallback: true);
            ResolveBootIntroView(createFallback: true);

            if (loadingPanel != null)
            {
                loadingPanel.SetActive(false);
                _isShowing = false;
                Debug.Log($"[LoadingManager] loadingPanel prepared: '{loadingPanel.name}'");
            }

            // Retry button hidden by default; shown only when offline
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

            StartCoroutine(BootThenInitFlow());
        }

        private IEnumerator BootThenInitFlow()
        {
            if (loadingPanel != null)
            {
                ResolveLoadingReferences(createFallback: true);
                loadingPanel.SetActive(true);
                _isShowing = true;
                UpdateLoadingUI("Starting up...", 0.02f);
                Debug.Log($"[LoadingManager] loadingPanel pre-activated behind intro: '{loadingPanel.name}'");
            }

            yield return StartCoroutine(PlayIntroBeforeLoadingFlow());

            if (loadingPanel != null)
            {
                loadingPanel.SetActive(true);
                _isShowing = true;
            }

            yield return StartCoroutine(InitFlow());
        }

        private IEnumerator PlayIntroBeforeLoadingFlow()
        {
            if (!playIntroBeforeFirstLoading)
                yield break;

            ResolveBootIntroView(createFallback: true);

            var audioManager = AudioManager.Instance;
            var introClip = audioManager != null ? audioManager.Library?.gameIntro : null;
            
            if (bootIntroView != null)
            {
                // Start the intro
                IEnumerator introRoutine = bootIntroView.PlayIntro(introClip, introMinDuration, introMaxDuration);
                
                // We want to activate the loading panel slightly before the intro finishes fading out
                // to avoid the "blank UI" gap.
                // Instead of yielding the whole routine, we'll wait until it's near completion or 
                // handle the overlap here.
                
                // For simplicity and safety, we'll let it play, but we'll modify the logic to 
                // show the loading panel earlier if we had a more complex hook.
                // However, the cleanest way is to ensure loadingPanel is Active(true) 
                // BEFORE the final HideImmediate in BootIntroView.
                
                yield return StartCoroutine(introRoutine);
                yield break;
            }

            if (introClip == null)
                yield break;

            audioManager.PlayUI(introClip);
            float wait = Mathf.Clamp(introClip.length, introMinDuration, introMaxDuration);
            yield return new WaitForSecondsRealtime(wait);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Main Init Flow
        // ─────────────────────────────────────────────────────────────────────

        private IEnumerator InitFlow()
        {
            float startTime = Time.time;

            // ── Step 1: Wait GameManager ──────────────────────────────────────
            UpdateLoadingUI("Starting up...", 0.05f);
            yield return StartCoroutine(WaitForGameManager());

            // ── Step 2: Wait PersistentUICanvas ──────────────────────────────
            UpdateLoadingUI("Initializing UI...", 0.25f);
            yield return StartCoroutine(WaitForPersistentUICanvas());

            // ── Step 3: Services warm-up ─────────────────────────────────
            UpdateLoadingUI("Loading services...", 0.40f);
            yield return new WaitForSeconds(0.1f);

            // ── Step 4: Internet check (OS-level) ───────────────────────────
            yield return StartCoroutine(WaitForInternet());

            // ── Step 5: Backend health check ───────────────────────────────
            yield return StartCoroutine(WaitForBackendHealth());

            // ── Step 6: Remember Me / Auto-Login check ───────────────────
            yield return StartCoroutine(CheckAutoLoginFlow());

            // ── Step 6.5: Fetch game config (modes + maps) from backend ──────
            // Must run AFTER auth — endpoints require a valid Bearer token.
            if (_targetPanel != PanelType.Login)
                yield return StartCoroutine(FetchGameConfigFlow());

            if (_targetPanel == PanelType.Home)
                yield return StartCoroutine(PreloadHomeDataBeforeNavigationFlow());

            // ── Step 7: Enforce minimum loading time ────────────────────────
            float elapsed = Time.time - startTime;
            if (elapsed < minLoadingTime)
                yield return new WaitForSeconds(minLoadingTime - elapsed);

            // ── Step 8: Navigate ─────────────────────────────────────────────────
            UpdateLoadingUI("Done!", 1.0f);
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
            UpdateLoadingUI("Loading game config...", 0.50f);

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
        /// Preloads first-home data while the loading overlay is still active.
        /// </summary>
        private IEnumerator PreloadHomeDataBeforeNavigationFlow()
        {
            UpdateLoadingUI("Loading player data...", 0.72f);

            var homeView = FindFirstObjectByType<HomeView>(FindObjectsInactive.Include);
            if (homeView == null)
            {
                Debug.LogWarning("[LoadingManager] HomeView not found. Navigation will continue without home preload.");
                yield break;
            }

            while (true)
            {
                _retryRequested = false;
                ShowRetryButton(false);

                Task preloadTask = homeView.PreloadDataAsync();
                float waited = 0f;

                while (!preloadTask.IsCompleted && waited < homePreloadTimeout)
                {
                    waited += Time.deltaTime;
                    float t = Mathf.Clamp01(waited / homePreloadTimeout);
                    UpdateLoadingUI("Loading player data...", Mathf.Lerp(0.72f, 0.90f, t));
                    yield return null;
                }

                if (preloadTask.IsCompleted && !preloadTask.IsFaulted && !preloadTask.IsCanceled)
                {
                    UpdateLoadingUI("Player data ready...", 0.92f);
                    yield break;
                }

                if (preloadTask.IsFaulted)
                    Debug.LogException(preloadTask.Exception);
                else if (preloadTask.IsCanceled)
                    Debug.LogWarning("[LoadingManager] Home preload was canceled.");
                else
                    Debug.LogWarning($"[LoadingManager] Home preload timed out after {homePreloadTimeout:F1}s.");

                UpdateLoadingUI("Could not load player data. Press Retry.", 0.72f);
                ShowRetryButton(true);

                while (!_retryRequested)
                    yield return null;
            }
        }

        /// <summary>
        /// Checks the local refresh token and selects Login or Home as the startup target.
        /// </summary>
        private IEnumerator CheckAutoLoginFlow()
        {
            // ── Read token local ──────────────────────────────────────────────
            string refreshToken = SecureStorage.GetString(KEY_REFRESH_TOKEN, "");

            if (string.IsNullOrEmpty(refreshToken))
            {
                UpdateLoadingUI("Please log in...", 0.85f);
                _targetPanel = PanelType.Login;
                yield break;
            }

            // ── Call AutoLogin API ────────────────────────────────────────────
            UpdateLoadingUI("Authenticating account...", 0.60f);

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
                // AuthService not available → redirect to Login
                Debug.LogWarning("[LoadingManager] AuthService null — skipping AutoLogin");
                SecureStorage.DeleteKey(KEY_REFRESH_TOKEN);
                _targetPanel = PanelType.Login;
                yield break;
            }

            // ── Wait for result (progress rises 60% → 82%) ───────────────────
            float timeout = 10f, waited = 0f;
            while (!completed && waited < timeout)
            {
                waited += Time.deltaTime;
                float t = Mathf.Clamp01(waited / timeout);
                UpdateLoadingUI("Authenticating account...", Mathf.Lerp(0.60f, 0.82f, t));
                yield return null;
            }

            // ── Process result ───────────────────────────────────────────────
            if (success)
            {
                UpdateLoadingUI("Login successful!", 0.88f);
                yield return new WaitForSeconds(0.1f);

                // Always clear stale room state on (re-)login.
                // The backend may have disbanded the room while the client was offline.
                // Routing to Lobby with a stale RoomState causes "leave custom room" blocks.
                NightHunt.State.RoomState.Instance?.ClearRoom();
                NightHunt.State.RoomState.Instance?.ClearNetworkSession();

                _targetPanel = PanelType.Home;
                Debug.Log($"[LoadingManager] AutoLogin OK → target={_targetPanel}");
            }
            else
            {
                // Token expired or API error → clear token, redirect to Login
                Debug.Log("[LoadingManager] AutoLogin failed — clearing token");
                SecureStorage.DeleteKey(KEY_REFRESH_TOKEN);

                UpdateLoadingUI("Session expired...", 0.88f);  // already English
                yield return new WaitForSeconds(0.1f);

                _targetPanel = PanelType.Login;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Internet check (block + retry)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Blocks flow until network is available.
        /// Uses Application.internetReachability — fast, no real ping required.
        /// IMPORTANT: NEVER delete the token while offline.
        /// </summary>
        private IEnumerator WaitForInternet()
        {
            if (Application.internetReachability != NetworkReachability.NotReachable)
            {
                UpdateLoadingUI("Connection ready...", 0.44f);
                yield break;
            }

            Debug.LogWarning("[LoadingManager] Offline detected — blocking until internet returns.");
            ShowRetryButton(true);

            while (Application.internetReachability == NetworkReachability.NotReachable)
            {
                UpdateLoadingUI("⚠️ No internet connection. Check your network and press Retry.", 0.42f);

                _retryRequested = false;
                float waited = 0f;
                while (!_retryRequested && waited < 2f)
                {
                    waited += Time.deltaTime;
                    yield return null;
                }

                if (_retryRequested && Application.internetReachability == NetworkReachability.NotReachable)
                {
                    var _ts = NightHunt.UI.ToastService.Instance;
                    if (_ts == null)
                    {
                        Debug.LogWarning("[LoadingManager] ToastService.Instance is null — cannot show toast 'No Internet'.");
                    }
                    else
                    {
                        Debug.Log("[LoadingManager] Requesting toast: 'No Internet' — 'No internet connection detected.'");
                        _ts.Show("No Internet", "No internet connection detected.");
                    }
                }

                _retryRequested = false;
            }

            ShowRetryButton(false);
            UpdateLoadingUI("Connection restored!", 0.44f);
            yield return new WaitForSeconds(0.3f); 
            Debug.Log("[LoadingManager] Internet restored — continuing.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Backend health check
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Pings GET {_healthPath} to confirm the server is running.
        /// Blocks flow if the server does not respond; allows manual retry or auto-retry after 5 s.
        /// Distinguishes clearly: "cannot connect" vs "server returned 5xx (maintenance)".
        /// </summary>
        private IEnumerator WaitForBackendHealth()
        {
            while (true)
            {
                UpdateLoadingUI("Checking server...", 0.50f);

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

                // Attach AcceptAllCertificatesHandler if the server uses a self-signed cert (mkcert + IP)
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
                    UpdateLoadingUI("Server ready!", 0.58f);
                    Debug.Log($"[LoadingManager] ✅ Backend health check PASSED - Status: {req.responseCode}");
                    yield break;
                }

                // ── Classify error ────────────────────────────────────────────
                bool is502        = req.responseCode == 502;
                bool is503        = req.responseCode == 503;
                bool isGatewayErr = is502 || is503;
                bool serverError  = req.responseCode >= 500;

                string errorMsg;
                if (is502)
                    errorMsg = "⚠️ Server starting up (502). Retrying...";
                else if (is503)
                    errorMsg = "⚠️ Server unavailable (503). Retrying...";
                else if (serverError)
                    errorMsg = "⚠️ Server error. Please try again later.";
                else
                    errorMsg = "⚠️ Cannot reach server.";

                UpdateLoadingUI(errorMsg, 0.50f);
                ShowRetryButton(true);

                // ── Log details ───────────────────────────────────────────────
                Debug.LogWarning($"[LoadingManager] ❌ Backend health FAILED:");
                Debug.LogWarning($"  Result:      {req.result}");
                Debug.LogWarning($"  Status Code: {req.responseCode}");
                Debug.LogWarning($"  Error:       {req.error}");
                Debug.LogWarning($"  URL:         {url}");

                if (!string.IsNullOrEmpty(req.downloadHandler?.text))
                    Debug.LogWarning($"  Response Body: {req.downloadHandler.text}");

                // ── Per-status diagnosis ──────────────────────────────────────
                if (req.result == UnityWebRequest.Result.ProtocolError)
                {
                    if (is502)
                    {
                        Debug.LogError($"[LoadingManager] 🔴 HTTP 502 Bad Gateway — proxy is up but backend app is NOT ready.");
                        Debug.LogError($"  Causes:");
                        Debug.LogError($"    1. Spring Boot / backend is still starting (JVM warmup can take 10–30 s).");
                        Debug.LogError($"    2. Backend crashed right after start — check server process logs.");
                        Debug.LogError($"    3. Backend is bound to wrong port — proxy upstream doesn't match.");
                        Debug.LogError($"    4. Docker container restarting (OOM or crash loop).");
                        Debug.LogError($"  Action: auto-retrying every 2 s until backend responds 200.");
                    }
                    else if (is503)
                    {
                        Debug.LogError($"[LoadingManager] 🔴 HTTP 503 Service Unavailable — backend overloaded or in maintenance.");
                        Debug.LogError($"  Action: auto-retrying every 5 s.");
                    }
                    else if (req.responseCode == 404)
                    {
                        Debug.LogError($"[LoadingManager] 🔴 HTTP 404 — health endpoint not found.");
                        Debug.LogError($"  Check health path: {url}");
                        Debug.LogError($"  Expected: /api/actuator/health");
                    }
                    else if (req.responseCode == 401 || req.responseCode == 403)
                    {
                        Debug.LogError($"[LoadingManager] 🔴 HTTP {req.responseCode} — health endpoint requires auth.");
                        Debug.LogError($"  Fix: allow unauthenticated access to /api/actuator/health on server.");
                    }
                    else
                    {
                        Debug.LogError($"[LoadingManager] 🔴 PROTOCOL ERROR - HTTP {req.responseCode}");
                    }
                }
                else if (req.result == UnityWebRequest.Result.ConnectionError)
                {
                    bool isSslError = req.error != null &&
                        (req.error.IndexOf("SSL",         System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         req.error.IndexOf("certificate", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         req.error.IndexOf("Cert",        System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         req.error.IndexOf("CA",          System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         req.error.IndexOf("TLS",         System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         req.error.IndexOf("UnityTls",    System.StringComparison.OrdinalIgnoreCase) >= 0);

                    if (isSslError)
                    {
                        bool isLocalHost  = url.IndexOf("localhost", System.StringComparison.OrdinalIgnoreCase) >= 0 || url.Contains("127.0.0.1");
                        bool isCnMismatch = req.error.IndexOf("mismatch",    System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            req.error.IndexOf("Common Name", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            req.error.IndexOf("Cert verify", System.StringComparison.OrdinalIgnoreCase) >= 0;
                        if (isLocalHost)
                        {
                            Debug.LogError($"[LoadingManager] SSL CERT ERROR (local) — mkcert CA not installed in Windows.");
                            Debug.LogError($"  Fix: run Tools/setup-dev-cert.ps1 or 'mkcert -install', then RESTART Unity Editor.");
                        }
                        else if (isCnMismatch)
                        {
                            Debug.LogError($"[LoadingManager] SSL CERT ERROR (CN mismatch) — cert CN does not match host '{_backendConfig?.apiHost}'.");
                            Debug.LogError($"  Fix: check 'prodApiHost' in BackendConfig matches the CN on the server cert.");
                        }
                        else
                        {
                            Debug.LogError($"[LoadingManager] SSL CERT ERROR (untrusted CA) — check server has a valid Let's Encrypt cert.");
                        }
                    }
                    else
                    {
                        Debug.LogError($"[LoadingManager] CONNECTION ERROR — cannot reach {_backendConfig?.apiHost ?? "localhost:8443"}.");
                        Debug.LogError($"  1. Proxy/server not running.  2. Firewall blocking port.  3. SSL cert missing.");
                    }
                }

                // ── Smart retry delay: 502/503 get shorter auto-retry (server booting) ──
                // If 502 → retry every 2 s automatically (don't wait 5 s or user press).
                // Other failures → 5 s wait or user presses Retry button.
                _retryRequested = false;
                float retryDelay = isGatewayErr ? 2f : 5f;
                float waited     = 0f;
                while (!_retryRequested && waited < retryDelay)
                {
                    waited += Time.deltaTime;
                    yield return null;
                }
                _retryRequested = false;

                UpdateLoadingUI("Retrying...", 0.50f);
                yield return new WaitForSeconds(0.2f);
            }
        }

        private void OnRetryClicked()
        {
            _retryRequested = true;
            {
                var _ts = NightHunt.UI.ToastService.Instance;
                if (_ts == null)
                {
                    Debug.LogWarning("[LoadingManager] ToastService.Instance is null — cannot show toast 'Retrying'.");
                }
                else
                {
                    Debug.Log("[LoadingManager] Requesting toast: 'Retrying' — 'Checking connection...'");
                    _ts.Show("Retrying", "Checking connection...");
                }
            }
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
                Debug.LogError("[FLOW][LoadingManager] Navigate — UINavigator.Instance is null!");
                Hide();
                return;
            }

            Debug.Log($"[FLOW][LoadingManager] Starting Navigation to {_targetPanel} at {System.DateTime.UtcNow:HH:mm:ss.fff}");
            // Hide loading panel BEFORE showing target panel to ensure it doesn't block raycasts
            Hide();
            HasCompletedInitialFlow = true;
            UINavigator.Instance.ShowPanel(_targetPanel, forceInstant: false);
            Debug.Log($"[FLOW][LoadingManager] Navigation call completed at {System.DateTime.UtcNow:HH:mm:ss.fff}");
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
                UpdateLoadingUI("Starting up...", Mathf.Lerp(0.05f, 0.22f, elapsed / timeout));
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
                UpdateLoadingUI("Initializing UI...", Mathf.Lerp(0.25f, 0.42f, elapsed / timeout));
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

        public sealed class LoadingHandle
        {
            private readonly LoadingManager _manager;
            private readonly int _id;
            private readonly string _owner;
            private bool _closed;

            internal LoadingHandle(LoadingManager manager, int id, string owner)
            {
                _manager = manager;
                _id = id;
                _owner = owner;
            }

            public string Owner => _owner;
            public bool IsActive => !_closed && _manager != null && _manager.IsActiveHandle(_id, _owner);

            public void SetProgress(float progress)
            {
                if (!_closed)
                    _manager?.SetProgressForHandle(_id, _owner, progress);
            }

            public void SetMessage(string message)
            {
                if (!_closed)
                    _manager?.SetMessageForHandle(_id, _owner, message);
            }

            public void Complete(string message = null)
            {
                if (_closed) return;
                _closed = true;
                _manager?.CloseHandle(_id, _owner, message, hide: true);
            }

            public void Fail(string message, bool hide = true)
            {
                if (_closed) return;
                _closed = true;
                _manager?.CloseHandle(_id, _owner, message, hide);
            }
        }

        public LoadingHandle Begin(string owner, string message, float initialProgress = 0f)
        {
            owner = string.IsNullOrWhiteSpace(owner) ? "Unknown" : owner;
            _activeOwner = owner;
            _activeHandleId++;

            if (loadingPanel != null)
            {
                loadingPanel.SetActive(true);
                _isShowing = true;
            }

            UpdateLoadingUI(message, Mathf.Clamp01(initialProgress));
            return new LoadingHandle(this, _activeHandleId, owner);
        }

        /// <summary>Shows the loading overlay with an optional message.</summary>
        public void Show(string message = null)
        {
            _activeOwner = "Legacy";
            _activeHandleId++;
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(true);
                _isShowing = true;
            }
            if (!string.IsNullOrEmpty(message))
                UpdateLoadingUI(message, GetCurrentProgress());
        }

        /// <summary>Hides the loading overlay.</summary>
        public void Hide()
        {
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(false);
                _isShowing = false;
                _activeOwner = null;
                Debug.Log("[LoadingManager] ✅ loadingPanel SetActive(false) — loading UI hidden.");
            }
            else
            {
                Debug.LogError("[LoadingManager] ❌ Hide() called but loadingPanel is null! Loading UI will not hide.");
            }
        }

        public bool IsShowing() => _isShowing;

        private bool IsActiveHandle(int id, string owner)
        {
            return _isShowing && _activeHandleId == id && _activeOwner == owner;
        }

        private void SetProgressForHandle(int id, string owner, float progress)
        {
            if (!IsActiveHandle(id, owner)) return;
            UpdateLoadingUI(loadingText != null ? loadingText.text : string.Empty, Mathf.Clamp01(progress));
        }

        private void SetMessageForHandle(int id, string owner, string message)
        {
            if (!IsActiveHandle(id, owner)) return;
            UpdateLoadingUI(message, GetCurrentProgress());
        }

        private void CloseHandle(int id, string owner, string message, bool hide)
        {
            if (!IsActiveHandle(id, owner)) return;
            if (!string.IsNullOrEmpty(message))
                UpdateLoadingUI(message, hide ? 1f : GetCurrentProgress());
            if (hide)
                Hide();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Internal helpers
        // ─────────────────────────────────────────────────────────────────────

        private void UpdateLoadingUI(string message, float progress)
        {
            // Cache references only once if possible, or use non-fallback resolution
            if (loadingText == null || progressBar == null)
                ResolveLoadingReferences(createFallback: true);
            
            if (_progressView == null)
                ResolveProgressView();

            if (loadingText  != null) loadingText.text  = message;
            if (progressBar  != null) progressBar.value = progress;
            _progressView?.SetMessage(message);
            _progressView?.SetProgress(progress);
            
            // Avoid logging every single frame if progress hasn't changed much
            if (Mathf.Abs(_lastLoggedProgress - progress) > 0.01f || message != _lastLoggedMessage)
            {
                Debug.Log($"[Loading] {progress:P0}  {message}");
                _lastLoggedProgress = progress;
                _lastLoggedMessage = message;
            }
        }

        private float  _lastLoggedProgress = -1f;
        private string _lastLoggedMessage = null;

        private float GetCurrentProgress()
        {
            ResolveProgressView();
            if (_progressView != null)
                return _progressView.CurrentProgress;
            return progressBar != null ? progressBar.value : 0f;
        }

        private void ResolveProgressView()
        {
            if (progressViewComponent is ILoadingProgressView assigned)
            {
                _progressView = assigned;
                return;
            }

            if (_progressView != null)
                return;

            if (loadingPanel != null)
            {
                _progressView = loadingPanel.GetComponentInChildren<ILoadingProgressView>(true);
                if (_progressView is MonoBehaviour mb)
                    progressViewComponent = mb;
            }
        }

        private void ResolveBootIntroView(bool createFallback)
        {
            var persistentCanvas = ResolvePersistentUICanvas(createFallback);
            Transform persistentRoot = persistentCanvas != null ? persistentCanvas.transform.root : null;

            if (bootIntroView != null && persistentRoot != null &&
                bootIntroView.transform.root != persistentRoot)
            {
                Debug.LogWarning(
                    $"[LoadingManager] Ignoring scene-scoped BootIntroView '{bootIntroView.name}'. " +
                    "Boot intro must live under PersistentUICanvas/CanvasDontDestroy.");
                bootIntroView = null;
            }

            if (bootIntroView == null && persistentCanvas != null)
                bootIntroView = createFallback
                    ? persistentCanvas.EnsureBootIntroView()
                    : persistentCanvas.BootIntroView;

            if (bootIntroView == null)
                bootIntroView = GetComponentInChildren<BootIntroView>(true);

            if (bootIntroView == null && createFallback && persistentCanvas != null)
                bootIntroView = persistentCanvas.EnsureBootIntroView();

            if (bootIntroView != null)
                bootIntroView.EnsureRuntimeWiring();
        }

        private static PersistentUICanvas ResolvePersistentUICanvas(bool createFallback)
        {
            var persistentCanvas = PersistentUICanvas.Instance
                ?? FindFirstObjectByType<PersistentUICanvas>(FindObjectsInactive.Include);

            if (persistentCanvas == null && createFallback)
                persistentCanvas = PersistentUICanvas.GetOrCreate();

            return persistentCanvas;
        }

        private void ResolveLoadingReferences(bool createFallback)
        {
            if (loadingPanel == null)
                return;

            if (progressBar == null)
                progressBar = loadingPanel.GetComponentInChildren<UnityEngine.UI.Slider>(true);

            if (loadingText == null)
                loadingText = loadingPanel.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);

            if (retryButton == null)
            {
                foreach (var button in loadingPanel.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                {
                    if (button.name.IndexOf("retry", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        retryButton = button;
                        break;
                    }
                }
            }

            if (!createFallback)
                return;

            if (progressBar == null)
                progressBar = CreateRuntimeProgressBar(loadingPanel.transform);

            if (loadingText == null)
                loadingText = CreateRuntimeLoadingText(loadingPanel.transform);

            if (retryButton == null)
                retryButton = CreateRuntimeRetryButton(loadingPanel.transform);
        }

        private UnityEngine.UI.Slider CreateRuntimeProgressBar(Transform parent)
        {
            var root = CreateRuntimeUIObject("Runtime Progress Bar", parent);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(480f, 12f);
            rect.anchoredPosition = new Vector2(0f, 96f);

            var background = root.AddComponent<UnityEngine.UI.Image>();
            background.color = new Color(0.08f, 0.1f, 0.12f, 0.85f);

            var fillArea = CreateRuntimeUIObject("Fill Area", root.transform);
            var fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = new Vector2(2f, 2f);
            fillAreaRect.offsetMax = new Vector2(-2f, -2f);

            var fill = CreateRuntimeUIObject("Fill", fillArea.transform);
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fill.AddComponent<UnityEngine.UI.Image>();
            fillImage.color = new Color(0.25f, 0.75f, 1f, 1f);

            var slider = root.AddComponent<UnityEngine.UI.Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.transition = UnityEngine.UI.Selectable.Transition.None;
            slider.targetGraphic = background;
            slider.fillRect = fillRect;

            var view = root.AddComponent<LoadingProgressView>();
            progressViewComponent = view;
            return slider;
        }

        private TMPro.TextMeshProUGUI CreateRuntimeLoadingText(Transform parent)
        {
            var go = CreateRuntimeUIObject("Runtime Loading Text", parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(720f, 44f);
            rect.anchoredPosition = new Vector2(0f, 132f);

            var text = go.AddComponent<TMPro.TextMeshProUGUI>();
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.fontSize = 18f;
            text.color = Color.white;
            text.text = "Starting up...";
            return text;
        }

        private UnityEngine.UI.Button CreateRuntimeRetryButton(Transform parent)
        {
            var go = CreateRuntimeUIObject("Runtime Retry Button", parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(180f, 44f);
            rect.anchoredPosition = new Vector2(0f, 48f);

            var image = go.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.15f, 0.2f, 0.24f, 0.95f);
            var button = go.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;

            var labelGo = CreateRuntimeUIObject("Label", go.transform);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var label = labelGo.AddComponent<TMPro.TextMeshProUGUI>();
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.fontSize = 16f;
            label.color = Color.white;
            label.text = "Retry";

            go.SetActive(false);
            return button;
        }

        private static GameObject CreateRuntimeUIObject(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.layer = 5;
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }
    }
}
