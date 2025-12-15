using NightHunt.Lobby;
using NightHunt.Networking;
using NightHunt.Services.Auth;
using NightHunt.Services.Backend;
using NightHunt.Services.Game;
using NightHunt.Services.Room;
using NightHunt.State;
using NightHunt.Core;
using UnityEngine;

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
        [SerializeField] private LobbyController lobbyController;
        [SerializeField] private SessionMonitor sessionMonitor; // Deprecated - will be removed, using WebSocket instead
        [SerializeField] private GameWebSocketService gameWebSocketService;

        [Header("State")]
        [SerializeField] private SessionState sessionState;
        [SerializeField] private RoomState roomState;

        // Public getters for services
        public BackendHttpClient BackendClient => backendHttpClient;
        public AuthService AuthService => authService;
        public RoomService RoomService => roomService;
        // Note: NetworkBootstrap đã bị xóa, dùng NetworkGameManager.Instance thay thế
        public LobbyController LobbyController => lobbyController;
        public SessionMonitor SessionMonitor => sessionMonitor; // Deprecated - using WebSocket instead
        public GameWebSocketService GameWebSocket => gameWebSocketService;
        public SessionState SessionState => sessionState;
        public RoomState RoomState => roomState;

        private void Awake()
        {
            // Singleton pattern - only one GameManager should exist
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeServices();
                
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
                backendHttpClient = GetComponent<BackendHttpClient>();
                if (backendHttpClient == null)
                {
                    backendHttpClient = gameObject.AddComponent<BackendHttpClient>();
                }
            }

            if (authService == null)
            {
                authService = GetComponent<AuthService>();
                if (authService == null)
                {
                    authService = gameObject.AddComponent<AuthService>();
                }
            }

            if (roomService == null)
            {
                roomService = GetComponent<RoomService>();
                if (roomService == null)
                {
                    roomService = gameObject.AddComponent<RoomService>();
                }
            }

            // Note: NetworkBootstrap đã bị xóa, dùng NetworkGameManager thay thế
            // NetworkGameManager là singleton và tự quản lý

            if (lobbyController == null)
            {
                lobbyController = GetComponent<LobbyController>();
                if (lobbyController == null)
                {
                    lobbyController = gameObject.AddComponent<LobbyController>();
                }
            }

            if (sessionState == null)
            {
                sessionState = GetComponent<SessionState>();
                if (sessionState == null)
                {
                    sessionState = gameObject.AddComponent<SessionState>();
                }
            }

            if (roomState == null)
            {
                roomState = GetComponent<RoomState>();
                if (roomState == null)
                {
                    roomState = gameObject.AddComponent<RoomState>();
                }
            }

            if (sessionMonitor == null)
            {
                sessionMonitor = GetComponent<SessionMonitor>();
                if (sessionMonitor == null)
                {
                    sessionMonitor = gameObject.AddComponent<SessionMonitor>();
                }
            }

            // Initialize GameWebSocketService (replaces SessionMonitor polling and RoomWebSocketService)
            if (gameWebSocketService == null)
            {
                gameWebSocketService = GetComponent<GameWebSocketService>();
                if (gameWebSocketService == null)
                {
                    gameWebSocketService = gameObject.AddComponent<GameWebSocketService>();
                }
            }

            Debug.Log("GameManager initialized with all services");
        }

        /// <summary>
        /// Get a service by type (useful for dependency injection)
        /// </summary>
        public T GetService<T>() where T : Component
        {
            // Direct property access is preferred, but this method provides fallback
            return GetComponent<T>();
        }

        /// <summary>
        /// Cleanup when application quits (PC/Desktop)
        /// </summary>
        private async void OnApplicationQuit()
        {
            Debug.Log("[GameManager] Application quitting - cleaning up...");
            await CleanupOnExit();
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

                // 3. Stop session monitoring (deprecated - kept for compatibility)
                if (sessionMonitor != null)
                {
                    sessionMonitor.StopPolling();
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
