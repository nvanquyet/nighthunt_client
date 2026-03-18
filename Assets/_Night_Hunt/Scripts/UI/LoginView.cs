using System.Collections;
using System.Threading.Tasks;
using Michsky.UI.Shift;
using NightHunt.Core;
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
    /// </summary>
    public class LoginView : MonoBehaviour, INavigableView
    {
        // ─── Login Form ─────────────────────────────────────────────
        [Header("Login Form")]
        [SerializeField] private TMP_InputField usernameInput;
        [SerializeField] private TMP_InputField passwordInput;
        [Tooltip("ShiftUI SwitchManager trên switch Gó nhớ đăng nhập.")]
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
            // CanvasGroup navigation does NOT trigger OnEnable on nav transitions.
            // Show logic is in INavigableView.OnShow().
        }

        // ─────────────────────────────────────────────
        // INavigableView — called by UINavigator
        // ─────────────────────────────────────────────

        public void OnShow()
        {
            // Restore RememberMe toggle and hide any stale loading screen.
            StartCoroutine(RestoreAfterFrame());

            if (_loadingManager != null && _loadingManager.IsShowing())
                _loadingManager.Hide();
        }

        public void OnHide() { }
        private IEnumerator RestoreAfterFrame()
        {
            yield return null; // chờ 1 frame sau SwitchManager.OnEnable()
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
                ShowToast("Lỗi", "Vui lòng nhập tên đăng nhập và mật khẩu");
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
            else
            {
                ShowToast("Đăng nhập thất bại", result.Message ?? "Vui lòng thử lại");
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
                ShowToast("Lỗi", "Vui lòng điền đầy đủ thông tin");
                return;
            }

            if (password != confirmPassword)
            {
                ShowToast("Lỗi", "Mật khẩu xác nhận không khớp");
                return;
            }

            if (agreeToTermsSwitch == null || !agreeToTermsSwitch.isOn)
            {
                ShowToast("Lỗi", "Vui lòng đồng ý với điều khoản sử dụng");
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
                ShowToast("Thành công", "Đăng ký thành công! Vui lòng đăng nhập.", ClearRegisterFields);
            }
            else
            {
                ShowToast("Đăng ký thất bại", result.Message ?? "Vui lòng thử lại");
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

            // Clear session state (FIX: đúng tên method)
            SessionState.Instance?.ClearSession();

            // Clear room state (FIX: đúng tên method)
            GameManager.Instance?.RoomState?.ClearRoom();

            // Navigate về Login panel
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