using System.Threading.Tasks;
using NightHunt.Core;
using NightHunt.Services.Auth;
using NightHunt.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    public class LoginView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_InputField usernameInput;
        [SerializeField] private TMP_InputField emailInput;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private TMP_InputField confirmPasswordInput;
        [SerializeField] private Button loginButton;
        [SerializeField] private Button registerButton;
        [SerializeField] private TextMeshProUGUI errorText;
        
        [Header("Service References (Optional - will auto-find if not assigned)")]
        [SerializeField] private AuthService authService;
        
        private LoadingManager loadingManager;

        private void Awake()
        {
            // Try to get AuthService from GameManager first
            if (authService == null && GameManager.Instance != null)
            {
                authService = GameManager.Instance.AuthService;
            }

            // Fallback: Find in scene
            if (authService == null)
            {
                authService = FindFirstObjectByType<AuthService>();
            }

            if (authService == null)
            {
                Debug.LogError("AuthService not found! Make sure GameManager scene is loaded or AuthService exists in current scene.");
            }

            if (loginButton != null)
            {
                loginButton.onClick.AddListener(OnLoginClicked);
            }
            if (registerButton != null)
            {
                registerButton.onClick.AddListener(OnRegisterClicked);
            }
        }

        private void Start()
        {
            // Get LoadingManager reference
            loadingManager = PersistentUICanvas.Instance != null ? PersistentUICanvas.Instance.LoadingManager : null;
            
            // Auto-login is already checked in FirstLoading scene
            // Just ensure loading is hidden (should already be hidden by LoadingManager)
            if (loadingManager != null && loadingManager.IsShowing())
            {
                loadingManager.Hide();
            }
        }

        private async void OnLoginClicked()
        {
            if (authService == null) return;

            string identifier = usernameInput != null ? usernameInput.text : "";
            string password = passwordInput != null ? passwordInput.text : "";

            if (string.IsNullOrEmpty(identifier) || string.IsNullOrEmpty(password))
            {
                ShowError("Please enter username/email and password");
                return;
            }

            // Show loading
            if (loadingManager != null)
            {
                loadingManager.Show();
            }
            
            SetButtonsInteractable(false);
            var result = await authService.Login(identifier, password);
            SetButtonsInteractable(true);
            
            // Hide loading
            if (loadingManager != null)
            {
                loadingManager.Hide();
            }

            if (result.Success)
            {
                OnLoginSuccess();
            }
            else
            {
                // Show error via notice popup instead of error text
                ShowErrorViaNotice(result.Message ?? "Đăng nhập thất bại", result.ErrorCode);
            }
        }

        private async void OnRegisterClicked()
        {
            if (authService == null) return;

            string username = usernameInput != null ? usernameInput.text : "";
            string email = emailInput != null ? emailInput.text : "";
            string password = passwordInput != null ? passwordInput.text : "";
            string confirmPassword = confirmPasswordInput != null ? confirmPasswordInput.text : "";

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email) || 
                string.IsNullOrEmpty(password) || string.IsNullOrEmpty(confirmPassword))
            {
                ShowError("Please fill all fields");
                return;
            }

            if (password != confirmPassword)
            {
                ShowError("Passwords do not match");
                return;
            }

            // Show loading
            if (loadingManager != null)
            {
                loadingManager.Show();
            }
            
            SetButtonsInteractable(false);
            var result = await authService.Register(username, email, password, confirmPassword);
            SetButtonsInteractable(true);
            
            // Hide loading
            if (loadingManager != null)
            {
                loadingManager.Hide();
            }

            if (result.Success)
            {
                OnLoginSuccess();
            }
            else
            {
                // Show error via notice popup instead of error text
                ShowErrorViaNotice(result.Message ?? "Đăng ký thất bại", result.ErrorCode);
            }
        }

        private void OnLoginSuccess()
        {
            // Load Home scene after successful login
            SceneLoader.LoadHome();
        }

        private void ShowError(string message)
        {
            if (errorText != null)
            {
                errorText.text = message;
                errorText.gameObject.SetActive(true);
            }
        }
        
        private void ShowErrorViaNotice(string message, string errorCode = null)
        {
            // Show error via notice popup
            var noticePopup = PersistentUICanvas.Instance != null ? PersistentUICanvas.Instance.NoticePopup : null;
            if (noticePopup != null)
            {
                noticePopup.Show(
                    title: "Lỗi",
                    message: message,
                    onConfirm: () =>
                    {
                        // Just close the popup
                    },
                    autoDismissSeconds: 3f // Auto dismiss after 3 seconds
                );
            }
            else
            {
                // Fallback: use error text if notice popup not available
                ShowError(message);
            }
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (loginButton != null) loginButton.interactable = interactable;
            if (registerButton != null) registerButton.interactable = interactable;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Auto-assign references in editor
            if (authService == null && GameManager.Instance != null)
            {
                authService = GameManager.Instance.AuthService;
            }
        }
#endif
    }
}

