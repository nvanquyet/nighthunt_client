using System;
using NightHunt.Networking;
using NightHunt.Services.Auth;
using NightHunt.Services.Backend;
using NightHunt.Services.Config;
using NightHunt.Services.Friend;
using NightHunt.Services.Party;
using NightHunt.Services.Game;
using NightHunt.Services.Profile;
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
    /// GameManager - Persistent manager persists throughout vòng đ�?i app.
    /// Single-scene setup: no longer depends on SceneLoader / scene name check.
    /// </summary>
    public class GameManager : SingletonPersistent<GameManager>
    {

        [Header("Services")]
        [SerializeField] private BackendHttpClient    backendHttpClient;
        [SerializeField] private AuthService          authService;
        [SerializeField] private GameConfigService    gameConfigService;
        [SerializeField] private FriendService        friendService;
        [SerializeField] private PartyService         partyService;
        [SerializeField] private RoomService          roomService;
        [SerializeField] private GameWebSocketService gameWebSocketService;
        [SerializeField] private ProfileManager       profileManager;

        [Header("Config")]
        [SerializeField] private Config.InstanceConfig instanceConfig;

        [Header("State")]
        [SerializeField] private SessionState sessionState;
        [SerializeField] private RoomState    roomState;

        // ── Public getters ────────────────────────────────────────────────
        public BackendHttpClient    BackendClient    => backendHttpClient;
        public AuthService          AuthService      => authService;
        public GameConfigService    GameConfigService=> gameConfigService;
        public FriendService        FriendService    => friendService;
        public PartyService         PartyService   => partyService;
        public RoomService          RoomService    => roomService;
        public GameWebSocketService GameWebSocket  => gameWebSocketService;
        public ProfileManager       ProfileManager => profileManager;
        public SessionState         SessionState   => sessionState;
        public RoomState            RoomState      => roomState;
        public Config.InstanceConfig InstanceConfig => instanceConfig;

        // ── App lifecycle events ──────────────────────────────────────────
        public event Action OnAppFocusLost;
        public event Action OnAppFocusGained;
        public event Action OnAppPaused;
        public event Action OnAppResumed;

        // ─────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────

        protected override void OnSingletonAwake()
        {
            InitializeServices();
            ConfigureRunInBackground();
            EnsurePersistentUICanvas();
        }

        // ─────────────────────────────────────────────────────────────────
        // Init
        // ─────────────────────────────────────────────────────────────────

        private void ConfigureRunInBackground()
        {
            bool runInBg = instanceConfig != null
                ? instanceConfig.ShouldRunInBackground()
                : Application.isEditor;

            Application.runInBackground = runInBg;
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
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
            gameConfigService = ResolveOrAdd<GameConfigService>(gameConfigService);
            friendService     = ResolveOrAdd<FriendService>(friendService);
            partyService      = ResolveOrAdd<PartyService>(partyService);
            roomService       = ResolveOrAdd<RoomService>(roomService);
            profileManager    = ResolveOrAdd<ProfileManager>(profileManager);
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

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                Debug.Log("[GameManager] All services initialized");
        }

        /// <summary>Resolve component từ self/children hoặc AddComponent nếu not available.</summary>
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

        // ─────────────────────────────────────────────────────────────────
        // Generic service getter
        // ─────────────────────────────────────────────────────────────────

        public T GetService<T>() where T : Component
        {
            return ComponentResolver.Find<T>(this)
                .OnSelf()
                .InChildren()
                .OrLogWarning($"[GetService] {typeof(T).Name} not found")
                .Resolve();
        }

        // ─────────────────────────────────────────────────────────────────
        // App Lifecycle — Quit
        // ─────────────────────────────────────────────────────────────────

        private async void OnApplicationQuit()
        {
            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                Debug.Log("[GameManager] Application quitting — cleaning up...");
            await CleanupOnExit();

            if (gameWebSocketService != null)
            {
                try { gameWebSocketService.Disconnect(disableReconnect: true); }
                catch (Exception ex) { Debug.LogWarning($"[GameManager] WS disconnect error: {ex.Message}"); }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // App Lifecycle — Pause / Focus (Mobile & PC)
        // ─────────────────────────────────────────────────────────────────

        private void OnApplicationPause(bool pauseStatus)
        {
#if UNITY_ANDROID || UNITY_IOS
            if (pauseStatus)
            {
                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                    Debug.Log("[GameManager] App paused → disconnect WS (keep room)");
                DisconnectWebSocket();
                OnAppPaused?.Invoke();
            }
            else
            {
                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                    Debug.Log("[GameManager] App resumed");
                HandleApplicationResumed();
                OnAppResumed?.Invoke();
            }
#else
            // Desktop/editor: losing focus is common (alt-tab, multi-client testing).
            // Keep WS connected to avoid false OFFLINE transitions and missed push events.
            if (pauseStatus)
            {
                OnAppPaused?.Invoke();
            }
            else
            {
                HandleApplicationResumed();
                OnAppResumed?.Invoke();
            }
#endif
        }

        private void OnApplicationFocus(bool hasFocus)
        {
#if UNITY_ANDROID || UNITY_IOS
            if (!hasFocus)
            {
                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                    Debug.Log("[GameManager] App lost focus → disconnect WS (keep room)");
                DisconnectWebSocket();
                OnAppFocusLost?.Invoke();
            }
            else
            {
                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                    Debug.Log("[GameManager] App regained focus");
                HandleApplicationResumed();
                OnAppFocusGained?.Invoke();
            }
#else
            // Desktop/editor: do not disconnect on focus changes.
            if (!hasFocus)
            {
                OnAppFocusLost?.Invoke();
            }
            else
            {
                HandleApplicationResumed();
                OnAppFocusGained?.Invoke();
            }
#endif
        }

        // ─────────────────────────────────────────────────────────────────
        // Resume handler
        // ─────────────────────────────────────────────────────────────────

        private async void HandleApplicationResumed()
        {
            bool shouldRefresh = instanceConfig == null || instanceConfig.ShouldRefreshOnFocusReturn();

            if (!shouldRefresh)
            {
                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                    Debug.Log("[GameManager] Refresh on focus disabled — skipping");
                return;
            }

            await System.Threading.Tasks.Task.Delay(500);

            // Reconnect WebSocket
            if (SessionState.Instance != null && SessionState.Instance.IsAuthenticated)
            {
                if (gameWebSocketService != null && !gameWebSocketService.IsWsConnected)
                {
                    if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                        Debug.Log("[GameManager] Reconnecting WebSocket...");
                    // Re-enable auto-reconnect (DisconnectWebSocket disabled it); now that we
                    // are explicitly reconnecting, future server-initiated closes should retry.
                    gameWebSocketService.Disconnect(disableReconnect: false);
                    _ = gameWebSocketService.Connect();
                }
            }

            RefreshCurrentPanelData();
        }

        // ─────────────────────────────────────────────────────────────────
        // Refresh data — dùng UINavigator instead of scene name
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Refresh data dựa trên panel đang display.
        /// Single-scene: no longer check SceneManager.GetActiveScene().name.
        /// </summary>
        private async void RefreshCurrentPanelData()
        {
            if (SessionState.Instance == null || !SessionState.Instance.IsAuthenticated)
            {
                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                    Debug.Log("[GameManager] Not authenticated — skip refresh");
                return;
            }

            var nav = UINavigator.Instance;
            if (nav == null)
            {
                Debug.LogWarning("[GameManager] UINavigator not found — skip refresh");
                return;
            }

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                Debug.Log($"[GameManager] Refreshing data for panel: {nav.CurrentPanel}");

            try
            {
                if (nav.CurrentPanel == PanelType.None) return;
                switch (nav.CurrentPanel)
                {
                    case PanelType.Lobby:
                    case PanelType.PartyCustomMode:
                        await RefreshLobbyData();
                        break;

                    case PanelType.Home:
                        // Home không cần refresh đặc biệt
                        if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                            Debug.Log("[GameManager] Home panel — no refresh needed");
                        break;

                    case PanelType.Login:
                        if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                            Debug.Log("[GameManager] Login panel � no refresh needed");
                        break;

                    default:
                        if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                            Debug.Log($"[GameManager] Panel {nav.CurrentPanel} � no refresh logic defined");
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

            if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                Debug.Log($"[GameManager] Refreshing room data: {roomState.RoomId}");
            var result = await roomService.GetRoom(roomState.RoomId);

            if (result.Success && result.Data != null)
            {
                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                    Debug.Log("[GameManager] Room data refreshed");
                var lobbyView = FindFirstObjectByType<UI.PartyCustomModeView>();
                lobbyView?.RefreshPlayerList();
            }
            else
            {
                Debug.LogWarning($"[GameManager] Room refresh failed: {result.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Cleanup on exit
        // ─────────────────────────────────────────────────────────────────

        private async System.Threading.Tasks.Task CleanupOnExit()
        {
            try
            {
                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                    Debug.Log("[GameManager] Starting exit cleanup...");

                if (roomState != null && roomState.IsInRoom && roomService != null)
                {
                    try
                    {
                        if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                            Debug.Log($"[GameManager] Leaving room {roomState.RoomId}...");
                        await roomService.LeaveRoom(roomState.RoomId);
                    }
                    catch (Exception ex) { Debug.LogWarning($"[GameManager] LeaveRoom error: {ex.Message}"); }
                }

                DisconnectWebSocket();
                if (NightHuntDebugConfig.Instance != null && NightHuntDebugConfig.Instance.EnableCoreDebugLogs)
                    Debug.Log("[GameManager] Exit cleanup done");
            }
            catch (Exception ex) { Debug.LogError($"[GameManager] Cleanup error: {ex.Message}"); }
        }

        // ─────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────

        private void DisconnectWebSocket()
        {
            if (gameWebSocketService == null) return;
            // disableReconnect: true � prevent OnClose from scheduling a competing
            // backoff reconnect. HandleApplicationResumed() will reconnect on focus-return.
            try { gameWebSocketService.Disconnect(disableReconnect: true); }
            catch (Exception ex) { Debug.LogWarning($"[GameManager] WS disconnect: {ex.Message}"); }
        }
    }
}
