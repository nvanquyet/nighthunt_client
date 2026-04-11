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
    /// LoginView - Xử lý form đăng nhập và đăng ký.
    /// Single-scene: dùng UINavigator thay vì SceneLoader.
    /// Hỗ trợ "Ghi nhớ đăng nhập" → lưu refreshToken vào PlayerPrefs.
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
        [Tooltip("ShiftUI SwitchManager trên switch Ghi nhớ đăng nhập.")]
        [SerializeField] private SwitchManager  rememberMeSwitch;
        [SerializeField] private Button         loginButton;

        // ─── Register Form ─────────────────────────────────────────
        [Header("Register Form")]
        [SerializeField] private TMP_InputField regUsernameInput;
        [SerializeField] private TMP_InputField emailInput;
        [SerializeField] private TMP_InputField regPasswordInput;
        [SerializeField] private TMP_InputField confirmPasswordInput;
        [Tooltip("ShiftUI SwitchManager trên switch Đồng ý điều khoản. saveValue=false, isOn=false, invokeAtStart=false.")]
        [SerializeField] private SwitchManager  agreeToTermsSwitch;
        [SerializeField] private Button         registerButton;

        // ─── Login Events ────────────────────────────────────────────────────
        [Header("Login Events")]
        [Tooltip("Fired khi login thành công. Wire animation, sound, v.v. ở đây.")]
        public UnityEvent onLoginSuccess;
        [Tooltip("Fired khi login thất bại (sau khi Toast đã show).")]
        public UnityEvent onLoginFailed;

        // ─── Register Events ─────────────────────────────────────────────────
        [Header("Register Events")]
        [Tooltip("Fired khi register thành công.")]
        public UnityEvent onRegisterSuccess;
        [Tooltip("Fired khi register thất bại (sau khi Toast đã show).")]
        public UnityEvent onRegisterFailed;

        // ─── Loading Indicators ──────────────────────────────────────────────
        [Header("Loading Indicators")]
        [Tooltip("Spinner/indicator cục bộ hiện trong khi đang gọi Login API.")]
        [SerializeField] private GameObject loginLoadingIndicator;
        [Tooltip("Spinner/indicator cục bộ hiện trong khi đang gọi Register API.")]
        [SerializeField] private GameObject registerLoadingIndicator;

        // ─── Optional refs ───────────────────────────────────────────────────
        [Header("Service References (Optional — auto-find if null)")]
        [SerializeField] private AuthService authService;

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
            // Ẩn stale loading screen nếu còn đang show.
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
            // AnimateSwitch() toggle trạng thái — chỉ gọi nếu khác với giá trị cần restore
            if (rememberMeSwitch.isOn != remembered)
                rememberMeSwitch.AnimateSwitch();
        }

        /// <summary>
        /// Gọi sau khi login thành công.
        /// Lưu refreshToken nếu "Ghi nhớ đăng nhập" đang bật.
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

            var result = await AuthService.Login(identifier, password);

            SetLoginLoading(false);
            SetButtonsInteractable(true);

            if (result.Success)
            {
                HandleRememberMe(result.data.refreshToken);
                onLoginSuccess?.Invoke();   // Inspector: wire animation/sound ở đây
                UINavigator.Instance?.GoHome();
            }
            else if (result.ErrorCode == ErrorCodes.AUTH_SESSION_CONFLICT)
            {
                // Old session was terminated — user just needs to try again once.
                Debug.Log("[LoginView] AUTH_SESSION_CONFLICT: previous session terminated, prompting retry");
                ShowToast("Đăng xuất thành công", result.Message ?? "Phiên trước đã bị đăng xuất. Vui lòng đăng nhập lại.");
                // Do NOT fire onLoginFailed — user can retry immediately without changing anything.
            }
            else
            {
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
            // Reset switch điều khoản về Off sau khi đăng ký thành công
            if (agreeToTermsSwitch != null && agreeToTermsSwitch.isOn)
                agreeToTermsSwitch.AnimateSwitch();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Logout helper (gọi từ nơi khác nếu cần)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Đăng xuất hoàn toàn: xóa token, clear state, về LoginPanel.
        /// Gọi từ bất kỳ đâu khi cần logout.
        /// </summary>
        public static void Logout()
        {
            // Xoá refresh token & remember-me flag
            SecureStorage.DeleteKey(LoadingManager.KEY_REFRESH_TOKEN);
            PlayerPrefs.DeleteKey(LoadingManager.KEY_REMEMBER_ME);
            PlayerPrefs.Save();

            // Clear session state
            SessionState.Instance?.ClearSession();

            // Clear room state
            GameManager.Instance?.RoomState?.ClearRoom();

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