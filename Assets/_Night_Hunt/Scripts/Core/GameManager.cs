using NightHunt.Lobby;
using NightHunt.Netcode;
using NightHunt.Services.Auth;
using NightHunt.Services.Backend;
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
        [SerializeField] private NetworkBootstrap networkBootstrap;
        [SerializeField] private LobbyController lobbyController;
        [SerializeField] private SessionMonitor sessionMonitor;

        [Header("State")]
        [SerializeField] private SessionState sessionState;
        [SerializeField] private RoomState roomState;

        // Public getters for services
        public BackendHttpClient BackendClient => backendHttpClient;
        public AuthService AuthService => authService;
        public RoomService RoomService => roomService;
        public NetworkBootstrap NetworkBootstrap => networkBootstrap;
        public LobbyController LobbyController => lobbyController;
        public SessionMonitor SessionMonitor => sessionMonitor;
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

            if (networkBootstrap == null)
            {
                networkBootstrap = GetComponent<NetworkBootstrap>();
                if (networkBootstrap == null)
                {
                    networkBootstrap = gameObject.AddComponent<NetworkBootstrap>();
                }
            }

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
    }
}
