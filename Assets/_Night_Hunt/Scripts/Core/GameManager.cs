using System;
using NightHunt.Networking;
using NightHunt.Services.Auth;
using NightHunt.Services.Backend;
using NightHunt.Services.Game;
using NightHunt.Services.Room;
using NightHunt.State;
using NightHunt.Core;
using UnityEngine;
using NightHunt.Utilities;

namespace NightHunt.Core
{
    /// <summary>
    /// GameManager - Persistent manager that survives scene changes
    /// Contains all core services and should be loaded first
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Services")]
        [SerializeField] private BackendHttpClient backendHttpClient;
        [SerializeField] private AuthService authService;
        [SerializeField] private RoomService roomService;
        // Note: NetworkBootstrap đã bị xóa, dùng NetworkGameManager thay thế
        [SerializeField] private GameWebSocketService gameWebSocketService;
        
        [Header("Config")]
        [SerializeField] private Config.InstanceConfig instanceConfig;

        [Header("State")]
        [SerializeField] private SessionState sessionState;
        [SerializeField] private RoomState roomState;

        // Public getters for services
        public BackendHttpClient BackendClient => backendHttpClient;
        public AuthService AuthService => authService;
        public RoomService RoomService => roomService;
        // Note: NetworkBootstrap đã bị xóa, dùng NetworkGameManager.Instance thay thế
        public GameWebSocketService GameWebSocket => gameWebSocketService;
        public SessionState SessionState => sessionState;
        public RoomState RoomState => roomState;
        public Config.InstanceConfig InstanceConfig => instanceConfig;

        // App lifecycle events - observers can subscribe/unsubscribe
        public event Action OnAppFocusLost;
        public event Action OnAppFocusGained;
        public event Action OnAppPaused;
        public event Action OnAppResumed;

        private void Awake()
        {
            // Singleton pattern - only one GameManager should exist
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeServices();
                
                // Configure run in background based on config
                ConfigureRunInBackground();
                
                // Đảm bảo PersistentUICanvas được tạo
                EnsurePersistentUICanvas();
                
                // Đánh dấu GameManager đã khởi tạo
                SceneLoader.MarkGameManagerInitialized();
            }
            else
            {
                // If another GameManager exists, destroy this one
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Configure whether app runs in background when losing focus
        /// </summary>
        private void ConfigureRunInBackground()
        {
            bool shouldRunInBackground = false;
            if (instanceConfig != null)
            {
                shouldRunInBackground = instanceConfig.ShouldRunInBackground();
            }
            else
            {
                // Fallback: Auto-detect (Editor = true, Build = false)
                shouldRunInBackground = Application.isEditor;
            }
            
            Application.runInBackground = shouldRunInBackground;
            Debug.Log($"[GameManager] Application.runInBackground set to: {shouldRunInBackground} (Editor: {Application.isEditor})");
        }

        /// <summary>
        /// Đảm bảo PersistentUICanvas được tạo nếu chưa có
        /// </summary>
        private void EnsurePersistentUICanvas()
        {
            if (UI.PersistentUICanvas.Instance == null)
            {
                UI.PersistentUICanvas.GetOrCreate();
            }
        }

        private void InitializeServices()
        {
            // Ensure all services are initialized
            if (backendHttpClient == null)
            {
                backendHttpClient = ComponentResolver.Find<BackendHttpClient>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] BackendHttpClient not found")
        .Resolve();
                if (backendHttpClient == null)
                {
                    backendHttpClient = gameObject.AddComponent<BackendHttpClient>();
                }
            }

            if (authService == null)
            {
                authService = ComponentResolver.Find<AuthService>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] AuthService not found")
        .Resolve();
                if (authService == null)
                {
                    authService = gameObject.AddComponent<AuthService>();
                }
            }

