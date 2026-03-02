using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using NightHunt.Core;
using NightHunt.Services.Auth;
using NightHunt.State;
using NightHunt.UI;

namespace NightHunt.Core
{
    /// <summary>
    /// LoadingManager - Manages loading screen and initializes GameManager
    /// Scene: FirstLoading (chứa cả GameManager và PersistentUICanvas)
    /// Flow: FirstLoading → Login (auto-login check happens in LoginView)
    /// Sử dụng Singleton pattern để dễ access
    /// </summary>
    public class LoadingManager : MonoBehaviour
    {
        private static LoadingManager instance;
        
        public static LoadingManager Instance
        {
            get
            {
                if (instance == null)
                {
                    // Try to find in PersistentUICanvas first
                    if (PersistentUICanvas.Instance != null)
                    {
                        instance = PersistentUICanvas.Instance.LoadingManager;
                    }
                    
                    // If still null, find in scene
                    if (instance == null)
                    {
                        instance = FindFirstObjectByType<LoadingManager>();
                    }
                }
                return instance;
            }
        }

        [Header("UI References")]
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private UnityEngine.UI.Slider progressBar;
        [SerializeField] private TMPro.TextMeshProUGUI loadingText;
        [SerializeField] private float minLoadingTime = 1f;

        private static string targetScene; // Will be set to SceneLoader.SCENE_LOGIN if empty
        private bool isShowing = false;

        private void Awake()
        {
            // Set instance
            if (instance == null)
            {
                instance = this;
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
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(true);
                isShowing = true;
            }
            StartCoroutine(InitializeAndLoad());
        }

        /// <summary>
        /// Set target scene to load after initialization
        /// </summary>
        public static void SetTargetScene(string sceneName)
        {
            targetScene = sceneName;
        }

        /// <summary>
        /// Initialize GameManager và PersistentUICanvas, check auto login, sau đó load target scene
        /// </summary>
        private IEnumerator InitializeAndLoad()
        {
            float startTime = Time.time;
            
            // Step 1: Initialize GameManager (đã có trong scene)
            UpdateLoadingText("Initializing game...");
            UpdateProgress(0.1f);
            yield return StartCoroutine(WaitForGameManager());

            // Step 2: Initialize PersistentUICanvas (GameManager sẽ tự động tạo)
            UpdateLoadingText("Initializing UI...");
            UpdateProgress(0.3f);
            yield return StartCoroutine(WaitForPersistentUICanvas());

            // Step 3: Initialize services
            UpdateLoadingText("Loading services...");
            UpdateProgress(0.5f);
            yield return new WaitForSeconds(0.2f); // Give services time to initialize

            // Step 4: Check auto login and decide target scene
            UpdateLoadingText("Checking session...");
            UpdateProgress(0.6f);
            yield return StartCoroutine(CheckAutoLoginAndDecideScene());

            // Step 5: Ensure minimum loading time
            float elapsedTime = Time.time - startTime;
            if (elapsedTime < minLoadingTime)
            {
                yield return new WaitForSeconds(minLoadingTime - elapsedTime);
            }

            // Step 6: Load target scene
            UpdateLoadingText("Loading game...");
            UpdateProgress(0.7f);
            yield return StartCoroutine(LoadTargetScene());

            // Step 7: Hide loading after scene is loaded
            UpdateProgress(1.0f);
            yield return new WaitForSeconds(0.2f); // Small delay to ensure scene is ready
            Hide();
        }

        /// <summary>
        /// Đợi GameManager khởi tạo (đã có trong scene)
        /// </summary>
        private IEnumerator WaitForGameManager()
        {
            float timeout = 5f;
            float elapsed = 0f;

            while (GameManager.Instance == null && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                UpdateProgress(0.1f + (elapsed / timeout) * 0.2f);
                yield return null;
            }

            if (GameManager.Instance == null)
            {
                Debug.LogError("GameManager failed to initialize!");
                yield break;
            }

            yield return new WaitForSeconds(0.1f);
        }

