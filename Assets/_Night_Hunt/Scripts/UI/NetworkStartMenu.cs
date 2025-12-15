using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NightHunt.Networking;

namespace NightHunt.UI
{
    /// <summary>
    /// Network Start Menu - UI với 3 nút
    /// 1. Start Server - Tạo dedicated server (hiện tại có UI, sau này sẽ headless)
    /// 2. Start Client Local - Test local (kết nối localhost)
    /// 3. Start Client - Kết nối server thật (sẽ phát triển sau)
    /// </summary>
    public class NetworkStartMenu : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button startServerButton;
        [SerializeField] private Button startClientLocalButton; // Test local
        [SerializeField] private Button startClientButton; // Kết nối server thật

        [Header("Server Address Input")]
        [SerializeField] private TMP_InputField serverAddressInput;
        [SerializeField] private TextMeshProUGUI serverAddressLabel;

        [Header("Status Display")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject loadingIndicator;

        [Header("UI Management")]
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private bool hideOnStart = true;

        private NetworkGameManager networkGameManager;
        private bool isConnecting = false;

        private void Awake()
        {
            // Setup button listeners
            if (startServerButton != null)
                startServerButton.onClick.AddListener(OnStartServerClicked);

            if (startClientLocalButton != null)
                startClientLocalButton.onClick.AddListener(OnStartClientLocalClicked);

            if (startClientButton != null)
                startClientButton.onClick.AddListener(OnStartClientClicked);

            // Setup server address input
            if (serverAddressInput != null)
            {
                serverAddressInput.text = "localhost"; // Default
                serverAddressInput.placeholder.GetComponent<TextMeshProUGUI>().text = "Enter server address";
            }

            // If menuPanel is not assigned, use this GameObject
            if (menuPanel == null)
                menuPanel = gameObject;

            UpdateStatus("Ready to start");
        }

        private void Start()
        {
            // Get NetworkGameManager
            networkGameManager = FindFirstObjectByType<NetworkGameManager>();
            if (networkGameManager == null)
            {
                Debug.LogError("[NetworkStartMenu] NetworkGameManager not found in scene!");
                UpdateStatus("Error: NetworkGameManager not found!");
            }
        }

        /// <summary>
        /// Start Server - Tạo dedicated server
        /// </summary>
        private void OnStartServerClicked()
        {
            if (networkGameManager == null || isConnecting) return;

            UpdateStatus("Starting Dedicated Server...");
            SetButtonsInteractable(false);
            isConnecting = true;

            try
            {
                networkGameManager.StartServer();
                UpdateStatus("Dedicated Server started!");
                SetButtonsInteractable(true);
                isConnecting = false;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NetworkStartMenu] Failed to start server: {e.Message}");
                UpdateStatus($"Error: {e.Message}");
                SetButtonsInteractable(true);
                isConnecting = false;
            }
        }

        /// <summary>
        /// Start Client Local - Test local (kết nối localhost)
        /// </summary>
        private void OnStartClientLocalClicked()
        {
            if (networkGameManager == null || isConnecting) return;

            UpdateStatus("Connecting to localhost...");
            SetButtonsInteractable(false);
            isConnecting = true;

            try
            {
                networkGameManager.StartClient("localhost");
                UpdateStatus("Connecting...");
                
                // Check connection status after delay
                Invoke(nameof(CheckConnectionStatus), 3f);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NetworkStartMenu] Failed to connect: {e.Message}");
                UpdateStatus($"Error: {e.Message}");
                SetButtonsInteractable(true);
                isConnecting = false;
            }
        }

        /// <summary>
        /// Start Client - Kết nối server thật (dùng address từ input field)
        /// </summary>
        private void OnStartClientClicked()
        {
            if (networkGameManager == null || isConnecting) return;

            // Lấy server address từ input field
            string serverAddress = "localhost";
            if (serverAddressInput != null && !string.IsNullOrEmpty(serverAddressInput.text))
            {
                serverAddress = serverAddressInput.text.Trim();
            }

            UpdateStatus($"Connecting to {serverAddress}...");
            SetButtonsInteractable(false);
            isConnecting = true;

            try
            {
                networkGameManager.StartClient(serverAddress);
                UpdateStatus("Connecting...");
                
                // Check connection status after delay
                Invoke(nameof(CheckConnectionStatus), 3f);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NetworkStartMenu] Failed to connect: {e.Message}");
                UpdateStatus($"Error: {e.Message}");
                SetButtonsInteractable(true);
                isConnecting = false;
            }
        }

        /// <summary>
        /// Hide menu UI and show game
        /// </summary>
        private void HideMenu()
        {
            if (menuPanel != null)
            {
                menuPanel.SetActive(false);
                Debug.Log("[NetworkStartMenu] Menu hidden, game started!");
            }
        }

        /// <summary>
        /// Show menu UI
        /// </summary>
        public void ShowMenu()
        {
            if (menuPanel != null)
            {
                menuPanel.SetActive(true);
                UpdateStatus("Ready to start");
                SetButtonsInteractable(true);
                isConnecting = false;
            }
        }

        /// <summary>
        /// Check connection status
        /// </summary>
        private void CheckConnectionStatus()
        {
            if (networkGameManager != null && networkGameManager.IsClient)
            {
                UpdateStatus("Connected!");
                
                if (hideOnStart)
                {
                    Invoke(nameof(HideMenu), 0.5f);
                }
            }
            else
            {
                UpdateStatus("Connection failed. Please try again.");
                SetButtonsInteractable(true);
                isConnecting = false;
            }
        }

        /// <summary>
        /// Update status text
        /// </summary>
        private void UpdateStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
            Debug.Log($"[NetworkStartMenu] {message}");
        }

        /// <summary>
        /// Set buttons interactable state
        /// </summary>
        private void SetButtonsInteractable(bool interactable)
        {
            if (startServerButton != null)
                startServerButton.interactable = interactable;

            if (startClientLocalButton != null)
                startClientLocalButton.interactable = interactable;

            if (startClientButton != null)
                startClientButton.interactable = interactable;

            if (loadingIndicator != null)
                loadingIndicator.SetActive(!interactable);
        }
    }
}
