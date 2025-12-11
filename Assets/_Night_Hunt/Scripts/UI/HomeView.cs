using System.Threading.Tasks;
using NightHunt.Core;
using NightHunt.Services.Auth;
using NightHunt.Services.Room;
using NightHunt.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// Home View - Main menu after login
    /// Shows user info, Create Lobby, Join Lobby buttons
    /// </summary>
    public class HomeView : MonoBehaviour
    {
        [Header("User Info")]
        [SerializeField] private TextMeshProUGUI usernameText;
        [SerializeField] private TextMeshProUGUI emailText;
        [SerializeField] private TextMeshProUGUI userIdText;

        [Header("Main Buttons")]
        [SerializeField] private Button createLobbyButton; // Button to open create lobby panel
        [SerializeField] private Button joinLobbyButton; // Button to open join lobby panel
        [SerializeField] private Button quickPlayButton;
        [SerializeField] private Button logoutButton;

        [Header("Join Lobby Panel")]
        [SerializeField] private GameObject joinLobbyPanel;
        [SerializeField] private TMP_InputField roomCodeInput;
        [SerializeField] private Button joinConfirmButton; // Button inside panel to confirm join
        [SerializeField] private Button cancelJoinButton;

        [Header("Create Lobby Panel")]
        [SerializeField] private GameObject createLobbyPanel;
        [SerializeField] private TMP_Dropdown modeDropdown;
        [SerializeField] private Toggle isPublicToggle;
        [SerializeField] private Toggle isLockedToggle;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private Button createConfirmButton; // Button inside panel to confirm create
        [SerializeField] private Button cancelCreateButton;

        [Header("Password Popup")]
        [SerializeField] private PasswordPopup passwordPopup;

        private AuthService authService;
        private RoomService roomService;
        private SessionState sessionState;
        private RoomState roomState;

        private void Awake()
        {
            // Get services from GameManager
            if (GameManager.Instance != null)
            {
                authService = GameManager.Instance.AuthService;
                roomService = GameManager.Instance.RoomService;
                sessionState = GameManager.Instance.SessionState;
            }
            
            // Get RoomState
            roomState = RoomState.Instance;

            // Setup buttons
            if (createLobbyButton != null)
                createLobbyButton.onClick.AddListener(OnCreateLobbyClicked);
            
            if (joinLobbyButton != null)
                joinLobbyButton.onClick.AddListener(OnJoinLobbyClicked);
            
            if (quickPlayButton != null)
                quickPlayButton.onClick.AddListener(OnQuickPlayClicked);
            
            if (logoutButton != null)
                logoutButton.onClick.AddListener(OnLogoutClicked);

            if (joinConfirmButton != null)
                joinConfirmButton.onClick.AddListener(OnJoinConfirmClicked);
            
            if (cancelJoinButton != null)
                cancelJoinButton.onClick.AddListener(OnCancelJoinClicked);

            if (createConfirmButton != null)
                createConfirmButton.onClick.AddListener(OnCreateConfirmClicked);
            
            if (cancelCreateButton != null)
                cancelCreateButton.onClick.AddListener(OnCancelCreateClicked);

            // Hide panels
            if (joinLobbyPanel != null)
                joinLobbyPanel.SetActive(false);
            
            if (createLobbyPanel != null)
                createLobbyPanel.SetActive(false);

            // Setup mode dropdown
            if (modeDropdown != null)
            {
                modeDropdown.ClearOptions();
                modeDropdown.AddOptions(new System.Collections.Generic.List<string> { "2v2", "3v3", "5v5" });
            }

            // Setup password input visibility
            if (isLockedToggle != null && passwordInput != null)
            {
                isLockedToggle.onValueChanged.AddListener((value) => {
                    passwordInput.gameObject.SetActive(value);
                });
                passwordInput.gameObject.SetActive(false);
            }
        }

        private async void Start()
        {
            UpdateUserInfo();
            
            // Ensure loading is hidden when Home scene is ready
            var loading = PersistentUICanvas.Instance != null ? PersistentUICanvas.Instance.LoadingManager : null;
            if (loading != null && loading.IsShowing())
            {
                loading.Hide();
            }
            
            // Check if player was in a room before (reconnect check)
            await CheckAndShowReconnectPopup();
        }
        
        /// <summary>
        /// Check if player was in a room and show reconnect popup if needed
        /// </summary>
        private async System.Threading.Tasks.Task CheckAndShowReconnectPopup()
        {
            if (roomState == null || !roomState.IsInRoom || roomService == null)
            {
                return;
            }
            
            // Player was in a room - check if room still exists
            var reconnectPopup = PersistentUICanvas.Instance != null ? PersistentUICanvas.Instance.ReconnectPopup : null;
            if (reconnectPopup == null)
            {
                Debug.LogWarning("[HomeView] ReconnectPopup not found in PersistentUICanvas");
                return;
            }
            
            // Try to reconnect to check if room still exists
            var result = await roomService.Reconnect(roomState.RoomId);
            
            if (result.Success && result.Data != null)
            {
                // Room still exists - show reconnect popup
                reconnectPopup.Show(
                    message: $"Bạn đang ở trong phòng {result.Data.roomCode}. Bạn có muốn quay lại không?",
                    onReconnectCallback: () =>
                    {
                        // Reconnect successful, go to waiting room
                        SceneLoader.LoadWaiting();
                    },
                    onLeaveCallback: () =>
                    {
                        // User chose to leave - clear room state
                        roomState.ClearRoom();
                    }
                );
            }
            else
            {
                // Room no longer exists - clear room state
                Debug.Log($"[HomeView] Room {roomState.RoomId} no longer exists. Clearing room state.");
                roomState.ClearRoom();
            }
        }

        private void UpdateUserInfo()
        {
            if (sessionState == null) return;

            if (usernameText != null)
                usernameText.text = $"Username: {sessionState.Username}";
            
            if (emailText != null)
                emailText.text = $"Email: {sessionState.Email}";
            
            if (userIdText != null)
                userIdText.text = $"User ID: {sessionState.UserId}";
        }

        /// <summary>
        /// Called when user clicks "Create Lobby" main button
        /// Opens the create lobby panel
        /// </summary>
        private void OnCreateLobbyClicked()
        {
            if (createLobbyPanel != null)
            {
                createLobbyPanel.SetActive(true);
            }
        }

        /// <summary>
        /// Called when user clicks "Join Lobby" main button
        /// Opens the join lobby panel
        /// </summary>
        private void OnJoinLobbyClicked()
        {
            if (joinLobbyPanel != null)
            {
                joinLobbyPanel.SetActive(true);
            }
        }

        private async void OnQuickPlayClicked()
        {
            if (roomService == null) return;

            // Quick play with default mode (2v2)
            var result = await roomService.QuickPlay("2v2");
            
            if (result.Success && result.Data != null)
            {
                // Join successful, go to waiting room
                SceneLoader.LoadWaiting();
            }
            else
            {
                Debug.LogError($"Quick play failed: {result.Message}");
                // Show error message
            }
        }

        /// <summary>
        /// Called when user clicks "Join" button inside join lobby panel
        /// Actually performs the join operation
        /// </summary>
        private async void OnJoinConfirmClicked()
        {
            if (roomService == null || roomCodeInput == null) return;

            string roomCode = roomCodeInput.text.Trim();
            if (string.IsNullOrEmpty(roomCode))
            {
                Debug.LogWarning("Room code is empty");
                return;
            }

            // First, get room info to check if password is required
            // For now, we'll try to join and handle password requirement in the response
            // Try to join without password first
            var result = await roomService.JoinByCode(roomCode, "");
            
            if (!result.Success && result.Message != null && result.Message.Contains("password"))
            {
                // Show password popup
                if (passwordPopup != null)
                {
                    passwordPopup.Show(roomCode, async (password) => {
                        var retryResult = await roomService.JoinByCode(roomCode, password);
                        if (retryResult.Success)
                        {
                            SceneLoader.LoadWaiting();
                        }
                        else
                        {
                            Debug.LogError($"Join failed: {retryResult.Message}");
                        }
                    });
                }
            }
            else if (result.Success)
            {
                // Join successful, go to waiting room
                SceneLoader.LoadWaiting();
            }
            else
            {
                Debug.LogError($"Join failed: {result.Message}");
            }
        }

        private void OnCancelJoinClicked()
        {
            if (joinLobbyPanel != null)
            {
                joinLobbyPanel.SetActive(false);
            }
            if (roomCodeInput != null)
            {
                roomCodeInput.text = "";
            }
        }

        /// <summary>
        /// Called when user clicks "Create" button inside create lobby panel
        /// Actually performs the create room operation
        /// </summary>
        private async void OnCreateConfirmClicked()
        {
            if (roomService == null) return;

            string mode = modeDropdown != null ? modeDropdown.options[modeDropdown.value].text : "2v2";
            bool isPublic = isPublicToggle != null ? isPublicToggle.isOn : true;
            bool isLocked = isLockedToggle != null ? isLockedToggle.isOn : false;
            string password = isLocked && passwordInput != null ? passwordInput.text : null;

            var result = await roomService.CreateRoom(mode, isPublic, isLocked, password);
            
            if (result.Success && result.Data != null)
            {
                // Create successful, go to waiting room
                SceneLoader.LoadWaiting();
            }
            else
            {
                Debug.LogError($"Create room failed: {result.Message}");
            }
        }

        private void OnCancelCreateClicked()
        {
            if (createLobbyPanel != null)
            {
                createLobbyPanel.SetActive(false);
            }
            if (passwordInput != null)
            {
                passwordInput.text = "";
            }
        }

        private void OnLogoutClicked()
        {
            if (authService != null)
            {
                authService.Logout();
            }
            SceneLoader.LoadLogin();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Auto-assign references in editor
            if (GameManager.Instance != null)
            {
                if (authService == null)
                    authService = GameManager.Instance.AuthService;
                if (roomService == null)
                    roomService = GameManager.Instance.RoomService;
                if (sessionState == null)
                    sessionState = GameManager.Instance.SessionState;
            }
        }
#endif
    }
}

