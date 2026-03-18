using System;
using NightHunt.Networking;
using NightHunt.Services.Auth;
using NightHunt.Services.Backend;
using NightHunt.Services.Friend;
using NightHunt.Services.Party;
using NightHunt.Services.Game;
using NightHunt.Services.Room;
using NightHunt.State;
using NightHunt.Core;
using NightHunt.UI;
using UnityEngine;
using NightHunt.Utilities;
using NightHunt.GameplaySystems.Core.Configs;

namespace NightHunt.Core
{
    /// <summary>
    /// GameManager - Persistent manager tá»“n táº¡i xuyÃªn suá»‘t vÃ²ng Ä‘á»i app.
    /// Single-scene setup: khÃ´ng cÃ²n phá»¥ thuá»™c SceneLoader / scene name check.
    /// </summary>
    public class GameManager : SingletonPersistent<GameManager>
    {

        [Header("Services")]
        [SerializeField] private BackendHttpClient    backendHttpClient;
        [SerializeField] private AuthService          authService;
        [SerializeField] private FriendService        friendService;
        [SerializeField] private PartyService         partyService;
        [SerializeField] private RoomService          roomService;
        [SerializeField] private GameWebSocketService gameWebSocketService;

        [Header("Config")]
        [SerializeField] private Config.InstanceConfig instanceConfig;

        [Header("State")]
        [SerializeField] private SessionState sessionState;
        [SerializeField] private RoomState    roomState;
        [Header("Debug")] [SerializeField] private NightHuntDebugConfig _debugConfig;

        // â”€â”€ Public getters â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public BackendHttpClient    BackendClient  => backendHttpClient;
        public AuthService          AuthService    => authService;
        public FriendService        FriendService  => friendService;
        public PartyService         PartyService   => partyService;
        public RoomService          RoomService    => roomService;
        public GameWebSocketService GameWebSocket  => gameWebSocketService;
        public SessionState         SessionState   => sessionState;
        public RoomState            RoomState      => roomState;
        public Config.InstanceConfig InstanceConfig => instanceConfig;

