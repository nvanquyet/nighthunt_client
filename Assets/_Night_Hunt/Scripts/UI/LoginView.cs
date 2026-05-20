using System.Collections;
using System.Threading.Tasks;
using Michsky.UI.Shift;
using NightHunt.Common;
using NightHunt.Core;
using NightHunt.Data;
using NightHunt.Data.DTOs;
using NightHunt.Services.Auth;
using NightHunt.State;
using NightHunt.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// LoginView - Xử lý form log in và register.
    /// Single-scene: dùng UINavigator instead of SceneLoader.
    /// Supports "Ghi nhớ log in" → lưu refreshToken vào PlayerPrefs.
    ///
    /// Implements INavigableView so UINavigator owns show/hide sequencing.
    /// OnEnable still prepares Shift UI visual state when the package activates the panel.
    /// </summary>
    public class LoginView : MonoBehaviour, INavigableView
    {
        // ─── Login Form ─────────────────────────────────────────────
        [Header("Login Form")]
        [SerializeField] private TMP_InputField usernameInput;
        [SerializeField] private TMP_InputField passwordInput;
        [Tooltip("ShiftUI SwitchManager trên switch Ghi nhớ log in.")]
        [SerializeField] private SwitchManager  rememberMeSwitch;
        [SerializeField] private Button         loginButton;

        // ─── Register Form ─────────────────────────────────────────
        [Header("Register Form")]
        [SerializeField] private TMP_InputField regUsernameInput;
        [SerializeField] private TMP_InputField emailInput;
        [SerializeField] private TMP_InputField regPasswordInput;
        [SerializeField] private TMP_InputField confirmPasswordInput;
        [Tooltip("ShiftUI SwitchManager trên switch Agree điều khoản. saveValue=false, isOn=false, invokeAtStart=false.")]
        [SerializeField] private SwitchManager  agreeToTermsSwitch;
        [SerializeField] private Button         registerButton;

        // ─── Login Events ────────────────────────────────────────────────────
        [Header("Login Events")]
        [Tooltip("Legacy visual-only hook. Disabled by default so scene callbacks cannot start loading/navigation.")]
        public UnityEvent onLoginSuccess;
        [Tooltip("Fired khi login failed (after Toast đã show).")]
        public UnityEvent onLoginFailed;
        [SerializeField] private bool invokeLegacyLoginSuccessEvents = false;

        // ─── Register Events ─────────────────────────────────────────────────
        [Header("Register Events")]
        [Tooltip("Fired khi register success.")]
        public UnityEvent onRegisterSuccess;
        [Tooltip("Fired khi register failed (after Toast đã show).")]
        public UnityEvent onRegisterFailed;

        // ─── Loading Indicators ──────────────────────────────────────────────
        [Header("Loading Indicators")]
        [Tooltip("Spinner/indicator cục bộ hiện trong khi calling Login API.")]
        [SerializeField] private GameObject loginLoadingIndicator;
        [Tooltip("Spinner/indicator cục bộ hiện trong khi calling Register API.")]
        [SerializeField] private GameObject registerLoadingIndicator;

        // ─── Optional refs ───────────────────────────────────────────────────
        [Header("Service References (Optional — auto-find if null)")]
        [SerializeField] private AuthService authService;
        [Header("Panel References")]
        [Tooltip("HomeView on the same scene. Required for the post-login preload flow.")]
        [SerializeField] private HomeView homeView;
        [Tooltip("Animator that owns the login/register visual states.")]
        [SerializeField] private Animator authFlowAnimator;
        [SerializeField] private string registerSuccessAnimatorState = "Sign Up to Login";
        [SerializeField] private float postLoginPreloadTimeout = 15f;
        // ─── Internal ────────────────────────────────────────────────────────
        private AuthService AuthService
        {
            get
            {
                if (authService != null) return authService;
                if (GameManager.Instance != null) authService = GameManager.Instance.AuthService;
                if (authService == null) authService = FindFirstObjectByType<AuthService>();
                if (authService == null)
                    Debug.LogError("[LoginView] AuthService not found!");
                return authService;
            }
        }

        // _loadingManager chỉ dùng để hide boot screen nếu nó còn show khi navigate về Login
        private LoadingManager _loadingManager;

        // ─────────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (loginButton    != null) loginButton.onClick.AddListener(OnLoginClicked);
            if (registerButton != null) registerButton.onClick.AddListener(OnRegisterClicked);
        }

        private void Start()
        {
            _loadingManager = PersistentUICanvas.Instance != null
                ? PersistentUICanvas.Instance.LoadingManager
                : LoadingManager.Instance;

            RestoreRememberMeToggle();
        }

        private void OnEnable()
        {
            PrepareForShow();
            StartCoroutine(RestoreAfterFrame());
        }

        public bool CanLeave(NavigationContext context) => true;

        public Task OnShowAsync(NavigationContext context)
        {
            PrepareForShow();
            RestoreRememberMeToggle();
            return Task.CompletedTask;
        }

        public Task OnHideAsync(NavigationContext context)
        {
            SetLoginLoading(false);
            SetRegisterLoading(false);
            SetButtonsInteractable(true);
            return Task.CompletedTask;
        }

        private void PrepareForShow()
        {
            if (_loadingManager == null)
            {
                _loadingManager = PersistentUICanvas.Instance != null
                    ? PersistentUICanvas.Instance.LoadingManager
                    : LoadingManager.Instance;
            }

            if (_loadingManager != null && _loadingManager.IsShowing())
                _loadingManager.Hide();
        }

        private IEnumerator RestoreAfterFrame()
        {
            yield return null;
            RestoreRememberMeToggle();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Remember Me
        // ─────────────────────────────────────────────────────────────────────

        private void RestoreRememberMeToggle()
        {
            if (rememberMeSwitch == null) return;
            bool remembered = PlayerPrefs.GetInt(LoadingManager.KEY_REMEMBER_ME, 0) == 1;
            ShiftUIBridge.SetSwitchSilently(rememberMeSwitch, remembered);
        }

        /// <summary>
        /// Call after login success.
        /// Save refreshToken nếu "Ghi nhớ log in" đang bật.
        /// </summary>
        private void HandleRememberMe(string refreshToken)
        {
            bool remember = rememberMeSwitch != null && rememberMeSwitch.isOn;

            PlayerPrefs.SetInt(LoadingManager.KEY_REMEMBER_ME, remember ? 1 : 0);

            if (remember && !string.IsNullOrEmpty(refreshToken))
            {
                SecureStorage.SetString(LoadingManager.KEY_REFRESH_TOKEN, refreshToken);
                Debug.Log("[LoginView] RefreshToken saved (Remember Me ON)");
            }
            else
            {
                SecureStorage.DeleteKey(LoadingManager.KEY_REFRESH_TOKEN);
                Debug.Log("[LoginView] RefreshToken cleared (Remember Me OFF)");
            }

            PlayerPrefs.Save();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Login
        // ─────────────────────────────────────────────────────────────────────

        private async void OnLoginClicked()
        {
            if (AuthService == null) return;

            string identifier = usernameInput  != null ? usernameInput.text.Trim()  : "";
            string password   = passwordInput  != null ? passwordInput.text          : "";

            if (string.IsNullOrEmpty(identifier) || string.IsNullOrEmpty(password))
            {
                Debug.Log("[LoginView] Login validation failed: empty credentials");
                ShowToast("Login Error", "Please enter your username and password.");
                return;
            }

            SetLoginLoading(true);
            SetButtonsInteractable(false);
            Debug.Log(
                $"[FLOW][AUTH] Login request start identifier='{identifier}' remember={(rememberMeSwitch != null && rememberMeSwitch.isOn)} " +
                $"authService={(AuthService == null ? "null" : AuthService.GetType().Name)} homeView={(homeView == null ? "null" : homeView.name)}");

            ApiResult<AuthResponse> result;
            try
            {
                result = await AuthService.Login(identifier, password);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LoginView] Login request failed: {ex}");
                SetLoginLoading(false);
                SetButtonsInteractable(true);
                ShowToast("Login Failed", "Could not connect to the server. Please try again.");
                onLoginFailed?.Invoke();
                return;
            }

            if (result == null)
            {
                Debug.LogError("[FLOW][AUTH] Login returned null ApiResult.");
                SetLoginLoading(false);
                SetButtonsInteractable(true);
                ShowToast("Login Failed", "Could not connect to the server. Please try again.");
                onLoginFailed?.Invoke();
                return;
            }

            SetLoginLoading(false);
            Debug.Log(
                $"[FLOW][AUTH] Login result success={result.Success} errorCode={result.ErrorCode ?? "null"} " +
                $"message='{result.Message ?? "null"}' hasData={result.Data != null}");

            if (result.Success)
            {
                HandleRememberMe(result.data.refreshToken);
                Debug.Log("[FLOW][AUTH] Login success; clearing stale room state and starting home preload.");
                // Always reset stale room/network state on fresh login (unconditional)
                RoomState.Instance?.ClearRoom();
                if (invokeLegacyLoginSuccessEvents)
                    onLoginSuccess?.Invoke();

                LoadingManager.LoadingHandle loadingHandle = null;
                try
                {
                    // Show loading overlay while pre-fetching home data before navigating.
                    var loading = PersistentUICanvas.Instance?.LoadingManager ?? LoadingManager.Instance;
                    loadingHandle = loading?.Begin("LoginFlow", "Loading config...", 0.15f);

                    // Config must complete first — MatchFlowCoordinator.HandleMatchReady() uses
                    // MapConfig to resolve the correct game scene. FetchGameConfigFlow is skipped
                    // during fresh login (no refresh token yet), so this is the guaranteed load point.
                    // IMPORTANT: do NOT start PreloadDataAsync() before awaiting this; calling it
                    // first would burst 6+ requests simultaneously and hit the rate limit.
                    bool configLoaded = await (GameManager.Instance?.GameConfigService?.FetchAsync()
                           ?? System.Threading.Tasks.Task.FromResult(false));
                    Debug.Log($"[FLOW][AUTH] Game config preload result={configLoaded}");
                    if (!configLoaded)
                        Debug.LogWarning("[LoginView] Game config fetch failed; continuing with local defaults.");
                    loadingHandle?.SetProgress(0.55f);
                    loadingHandle?.SetMessage("Loading profile...");

                    // Config is now populated. Pre-fetch home panel data (profile, friends, party)
                    // while the overlay is still visible. Sets HomeView._homeDataPreloaded so
                    // OnShow() skips the redundant refetch on the first show after login.
                    if (homeView == null)
                        throw new System.InvalidOperationException("[LoginView] HomeView is not assigned; cannot preload Home data.");

                    await AwaitWithTimeout(
                        homeView.PreloadDataAsync(),
                        postLoginPreloadTimeout,
                        "home data preload");
                    Debug.Log("[FLOW][AUTH] Home preload completed.");

                    // Brief pause for a smooth UX transition before the panel swap.
                    await System.Threading.Tasks.Task.Delay(300);

                    loadingHandle?.SetProgress(0.95f);
                    if (UINavigator.Instance != null)
                        await UINavigator.Instance.ShowPanelAsync(PanelType.Home, reason: "LoginSuccess");

                    loadingHandle?.Complete("Ready");
                    Debug.Log("[FLOW][AUTH] Login flow completed; navigated to Home.");
                    SetButtonsInteractable(true);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[FLOW][AUTH] Post-login preload failed: {ex}");

                    // Critical error in preload -> Clear token to avoid AUTH_SESSION_CONFLICT on next try
                    // as the previous login was successful but the state is now broken.
                    SecureStorage.DeleteKey(LoadingManager.KEY_REFRESH_TOKEN);
                    if (GameManager.Instance != null && GameManager.Instance.SessionState != null)
                        GameManager.Instance.SessionState.ClearSession();

                    loadingHandle?.Fail("Login failed. Home data could not be loaded.", hide: true);

                    // Brief delay to allow Loading panel to hide before showing toast
                    Debug.Log("[FLOW][AUTH] Scheduling delayed login-failed toast after loading overlay hide.");
                    _ = ShowErrorToastDelayed("Login Failed", "Could not load home data. Please try again.");

                    SetButtonsInteractable(true);
                    onLoginFailed?.Invoke();
                }
                }
                else if (result.ErrorCode == ErrorCodes.AUTH_SESSION_CONFLICT)
                {
                // Clear token as it's definitely stale/conflicting
                SecureStorage.DeleteKey(LoadingManager.KEY_REFRESH_TOKEN);

                // Old session was terminated — user just needs to try again once.
                Debug.Log("[FLOW][AUTH] AUTH_SESSION_CONFLICT: previous session terminated, prompting retry");
                ShowToast("Authentication Conflict", result.Message ?? "Previous session was terminated. Please login again.");
                SetButtonsInteractable(true);
                }
                else
                {
                Debug.LogWarning($"[FLOW][AUTH] Login failed errorCode={result.ErrorCode ?? "null"} message='{result.Message ?? "null"}'");
                ShowToast("Login Failed", result.Message ?? "Please check credentials and try again.");
                SetButtonsInteractable(true);
                onLoginFailed?.Invoke();
                }
                }

                private async Task ShowErrorToastDelayed(string title, string message)
                {
                Debug.Log($"[FLOW][AUTH] Delayed toast wait start title='{title}' message='{message}'");
                await Task.Delay(500);
                Debug.Log($"[FLOW][AUTH] Delayed toast show title='{title}'");
                ShowToast(title, message);
                }

        // ─────────────────────────────────────────────────────────────────────
        // Register
        // ─────────────────────────────────────────────────────────────────────

        private async void OnRegisterClicked()
        {
            if (AuthService == null) return;

            string username         = regUsernameInput     != null ? regUsernameInput.text.Trim()     : "";
            string email            = emailInput           != null ? emailInput.text.Trim()            : "";
            string password         = regPasswordInput     != null ? regPasswordInput.text             : "";
            string confirmPassword  = confirmPasswordInput != null ? confirmPasswordInput.text         : "";

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email) ||
                string.IsNullOrEmpty(password) || string.IsNullOrEmpty(confirmPassword))
            {
                Debug.Log("[LoginView] Register validation failed: incomplete fields");
                ShowToast("Registration Error", "Please fill in all fields.");
                return;
            }

            if (password != confirmPassword)
            {
                Debug.Log("[LoginView] Register validation failed: password mismatch");
                ShowToast("Registration Error", "Passwords do not match.");
                return;
            }

            if (agreeToTermsSwitch == null || !agreeToTermsSwitch.isOn)
            {
                Debug.Log("[LoginView] Register validation failed: terms not accepted");
                ShowToast("Registration Error", "Please agree to the terms of service.");
                return;
            }

            SetRegisterLoading(true);
            SetButtonsInteractable(false);

            ApiResult<AuthResponse> result;
            try
            {
                result = await AuthService.Register(username, email, password, confirmPassword);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LoginView] Register request failed: {ex}");
                SetRegisterLoading(false);
                SetButtonsInteractable(true);
                ShowToast("Registration Failed", "Could not connect to the server. Please try again.");
                onRegisterFailed?.Invoke();
                return;
            }

            SetRegisterLoading(false);
            SetButtonsInteractable(true);

            if (result.Success)
            {
                PlayRegisterSuccessVisual();
                onRegisterSuccess?.Invoke();
                Debug.Log($"[LoginView] Register success — username='{username}'");
                ShowToast("Success", "Registration complete! Please log in.", ClearRegisterFields);
            }
            else
            {
                Debug.LogWarning($"[LoginView] Register failed: {result.Message}");
                ShowToast("Registration Failed", result.Message ?? "Please try again.");
                onRegisterFailed?.Invoke();
            }
        }

        private void ClearRegisterFields()
        {
            if (regUsernameInput     != null) regUsernameInput.text     = "";
            if (emailInput           != null) emailInput.text           = "";
            if (regPasswordInput     != null) regPasswordInput.text     = "";
            if (confirmPasswordInput != null) confirmPasswordInput.text = "";
            // Reset switch dieu khoan ve Off without firing Shift events.
            ShiftUIBridge.SetSwitchSilently(agreeToTermsSwitch, false);
        }

        private void PlayRegisterSuccessVisual()
        {
            if (authFlowAnimator == null || string.IsNullOrWhiteSpace(registerSuccessAnimatorState))
                return;

            ShiftUIBridge.PlayAnimatorState(authFlowAnimator, registerSuccessAnimatorState);
        }

        private static async Task AwaitWithTimeout(Task task, float timeoutSeconds, string label)
        {
            if (task == null)
                return;

            float clampedTimeout = Mathf.Max(1f, timeoutSeconds);
            Task timeoutTask = Task.Delay(System.TimeSpan.FromSeconds(clampedTimeout));
            Task completed = await Task.WhenAny(task, timeoutTask);
            if (completed != task)
                throw new System.TimeoutException($"{label} timed out after {clampedTimeout:F1}s.");

            await task;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Logout helper (called from nơi khác nếu cần)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Đăng xuất hoàn toàn: xóa token, clear state, về LoginPanel.
        /// Call từ bất kỳ đâu when needed logout.
        /// </summary>
        public static void Logout()
        {
            // Xoá refresh token & remember-me flag
            SecureStorage.DeleteKey(LoadingManager.KEY_REFRESH_TOKEN);
            PlayerPrefs.DeleteKey(LoadingManager.KEY_REMEMBER_ME);
            PlayerPrefs.Save();

            // Clear session state
            SessionState.Instance?.ClearSession();

            // Clear room state — use RoomState.Instance directly (SerializeField in GameManager may be unassigned)
            RoomState.Instance?.ClearRoom();
            RoomState.Instance?.ClearNetworkSession();

            // Navigate về Login panel through the code-first navigator when Home scene is alive.
            // During gameplay there is no scene-scoped UINavigator, so return to 01_Home;
            // LoadingManager will route to Login because the session has already been cleared.
            if (SceneLoader.HasPendingSceneLoad)
                SceneLoader.ReturnHomeFromGameplayFlow();
            else if (UINavigator.Instance != null)
                UINavigator.Instance.GoForce(PanelType.Login);
            else
                SceneLoader.ReturnHomeFromGameplayFlow();

            Debug.Log("[LoginView] Logged out — all tokens & state cleared.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // UI helpers
        // ─────────────────────────────────────────────────────────────────────

        private void SetLoginLoading(bool active)
        {
            if (loginLoadingIndicator != null)
                loginLoadingIndicator.SetActive(active);
        }

        private void SetRegisterLoading(bool active)
        {
            if (registerLoadingIndicator != null)
                registerLoadingIndicator.SetActive(active);
        }

        private void ShowToast(string title, string message, System.Action onConfirm = null)
        {
            var persistent = PersistentUICanvas.Instance;
            var toast = persistent?.ToastService ?? ToastService.Instance;

            Debug.Log(
                $"[FLOW][AUTH] ShowToast title='{title}' message='{message}' " +
                $"persistent={(persistent == null ? "null" : persistent.name)} service={(toast == null ? "null" : toast.name)}");

            if (toast == null)
            {
                Debug.LogError("[FLOW][AUTH] ShowToast aborted: ToastService not found.");
                return;
            }

            toast.Show(title: title, message: message, onConfirm: onConfirm);
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (loginButton    != null) loginButton.interactable    = interactable;
            if (registerButton != null) registerButton.interactable = interactable;
        }
    }
}
