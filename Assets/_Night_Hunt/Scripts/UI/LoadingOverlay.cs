using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;

namespace NightHunt.UI
{
    /// <summary>
    /// LoadingOverlay - Singleton loading indicator for API calls
    /// Shows semi-transparent overlay with loading message to provide user feedback
    /// Disables UI interaction while loading to prevent double-clicks
    /// Auto-hides after timeout to prevent stuck UI
    /// </summary>
    public class LoadingOverlay : MonoBehaviour
    {
        private static LoadingOverlay instance;
        public static LoadingOverlay Instance
        {
            get
            {
                if (instance == null)
                {
                    // Try to find existing instance
    #if UNITY_2023_2_OR_NEWER
                        instance = FindFirstObjectByType<LoadingOverlay>();
    #else
                        instance = FindObjectOfType<LoadingOverlay>();
    #endif
                    
                    if (instance == null)
                    {
                        // Create new instance
                        var go = new GameObject("LoadingOverlay");
                        instance = go.AddComponent<LoadingOverlay>();
                        DontDestroyOnLoad(go);
                        
                        Debug.LogWarning("[LoadingOverlay] Creating runtime instance - prefer adding to GameManager prefab!");
                    }
                }
                return instance;
            }
        }

        [Header("UI References")]
        [SerializeField] private GameObject overlayPanel;
        [SerializeField] private TMP_Text loadingText;
        [SerializeField] private Image loadingSpinner;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Animation Settings")]
        [SerializeField] private float spinnerRotationSpeed = 180f; // degrees per second
        [SerializeField] private float fadeInDuration = 0.2f;
        [SerializeField] private float fadeOutDuration = 0.2f;

        [Header("Timeout Settings")]
        [SerializeField] private float autoHideTimeoutSeconds = 30f; // Auto-hide after 30 seconds to prevent stuck UI