            if (roomService == null)
            {
                roomService = ComponentResolver.Find<RoomService>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] RoomService not found")
        .Resolve();
                if (roomService == null)
                {
                    roomService = gameObject.AddComponent<RoomService>();
                }
            }

            // Note: NetworkBootstrap đã bị xóa, dùng NetworkGameManager thay thế
            // NetworkGameManager là singleton và tự quản lý

            if (sessionState == null)
            {
                sessionState = ComponentResolver.Find<SessionState>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] SessionState not found")
        .Resolve();
                if (sessionState == null)
                {
                    sessionState = gameObject.AddComponent<SessionState>();
                }
            }

            if (roomState == null)
            {
                roomState = ComponentResolver.Find<RoomState>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] RoomState not found")
        .Resolve();
                if (roomState == null)
                {
                    roomState = gameObject.AddComponent<RoomState>();
                }
            }

            // Initialize GameWebSocketService (unified WebSocket for all game events)
            if (gameWebSocketService == null)
            {
                gameWebSocketService = ComponentResolver.Find<GameWebSocketService>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] GameWebSocketService not found")
        .Resolve();
                if (gameWebSocketService == null)
                {
                    gameWebSocketService = gameObject.AddComponent<GameWebSocketService>();
                }
            }

            // Initialize GameEventBus (centralized event system)
            if (GameEventBus.Instance == null)
            {
                GameObject eventBusObj = new GameObject("GameEventBus");
                eventBusObj.AddComponent<GameEventBus>();
                DontDestroyOnLoad(eventBusObj);
            }

            Debug.Log("GameManager initialized with all services");
        }

        /// <summary>
        /// Get a service by type (useful for dependency injection)
        /// </summary>
        public T GetService<T>() where T : Component
        {
            // Direct property access is preferred, but this method provides fallback
            return ComponentResolver.Find<T>(this)
        .OnSelf()
        .InChildren()
        .OrLogWarning("[Auto] T not found")
        .Resolve();
        }

        /// <summary>
        /// Cleanup when application quits (PC/Desktop)
        /// </summary>
        private async void OnApplicationQuit()
        {
            Debug.Log("[GameManager] Application quitting - cleaning up...");
            await CleanupOnExit();
            
            // Ensure WebSocket is fully disconnected without auto-reconnect
            if (gameWebSocketService != null)
            {
                try
                {
                    gameWebSocketService.Disconnect(disableReconnect: true);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[GameManager] Error disconnecting WebSocket on quit: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Cleanup when application pauses (Mobile - when app goes to background)
        /// </summary>
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                Debug.Log("[GameManager] Application paused (going to background) - disconnecting WebSocket only (keeping room)...");
                // Only disconnect WebSocket, DON'T leave room
                // User might switch apps but still want to be in room when they come back
                if (gameWebSocketService != null)
                {
                    try
                    {
                        gameWebSocketService.Disconnect();
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[GameManager] Error disconnecting WebSocket on pause: {ex.Message}");
                    }
                }

                OnAppPaused?.Invoke();
            }
            else
            {
                // App resumed from background
                Debug.Log("[GameManager] Application resumed from background");
                HandleApplicationResumed();
                OnAppResumed?.Invoke();
            }
        }

        /// <summary>
        /// Cleanup when application loses focus (PC - when window loses focus, but app still running)
        /// Note: On mobile, this is called when app goes to background
        /// </summary>
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                Debug.Log("[GameManager] Application lost focus - disconnecting WebSocket only (keeping room)...");
                // Only disconnect WebSocket, DON'T leave room
                // User might switch windows but still want to be in room
                if (gameWebSocketService != null)
                {
                    try
                    {
                        gameWebSocketService.Disconnect();
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[GameManager] Error disconnecting WebSocket on focus loss: {ex.Message}");
                    }
                }

                OnAppFocusLost?.Invoke();
            }
            else
            {
                // App regained focus
                Debug.Log("[GameManager] Application regained focus");
                HandleApplicationResumed();
                OnAppFocusGained?.Invoke();
            }
        }

        /// <summary>
        /// Handle application resumed (from pause or focus return)
        /// Reconnect WebSocket and refresh current scene data
        /// </summary>
        private async void HandleApplicationResumed()
        {
            bool shouldRefresh = instanceConfig != null ? instanceConfig.ShouldRefreshOnFocusReturn() : true;
            if (!shouldRefresh)
            {
                Debug.Log("[GameManager] Refresh on focus return is disabled, skipping refresh");
                return;
            }

            // Wait a bit for app to fully resume
            await System.Threading.Tasks.Task.Delay(500);

            // Reconnect WebSocket if user is authenticated
            if (SessionState.Instance != null && SessionState.Instance.IsAuthenticated)
            {
                if (gameWebSocketService != null && !gameWebSocketService.IsWsConnected)
                {
                    Debug.Log("[GameManager] Reconnecting GameWebSocket after resume...");
                    _ = gameWebSocketService.Connect(); // Fire and forget
                }
            }

            // Refresh current scene data based on active scene
            RefreshCurrentSceneData();
        }

        /// <summary>
        /// Refresh data for current active scene
        /// </summary>
        private async void RefreshCurrentSceneData()
        {
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            Debug.Log($"[GameManager] Refreshing data for scene: {currentScene}");

            try
            {
                // Check if user is authenticated
                if (SessionState.Instance == null || !SessionState.Instance.IsAuthenticated)
                {
                    Debug.Log("[GameManager] User not authenticated, skipping refresh");
                    return;
                }

                // Refresh based on scene
                switch (currentScene)
                {
                    case "03_Waiting":
                    case "04_Waiting":
                    case "Waiting":
                        // Refresh lobby/room data
                        if (roomState != null && roomState.IsInRoom && roomService != null)
                        {
                            Debug.Log($"[GameManager] Refreshing room data for room {roomState.RoomId}");
                            var result = await roomService.GetRoom(roomState.RoomId);
                            if (result.Success && result.Data != null)
                            {
                                Debug.Log("[GameManager] Room data refreshed successfully");
                                
                                // Trigger CustomLobbyView refresh if it exists
                                var lobbyView = FindFirstObjectByType<UI.CustomLobbyView>();
                                if (lobbyView != null)
                                {
                                    lobbyView.RefreshPlayerList();
                                    Debug.Log("[GameManager] CustomLobbyView refresh triggered");
                                }
                            }
                            else
                            {
                                Debug.LogWarning($"[GameManager] Failed to refresh room data: {result.Message}");
                            }
                        }
                        break;

                    case "02_Home":
                    case "Home":
                        // Home scene - no specific refresh needed
                        Debug.Log("[GameManager] Home scene - no refresh needed");
                        break;

                    case "01_Login":
                    case "Login":
                        // Login scene - no refresh needed
                        Debug.Log("[GameManager] Login scene - no refresh needed");
                        break;

                    default:
                        Debug.Log($"[GameManager] Unknown scene '{currentScene}' - no refresh logic defined");
                        break;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GameManager] Error refreshing scene data: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Cleanup resources when app exits (ONLY called on actual quit, not focus/pause)
        /// </summary>
        private async System.Threading.Tasks.Task CleanupOnExit()
        {
            try
            {
                Debug.Log("[GameManager] Starting cleanup on app exit...");
                
                // 1. Leave room if in room (only on actual quit)
                if (roomState != null && roomState.IsInRoom && roomService != null)
                {
                    try
                    {
                        Debug.Log($"[GameManager] Leaving room {roomState.RoomId} on app exit...");
                        await roomService.LeaveRoom(roomState.RoomId);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[GameManager] Error leaving room on exit: {ex.Message}");
                    }
                }

                // 2. Disconnect GameWebSocket
                if (gameWebSocketService != null)
                {
                    try
                    {
                        gameWebSocketService.Disconnect();
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[GameManager] Error disconnecting WebSocket on exit: {ex.Message}");
                    }
                }

                Debug.Log("[GameManager] Cleanup completed");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GameManager] Error during cleanup: {ex.Message}");
            }
        }
    }
}
