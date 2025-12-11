using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// Password Popup - Shows when joining a password-protected room
    /// </summary>
    public class PasswordPopup : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject popupPanel;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private Action<string> onConfirmCallback;
        private string currentRoomCode;

        private void Awake()
        {
            if (confirmButton != null)
                confirmButton.onClick.AddListener(OnConfirmClicked);
            
            if (cancelButton != null)
                cancelButton.onClick.AddListener(OnCancelClicked);

            Hide();
        }

        /// <summary>
        /// Show password popup
        /// </summary>
        /// <param name="roomCode">Room code that requires password</param>
        /// <param name="onConfirm">Callback when password is confirmed</param>
        public void Show(string roomCode, Action<string> onConfirm)
        {
            currentRoomCode = roomCode;
            onConfirmCallback = onConfirm;

            if (titleText != null)
                titleText.text = "Room Password Required";
            
            if (messageText != null)
                messageText.text = $"Room '{roomCode}' requires a password.";

            if (passwordInput != null)
                passwordInput.text = "";

            if (popupPanel != null)
                popupPanel.SetActive(true);
        }

        /// <summary>
        /// Hide password popup
        /// </summary>
        public void Hide()
        {
            if (popupPanel != null)
                popupPanel.SetActive(false);
            
            currentRoomCode = null;
            onConfirmCallback = null;
            
            if (passwordInput != null)
                passwordInput.text = "";
        }

        private void OnConfirmClicked()
        {
            if (passwordInput == null) return;

            string password = passwordInput.text.Trim();
            if (string.IsNullOrEmpty(password))
            {
                Debug.LogWarning("Password is empty");
                return;
            }

            onConfirmCallback?.Invoke(password);
            Hide();
        }

        private void OnCancelClicked()
        {
            Hide();
        }

        // Allow Enter key to confirm
        private void Update()
        {
            if (popupPanel != null && popupPanel.activeSelf)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    OnConfirmClicked();
                }
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