        private Coroutine currentShowCoroutine;
        private Coroutine spinnerCoroutine;
        private Coroutine timeoutCoroutine;
        private bool isShowing = false;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                Initialize();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Initialize()
        {
            // If UI references not set, try to find them
            if (overlayPanel == null)
            {
                overlayPanel = transform.Find("OverlayPanel")?.gameObject;
            }

            if (loadingText == null)
            {
                loadingText = GetComponentInChildren<TMP_Text>();
            }

            if (loadingSpinner == null)
            {
                loadingSpinner = GetComponentInChildren<Image>();
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            // Start hidden
            if (overlayPanel != null)
            {
                overlayPanel.SetActive(false);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Public API
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Show loading overlay with message
        /// </summary>
        public static void Show(string message = "Loading...")
        {
            if (Instance == null)
            {
                Debug.LogError("[LoadingOverlay] Instance not found!");
                return;
            }

            Instance.ShowInternal(message, duration: -1f);
        }

        /// <summary>
        /// Show loading overlay for a specific duration (auto-hide)
        /// </summary>
        public static void Show(string message, float duration)
        {
            if (Instance == null)
            {
                Debug.LogError("[LoadingOverlay] Instance not found!");
                return;
            }

            Instance.ShowInternal(message, duration);
        }

        /// <summary>
        /// Hide loading overlay
        /// </summary>
        public static void Hide()
        {
            if (Instance == null)
            {
                return;
            }

            Instance.HideInternal();
        }

        /// <summary>
        /// Show error message (red text, auto-hide after 2 seconds)
        /// </summary>
        public static void ShowError(string errorMessage)
        {
            if (Instance == null)
            {
                Debug.LogError("[LoadingOverlay] Instance not found!");
                return;
            }

            Instance.ShowErrorInternal(errorMessage);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Internal Implementation
        // ══════════════════════════════════════════════════════════════════════

        private void ShowInternal(string message, float duration)
        {
            // Cancel any previous show/hide operations
            if (currentShowCoroutine != null)
            {
                StopCoroutine(currentShowCoroutine);
            }

            if (timeoutCoroutine != null)
            {
                StopCoroutine(timeoutCoroutine);
            }

            currentShowCoroutine = StartCoroutine(ShowCoroutine(message, duration));
        }

        private void HideInternal()
        {
            // Cancel any previous operations
            if (currentShowCoroutine != null)
            {
                StopCoroutine(currentShowCoroutine);
                currentShowCoroutine = null;
            }

            if (timeoutCoroutine != null)
            {
                StopCoroutine(timeoutCoroutine);
                timeoutCoroutine = null;
            }

            if (spinnerCoroutine != null)
            {
                StopCoroutine(spinnerCoroutine);
                spinnerCoroutine = null;
            }

            StartCoroutine(HideCoroutine());
        }

        private void ShowErrorInternal(string errorMessage)
        {
            ShowInternal(errorMessage, duration: 2f);

            // Change text color to red
            if (loadingText != null)
            {
                loadingText.color = Color.red;
            }

            // Hide spinner for error messages
            if (loadingSpinner != null)
            {
                loadingSpinner.gameObject.SetActive(false);
            }
        }

        private IEnumerator ShowCoroutine(string message, float duration)
        {
            isShowing = true;

            // Enable overlay panel
            if (overlayPanel != null)
            {
                overlayPanel.SetActive(true);
            }

            // Set message
            if (loadingText != null)
            {
                loadingText.text = message;
                loadingText.color = Color.white; // Reset color
            }

            // Show spinner
            if (loadingSpinner != null)
            {
                loadingSpinner.gameObject.SetActive(true);
            }

            // Disable UI interaction
            // NOTE: Do NOT disable EventSystem globally — it would block ALL input permanently
            // if HideCoroutine is interrupted mid-run. The overlay panel's full-screen Image
            // with raycastTarget=true is the correct mechanism for blocking clicks behind the overlay.

            // Fade in
            if (canvasGroup != null)
            {
                float elapsed = 0f;
                while (elapsed < fadeInDuration)
                {
                    elapsed += Time.deltaTime;
                    canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
                    yield return null;
                }
                canvasGroup.alpha = 1f;
            }

            // Start spinner rotation
            if (loadingSpinner != null && spinnerCoroutine == null)
            {
                spinnerCoroutine = StartCoroutine(SpinCoroutine());
            }

            // Auto-hide after duration (if specified)
            if (duration > 0f)
            {
                yield return new WaitForSeconds(duration);
                HideInternal();
            }
            else
            {
                // Start timeout coroutine to prevent stuck UI
                timeoutCoroutine = StartCoroutine(TimeoutCoroutine());
            }
        }

        private IEnumerator HideCoroutine()
        {
            if (!isShowing)
            {
                yield break;
            }

            isShowing = false;

            // Stop spinner
            if (spinnerCoroutine != null)
            {
                StopCoroutine(spinnerCoroutine);
                spinnerCoroutine = null;
            }

            // Fade out
            if (canvasGroup != null)
            {
                float elapsed = 0f;
                float startAlpha = canvasGroup.alpha;
                while (elapsed < fadeOutDuration)
                {
                    elapsed += Time.deltaTime;
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeOutDuration);
                    yield return null;
                }
                canvasGroup.alpha = 0f;
            }

            // Disable overlay panel
            if (overlayPanel != null)
            {
                overlayPanel.SetActive(false);
            }

            // Re-enable UI interaction
            // (EventSystem is no longer toggled here — see ShowCoroutine comment)
        }

        private IEnumerator SpinCoroutine()
        {
            if (loadingSpinner == null)
            {
                yield break;
            }

            while (true)
            {
                loadingSpinner.transform.Rotate(0f, 0f, -spinnerRotationSpeed * Time.deltaTime);
                yield return null;
            }
        }

        private IEnumerator TimeoutCoroutine()
        {
            yield return new WaitForSeconds(autoHideTimeoutSeconds);

            Debug.LogWarning($"[LoadingOverlay] Auto-hide timeout reached ({autoHideTimeoutSeconds}s) - forcing hide");
            HideInternal();
        }

        // ══════════════════════════════════════════════════════════════════════
        // Editor Helpers
        // ══════════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
        [ContextMenu("Test Show")]
        private void TestShow()
        {
            Show("Testing loading overlay...");
        }

        [ContextMenu("Test Hide")]
        private void TestHide()
        {
            Hide();
        }

        [ContextMenu("Test Error")]
        private void TestError()
        {
            ShowError("Test error message");
        }
#endif
    }
}