        // â”€â”€ App lifecycle events â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public event Action OnAppFocusLost;
        public event Action OnAppFocusGained;
        public event Action OnAppPaused;
        public event Action OnAppResumed;

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Lifecycle
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        protected override void OnSingletonAwake()
        {
            InitializeServices();
            ConfigureRunInBackground();
            EnsurePersistentUICanvas();
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Init
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void ConfigureRunInBackground()
        {
            bool runInBg = instanceConfig != null
                ? instanceConfig.ShouldRunInBackground()
                : Application.isEditor;

            Application.runInBackground = runInBg;
            if (_debugConfig != null && _debugConfig.EnableCoreDebugLogs)
                Debug.Log($"[GameManager] runInBackground = {runInBg}");
        }

        private void EnsurePersistentUICanvas()
        {
            if (UI.PersistentUICanvas.Instance == null)
                UI.PersistentUICanvas.GetOrCreate();
        }

        private void InitializeServices()
        {
            backendHttpClient = ResolveOrAdd<BackendHttpClient>(backendHttpClient);
            authService       = ResolveOrAdd<AuthService>(authService);
            friendService     = ResolveOrAdd<FriendService>(friendService);
            partyService      = ResolveOrAdd<PartyService>(partyService);
            roomService       = ResolveOrAdd<RoomService>(roomService);
            sessionState      = ResolveOrAdd<SessionState>(sessionState);
            roomState         = ResolveOrAdd<RoomState>(roomState);
            gameWebSocketService = ResolveOrAdd<GameWebSocketService>(gameWebSocketService);

            // GameEventBus
            if (GameEventBus.Instance == null)
            {
                var go = new GameObject("GameEventBus");
                go.AddComponent<GameEventBus>();
                DontDestroyOnLoad(go);
            }

            if (_debugConfig != null && _debugConfig.EnableCoreDebugLogs)
                Debug.Log("[GameManager] All services initialized");
        }

        /// <summary>Resolve component tá»« self/children hoáº·c AddComponent náº¿u khÃ´ng cÃ³.</summary>
        private T ResolveOrAdd<T>(T existing) where T : Component
        {
            if (existing != null) return existing;

            var found = ComponentResolver.Find<T>(this)
                .OnSelf()
                .InChildren()
                .OrLogWarning($"[Auto] {typeof(T).Name} not found")
                .Resolve();

            return found != null ? found : gameObject.AddComponent<T>();
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Generic service getter
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public T GetService<T>() where T : Component
        {
            return ComponentResolver.Find<T>(this)
                .OnSelf()
                .InChildren()
                .OrLogWarning($"[GetService] {typeof(T).Name} not found")
                .Resolve();
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // App Lifecycle â€” Quit
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private async void OnApplicationQuit()
        {
            if (_debugConfig != null && _debugConfig.EnableCoreDebugLogs)
                Debug.Log("[GameManager] Application quitting â€” cleaning up...");
            await CleanupOnExit();

            if (gameWebSocketService != null)
            {
                try { gameWebSocketService.Disconnect(disableReconnect: true); }
                catch (Exception ex) { Debug.LogWarning($"[GameManager] WS disconnect error: {ex.Message}"); }
            }
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // App Lifecycle â€” Pause / Focus (Mobile & PC)
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                if (_debugConfig != null && _debugConfig.EnableCoreDebugLogs)
                    Debug.Log("[GameManager] App paused â†’ disconnect WS (keep room)");
                DisconnectWebSocket();
                OnAppPaused?.Invoke();
            }
            else
            {
                if (_debugConfig != null && _debugConfig.EnableCoreDebugLogs)
                    Debug.Log("[GameManager] App resumed");
                HandleApplicationResumed();
                OnAppResumed?.Invoke();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                if (_debugConfig != null && _debugConfig.EnableCoreDebugLogs)
                    Debug.Log("[GameManager] App lost focus â†’ disconnect WS (keep room)");
                DisconnectWebSocket();
                OnAppFocusLost?.Invoke();
            }
            else
            {
                if (_debugConfig != null && _debugConfig.EnableCoreDebugLogs)
                    Debug.Log("[GameManager] App regained focus");
                HandleApplicationResumed();
                OnAppFocusGained?.Invoke();
            }
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Resume handler
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private async void HandleApplicationResumed()
        {
            bool shouldRefresh = instanceConfig != null
                ? instanceConfig.ShouldRefreshOnFocusReturn()
                : true;

            if (!shouldRefresh)
            {
                if (_debugConfig != null && _debugConfig.EnableCoreDebugLogs)
                    Debug.Log("[GameManager] Refresh on focus disabled â€” skipping");
                return;
            }

            await System.Threading.Tasks.Task.Delay(500);

            // Reconnect WebSocket
            if (SessionState.Instance != null && SessionState.Instance.IsAuthenticated)
            {
                if (gameWebSocketService != null && !gameWebSocketService.IsWsConnected)
                {
                    if (_debugConfig != null && _debugConfig.EnableCoreDebugLogs)
                        Debug.Log("[GameManager] Reconnecting WebSocket...");
                    _ = gameWebSocketService.Connect();
                }
            }

            RefreshCurrentPanelData();
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Refresh data â€” dÃ¹ng UINavigator thay vÃ¬ scene name
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Refresh dá»¯ liá»‡u dá»±a trÃªn panel Ä‘ang hiá»ƒn thá»‹.
        /// Single-scene: khÃ´ng cÃ²n check SceneManager.GetActiveScene().name.
        /// </summary>
        private async void RefreshCurrentPanelData()
        {
            if (SessionState.Instance == null || !SessionState.Instance.IsAuthenticated)
            {
                if (_debugConfig != null && _debugConfig.EnableCoreDebugLogs)
                    Debug.Log("[GameManager] Not authenticated â€” skip refresh");
                return;
            }

            var nav = UINavigator.Instance;
            if (nav == null)
            {
                Debug.LogWarning("[GameManager] UINavigator not found â€” skip refresh");
                return;
            }

            if (_debugConfig != null && _debugConfig.EnableCoreDebugLogs)
                Debug.Log($"[GameManager] Refreshing data for panel: {nav.CurrentPanel?.ToString() ?? "none"}");

            try
            {
                if (!nav.CurrentPanel.HasValue) return;
                switch (nav.CurrentPanel.Value)
                {
                    case PanelType.Lobby:
                        await RefreshLobbyData();
                        break;

                    case PanelType.Home:
                        // Home khÃ´ng cáº§n refresh Ä‘áº·c biá»‡t
                        if (_debugConfig != null && _debugConfig.EnableCoreDebugLogs)
                            Debug.Log("[GameManager] Home panel â€” no refresh needed");
                        break;

                    case PanelType.Login:
                        if (_debugConfig != null && _debugConfig.EnableCoreDebugLogs)
                            Debug.Log("[GameManager] Login panel — no refresh needed");
                        break;

                    default:
                        if (_debugConfig != null && _debugConfig.EnableCoreDebugLogs)
                            Debug.Log($"[GameManager] Panel {nav.CurrentPanel.Value} — no refresh logic defined");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameManager] Error refreshing panel data: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async System.Threading.Tasks.Task RefreshLobbyData()
        {
            if (roomState == null || !roomState.IsInRoom || roomService == null) return;

            if (_debugConfig != null && _debugConfig.EnableCoreDebugLogs)
                Debug.Log($"[GameManager] Refreshing room data: {roomState.RoomId}");
            var result = await roomService.GetRoom(roomState.RoomId);

            if (result.Success && result.Data != null)
            {
                if (_debugConfig != null && _debugConfig.EnableCoreDebugLogs)
                    Debug.Log("[GameManager] Room data refreshed");
                var lobbyView = FindFirstObjectByType<UI.CustomLobbyView>();
                lobbyView?.RefreshPlayerList();
            }
            else
            {
                Debug.LogWarning($"[GameManager] Room refresh failed: {result.Message}");
            }
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Cleanup on exit
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private async System.Threading.Tasks.Task CleanupOnExit()
        {
            try
            {
                if (_debugConfig != null && _debugConfig.EnableCoreDebugLogs)
                    Debug.Log("[GameManager] Starting exit cleanup...");

                if (roomState != null && roomState.IsInRoom && roomService != null)
                {
                    try
                    {
                        if (_debugConfig != null && _debugConfig.EnableCoreDebugLogs)
                            Debug.Log($"[GameManager] Leaving room {roomState.RoomId}...");
                        await roomService.LeaveRoom(roomState.RoomId);
                    }
                    catch (Exception ex) { Debug.LogWarning($"[GameManager] LeaveRoom error: {ex.Message}"); }
                }

                DisconnectWebSocket();
                if (_debugConfig != null && _debugConfig.EnableCoreDebugLogs)
                    Debug.Log("[GameManager] Exit cleanup done");
            }
            catch (Exception ex) { Debug.LogError($"[GameManager] Cleanup error: {ex.Message}"); }
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Helpers
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void DisconnectWebSocket()
        {
            if (gameWebSocketService == null) return;
            try { gameWebSocketService.Disconnect(); }
            catch (Exception ex) { Debug.LogWarning($"[GameManager] WS disconnect: {ex.Message}"); }
        }
    }
}
