using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using NightHunt.Utilities;

namespace NightHunt.UI
{
    /// <summary>
    /// NoticePopup - Reusable popup for displaying notices/warnings
    /// Can be used for force logout, session expired, or other important notifications
    /// Supports auto-dismiss after delay or manual OK button
    /// </summary>
    public class NoticePopup : MonoBehaviour
    {
        private static NoticePopup instance;
        
        public static NoticePopup Instance
        {
            get
            {
                if (instance == null)
                {
                    // Try to find in PersistentUICanvas first
                    if (PersistentUICanvas.Instance != null)
                    {
                        instance = PersistentUICanvas.Instance.NoticePopup;
                    }
                    
                    // If still null, find in scene
                    if (instance == null)
                    {
#if UNITY_2023_1_OR_NEWER
                        instance = FindFirstObjectByType<NoticePopup>();
#else
                        instance = FindObjectOfType<NoticePopup>();
#endif
                    }
                }
                return instance;
            }
        }

        [Header("UI References")]
        [SerializeField] private GameObject popupPanel;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button okButton;
        [SerializeField] private TextMeshProUGUI countdownText; // Optional countdown display

        private Action onConfirmCallback;
        private Coroutine autoDismissCoroutine;
        private float autoDismissDelay = 2f;
        private bool isShowing = false;

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

            if (okButton != null)
            {
                okButton.onClick.AddListener(OnOkClicked);
            }

            Hide();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
            
            if (autoDismissCoroutine != null)
            {
                StopCoroutine(autoDismissCoroutine);
            }
        }

        /// <summary>
        /// Show notice popup with message
        /// </summary>
        /// <param name="title">Title of the notice</param>
        /// <param name="message">Message to display</param>
        /// <param name="onConfirm">Callback when OK is clicked or auto-dismiss</param>
        /// <param name="autoDismissSeconds">Auto dismiss after seconds (0 = no auto dismiss)</param>
        public void Show(string title, string message, Action onConfirm = null, float autoDismissSeconds = 2f)
        {
            if (popupPanel == null)
            {
                Debug.LogWarning("NoticePopup panel is not assigned. Cannot show notice.");
                return;
            }

            isShowing = true;
            onConfirmCallback = onConfirm;
            autoDismissDelay = autoDismissSeconds;

            if (titleText != null)
            {
                titleText.text = title;
            }

            if (messageText != null)
            {
                messageText.text = message;
            }

            popupPanel.SetActive(true);

            // Start auto dismiss if delay > 0
            if (autoDismissDelay > 0)
            {
                if (autoDismissCoroutine != null)
                {
                    StopCoroutine(autoDismissCoroutine);
                }
                autoDismissCoroutine = StartCoroutine(AutoDismissCoroutine());
            }
            else
            {
                // Hide countdown if no auto dismiss
                if (countdownText != null)
                {
                    countdownText.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Hide notice popup
        /// </summary>
        public void Hide()
        {
            if (popupPanel != null)
            {
                popupPanel.SetActive(false);
            }

            isShowing = false;
            onConfirmCallback = null;

            if (autoDismissCoroutine != null)
            {
                StopCoroutine(autoDismissCoroutine);
                autoDismissCoroutine = null;
            }

            if (countdownText != null)
            {
                countdownText.gameObject.SetActive(false);
            }
        }

        private void OnOkClicked()
        {
            Hide();
            onConfirmCallback?.Invoke();
        }

        private IEnumerator AutoDismissCoroutine()
        {
            float elapsed = 0f;

            while (elapsed < autoDismissDelay)
            {
                elapsed += Time.deltaTime;
                float remaining = autoDismissDelay - elapsed;

                // Update countdown text if available
                if (countdownText != null)
                {
                    countdownText.gameObject.SetActive(true);
                    countdownText.text = $"Tự động đóng sau {Mathf.CeilToInt(remaining)}s";
                }

                yield return null;
            }

            // Auto dismiss
            Hide();
            onConfirmCallback?.Invoke();
        }

        public bool IsShowing() => isShowing;

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Auto-assign references in editor
            if (popupPanel == null)
            {
                popupPanel = transform.Find("Panel")?.gameObject ?? gameObject;
            }

            if (okButton == null)
            {
                okButton = ComponentResolver.Find<Button>(this)
        .OnSelf()
        .InChildren()
        .InParent()
        .OrLogWarning("[Auto] Button not found")
        .Resolve();
            }
        }
#endif
    }
}

