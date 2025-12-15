using System;
using NightHunt.Core;
using NightHunt.Networking;
using NightHunt.Services.Room;
using NightHunt.Services.Auth;
using NightHunt.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// Popup for reconnection option when connection is lost
    /// Sử dụng Singleton pattern để dễ access
    /// </summary>
    public class ReconnectPopup : MonoBehaviour
    {
        private static ReconnectPopup instance;
        
        public static ReconnectPopup Instance
        {
            get
            {
                if (instance == null)
                {
                    // Try to find in PersistentUICanvas first
                    if (PersistentUICanvas.Instance != null)
                    {
                        instance = PersistentUICanvas.Instance.ReconnectPopup;
                    }
                    
                    // If still null, find in scene
                    if (instance == null)
                    {
                        instance = FindFirstObjectByType<ReconnectPopup>();
                    }
                }
                return instance;
            }
        }

        [Header("UI References")]
        [SerializeField] private GameObject popupPanel;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button reconnectButton;
        [SerializeField] private Button leaveButton;

        // Note: NetworkBootstrap đã bị xóa, không cần thiết cho reconnect
        private RoomService roomService;
        private AuthService authService;
        private Action onReconnect;
        private Action onLeave;

        private void Awake()
        {
            // Set instance
            if (instance == null)
            {
                instance = this;
            }
            else if (instance != this)
            {
                // Duplicate found, destroy this one
                Destroy(gameObject);
                return;
            }

            if (popupPanel != null)
            {
                popupPanel.SetActive(false);
            }

            if (reconnectButton != null)
            {
                reconnectButton.onClick.AddListener(OnReconnectClicked);
            }

            if (leaveButton != null)
            {
                leaveButton.onClick.AddListener(OnLeaveClicked);
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void Start()
        {
            // Try to get from GameManager first
            if (GameManager.Instance != null)
            {
                roomService = GameManager.Instance.RoomService;
                authService = GameManager.Instance.AuthService;
            }

            // Fallback: Find in scene
            if (roomService == null)
                roomService = FindFirstObjectByType<RoomService>();
            if (authService == null)
                authService = FindFirstObjectByType<AuthService>();
        }

        public void Show(string message, Action onReconnectCallback = null, Action onLeaveCallback = null)
        {
            if (messageText != null)
            {
                messageText.text = message;
            }

            onReconnect = onReconnectCallback;
            onLeave = onLeaveCallback;

            if (popupPanel != null)
            {
                popupPanel.SetActive(true);
            }
        }

        public void Hide()
        {
            if (popupPanel != null)
            {
                popupPanel.SetActive(false);
            }
        }

        private async void OnReconnectClicked()
        {
            if (reconnectButton != null)
            {
                reconnectButton.interactable = false;
            }

            try
            {
                if (SessionState.Instance == null || !SessionState.Instance.IsAuthenticated)
                {
                    ShowError("Session expired. Please login again.");
                    return;
                }

                if (authService == null)
                {
                    ShowError("Auth service not available.");
                    return;
                }

                var autoLoginResult = await authService.AutoLogin();
                if (!autoLoginResult.Success)
                {
                    ShowError(autoLoginResult.Message ?? "Auto-login failed. Please login again.");
                    return;
                }

                bool wasInRoom = RoomState.Instance != null && RoomState.Instance.IsInRoom;

                if (wasInRoom && roomService != null)
                {
                    var reconnectResult = await roomService.Reconnect(RoomState.Instance.RoomId);
                    if (!reconnectResult.Success)
                    {
                        ShowError(reconnectResult.Message ?? "Reconnect failed.");
                        return;
                    }
                    SceneLoader.LoadWaiting();
                }
                else
                {
                    SceneLoader.LoadHome();
                }

                Hide();
                onReconnect?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Reconnect error: {ex.Message}");
                ShowError("Reconnection failed. Please try again.");
            }
            finally
            {
                if (reconnectButton != null)
                {
                    reconnectButton.interactable = true;
                }
            }
        }

        private async void OnLeaveClicked()
        {
            if (leaveButton != null)
            {
                leaveButton.interactable = false;
            }

            try
            {
                // Leave room
                if (roomService != null && RoomState.Instance != null && RoomState.Instance.IsInRoom)
                {
                    var result = await roomService.LeaveRoom(RoomState.Instance.RoomId);
                    if (!result.Success)
                    {
                        ShowError("Failed to leave room");
                        return;
                    }
                }

                Hide();
                onLeave?.Invoke();
                SceneLoader.LoadHome();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Leave error: {ex.Message}");
                ShowError("Failed to leave room");
            }
            finally
            {
                if (leaveButton != null)
                {
                    leaveButton.interactable = true;
                }
            }
        }

        private void ShowError(string message)
        {
            if (messageText != null)
            {
                messageText.text = $"{messageText.text}\n{message}";
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Auto-assign references in editor
            if (popupPanel == null)
                popupPanel = transform.Find("Panel")?.gameObject ?? gameObject;
        }
#endif
    }
}

