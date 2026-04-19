using System.Collections;
using System.Threading.Tasks;
using Michsky.UI.Shift;
using NightHunt.Core;
using NightHunt.Data;
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
    /// KHÔNG implement INavigableView — dùng OnEnable() tự nhiên của Unity.
    /// Shift UI dùng SetActive(true) để show panel → OnEnable tự fire.
    /// </summary>
    public class LoginView : MonoBehaviour
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
        [Tooltip("Fired khi login success. Wire animation, sound, v.v. ở đây.")]
        public UnityEvent onLoginSuccess;
        [Tooltip("Fired khi login failed (after Toast đã show).")]
        public UnityEvent onLoginFailed;

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
            // Khi Shift UI / UINavigator.OnGoLogin fire SetActive(true) → OnEnable chạy.
            // Hide stale loading screen nếu còn đang show.
            if (_loadingManager != null && _loadingManager.IsShowing())
                _loadingManager.Hide();

            // Restore RememberMe toggle sau 1 frame (chờ SwitchManager.OnEnable xong).
            StartCoroutine(RestoreAfterFrame());
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
            // AnimateSwitch() toggle trạng thái — chỉ gọi nếu khác với value cần restore
            if (rememberMeSwitch.isOn != remembered)
                rememberMeSwitch.AnimateSwitch();
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

            // Suppress HomeView's OnUserLoggedIn fallback — LoginView owns the entire post-login
            // flow (loading overlay → config fetch → home data preload → GoHome).
            // SessionState.OnUserLoggedIn fires synchronously inside AuthService.Login(), so
            // suppression must be set BEFORE the await to prevent a premature OnShow() burst.
            homeView?.SuppressNextLoginFallback();

            var result = await AuthService.Login(identifier, password);

            SetLoginLoading(false);
            SetButtonsInteractable(true);

            if (result.Success)
            {
                HandleRememberMe(result.data.refreshToken);
                // Always reset stale room/network state on fresh login (unconditional)
                RoomState.Instance?.ClearRoom();
                onLoginSuccess?.Invoke();   // Inspector: wire animation/sound here

                // Show loading overlay while pre-fetching home data before navigating.
                var loading = PersistentUICanvas.Instance?.LoadingManager ?? LoadingManager.Instance;
                loading?.Show("Loading...");

                // Config must complete first — MatchFlowCoordinator.HandleMatchReady() uses
                // MapConfig to resolve the correct game scene. FetchGameConfigFlow is skipped
                // during fresh login (no refresh token yet), so this is the guaranteed load point.
                // IMPORTANT: do NOT start PreloadDataAsync() before awaiting this; calling it
                // first would burst 6+ requests simultaneously and hit the rate limit.
                await (GameManager.Instance?.GameConfigService?.FetchAsync()
                       ?? System.Threading.Tasks.Task.FromResult(false));

                // Config is now populated. Pre-fetch home panel data (profile, friends, party)
                // while the overlay is still visible. Sets HomeView._homeDataPreloaded so
                // OnShow() skips the redundant refetch on the first show after login.
                if (homeView != null)
                    await homeView.PreloadDataAsync();

                // Brief pause for a smooth UX transition before the panel swap.
                await System.Threading.Tasks.Task.Delay(300);

                loading?.Hide();
                UINavigator.Instance?.GoHome();
            }
            else if (result.ErrorCode == ErrorCodes.AUTH_SESSION_CONFLICT)
            {
                // Release suppression — login flow did not complete.
                homeView?.ClearSuppressLoginFallback();
                // Old session was terminated — user just needs to try again once.
                Debug.Log("[LoginView] AUTH_SESSION_CONFLICT: previous session terminated, prompting retry");
                ShowToast("Đăng xuất success", result.Message ?? "Phiên trước đã bị log out. Vui lòng log in lại.");
                // Do NOT fire onLoginFailed — user can retry immediately without changing anything.
            }
            else
            {
                // Release suppression — login flow did not complete.
                homeView?.ClearSuppressLoginFallback();
                Debug.LogWarning($"[LoginView] Login failed: {result.Message}");
                ShowToast("Login Failed", result.Message ?? "Please try again.");
                onLoginFailed?.Invoke();
            }
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

            var result = await AuthService.Register(username, email, password, confirmPassword);

            SetRegisterLoading(false);
            SetButtonsInteractable(true);

            if (result.Success)
            {
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
            // Reset switch điều khoản về Off after register success
            if (agreeToTermsSwitch != null && agreeToTermsSwitch.isOn)
                agreeToTermsSwitch.AnimateSwitch();
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

            // Navigate về Login panel — fire UINavigator.OnGoLogin event
            UINavigator.Instance?.GoLogin();
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
            var toast = PersistentUICanvas.Instance != null
                ? PersistentUICanvas.Instance.ToastService
                : ToastService.Instance;

            toast?.Show(title: title, message: message, onConfirm: onConfirm);
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (loginButton    != null) loginButton.interactable    = interactable;
            if (registerButton != null) registerButton.interactable = interactable;
        }
    }
}