        /// <summary>
        /// Đợi PersistentUICanvas khởi tạo (GameManager sẽ tự động tạo)
        /// </summary>
        private IEnumerator WaitForPersistentUICanvas()
        {
            float timeout = 3f;
            float elapsed = 0f;

            while (PersistentUICanvas.Instance == null && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                UpdateProgress(0.3f + (elapsed / timeout) * 0.2f);
                yield return null;
            }

            if (PersistentUICanvas.Instance == null)
            {
                Debug.LogWarning("PersistentUICanvas failed to initialize, creating manually...");
                PersistentUICanvas.GetOrCreate();
            }

            yield return new WaitForSeconds(0.1f);
        }

        /// <summary>
        /// Check auto login and decide which scene to load
        /// </summary>
        private IEnumerator CheckAutoLoginAndDecideScene()
        {
            // If target scene is already set, use it
            if (!string.IsNullOrEmpty(targetScene))
            {
                yield break;
            }

            // Check if user has saved session
            if (SessionState.Instance != null && SessionState.Instance.IsAuthenticated)
            {
                // Try auto login
                if (GameManager.Instance != null && GameManager.Instance.AuthService != null)
                {
                    var authService = GameManager.Instance.AuthService;
                    bool autoLoginCompleted = false;
                    bool autoLoginSuccess = false;

                    // Call auto login asynchronously
                    authService.AutoLogin().ContinueWith(task =>
                    {
                        autoLoginSuccess = task.Result.Success;
                        autoLoginCompleted = true;
                    });

                    // Wait for auto login to complete
                    float timeout = 10f;
                    float elapsed = 0f;
                    while (!autoLoginCompleted && elapsed < timeout)
                    {
                        elapsed += Time.deltaTime;
                        yield return null;
                    }

                    if (autoLoginSuccess)
                    {
                        // Auto login successful, go to Home
                        targetScene = SceneLoader.SCENE_HOME;
                        Debug.Log("[LoadingManager] Auto login successful, loading Home scene");
                    }
                    else
                    {
                        // Auto login failed, go to Login
                        targetScene = SceneLoader.SCENE_LOGIN;
                        Debug.Log("[LoadingManager] Auto login failed, loading Login scene");
                    }
                }
                else
                {
                    // AuthService not available, go to Login
                    targetScene = SceneLoader.SCENE_LOGIN;
                    Debug.Log("[LoadingManager] AuthService not available, loading Login scene");
                }
            }
            else
            {
                // No saved session, go to Login
                targetScene = SceneLoader.SCENE_LOGIN;
                Debug.Log("[LoadingManager] No saved session, loading Login scene");
            }
        }

        /// <summary>
        /// Load target scene (Login, Home, etc.)
        /// </summary>
        private IEnumerator LoadTargetScene()
        {
            if (string.IsNullOrEmpty(targetScene))
            {
                targetScene = SceneLoader.SCENE_LOGIN; // Default fallback
            }

            if (!Application.CanStreamedLevelBeLoaded(targetScene))
            {
                Debug.LogError($"Target scene '{targetScene}' not found in Build Settings!");
                targetScene = SceneLoader.SCENE_LOGIN;
            }

            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(targetScene);
            asyncLoad.allowSceneActivation = false;

            while (asyncLoad.progress < 0.9f)
            {
                UpdateProgress(0.7f + asyncLoad.progress * 0.3f);
                yield return null;
            }

            asyncLoad.allowSceneActivation = true;
            UpdateProgress(1.0f);

            while (!asyncLoad.isDone)
            {
                yield return null;
            }
        }

        private void UpdateProgress(float progress)
        {
            if (progressBar != null)
            {
                progressBar.value = progress;
            }
        }

        private void UpdateLoadingText(string text)
        {
            if (loadingText != null)
            {
                loadingText.text = text;
            }
            Debug.Log($"[Loading] {text}");
        }

        /// <summary>
        /// Public API to show loading panel (for target scenes to reuse).
        /// Optionally override the loading label text.
        /// </summary>
        public void Show(string message = null)
        {
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(true);
                isShowing = true;
            }

            if (!string.IsNullOrEmpty(message) && loadingText != null)
                loadingText.text = message;
        }

        /// <summary>
        /// Public API to hide loading panel (call from LoginView after auto-login check)
        /// </summary>
        public void Hide()
        {
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(false);
                isShowing = false;
            }
        }

        public bool IsShowing() => isShowing;
    }
}
